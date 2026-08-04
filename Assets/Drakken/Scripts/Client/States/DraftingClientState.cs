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

            GameEntrypoint.Singleton.Client.Camera.SetTarget(SceneLayout.Drafting.CameraPosition);

            UpdateStatusUI();

            if (fromStateType == ClientStateType.Title)
            {
                SceneLayout.Shared.Mat1.SetActive(true);
                SceneLayout.Shared.Mat2.SetActive(true);

                // When coming from the title create the dice
                CreateAndRollDice();
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
                SceneLayout.Shared.OnDisconnect();
                SceneObjects.OnDisconnect();
                client.UI.OnDisconnect();
            }

            // Cleanup this scenes specific objects
            SceneLayout.Drafting.DraftConfirmButton.Clicked -= OnConfirmDiscardClicked;
            SceneLayout.Drafting.DraftConfirmButton.gameObject.SetActive(false);
        }

        private void CreateAndRollDice()
        {
            Assert.True(SceneObjects.MyDiceViews.Length == 0);
            Assert.True(SceneObjects.OpDiceViews.Length == 0);

            // For both you and opponent
            for (int clientIndex = 0; clientIndex < 2; clientIndex++)
            {
                var areMyDice = Match.ClientIndex == clientIndex;
                var diceRow = areMyDice ? SceneLayout.Shared.MyDiceRow : SceneLayout.Shared.OpDiceRow;
                var clientDice = GameState.Clients[clientIndex].Dice;

                // Create a row of dice views
                if (areMyDice) SceneObjects.MyDiceViews = new DiceView[clientDice.Count];
                else SceneObjects.OpDiceViews = new DiceView[clientDice.Count];

                for (int i = 0; i < clientDice.Count; i++)
                {
                    var diceInstance = clientDice[i];
                    var diceView = DiceView.Create(client.Assets, diceInstance);
                    if (diceView == null) continue;

                    if (areMyDice) SceneObjects.MyDiceViews[i] = diceView;
                    else SceneObjects.OpDiceViews[i] = diceView;

                    float offset = (i - (clientDice.Count - 1) / 2f) * SceneLayout.Shared.TokenSpacing;
                    Vector3 targetPos = diceRow.position + new Vector3(offset, 0f, 0f);
                    diceView.transform.SetPositionAndRotation(targetPos, Quaternion.identity);

                    diceView.SetInteractable(true);
                }
            }

            // Update UI to match dice totals
            client.UI.UpdateMyDiceTotal();
            client.UI.UpdateOpDiceTotal();
        }

        private void RollExistingDice()
        {
            Assert.True(SceneObjects.MyDiceViews.Length != 0);
            Assert.True(SceneObjects.OpDiceViews.Length != 0);

            // TODO: Update dice to match the gamestate

            // Update UI to match dice totals
            client.UI.UpdateMyDiceTotal();
            client.UI.UpdateOpDiceTotal();
        }

        private async void SpawnTokens()
        {
            Assert.True(SceneObjects.MyTokenViews.Length == 0);

            Vector3 bagStartPos = SceneLayout.Shared.Bag1.transform.position;

            // Create each token object in a row
            var tokenCount = GameState.Clients[Match.ClientIndex].Tokens.Count;
            SceneObjects.MyTokenViews = new TokenView[tokenCount];

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

                    SceneObjects.MyTokenViews[tokenIndex] = tokenView;

                    // Start small inside the bag
                    tokenView.SetPositionAndRotation(bagStartPos, Quaternion.identity);
                    tokenView.transform.localScale = Vector3.zero;

                    // Animate up and then across
                    Vector3 bagAbovePos = bagStartPos + Vector3.up * 1.5f;
                    float targetOffsetX = (tokenIndex - (tokenCount - 1) / 2f) * SceneLayout.Shared.TokenSpacing;
                    Vector3 targetPos = SceneLayout.Drafting.DraftTokenRow.position + new Vector3(targetOffsetX, 0f, 0f);

                    var animation = new AnimationSequenceBuilder()
                        .At(0.0f, AnimationTracks.LocalScale(0.6f, tokenView.transform, Vector3.zero, Vector3.one, Easing.EaseInCubic))
                        .At(0.0f, AnimationTracks.Rotation(1.2f, tokenView.transform, Quaternion.identity, SceneLayout.Drafting.DraftTokenRow.rotation, Easing.Linear))
                        .Next(tokenView.CreateCurrentPositionAnimationTrack(
                            0.4f, AnimationCurves.Lerp(bagStartPos, bagAbovePos), Easing.Linear))
                        .Next(tokenView.CreateCurrentPositionAnimationTrack(
                            0.8f, AnimationCurves.QuadraticBezier(bagAbovePos, bagAbovePos + Vector3.up * 0.7f, targetPos), Easing.EaseOutCubic))
                        .Build();

                    await tokenView.Animator.Play(animation, cts.Token);

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
                    foreach (var otherTokenView in SceneObjects.MyTokenViews)
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
                        foreach (var otherTokenView in SceneObjects.MyTokenViews)
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

            SceneObjects.MyTokenViews = SceneObjects.MyTokenViews
                .Where(tv => !selectedTokenViews.Contains(tv))
                .ToArray();

            // Animate each token back into the row
            for (int i = 0; i < SceneObjects.MyTokenViews.Length; i++)
            {
                var tokenView = SceneObjects.MyTokenViews[i];

                tokenView.OnClicked.RemoveListener(OnTokenClicked);
                tokenView.InteractionMode = TokenView.InteractionModeType.None;

                float targetOffsetX = (i - (SceneObjects.MyTokenViews.Length - 1) / 2f) * SceneLayout.Shared.TokenSpacing;
                Vector3 targetPos = SceneLayout.Drafting.DraftTokenRow.position + new Vector3(targetOffsetX, 0f, 0f);

                // Animate directly into the row
                var animation = new AnimationSequenceBuilder()
                    .Next(tokenView.CreateCurrentPositionAnimationTrack(
                        0.6f, AnimationCurves.Lerp(tokenView.transform.position, targetPos), Easing.EaseOutCubic))
                    .Build();

                _ = tokenView.Animator.Play(animation, cts.Token);
            }

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
