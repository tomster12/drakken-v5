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
        private CancellationTokenSource cts = new();

        // ------------------------------ Setup

        public override async Task Enter(ClientStateType fromStateType)
        {
            cts = new();

            Match.OnPlayingPhaseStarted += OnPlayingPhaseStarted;
            Match.OnOtherPlayerDiscarded += OnOtherPlayerDiscarded;

            // Initialise this scenes objects
            SceneLayout.Drafting.DraftConfirmButton.gameObject.SetActive(true);
            SceneLayout.Drafting.DraftConfirmButton.Interactable = false;
            SceneLayout.Drafting.DraftConfirmButton.Clicked += OnConfirmDiscardClicked;

            SceneLayout.Drafting.DraftConfirmButton.transform.SetPositionAndRotation(
                MyDraftingLayout.DraftConfirmButtonPosition.position, MyDraftingLayout.DraftConfirmButtonPosition.rotation);

            GameEntrypoint.Singleton.Client.Camera.SetTarget(
                MyDraftingLayout.CameraPosition, snap: true);

            UpdateStatusUI();

            if (fromStateType == ClientStateType.Title)
            {
                SceneLayout.Game.P1.Mat.SetActive(true);
                SceneLayout.Game.P2.Mat.SetActive(true);

                // When coming from the title create the dice
                await CreateAndRollDice();
            }

            else
            {
                // Otherwise roll the existing dice
                RollExistingDice();
            }

            // And now spawn tokens ready to discard
            SpawnTokens();
        }

        public override async Task Exit(ClientStateType toStateType)
        {
            cts.Cancel();
            cts.Dispose();

            Match.OnPlayingPhaseStarted -= OnPlayingPhaseStarted;
            Match.OnOtherPlayerDiscarded -= OnOtherPlayerDiscarded;

            client.UI.SetMyAvatar(AvatarState.Visible);
            client.UI.SetOpAvatar(AvatarState.Visible);

            // If we are going back to title then clean up the token / dice views
            if (toStateType == ClientStateType.Title)
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

            // For both you and opponent
            List<Task> tasks = new();
            for (int clientIndex = 0; clientIndex < 2; clientIndex++)
            {
                var diceRow = SceneLayout.Game.Player(clientIndex).DiceRow;
                var clientDice = GameState.Clients[clientIndex].Dice;

                // Create a row of dice views
                if (clientIndex == 0) SceneObjects.P1.DiceViews = new DiceView[clientDice.Count];
                else SceneObjects.P2.DiceViews = new DiceView[clientDice.Count];

                for (int i = 0; i < clientDice.Count; i++)
                {
                    var diceInstance = clientDice[i];
                    var diceView = DiceView.Create(client.Assets, diceInstance);
                    if (diceView == null) continue;

                    SceneObjects.Player(clientIndex).DiceViews[i] = diceView;

                    float offset = (i - (clientDice.Count - 1) / 2f) * SceneLayout.TokenSpacing;
                    Vector3 targetPos = diceRow.position + new Vector3(offset, 0f, 0f);
                    Quaternion targetRot = Quaternion.Euler(0, Match.ClientIndex * 180f, 0);
                    diceView.transform.SetPositionAndRotation(targetPos, targetRot);

                    // Roll dice to correct value
                    tasks.Add(diceView.AnimateRoll(cts.Token));
                }
            }
            await Task.WhenAll(tasks);

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

            // For both you and opponent roll all dice
            List<Task> tasks = new();
            for (int clientIndex = 0; clientIndex < 2; clientIndex++)
            {
                for (int i = 0; i < SceneObjects.Player(clientIndex).DiceViews.Length; i++)
                {
                    var diceView = SceneObjects.Player(clientIndex).DiceViews[i];
                    tasks.Add(diceView.AnimateRoll(cts.Token));
                }
            }
            await Task.WhenAll(tasks);

            // Update UI to match dice totals
            client.UI.UpdateDiceTotal(Match.ClientIndex, 0);
            client.UI.UpdateDiceTotal(Match.ClientIndex, 1);
        }

        private async void SpawnTokens()
        {
            Assert.True(SceneObjects.P1.TokenViews.Length == 0);
            Assert.True(SceneObjects.P2.TokenViews.Length == 0);

            Vector3 bagStartPos = SceneLayout.Game.Player(Match.ClientIndex).Bag.transform.position;

            // Create each token object in a row
            var tokenCount = GameState.Clients[Match.ClientIndex].Tokens.Count;
            MySceneObjects.TokenViews = new TokenView[tokenCount];

            for (int i = 0; i < tokenCount; i++)
            {
                // Start a new task for this token
                int tokenIndex = i;
                int taskDelay = (tokenCount - 1 - tokenIndex) * 400;
                AsyncUtility.DelayTask(taskDelay, async () =>
                {
                    var tokenInstance = GameState.Clients[Match.ClientIndex].Tokens[tokenIndex];
                    var tokenView = TokenView.Create(client.Assets, GameEntrypoint.Singleton.TokenRegistry, tokenInstance);
                    Assert.NotNull(tokenView);

                    MySceneObjects.TokenViews[tokenIndex] = tokenView;

                    // Start small inside the bag
                    Quaternion baseRot = Quaternion.Euler(0, Match.ClientIndex * 180f, 0);
                    tokenView.SetPositionAndRotation(bagStartPos, baseRot);
                    tokenView.transform.localScale = Vector3.zero;

                    // Animate up and then across
                    Vector3 bagAbovePos = bagStartPos + Vector3.up * 1.5f;
                    float targetOffsetX = (tokenIndex - (tokenCount - 1) / 2f) * SceneLayout.TokenSpacing;
                    Vector3 targetPos = MyDraftingLayout.DraftTokenRow.position + MyDraftingLayout.DraftTokenRow.right * targetOffsetX;

                    await tokenView.Animator.Play(AnimationSequenceBuilder
                        .Start()
                        .At(0.0f, AnimationTracks.LocalScale(
                            0.6f, tokenView.transform, Vector3.zero, Vector3.one, Easing.EaseInCubic))
                        .At(0.0f, AnimationTracks.Rotation(
                            1.2f, tokenView.transform, baseRot, MyDraftingLayout.DraftTokenRow.rotation, Easing.Linear))
                        .Next(tokenView.CreateCurrentPositionAnimationTrack(
                            0.7f, AnimationCurves.Lerp(bagStartPos, bagAbovePos), Easing.Linear))
                        .Next(tokenView.CreateCurrentPositionAnimationTrack(
                            0.8f, AnimationCurves.QuadraticBezier(bagAbovePos, bagAbovePos + Vector3.up * 0.7f, targetPos), Easing.EaseOutCubic))
                        .Build(), cts.Token);

                    tokenView.OnClicked.AddListener(OnTokenClicked);
                    tokenView.InteractionMode = TokenView.InteractionModeType.Discard;
                }, cts.Token);
            }
        }

        // ------------------------------ Logic

        private void UpdateStatusUI()
        {
            var statusText =
                hasDiscarded ? "Waiting for opponent..."
                : SelectedEnoughTokens ? "Ready to confirm"
                : $"Select {CountToDiscard} tokens to discard";

            client.UI.SetStatus("Drafting", statusText);
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
                        otherTokenView.InteractionMode = TokenView.InteractionModeType.Discard;
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
                                otherTokenView.InteractionMode = TokenView.InteractionModeType.None;
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
                tokenView.InteractionMode = TokenView.InteractionModeType.None;

                float targetOffsetX = (i - (MySceneObjects.TokenViews.Length - 1) / 2f) * SceneLayout.TokenSpacing;
                Vector3 targetPos = MyDraftingLayout.DraftTokenRow.position + MyDraftingLayout.DraftTokenRow.right * targetOffsetX;

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
