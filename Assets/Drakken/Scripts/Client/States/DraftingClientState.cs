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

        private SceneLayout Layout => SceneLayout.Singleton;

        public override Task Enter()
        {
            SpawnTokens();
            SpawnDice();

            Layout.Drafting.DraftConfirmButton.gameObject.SetActive(true);
            Layout.Drafting.DraftConfirmButton.Interactable = true;
            Layout.Drafting.DraftConfirmButton.Clicked += OnConfirmClicked;

            return Task.CompletedTask;
        }

        public override Task Exit()
        {
            client.Match.PlayingPhaseStarted -= OnPlayingPhaseStarted;

            // foreach (var view in tokenViews)
            // {
            //     if (view != null)
            //     {
            //         Object.Destroy(view.gameObject);
            //     }
            // }
            // tokenViews.Clear();
            // selectedTokenViews.Clear();

            Layout.Drafting.DraftConfirmButton.gameObject.SetActive(false);
            Layout.Drafting.DraftConfirmButton.Interactable = false;
            Layout.Drafting.DraftConfirmButton.Clicked -= OnConfirmClicked;

            return Task.CompletedTask;
        }

        private void SpawnTokens()
        {
            var registry = client.TokenRegistry;
            var anchor = Layout.Drafting.DraftTokenRow;
            var hand = GameState.Clients[Match.ClientIndex].Hand;

            for (int i = 0; i < hand.Count; i++)
            {
                var tokenView = TokenView.Create(hand[i], anchor);
                if (tokenView == null) continue;

                tokenView.OnClicked.AddListener(OnTokenClicked);
                tokenViews.Add(tokenView);

                float offset = (i - (hand.Count - 1) / 2f) * Layout.Shared.TokenSpacing;
                Vector3 worldPos = anchor.TransformPoint(new Vector3(offset, 0f, 0f));
                tokenView.TargetPosition = worldPos;
                tokenView.transform.position = worldPos;
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

            Layout.Drafting.DraftConfirmButton.Interactable = selectedTokenViews.Count == DiscardCount;
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
                var diceGo = Object.Instantiate(client.Assets.DiceViewPrefab, anchor);
                var diceView = diceGo.GetComponent<DiceView>();

                diceView.Bind(dice[i]);

                float offset = (i - (dice.Count - 1) / 2f) * Layout.Shared.DiceSpacing;
                diceGo.transform.localPosition = new Vector3(offset, 0f, 0f);
            }
        }

        private async void OnConfirmClicked()
        {
            Assert.True(selectedTokenViews.Count == DiscardCount);

            Layout.Drafting.DraftConfirmButton.Interactable = false;
            foreach (var view in tokenViews) view.SetInteractable(false);

            var message = new DraftDiscardMessage
            {
                DiscardInstanceId0 = selectedTokenViews[0].TokenInstance.InstanceId,
                DiscardInstanceId1 = selectedTokenViews[1].TokenInstance.InstanceId,
            };

            GameConnection.Singleton.C2S_MessageMatchDraftDiscard_Rpc(client.Match.MatchId, message);
            Log.Info("DraftingState", "Discard sent, waiting for playing phase...");

            client.Match.PlayingPhaseStarted += OnPlayingPhaseStarted;
        }

        private async void OnPlayingPhaseStarted()
        {
            await client.GotoState(new PlayingClientState());
        }
    }
}
