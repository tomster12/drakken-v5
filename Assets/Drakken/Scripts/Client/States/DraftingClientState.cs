using Drakken.Client.World;
using Drakken.Common.Utility;
using Drakken.Domain;
using Drakken.Domain.Networking;
using Drakken.Domain.Static;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;

namespace Drakken.Client.States
{
    public class DraftingClientState : ClientState
    {
        private SceneLayout Layout => GameEntrypoint.Singleton.Scene;
        private SceneShared Shared => GameEntrypoint.Singleton.Client.Shared;
        private readonly List<TokenView> selectedTokenViews = new();
        private int CountToDiscard => GameConstants.DraftingTokenCount - GameConstants.StandardTokenCount;
        private bool SelectedEnoughTokens => selectedTokenViews.Count >= CountToDiscard;

        // ------------------------------ Setup

        public override Task Enter(ClientStateType fromStateType)
        {
            // Initialise this scenes objects
            Layout.Drafting.DraftConfirmButton.gameObject.SetActive(true);
            Layout.Drafting.DraftConfirmButton.Interactable = false;
            Layout.Drafting.DraftConfirmButton.Clicked += OnConfirmClicked;

            GameEntrypoint.Singleton.Client.Camera.SetTarget(Layout.Drafting.CameraPosition);

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

            return Task.CompletedTask;
        }

        public override Task Exit(ClientStateType toStateType)
        {
            Match.PlayingPhaseStarted -= OnPlayingPhaseStarted;

            // If we are going back to title then clean up the token / dice views
            if (toStateType == ClientStateType.Title)
            {
                Layout.Shared.Mat1.SetActive(false);
                Layout.Shared.Mat2.SetActive(false);

                Shared.Clear();
            }

            // Cleanup this scenes specific objects
            Layout.Drafting.DraftConfirmButton.Clicked -= OnConfirmClicked;
            Layout.Drafting.DraftConfirmButton.gameObject.SetActive(false);

            return Task.CompletedTask;
        }

        private void CreateAndRollDice()
        {
            Assert.True(Shared.MyDiceViews.Count == 0);
            Assert.True(Shared.OpponentDiceViews.Count == 0);

            // For each player create a row of dice views
            for (int clientIndex = 0; clientIndex < 2; clientIndex++)
            {
                // Check if placing yours or opponents
                var areMyDice = Match.ClientIndex == clientIndex;
                var diceRowParent = areMyDice ? Layout.Shared.MyDiceRow : Layout.Shared.OpponentDiceRow;
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
                    else Shared.OpponentDiceViews.Add(diceView);
                }
            }
        }

        private void RollExistingDice()
        {
            Assert.True(Shared.MyDiceViews.Count != 0);
            Assert.True(Shared.OpponentDiceViews.Count != 0);
        }

        private void SpawnTokens()
        {
            Assert.True(Shared.TokenViews.Count == 0);

            // Create each token object in a row
            var tokenRowParent = Layout.Drafting.DraftTokenRow;
            var tokenHand = GameState.Clients[Match.ClientIndex].Hand;
            for (int i = 0; i < tokenHand.Count; i++)
            {
                // Create new instance
                var tokenInstance = tokenHand[i];
                var tokenView = TokenView.Create(client.Assets, client.TokenRegistry, tokenInstance, tokenRowParent);
                if (tokenView == null) continue;

                // Place into the row
                float offset = (i - (tokenHand.Count - 1) / 2f) * Layout.Shared.TokenSpacing;
                Vector3 localPos = new(offset, 0f, 0f);
                tokenView.TargetLocalPosition = localPos;
                tokenView.transform.localPosition = localPos;
                tokenView.transform.localRotation = Quaternion.identity;

                // Can always interact with all the tokens
                tokenView.SetInteractable(true);

                tokenView.OnClicked.AddListener(OnTokenClicked);

                Shared.TokenViews.Add(tokenView);
            }
        }

        // ------------------------------ Logic

        private void OnTokenClicked(TokenView tokenView)
        {
            // Deselect a selected token
            if (tokenView.IsSelected)
            {
                if (tokenView.SetSelected(false))
                {
                    selectedTokenViews.Remove(tokenView);

                    // We can ensure all tokens are interactable again
                    foreach (var otherTokenView in Shared.TokenViews)
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
                        foreach (var otherTokenView in Shared.TokenViews)
                        {
                            if (!otherTokenView.IsSelected)
                            {
                                otherTokenView.SetInteractable(false);
                            }
                        }
                    }
                }
            }

            Layout.Drafting.DraftConfirmButton.Interactable = SelectedEnoughTokens;
        }

        private async void OnConfirmClicked()
        {
            Assert.True(SelectedEnoughTokens);

            // Disable confirm button and tokens now
            Layout.Drafting.DraftConfirmButton.Interactable = false;
            foreach (var tokenView in Shared.TokenViews)
            {
                tokenView.SetInteractable(false);
            }

            // Wait for playing to start now
            Match.PlayingPhaseStarted += OnPlayingPhaseStarted;

            // Tell the server which instances we discarded
            var message = new DraftDiscardMessage
            {
                DiscardedInstanceIds = selectedTokenViews
                    .Select(tv => tv.TokenInstance.InstanceId)
                    .ToList()
            };
            Match.SendDraftDiscard(message);
        }

        private async void OnPlayingPhaseStarted()
        {
            await client.GotoState(ClientStateType.Playing);
        }
    }
}
