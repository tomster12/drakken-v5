using Drakken.Client.World;
using Drakken.Common.Utility;
using Drakken.Domain;
using Drakken.Domain.Networking;
using Drakken.Domain.Static;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using static Drakken.Client.ClientUI;

namespace Drakken.Client.States
{
    public class DraftingClientState : ClientState
    {
        private SceneLayout Layout => GameEntrypoint.Singleton.Scene;
        private SceneShared Shared => GameEntrypoint.Singleton.Client.Shared;
        private readonly List<TokenView> selectedTokenViews = new();
        private bool hasDiscarded;
        private int CountToDiscard => GameConstants.DraftingTokenCount - GameConstants.StandardTokenCount;
        private bool SelectedEnoughTokens => selectedTokenViews.Count >= CountToDiscard;

        // ------------------------------ Setup

        public override async Task Enter(ClientStateType fromStateType)
        {
            Match.OnPlayingPhaseStarted += OnPlayingPhaseStarted;
            Match.OnOtherPlayerDiscarded += OnOtherPlayerDiscarded;

            // Initialise this scenes objects
            Layout.Drafting.DraftConfirmButton.gameObject.SetActive(true);
            Layout.Drafting.DraftConfirmButton.Interactable = false;
            Layout.Drafting.DraftConfirmButton.Clicked += OnConfirmDiscardClicked;

            GameEntrypoint.Singleton.Client.Camera.SetTarget(Layout.Drafting.CameraPosition);

            UpdateStatusUI();

            // When coming from the title create the dice
            if (fromStateType == ClientStateType.Title)
            {
                Layout.Shared.Mat1.SetActive(true);
                Layout.Shared.Mat2.SetActive(true);

                CreateAndRollDice();
            }

            // Otherwise roll the existing dice
            else
            {
                RollExistingDice();
            }

            // And now spawn tokens ready to discard
            SpawnTokens();
        }

        public override async Task Exit(ClientStateType toStateType)
        {
            Match.OnPlayingPhaseStarted -= OnPlayingPhaseStarted;
            Match.OnOtherPlayerDiscarded -= OnOtherPlayerDiscarded;

            client.UI.SetMyAvatar(AvatarState.Visible);
            client.UI.SetOpAvatar(AvatarState.Visible);

            // If we are going back to title then clean up the token / dice views
            if (toStateType == ClientStateType.Title)
            {
                Layout.Shared.OnDisconnect();
                Shared.OnDisconnect();
                client.UI.OnDisconnect();
            }

            // Cleanup this scenes specific objects
            Layout.Drafting.DraftConfirmButton.Clicked -= OnConfirmDiscardClicked;
            Layout.Drafting.DraftConfirmButton.gameObject.SetActive(false);
        }

        private void CreateAndRollDice()
        {
            Assert.True(Shared.MyDiceViews.Count == 0);
            Assert.True(Shared.OpDiceViews.Count == 0);

            // For each player create a row of dice views
            for (int clientIndex = 0; clientIndex < 2; clientIndex++)
            {
                // Check if placing yours or opponents
                var areMyDice = Match.ClientIndex == clientIndex;
                var diceRowParent = areMyDice ? Layout.Shared.MyDiceRow : Layout.Shared.OpDiceRow;
                var clientDice = GameState.Clients[clientIndex].Dice;

                for (int i = 0; i < clientDice.Count; i++)
                {
                    // Create new instance
                    var diceInstance = clientDice[i];
                    var diceView = DiceView.Create(client.Assets, diceInstance, diceRowParent);
                    if (diceView == null) continue;

                    // Place into the row
                    float offset = (i - (clientDice.Count - 1) / 2f) * Layout.Shared.TokenSpacing;
                    Vector3 localPos = new(offset, 0f, 0f);
                    diceView.transform.localPosition = localPos;
                    diceView.transform.localRotation = Quaternion.identity;

                    // Allow hovering both players dice
                    diceView.SetInteractable(true);

                    if (areMyDice)
                        Shared.MyDiceViews.Add(diceView);
                    else Shared.OpDiceViews.Add(diceView);
                }
            }

            // Update UI to match dice totals
            client.UI.UpdateMyDiceTotal();
            client.UI.UpdateOpDiceTotal();
        }

        private void RollExistingDice()
        {
            Assert.True(Shared.MyDiceViews.Count != 0);
            Assert.True(Shared.OpDiceViews.Count != 0);

            // TODO: Update dice to match the gamestate

            // Update UI to match dice totals
            client.UI.UpdateMyDiceTotal();
            client.UI.UpdateOpDiceTotal();
        }

        private void SpawnTokens()
        {
            Assert.True(Shared.MyTokenViews.Count == 0);

            // Create each token object in a row
            for (int i = 0; i < GameState.Clients[Match.ClientIndex].Tokens.Count; i++)
            {
                // Create new instance
                var tokenInstance = GameState.Clients[Match.ClientIndex].Tokens[i];
                var tokenView = TokenView.Create(client.Assets, client.TokenRegistry, tokenInstance, Layout.Drafting.DraftTokenRow);
                if (tokenView == null) continue;

                // Place into the row
                float offset = (i - (GameState.Clients[Match.ClientIndex].Tokens.Count - 1) / 2f) * Layout.Shared.TokenSpacing;
                Vector3 localPos = new(offset, 0f, 0f);
                tokenView.TargetLocalPosition = localPos;
                tokenView.transform.localPosition = localPos;
                tokenView.transform.localRotation = Quaternion.identity;

                tokenView.SetInteractable(true);
                tokenView.OnClicked.AddListener(OnTokenClicked);
                Shared.MyTokenViews.Add(tokenView);
            }
        }

        // ------------------------------ Logic

        private void UpdateStatusUI()
        {
            var statusText = hasDiscarded
                ? "Waiting for opponent..."
                : SelectedEnoughTokens
                    ? "Ready to confirm"
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
                    foreach (var otherTokenView in Shared.MyTokenViews)
                    {
                        otherTokenView.SetInteractable(true);
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
                        foreach (var otherTokenView in Shared.MyTokenViews)
                        {
                            if (!otherTokenView.IsSelected)
                            {
                                otherTokenView.SetInteractable(false);
                            }
                        }
                    }
                }
            }

            // Cannot interact once we've selected enough
            Layout.Drafting.DraftConfirmButton.Interactable = SelectedEnoughTokens;

            UpdateStatusUI();
        }

        private async void OnConfirmDiscardClicked()
        {
            Assert.True(SelectedEnoughTokens);
            Assert.False(hasDiscarded);

            // Update local state to stop further interaction
            hasDiscarded = true;
            Layout.Drafting.DraftConfirmButton.Interactable = false;
            Layout.Drafting.DraftConfirmButton.gameObject.SetActive(false);

            client.UI.SetMyAvatar(AvatarState.Ready);

            // Tell the server which instances we discarded
            var message = new DraftDiscardMessage
            {
                DiscardedInstanceIds = selectedTokenViews
                    .Select(tv => tv.TokenInstance.InstanceId)
                    .ToList()
            };
            var response = await Match.RequestDraftDiscard(message);

            // For now assert this went through correctly
            Assert.True(response);

            // Delete discarded token views
            foreach (var tokenView in selectedTokenViews)
            {
                GameObject.Destroy(tokenView.gameObject);
                Shared.MyTokenViews.Remove(tokenView);
            }

            // Place each token back into the row
            for (int i = 0; i < Shared.MyTokenViews.Count; i++)
            {
                var tokenView = Shared.MyTokenViews[i];

                tokenView.transform.SetParent(Layout.Drafting.DraftTokenRow, worldPositionStays: true);
                float offset = (i - (Shared.MyTokenViews.Count - 1) / 2f) * Layout.Shared.TokenSpacing;
                Vector3 localPos = new(offset, 0f, 0f);
                tokenView.TargetLocalPosition = localPos;
                tokenView.transform.localPosition = localPos;
                tokenView.transform.localRotation = Quaternion.identity;

                tokenView.OnClicked.RemoveListener(OnTokenClicked);
                tokenView.SetInteractable(false);
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
