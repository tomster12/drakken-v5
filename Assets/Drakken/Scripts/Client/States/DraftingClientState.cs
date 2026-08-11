using Drakken.Client.World;
using Drakken.Domain.Animation;
using Drakken.Common.Utility;
using Drakken.Domain;
using Drakken.Networking;
using Drakken.Domain.Static;
using Drakken.Utility;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;

using static Drakken.Client.ClientUI;
using Drakken.Domain.Networking;

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

            GameEntrypoint.Singleton.Client.Camera.SetTarget(MyDraftingLayout.CameraPosition, snap: true);

            SceneLayout.Dice.P1.gameObject.SetActive(true);
            SceneLayout.Dice.P2.gameObject.SetActive(true);

            UpdateClientUI();

            await Task.Delay(500);
            await RollDice();

            await Task.Delay(1000);
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
                SceneObjects.OnDisconnect();
                client.UI.OnDisconnect();
            }

            // Cleanup this scenes specific objects
            SceneLayout.Drafting.DraftConfirmButton.Clicked -= OnConfirmDiscardClicked;
            SceneLayout.Drafting.DraftConfirmButton.gameObject.SetActive(false);
        }

        private async Task RollDice()
        {
            client.UI.UpdateDiceTotal(Match.ClientIndex, 0, unknown: true);
            client.UI.UpdateDiceTotal(Match.ClientIndex, 1, unknown: true);

            // Reset dice views to let us spawn fresh ones for this roll
            SceneObjects.P1.DestroyAllDice();
            SceneObjects.P2.DestroyAllDice();

            // Spawn dice at their resting position (grow + shake) before replaying the physics throw
            await SpawnDiceForRoll(Match.LastDiceTraces);
            await SimulateDiceTraces(Match.LastDiceTraces);

            // Update UI to match dice totals
            client.UI.UpdateDiceTotal(Match.ClientIndex, 0);
            client.UI.UpdateDiceTotal(Match.ClientIndex, 1);
        }

        private async Task SpawnDiceForRoll(MatchDiceSimulationTraces diceTraces)
        {
            List<Task> tasks = new();

            async Task AnimateDiceSpawnIn(DiceView view)
            {
                await view.AnimateGrow(cts.Token, 0.5f);
                await view.AnimateShake(cts.Token);
            }

            // Prepare a dice view based off the simulation trace
            for (int clientIndex = 0; clientIndex < 2; clientIndex++)
            {
                var sceneObjects = SceneObjects.Player(clientIndex);
                var trace = diceTraces.Player(clientIndex);

                List<DiceView> views = new();
                foreach (var diceTrace in trace.Dice)
                {
                    if (diceTrace.PoseTraces.Count == 0) continue;

                    var startPose = diceTrace.PoseTraces[0];
                    var view = DiceView.Create(client.Assets, diceTrace.Instance);
                    view.transform.SetPositionAndRotation(startPose.Position, startPose.Rotation);
                    view.transform.localScale = Vector3.zero;

                    views.Add(view);
                    tasks.Add(AnimateDiceSpawnIn(view));
                }

                sceneObjects.DiceViews = views.ToArray();
            }

            await Task.WhenAll(tasks);
        }

        private async Task SimulateDiceTraces(MatchDiceSimulationTraces diceTraces)
        {
            List<Task<Dictionary<int, DiceView>>> tasks = new();

            // Replay the dice simulation traces for each player, reusing the views we already spawned in
            for (int clientIndex = 0; clientIndex < 2; clientIndex++)
            {
                var sceneObjects = SceneObjects.Player(clientIndex);
                tasks.Add(sceneObjects.DiceSimReplayer.Play(
                    client.Assets, diceTraces.Player(clientIndex), sceneObjects, cts.Token));
            }

            // Get the final dice views out and update scene objects
            var results = await Task.WhenAll(tasks);
            for (int clientIndex = 0; clientIndex < 2; clientIndex++)
            {
                SceneObjects.Player(clientIndex).DiceViews = results[clientIndex].Values.ToArray();
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
                int taskDelay = (tokenCount - 1 - tokenIndex) * 250;

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
                            0.5f, tokenView.transform, Vector3.zero, Vector3.one, Easing.EaseInCubic))
                        .At(0.0f, AnimationTracks.Rotation(
                            0.9f, tokenView.transform, baseRot, MyDraftingLayout.DraftTokenRow.rotation, Easing.Linear))
                        .Next(tokenView.CreateCurrentPositionAnimationTrack(
                            0.3f, AnimationCurves.Lerp(bagStartPos, bagAbovePos), Easing.Linear))
                        .Next(tokenView.CreateCurrentPositionAnimationTrack(
                            0.8f, AnimationCurves.QuadraticBezier(bagAbovePos, bagAbovePos + Vector3.up * 2.0f, targetPos), Easing.EaseOutCubic))
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

        private void UpdateClientUI()
        {
            var statusText =
                hasDiscarded ? "Waiting for opponent..."
                : SelectedEnoughTokens ? "Ready to confirm"
                : $"Select {CountToDiscard} tokens to discard";

            client.UI.SetStatus("Drafting", statusText);
            client.UI.UpdateScore(
                GameState.Clients[Match.ClientIndex].Score,
                GameState.Clients[Match.OpClientIndex].Score);
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

            UpdateClientUI();
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

            UpdateClientUI();
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
