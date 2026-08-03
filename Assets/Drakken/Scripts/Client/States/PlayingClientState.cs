using System;
using System.Threading.Tasks;
using Drakken.Client.World;
using Drakken.Common.Utility;
using Drakken.Domain.Networking;
using Drakken.Domain.Tokens.Logic;
using UnityEngine;

namespace Drakken.Client.States
{
    public class PlayingClientState : ClientState
    {
        private SceneLayout SceneLayout => GameEntrypoint.Singleton.Client.SceneLayout;
        private SceneObjects SceneObjects => GameEntrypoint.Singleton.Client.SceneObjects;
        private TokenView selectedTokenView = null;

        public override async Task Enter(ClientStateType fromType)
        {
            Match.OnTokenResolved += OnTokenResolved;

            GameEntrypoint.Singleton.Client.Camera.SetTarget(SceneLayout.Playing.CameraPosition);

            SetupTokens();

            UpdateStatusUI();
        }

        private void SetupTokens()
        {
            // Place each token into the row
            for (int i = 0; i < SceneObjects.MyTokenViews.Count; i++)
            {
                var tokenView = SceneObjects.MyTokenViews[i];

                tokenView.transform.SetParent(SceneLayout.Shared.MyTokenRow, worldPositionStays: true);
                float offset = (i - (SceneObjects.MyTokenViews.Count - 1) / 2f) * SceneLayout.Shared.TokenSpacing;
                Vector3 localPos = new(offset, 0f, 0f);
                tokenView.TargetLocalPosition = localPos;
                tokenView.transform.localPosition = localPos;
                tokenView.transform.localRotation = Quaternion.identity;

                tokenView.InteractionMode = TokenView.InteractionModeType.None;
            }

            // Create each opponent token object in a row
            for (int i = 0; i < GameState.Clients[Match.OpClientIndex].Tokens.Count; i++)
            {
                var tokenView = TokenView.CreateEmpty(client.Assets, SceneLayout.Shared.OpTokenRow);
                if (tokenView == null) continue;

                // Place into the row
                float offset = (i - (GameState.Clients[Match.OpClientIndex].Tokens.Count - 1) / 2f) * SceneLayout.Shared.TokenSpacing;
                Vector3 localPos = new(offset, 0f, 0f);
                tokenView.TargetLocalPosition = localPos;
                tokenView.transform.localPosition = localPos;
                tokenView.transform.localRotation = Quaternion.identity;

                tokenView.InteractionMode = TokenView.InteractionModeType.None;
                SceneObjects.OpTokenViews.Add(tokenView);
            }

            // Start turn
            if (Match.IsMyTurn) StartMyTurn();
            else StartOpTurn();
        }

        public override async Task Exit(ClientStateType toStateType)
        {
            Match.OnTokenResolved -= OnTokenResolved;

            // If we are going back to title then clean up the token / dice views
            if (toStateType == ClientStateType.Title)
            {
                SceneLayout.Shared.OnDisconnect();
                SceneObjects.OnDisconnect();
                client.UI.OnDisconnect();
            }

            selectedTokenView = null;
        }

        private void StartMyTurn()
        {
            // Enable all tokens to be playable
            foreach (var tokenView in SceneObjects.MyTokenViews)
            {
                tokenView.InteractionMode = TokenView.InteractionModeType.Play;
                tokenView.OnClicked.AddListener(OnTokenClicked);
            }
        }

        private void StartOpTurn()
        {
            // Cannot play tokens on opponents turn
            foreach (var tokenView in SceneObjects.MyTokenViews)
            {
                tokenView.InteractionMode = TokenView.InteractionModeType.None;
            }
        }

        private void UpdateStatusUI()
        {
            var whoseTurn = Match.IsMyTurn ? "Your Turn" : "Opponent's Turn";
            client.UI.SetStatus($"Round {Match.GameState.Round}", whoseTurn);
        }

        private async void OnTokenClicked(TokenView tokenView)
        {
            // First we want to select a token
            if (selectedTokenView != tokenView)
            {
                selectedTokenView?.SetSelected(false);
                tokenView.SetSelected(true);
                selectedTokenView = tokenView;
                return;
            }

            // We must be playing a token, first disable interacting with tokens
            tokenView.SetSelected(false);
            foreach (var otherTokenView in SceneObjects.MyTokenViews)
            {
                otherTokenView.InteractionMode = TokenView.InteractionModeType.None;
                otherTokenView.OnClicked.RemoveListener(OnTokenClicked);
            }

            // Now setup the intent for the token
            var tokenRegistry = GameEntrypoint.Singleton.TokenRegistry;
            var entry = tokenRegistry.GetEntryOrThrow(tokenView.TokenDefinition.TokenId);

            var visualContext = new TokenVisualContext(SceneLayout, SceneObjects);
            var intent = await entry.Visuals.IntentPicker.PickIntent(visualContext);

            // Tell the server our intended play
            var message = new TokenIntentMessage
            {
                TokenId = tokenView.TokenDefinition.TokenId,
                InstanceId = tokenView.TokenInstance.InstanceId,
                Intent = intent
            };
            var response = await Match.RequestPlayToken(message);

            // TODO: Handle failure response
            Assert.True(response);
        }

        private async void OnTokenResolved(TokenResolutionMessage message)
        {
            var tokenRegistry = GameEntrypoint.Singleton.TokenRegistry;
            var entry = tokenRegistry.GetEntryOrThrow(message.TokenId);

            var visualContext = new TokenVisualContext(SceneLayout, SceneObjects);
            await entry.Visuals.Animator.Animate(Match.GameState, message.Resolution, visualContext, message.SourceClientIndex);
        }
    }
}
