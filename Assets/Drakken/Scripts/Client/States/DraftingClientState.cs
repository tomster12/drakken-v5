using Drakken.Client.World;
using Drakken.Client.World.Animation;
using Drakken.Common.Utility;
using Drakken.Domain;
using Drakken.Domain.Networking;
using Drakken.Domain.Static;
using Drakken.Utility;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;

using static Drakken.Client.ClientUI;

namespace Drakken.Client.States
{
    public class DraftingClientState : ClientState
    {
        private SceneLayout SceneLayout => GameEntrypoint.Singleton.Client.SceneLayout;
        private SceneObjects SceneObjects => GameEntrypoint.Singleton.Client.SceneObjects;
        private DraftingPlayerLayout MyDraftingLayout => SceneLayout.Drafting.Player(Match.ClientIndex);
        private ScenePlayerObjects MySceneObjects => SceneObjects.Player(Match.ClientIndex);
        private int CountToDiscard => GameConstants.DraftingTokenCount - GameConstants.StandardTokenCount;
        private bool SelectedEnoughTokens => selectedTokenViews.Count >= CountToDiscard;
        private readonly List<TokenView> selectedTokenViews = new();
        private bool hasDiscarded;

        // ------------------------------ Setup

        public override async Task Enter(ClientStateType fromType)
        {
            await base.Enter(fromType);

            Match.OnPlayingPhaseStarted += OnPlayingPhaseStarted;
            Match.OnDraftingOtherPlayerDiscarded += OnOtherPlayerDiscarded;

            // Reset per-round selection state
            hasDiscarded = false;
            selectedTokenViews.Clear();

            // Initialise this scenes objects
            SceneLayout.Drafting.DraftConfirmButton.gameObject.SetActive(true);
            SceneLayout.Drafting.DraftConfirmButton.Interactable = false;
            SceneLayout.Drafting.DraftConfirmButton.Clicked += OnConfirmDiscardClicked;

            SceneLayout.Drafting.DraftConfirmButton.transform.SetPositionAndRotation(
                MyDraftingLayout.DraftConfirmButtonPosition.position, MyDraftingLayout.DraftConfirmButtonPosition.rotation);

            GameEntrypoint.Singleton.Client.Camera.SetTarget(MyDraftingLayout.CameraPosition);

            UpdateStatusUI();

            // Starting new game
            if (fromType == ClientStateType.Title)
            {
                SceneLayout.Game.P1.Mat.SetActive(true);
                SceneLayout.Game.P2.Mat.SetActive(true);

                await CreateAndRollDice();
            }

            // Starting next round
            else
            {
                RollExistingDice();
            }

            // And now spawn tokens ready to discard
            SpawnTokens();
        }

        public override async Task Exit(ClientStateType toType)
        {
            await base.Exit(toType);

            Match.OnPlayingPhaseStarted -= OnPlayingPhaseStarted;
            Match.OnDraftingOtherPlayerDiscarded -= OnOtherPlayerDiscarded;

            client.UI.SetMyAvatar(AvatarState.Visible);
            client.UI.SetOpAvatar(AvatarState.Visible);

            // If we are going back to title then clean up the token / dice views
            if (toType == ClientStateType.Title)
            {
                SceneLayout.Game.OnDisconnect();
                SceneObjects.OnDisconnect();
                client.UI.OnDisconnect();
            }

            // Cleanup this scenes specific objects
            SceneLayout.Drafting.DraftConfirmButton.Clicked -= OnConfirmDiscardClicked;
            SceneLayout.Drafting.DraftConfirmButton.gameObject.SetActive(false);
        }

        private async Task CreateAndRollDice()
        {
            Assert.True(SceneObjects.P1.DiceViews.Length == 0);
            Assert.True(SceneObjects.P2.DiceViews.Length == 0);

            client.UI.UpdateDiceTotal(Match.ClientIndex, 0, unknown: true);
            client.UI.UpdateDiceTotal(Match.ClientIndex, 1, unknown: true);

            // Play the server-recorded physics roll first (temporary procedural dice)
            await PlayDiceTraces(Match.LastDiceTraces);

            // Then settle the real, interactable dice into their row at the resolved values
            Quaternion targetRot = Quaternion.Euler(0, Match.ClientIndex * 180f, 0);
            for (int clientIndex = 0; clientIndex < 2; clientIndex++)
            {
                var clientDice = GameState.Clients[clientIndex].Dice;
                var sceneObjects = SceneObjects.Player(clientIndex);
                sceneObjects.DiceViews = new DiceView[clientDice.Count];

                for (int i = 0; i < clientDice.Count; i++)
                {
                    sceneObjects.SpawnDiceAtIndex(clientDice[i], i, targetRot);
                }

                sceneObjects.DicePhysicsReplayer.ClearAll();
            }

            // Update UI to match dice totals
            client.UI.UpdateDiceTotal(Match.ClientIndex, 0);
            client.UI.UpdateDiceTotal(Match.ClientIndex, 1);
        }

        private async void RollExistingDice()
        {
            Assert.True(SceneObjects.P1.DiceViews.Length != 0);
            Assert.True(SceneObjects.P2.DiceViews.Length != 0);

            client.UI.UpdateDiceTotal(Match.ClientIndex, 0, unknown: true);
            client.UI.UpdateDiceTotal(Match.ClientIndex, 1, unknown: true);

            // Hide the resting row dice while the physics reroll plays out in their place
            SetDiceViewsVisible(Match.ClientIndex, visible: false);
            SetDiceViewsVisible(Match.OpClientIndex, visible: false);

            await PlayDiceTraces(Match.LastDiceTraces);

            // Rebind the row dice to this round's resolved values and reveal them again
            for (int clientIndex = 0; clientIndex < 2; clientIndex++)
            {
                var clientDice = GameState.Clients[clientIndex].Dice;
                var sceneObjects = SceneObjects.Player(clientIndex);
                for (int i = 0; i < sceneObjects.DiceViews.Length; i++)
                {
                    sceneObjects.DiceViews[i].Rebind(clientDice[i]);
                }

                sceneObjects.DicePhysicsReplayer.ClearAll();
            }

            SetDiceViewsVisible(Match.ClientIndex, visible: true);
            SetDiceViewsVisible(Match.OpClientIndex, visible: true);

            // Update UI to match dice totals
            client.UI.UpdateDiceTotal(Match.ClientIndex, 0);
            client.UI.UpdateDiceTotal(Match.ClientIndex, 1);
        }

        private async Task PlayDiceTraces(MatchDiceTraces diceTraces)
        {
            List<Task> tasks = new();
            for (int clientIndex = 0; clientIndex < 2; clientIndex++)
            {
                var sceneObjects = SceneObjects.Player(clientIndex);
                tasks.Add(sceneObjects.DicePhysicsReplayer.Play(client.Assets, diceTraces.Player(clientIndex), cts.Token));
            }
            await Task.WhenAll(tasks);
        }

        private void SetDiceViewsVisible(int clientIndex, bool visible)
        {
            foreach (var diceView in SceneObjects.Player(clientIndex).DiceViews)
            {
                diceView.gameObject.SetActive(visible);
            }
        }

        private async void SpawnTokens()
        {
            Assert.True(SceneObjects.P1.TokenViews.Length == 0);
            Assert.True(SceneObjects.P2.TokenViews.Length == 0);

            Vector3 bagStartPos = MySceneObjects.BagPosition;

            // Create each token object in a row
            var tokenCount = GameState.Clients[Match.ClientIndex].Tokens.Count;
            MySceneObjects.TokenViews = new TokenView[tokenCount];

            List<Task> tasks = new();

            for (int i = 0; i < tokenCount; i++)
            {
                // Start a new task for this token
                int tokenIndex = i;
                int taskDelay = (tokenCount - 1 - tokenIndex) * 400;

                tasks.Add(AsyncUtility.DelayTask(taskDelay, async () =>
                {
                    var tokenInstance = GameState.Clients[Match.ClientIndex].Tokens[tokenIndex];
                    var tokenView = MySceneObjects.SpawnTokenAtIndex(tokenInstance, tokenIndex);

                    tokenView.SetInteractionMode(TokenView.InteractionModeType.None);

                    // Start small inside the bag
                    Quaternion baseRot = Quaternion.Euler(0, Match.ClientIndex * 180f, 0);
                    tokenView.SetPositionAndRotation(bagStartPos, baseRot);
                    tokenView.transform.localScale = Vector3.zero;

                    // Animate up and then across
                    Vector3 bagAbovePos = bagStartPos + Vector3.up * 1.5f;
                    Vector3 targetPos = MySceneObjects.GetDraftTokenRowIndexPosition(tokenIndex, tokenCount);

                    await tokenView.Animator.Play(AnimationSequenceBuilder
                        .Start()
                        .At(0.0f, AnimationTracks.LocalScale(
                            0.8f, tokenView.transform, Vector3.zero, Vector3.one, Easing.EaseInCubic))
                        .At(0.0f, AnimationTracks.Rotation(
                            1.2f, tokenView.transform, baseRot, MyDraftingLayout.DraftTokenRow.rotation, Easing.Linear))
                        .Next(tokenView.CreateCurrentPositionAnimationTrack(
                            0.6f, AnimationCurves.Lerp(bagStartPos, bagAbovePos), Easing.Linear))
                        .Next(tokenView.CreateCurrentPositionAnimationTrack(
                            0.8f, AnimationCurves.QuadraticBezier(bagAbovePos, bagAbovePos + Vector3.up * 0.7f, targetPos), Easing.EaseOutCubic))
                        .Build(), cts.Token);
                }, cts.Token));
            }

            await Task.WhenAll(tasks);

            // Can interact with all once finished
            for (int i = 0; i < tokenCount; i++)
            {
                MySceneObjects.TokenViews[i].OnClicked.AddListener(OnTokenClicked);
                MySceneObjects.TokenViews[i].SetInteractionMode(TokenView.InteractionModeType.Discard);
            }
        }

        // ------------------------------ Main

        private void UpdateStatusUI()
        {
            var statusText =
                hasDiscarded ? "Waiting for opponent..."
                : SelectedEnoughTokens ? "Ready to confirm"
                : $"Select {CountToDiscard} tokens to discard";

            client.UI.SetStatus("Drafting", statusText);
            client.UI.UpdateScore(GameState.Clients[Match.ClientIndex].Score, GameState.Clients[Match.OpClientIndex].Score);
        }

        private void OnTokenClicked(TokenView tokenView)
        {
            // Deselect a selected token
            if (tokenView.IsSelected)
            {
                if (tokenView.SetSelected(false))
                {
                    selectedTokenViews.Remove(tokenView);

                    // We can ensure all tokens are interactable again
                    foreach (var otherTokenView in MySceneObjects.TokenViews)
                    {
                        otherTokenView.SetInteractionMode(TokenView.InteractionModeType.Discard);
                    }
                }
            }

            // Select a new token
            else if (!SelectedEnoughTokens)
            {
                if (tokenView.SetSelected(true))
                {
                    selectedTokenViews.Add(tokenView);

                    // If we have selected the limit disable others
                    if (SelectedEnoughTokens)
                    {
                        foreach (var otherTokenView in MySceneObjects.TokenViews)
                        {
                            if (!otherTokenView.IsSelected)
                            {
                                otherTokenView.SetInteractionMode(TokenView.InteractionModeType.None);
                            }
                        }
                    }
                }
            }

            // Cannot interact once we've selected enough
            SceneLayout.Drafting.DraftConfirmButton.Interactable = SelectedEnoughTokens;

            UpdateStatusUI();
        }

        private async void OnConfirmDiscardClicked()
        {
            Assert.True(SelectedEnoughTokens);
            Assert.False(hasDiscarded);

            // Update local state to stop further interaction
            hasDiscarded = true;
            SceneLayout.Drafting.DraftConfirmButton.Interactable = false;
            SceneLayout.Drafting.DraftConfirmButton.gameObject.SetActive(false);

            client.UI.SetMyAvatar(AvatarState.Ready);

            // Tell the server which instances we discarded
            var message = new DraftDiscardMessage
            {
                DiscardedInstanceIds = selectedTokenViews
                    .Select(tv => tv.TokenInstance.InstanceId)
                    .ToList()
            };
            var response = await Match.RequestDraftDiscard(message);

            // TODO: Handle failure response
            Assert.True(response);

            // Delete discarded token views
            foreach (var tokenView in selectedTokenViews)
            {
                GameObject.Destroy(tokenView.gameObject);
            }

            MySceneObjects.TokenViews = MySceneObjects.TokenViews
                .Where(tv => !selectedTokenViews.Contains(tv))
                .ToArray();

            // Animate each token back into the row
            List<Task> tasks = new();
            for (int i = 0; i < MySceneObjects.TokenViews.Length; i++)
            {
                var tokenView = MySceneObjects.TokenViews[i];

                tokenView.OnClicked.RemoveListener(OnTokenClicked);
                tokenView.SetInteractionMode(TokenView.InteractionModeType.None);

                Vector3 targetPos = MySceneObjects.GetDraftTokenRowIndexPosition(i, MySceneObjects.TokenViews.Length);

                // Animate directly into the row
                tasks.Add(tokenView.Animator.Play(AnimationSequenceBuilder
                    .Start()
                    .Next(tokenView.CreateCurrentPositionAnimationTrack(
                        0.6f, AnimationCurves.Lerp(tokenView.transform.position, targetPos), Easing.EaseOutCubic))
                    .Build(), cts.Token));
            }
            await Task.WhenAll(tasks);

            UpdateStatusUI();
        }

        private async void OnOtherPlayerDiscarded()
        {
            client.UI.SetOpAvatar(AvatarState.Ready);
        }

        private async void OnPlayingPhaseStarted()
        {
            await client.GotoState(ClientStateType.Playing);
        }
    }
}
