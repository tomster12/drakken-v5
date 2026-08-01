using System.Threading.Tasks;
using Drakken.Client.World;
using UnityEngine;

namespace Drakken.Client.States
{
    public class PlayingClientState : ClientState
    {
        private SceneLayout Layout => GameEntrypoint.Singleton.Scene;
        private SceneShared Shared => GameEntrypoint.Singleton.Client.Shared;

        public override Task Enter(ClientStateType fromType)
        {
            UpdateStatusUI();

            SetupTokens();

            return Task.CompletedTask;
        }

        private void SetupTokens()
        {
            // Place each token into the row
            for (int i = 0; i < Shared.MyTokenViews.Count; i++)
            {
                var tokenView = Shared.MyTokenViews[i];

                tokenView.transform.SetParent(Layout.Shared.MyTokenRow, worldPositionStays: true);
                float offset = (i - (Shared.MyTokenViews.Count - 1) / 2f) * Layout.Shared.TokenSpacing;
                Vector3 localPos = new(offset, 0f, 0f);
                tokenView.TargetLocalPosition = localPos;
                tokenView.transform.localPosition = localPos;
                tokenView.transform.localRotation = Quaternion.identity;

                tokenView.SetInteractable(false);
            }

            // Create each token object in a row
            for (int i = 0; i < GameState.Clients[Match.OpClientIndex].Tokens.Count; i++)
            {
                var tokenView = TokenView.CreateEmpty(client.Assets, Layout.Shared.OpTokenRow);
                if (tokenView == null) continue;

                // Place into the row
                float offset = (i - (GameState.Clients[Match.OpClientIndex].Tokens.Count - 1) / 2f) * Layout.Shared.TokenSpacing;
                Vector3 localPos = new(offset, 0f, 0f);
                tokenView.TargetLocalPosition = localPos;
                tokenView.transform.localPosition = localPos;
                tokenView.transform.localRotation = Quaternion.identity;

                tokenView.SetInteractable(false);
                Shared.OpTokenViews.Add(tokenView);
            }

            // Start turn
            if (Match.IsMyTurn) StartMyTurn();
            else StartOpTurn();
        }

        public override Task Exit(ClientStateType toStateType)
        {
            // If we are going back to title then clean up the token / dice views
            if (toStateType == ClientStateType.Title)
            {
                Layout.Shared.OnDisconnect();
                Shared.OnDisconnect();
                client.UI.OnDisconnect();
            }

            return Task.CompletedTask;
        }

        private void StartMyTurn()
        {
            // Enable all tokens to be playable
            foreach (var tokenView in Shared.MyTokenViews)
            {
                tokenView.IsInteractable = true;
                tokenView.OnClicked.AddListener(OnTokenClicked);
            }
        }

        private void StartOpTurn()
        {
            // Cannot play tokens on opponents turn
            foreach (var tokenView in Shared.MyTokenViews)
            {
                tokenView.IsInteractable = false;
            }
        }

        private void UpdateStatusUI()
        {
            var whoseTurn = Match.IsMyTurn ? "Your Turn" : "Opponent's Turn";
            client.UI.SetStatus($"Round {Match.GameState.Round}", whoseTurn);
        }

        private void OnTokenClicked(TokenView tokenView)
        {
            // Disable all tokens
            foreach (var otherTokenView in Shared.MyTokenViews)
            {
                otherTokenView.IsInteractable = false;
                otherTokenView.OnClicked.RemoveListener(OnTokenClicked);
            }

            // Send message to play token
        }
    }
}
