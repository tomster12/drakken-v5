using System;
using System.Threading;
using System.Threading.Tasks;
using Drakken.Client.World;
using Drakken.Client.World.Animation;
using Drakken.Common.Utility;
using Drakken.Domain.Networking;
using Drakken.Domain.Tokens.Logic;
using Drakken.Utility;
using UnityEngine;

namespace Drakken.Client.States
{
    public class PlayingClientState : ClientState
    {
        private SceneLayout SceneLayout => GameEntrypoint.Singleton.Client.SceneLayout;
        private SceneObjects SceneObjects => GameEntrypoint.Singleton.Client.SceneObjects;
        private TokenView selectedTokenView = null;
        private CancellationTokenSource cts = new();

        public override async Task Enter(ClientStateType fromType)
        {
            cts = new();

            Match.OnTokenResolved += OnTokenResolved;

            GameEntrypoint.Singleton.Client.Camera.SetTarget(SceneLayout.Playing.CameraPosition);

            SetupTokens();

            UpdateStatusUI();
        }

        private void SetupTokens()
        {
            // Place each token into the row
            for (int i = 0; i < SceneObjects.MyTokenViews.Length; i++)
            {
                var tokenView = SceneObjects.MyTokenViews[i];

                tokenView.InteractionMode = TokenView.InteractionModeType.None;

                float offset = (i - (SceneObjects.MyTokenViews.Length - 1) / 2f) * SceneLayout.Shared.TokenSpacing;
                Vector3 targetPos = SceneLayout.Shared.MyTokenRow.position + new Vector3(offset, 0f, 0f);

                // Animate directly into the row
                var animation = new AnimationSequenceBuilder()
                    .Next(
                        tokenView.CreateCurrentPositionAnimationTrack(
                            0.6f, AnimationCurves.Lerp(tokenView.transform.position, targetPos), Easing.EaseOutCubic),
                        AnimationTracks.Rotation(
                            0.4f, tokenView.transform, tokenView.transform.rotation, SceneLayout.Shared.MyTokenRow.rotation, Easing.EaseOutCubic))
                    .Build();

                _ = tokenView.Animator.Play(animation, cts.Token);
            }

            // Create each opponent token object in a row
            var opTokenCount = GameState.Clients[Match.OpClientIndex].Tokens.Count;
            SceneObjects.OpTokenViews = new TokenView[opTokenCount];

            for (int i = 0; i < opTokenCount; i++)
            {
                var tokenView = TokenView.CreateEmpty(client.Assets);
                Assert.NotNull(tokenView);
                SceneObjects.OpTokenViews[i] = tokenView;

                tokenView.InteractionMode = TokenView.InteractionModeType.None;

                float offset = (i - (opTokenCount - 1) / 2f) * SceneLayout.Shared.TokenSpacing;
                Vector3 targetPos = SceneLayout.Shared.OpTokenRow.position + new Vector3(offset, 0f, 0f);

                tokenView.SetPositionAndRotation(targetPos, SceneLayout.Shared.OpTokenRow.rotation);
            }

            // Start turn
            if (Match.IsMyTurn) StartMyTurn();
            else StartOpTurn();
        }

        public override async Task Exit(ClientStateType toStateType)
        {
            cts.Cancel();
            cts.Dispose();

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
