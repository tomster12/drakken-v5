using Drakken.Client.GameObjects;
using Drakken.Common.Utility;
using Drakken.Domain;
using Drakken.Domain.Networking;
using Drakken.Networking;
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
        private SceneLayout Layout => GameEntrypoint.Singleton.Scene;
        private bool SelectedEnoughTokens => selectedTokenViews.Count >= DiscardCount;

        // ------------------------------ Setup

        public override Task Enter()
        {
            var registry = client.TokenRegistry;

            // Spawn tokens
            var tokenAnchor = Layout.Drafting.DraftTokenRow;
            var tokenHand = GameState.Clients[Match.ClientIndex].Hand;
            for (int i = 0; i < tokenHand.Count; i++)
            {
                var tokenInstance = tokenHand[i];
                var tokenView = TokenView.Create(client.Assets, client.TokenRegistry, tokenInstance, tokenAnchor);
                if (tokenView == null) continue;

                tokenView.OnClicked.AddListener(OnTokenClicked);
                tokenViews.Add(tokenView);

                float offset = (i - (tokenHand.Count - 1) / 2f) * Layout.Shared.TokenSpacing;
                Vector3 worldPos = tokenAnchor.TransformPoint(new Vector3(offset, 0f, 0f));
                tokenView.TargetPosition = worldPos;
                tokenView.transform.position = worldPos;
            }

            // Spawn own and opponent dice
            SpawnDiceRow(Match.ClientIndex, Layout.Shared.MyDiceRow);
            SpawnDiceRow(1 - Match.ClientIndex, Layout.Shared.OpponentDiceRow);
            Layout.Drafting.DraftConfirmButton.gameObject.SetActive(true);
            Layout.Drafting.DraftConfirmButton.Interactable = false;
            Layout.Drafting.DraftConfirmButton.Clicked += OnConfirmClicked;

            return Task.CompletedTask;
        }

        public override Task Exit()
        {
            Match.PlayingPhaseStarted -= OnPlayingPhaseStarted;

            foreach (var view in tokenViews)
            {
                Object.Destroy(view.gameObject);
            }
            tokenViews.Clear();
            selectedTokenViews.Clear();

            Layout.Drafting.DraftConfirmButton.gameObject.SetActive(false);
            Layout.Drafting.DraftConfirmButton.Interactable = false;
            Layout.Drafting.DraftConfirmButton.Clicked -= OnConfirmClicked;

            return Task.CompletedTask;
        }

        private void SpawnDiceRow(int playerIndex, Transform anchor)
        {
            var dice = GameState.Clients[playerIndex].Dice;
            for (int i = 0; i < dice.Count; i++)
            {
                var diceGo = Object.Instantiate(client.Assets.DiceViewPrefab, anchor);
                var diceView = diceGo.GetComponent<DiceView>();

                diceView.Bind(dice[i]);

                float offset = (i - (dice.Count - 1) / 2f) * Layout.Shared.DiceSpacing;
                diceGo.transform.localPosition = new Vector3(offset, 0f, 0f);
            }
        }

        // ------------------------------ Logic

        private void OnTokenClicked(TokenView tokenView)
        {
            if (tokenView.IsSelected)
            {
                tokenView.SetSelected(false);
                selectedTokenViews.Remove(tokenView);
            }
            else
            {
                if (SelectedEnoughTokens) return;

                tokenView.SetSelected(true);
                selectedTokenViews.Add(tokenView);
            }

            Layout.Drafting.DraftConfirmButton.Interactable = SelectedEnoughTokens;
        }

        private async void OnConfirmClicked()
        {
            Assert.True(SelectedEnoughTokens);

            Layout.Drafting.DraftConfirmButton.Interactable = false;
            foreach (var view in tokenViews) view.SetInteractable(false);

            var message = new DraftDiscardMessage
            {
                DiscardInstanceId0 = selectedTokenViews[0].TokenInstance.InstanceId,
                DiscardInstanceId1 = selectedTokenViews[1].TokenInstance.InstanceId,
            };

            Match.PlayingPhaseStarted += OnPlayingPhaseStarted;

            Match.SendDraftDiscard(message);
        }

        private async void OnPlayingPhaseStarted()
        {
            await client.GotoState(new PlayingClientState());
        }
    }
}
