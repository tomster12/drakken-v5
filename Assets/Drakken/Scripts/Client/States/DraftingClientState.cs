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
        private SceneLayout SceneLayout => GameEntrypoint.Singleton.Client.SceneLayout;
        private SceneObjects SceneObjects => GameEntrypoint.Singleton.Client.SceneObjects;
        private int CountToDiscard => GameConstants.DraftingTokenCount - GameConstants.StandardTokenCount;
        private bool SelectedEnoughTokens => selectedTokenViews.Count >= CountToDiscard;
        private readonly List<TokenView> selectedTokenViews = new();
        private bool hasDiscarded;

        // ------------------------------ Setup

        public override async Task Enter(ClientStateType fromStateType)
        {
            Match.OnPlayingPhaseStarted += OnPlayingPhaseStarted;
            Match.OnOtherPlayerDiscarded += OnOtherPlayerDiscarded;

            // Initialise this scenes objects
            SceneLayout.Drafting.DraftConfirmButton.gameObject.SetActive(true);
            SceneLayout.Drafting.DraftConfirmButton.Interactable = false;
            SceneLayout.Drafting.DraftConfirmButton.Clicked += OnConfirmDiscardClicked;

            GameEntrypoint.Singleton.Client.Camera.SetTarget(SceneLayout.Drafting.CameraPosition);

            UpdateStatusUI();

            // When coming from the title create the dice
            if (fromStateType == ClientStateType.Title)
            {
                SceneLayout.Shared.Mat1.SetActive(true);
                SceneLayout.Shared.Mat2.SetActive(true);

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
            Assert.True(SceneObjects.MyDiceViews.Count == 0);
            Assert.True(SceneObjects.OpDiceViews.Count == 0);

            // For each player create a row of dice views
            for (int clientIndex = 0; clientIndex < 2; clientIndex++)
            {
                // Check if placing yours or opponents
                var areMyDice = Match.ClientIndex == clientIndex;
                var diceRowParent = areMyDice ? SceneLayout.Shared.MyDiceRow : SceneLayout.Shared.OpDiceRow;
                var clientDice = GameState.Clients[clientIndex].Dice;

                for (int i = 0; i < clientDice.Count; i++)
                {
                    // Create new instance
                    var diceInstance = clientDice[i];
                    var diceView = DiceView.Create(client.Assets, diceInstance, diceRowParent);
                    if (diceView == null) continue;

                    // Place into the row
                    float offset = (i - (clientDice.Count - 1) / 2f) * SceneLayout.Shared.TokenSpacing;
                    Vector3 localPos = new(offset, 0f, 0f);
                    diceView.transform.localPosition = localPos;
                    diceView.transform.localRotation = Quaternion.identity;

                    // Allow hovering both players dice
                    diceView.SetInteractable(true);

                    if (areMyDice)
                        SceneObjects.MyDiceViews.Add(diceView);
                    else SceneObjects.OpDiceViews.Add(diceView);
                }
            }

            // Update UI to match dice totals
            client.UI.UpdateMyDiceTotal();
            client.UI.UpdateOpDiceTotal();
        }

        private void RollExistingDice()
        {
            Assert.True(SceneObjects.MyDiceViews.Count != 0);
            Assert.True(SceneObjects.OpDiceViews.Count != 0);

            // TODO: Update dice to match the gamestate

            // Update UI to match dice totals
            client.UI.UpdateMyDiceTotal();
            client.UI.UpdateOpDiceTotal();
        }

        private async void SpawnTokens()
        {
            Assert.True(SceneObjects.MyTokenViews.Count == 0);

            // Create each token object in a row
            for (int i = 0; i < GameState.Clients[Match.ClientIndex].Tokens.Count; i++)
            {
                var tokenInstance = GameState.Clients[Match.ClientIndex].Tokens[i];
                var tokenView = TokenView.Create(client.Assets, GameEntrypoint.Singleton.TokenRegistry, tokenInstance, SceneLayout.Drafting.DraftTokenRow);
                if (tokenView == null) continue;

                // Start at the bag
                // tokenView.transform.position = SceneLayout.Shared.Bag1.transform.position;

                // Calculate target position
                float targetOffsetX = (i - (GameState.Clients[Match.ClientIndex].Tokens.Count - 1) / 2f) * SceneLayout.Shared.TokenSpacing;
                Vector3 targeLocalPos = new(targetOffsetX, 0f, 0f);
                tokenView.TargetLocalPosition = targeLocalPos;
                tokenView.transform.localPosition = targeLocalPos;
                tokenView.transform.localRotation = Quaternion.identity;

                SceneObjects.MyTokenViews.Add(tokenView);

                // tokenView.InteractionMode = TokenView.InteractionModeType.Discard;
                // tokenView.OnClicked.AddListener(OnTokenClicked);
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
                SceneObjects.MyTokenViews.Remove(tokenView);
            }

            // Place each token back into the row
            for (int i = 0; i < SceneObjects.MyTokenViews.Count; i++)
            {
                var tokenView = SceneObjects.MyTokenViews[i];

                tokenView.transform.SetParent(SceneLayout.Drafting.DraftTokenRow, worldPositionStays: true);
                float offset = (i - (SceneObjects.MyTokenViews.Count - 1) / 2f) * SceneLayout.Shared.TokenSpacing;
                Vector3 localPos = new(offset, 0f, 0f);
                tokenView.TargetLocalPosition = localPos;
                tokenView.transform.localPosition = localPos;
                tokenView.transform.localRotation = Quaternion.identity;

                tokenView.OnClicked.RemoveListener(OnTokenClicked);
                tokenView.InteractionMode = TokenView.InteractionModeType.None;
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
