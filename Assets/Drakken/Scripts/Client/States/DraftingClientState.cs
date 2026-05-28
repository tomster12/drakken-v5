using Drakken.Client.GameObjects;
using Drakken.Common.Utility;
using Drakken.Domain;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

namespace Drakken.Client.States
{
    public class DraftingClientState : ClientState
    {
        private const int DiscardCount = 2;

        private readonly List<TokenView> tokenViews = new();
        private readonly List<TokenView> selectedTokenViews = new();
        private PhysicalButton confirmButton;
        private SceneLayout Layout => SceneLayout.Singleton;

        public override Task Enter()
        {
            SpawnTokens();
            SpawnDice();

            Layout.Drafting.ConfirmButton.gameObject.SetActive(true);
            Layout.Drafting.ConfirmButton.Interactable = true;
            Layout.Drafting.ConfirmButton.Clicked += OnConfirmClicked;

            return Task.CompletedTask;
        }

        public override async Task Exit()
        {
            client.Match.PlayingPhaseStarted -= OnPlayingPhaseStarted;

            foreach (var card in tokenViews)
            {
                if (card != null) Object.Destroy(card.gameObject);
            }

            tokenViews.Clear();
            selectedTokenViews.Clear();

            Layout.Drafting.ConfirmButton.gameObject.SetActive(false);
            Layout.Drafting.ConfirmButton.Interactable = false;
            Layout.Drafting.ConfirmButton.Clicked -= OnConfirmClicked;
        }

        private void SpawnTokens()
        {
            var registry = client.TokenRegistry;
            var assets = client.Assets;
            var anchor = Layout.Drafting.DraftTokenRow;

            var hand = GameState.Clients[Match.ClientIndex].Hand;
            for (int i = 0; i < hand.Count; i++)
            {
                registry.TryGetDefinition(hand[i].TokenId, out var definition);

                var tokenGo = Object.Instantiate(assets.TokenPrefab, anchor);
                var tokenView = tokenGo.GetComponent<TokenView>();
                tokenView.Bind(hand[i], definition);
                
                tokenView.OnClicked.AddListener(OnTokenClicked);
                tokenViews.Add(tokenView);

                // Space cards evenly in a row along X
                float offset = (i - (hand.Count - 1) / 2f) * Layout.Shared.CardSpacing;
                tokenGo.transform.localPosition = new Vector3(offset, 0f, 0f);
            }
        }

        private void OnTokenClicked(TokenView tokenView)
        {
            if (tokenView.IsSelected)
            {
                tokenView.SetSelected(false);
                selectedTokenViews.Remove(tokenView);
            }
            else
            {
                if (selectedTokenViews.Count >= DiscardCount) return;
                tokenView.SetSelected(true);
                selectedTokenViews.Add(tokenView);
            }

            confirmButton.Interactable = selectedTokenViews.Count == DiscardCount;
        }

        private void SpawnDice()
        {
            SpawnDiceRow(Match.ClientIndex, Layout.Shared.MyDiceRow);
            SpawnDiceRow(1 - Match.ClientIndex, Layout.Shared.OpponentDiceRow);
        }

        private void SpawnDiceRow(int playerIndex, Transform anchor)
        {
            var dice = GameState.Clients[playerIndex].Dice;
            for (int i = 0; i < dice.Count; i++)
            {
                var go = Object.Instantiate(client.Assets.DicePrefab, anchor);
                var view = go.GetComponent<DiceView>();

                float offset = (i - (dice.Count - 1) / 2f) * Layout.Shared.DiceSpacing;
                go.transform.localPosition = new Vector3(offset, 0f, 0f);

                view.Bind(dice[i]);
            }
        }

        private async void OnConfirmClicked()
        {
            Assert.True(selectedTokenViews.Count == DiscardCount);

            confirmButton.Interactable = false;
            foreach (var card in tokenViews) card.SetInteractable(false);

            var msg = new DraftDiscardMessage
            {
                DiscardInstanceId0 = selectedTokenViews[0].TokenInstance.InstanceId,
                DiscardInstanceId1 = selectedTokenViews[1].TokenInstance.InstanceId,
            };

            client.Connection.MessageMatchDraftDiscard(msg);

            Log.Info("DraftingState", "Discard sent, waiting for playing phase...");

            // Wait for server to confirm both players drafted → PlayingPhase
            client.Match.PlayingPhaseStarted += OnPlayingPhaseStarted;
        }

        private async void OnPlayingPhaseStarted()
        {
            await client.GotoState(new PlayingClientState());
        }
    }
}