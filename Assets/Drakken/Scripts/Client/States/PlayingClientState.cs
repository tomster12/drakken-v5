using System.Collections.Generic;
using System.Threading.Tasks;
using Drakken.Client.GameObjects;
using UnityEngine;

namespace Drakken.Client.States
{
    public class PlayingClientState : ClientState
    {
        private readonly List<DiceView> myDiceViews = new();
        private readonly List<DiceView> opponentDiceViews = new();
        private readonly List<TokenView> myHandViews = new();
        private SceneLayout Layout => SceneLayout.Singleton;

        public override Task Enter()
        {
            ClearRow(Layout.Shared.MyDiceRow);
            ClearRow(Layout.Shared.OpponentDiceRow);
            ClearRow(Layout.Shared.MyHandRow);

            SpawnMyDice();
            SpawnOpponentDice();
            SpawnMyHand();

            return Task.CompletedTask;
        }

        private void SpawnMyDice()
        {
            var dice = GameState.Clients[Match.ClientIndex].Dice;
            for (int i = 0; i < dice.Count; i++)
            {
                var go = Object.Instantiate(client.Assets.DicePrefab, Layout.Shared.MyDiceRow);
                var view = go.GetComponent<DiceView>();
                go.transform.localPosition = new Vector3(
                    (i - (dice.Count - 1) / 2f) * Layout.Shared.DiceSpacing, 0f, 0f);
                view.Bind(dice[i]);
                myDiceViews.Add(view);
            }
        }

        private void SpawnOpponentDice()
        {
            var dice = GameState.Clients[1 - Match.ClientIndex].Dice;
            for (int i = 0; i < dice.Count; i++)
            {
                var go = Object.Instantiate(client.Assets.DicePrefab, Layout.Shared.OpponentDiceRow);
                var view = go.GetComponent<DiceView>();
                go.transform.localPosition = new Vector3(
                    (i - (dice.Count - 1) / 2f) * Layout.Shared.DiceSpacing, 0f, 0f);
                view.Bind(dice[i]);
                opponentDiceViews.Add(view);
            }
        }

        private void SpawnMyHand()
        {
            var registry = client.TokenRegistry;
            var assets = client.Assets;
            var anchor = Layout.Shared.MyHandRow;

            var hand = GameState.Clients[Match.ClientIndex].Hand;
            for (int i = 0; i < hand.Count; i++)
            {
                registry.TryGetDefinition(hand[i].TokenId, out var definition);

                var tokenGo = Object.Instantiate(assets.TokenPrefab, anchor);
                var tokenView = tokenGo.GetComponent<TokenView>();
                tokenView.Bind(hand[i], definition);

                tokenView.OnClicked.AddListener(OnHandTokenClicked);
                myHandViews.Add(tokenView);

                // Space cards evenly in a row along X
                float offset = (i - (hand.Count - 1) / 2f) * Layout.Shared.CardSpacing;
                tokenGo.transform.localPosition = new Vector3(offset, 0f, 0f);
            }
        }

        private void OnHandTokenClicked(TokenView tokenView)
        {
            Debug.Log($"[PlayingState] Clicked token {tokenView.TokenInstance.TokenId}");
        }

        private static void ClearRow(Transform row)
        {
            if (row == null) return;
            foreach (Transform child in row)
                Object.Destroy(child.gameObject);
        }
    }
}