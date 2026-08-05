using System;
using System.Collections.Generic;
using System.Linq;
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
        private GamePlayerLayout MyPlayingLayout => SceneLayout.Game.Player(Match.ClientIndex);
        private ScenePlayerObjects MySceneObjects => SceneObjects.Player(Match.ClientIndex);
        private TokenView selectedTokenView = null;
        private CancellationTokenSource cts = new();

        public override async Task Enter(ClientStateType fromType)
        {
            cts = new();

            Match.OnTokenResolved += OnTokenResolved;

            GameEntrypoint.Singleton.Client.Camera
                .SetTarget(MyPlayingLayout.PlayingCameraPosition);

            SetupTokens();

            UpdateStatusUI();
        }

        private async void SetupTokens()
        {
            List<Task> tasks = new();

            // Place each of my token into my row
            var myTokenViews = SceneObjects.Player(Match.ClientIndex).TokenViews;
            for (int i = 0; i < myTokenViews.Length; i++)
            {
                var tokenView = myTokenViews[i];
                tokenView.InteractionMode = TokenView.InteractionModeType.None;

                // Animate directly into the row
                float offset = (i - (myTokenViews.Length - 1) / 2f) * SceneLayout.TokenSpacing;
                Vector3 targetPos = MyPlayingLayout.TokenRow.position + MyPlayingLayout.TokenRow.right * offset;

                tasks.Add(tokenView.Animator.Play(AnimationSequenceBuilder
                    .Start()
                    .Next(
                        tokenView.CreateCurrentPositionAnimationTrack(
                            0.6f, AnimationCurves.Lerp(tokenView.transform.position, targetPos), Easing.EaseOutCubic),
                        AnimationTracks.Rotation(
                            0.4f, tokenView.transform, tokenView.transform.rotation, MyPlayingLayout.TokenRow.rotation, Easing.EaseOutCubic))
                    .Build(), cts.Token));
            }

            // Create each opponent token object in a row
            var opTokenRow = SceneLayout.Game.Player(Match.OpClientIndex).TokenRow;
            var opTokenCount = GameState.Clients[Match.OpClientIndex].Tokens.Count;

            SceneObjects.Player(Match.OpClientIndex).TokenViews = new TokenView[opTokenCount];
            var opTokenViews = SceneObjects.Player(Match.OpClientIndex).TokenViews;

            for (int i = 0; i < opTokenCount; i++)
            {
                var tokenView = TokenView.CreateEmpty(client.Assets);
                Assert.NotNull(tokenView);

                opTokenViews[i] = tokenView;
                tokenView.InteractionMode = TokenView.InteractionModeType.None;

                // Place directly into the row
                float offset = (i - (opTokenCount - 1) / 2f) * SceneLayout.TokenSpacing;
                Vector3 targetPos = opTokenRow.position + opTokenRow.right * offset;
                tokenView.SetPositionAndRotation(targetPos, opTokenRow.rotation);
            }

            StartTurn(GameState.TurnClientIndex);
        }

        public override async Task Exit(ClientStateType toStateType)
        {
            cts.Cancel();
            cts.Dispose();

            Match.OnTokenResolved -= OnTokenResolved;

            // If we are going back to title then clean up the token / dice views
            if (toStateType == ClientStateType.Title)
            {
                SceneLayout.Game.OnDisconnect();
                SceneObjects.OnDisconnect();
                client.UI.OnDisconnect();
            }

            selectedTokenView = null;
        }

        private void StartTurn(int clientIndex)
        {
            // Enable all tokens to be playable
            foreach (var tokenView in SceneObjects.Player(clientIndex).TokenViews)
            {
                tokenView.InteractionMode = TokenView.InteractionModeType.Play;

                if (clientIndex == Match.ClientIndex)
                {
                    tokenView.OnClicked.AddListener(OnTokenClicked);
                }
            }
        }

        private void UpdateStatusUI()
        {
            var whoseTurn = Match.IsMyTurn ? "Your Turn" : "Opponent's Turn";
            client.UI.SetStatus($"Round {Match.GameState.Round}", whoseTurn);
        }

        private async void OnTokenClicked(TokenView tokenView)
        {
            if (selectedTokenView != tokenView)
            {
                selectedTokenView?.SetSelected(false);
                tokenView.SetSelected(true);
                selectedTokenView = tokenView;
                return;
            }
        }

        private async void OnTokenPlayed()
        {
            Assert.NotNull(selectedTokenView);

            // We must be playing a token, first disable interacting with tokens
            selectedTokenView.SetSelected(false);
            foreach (var otherTokenView in MySceneObjects.TokenViews)
            {
                otherTokenView.InteractionMode = TokenView.InteractionModeType.None;
                otherTokenView.OnClicked.RemoveListener(OnTokenClicked);
            }

            // Now setup the intent for the token
            var visualContext = new TokenVisualContext(selectedTokenView, SceneLayout, SceneObjects);
            var tokenRegistry = GameEntrypoint.Singleton.TokenRegistry;
            var tokenRegistryEntry = tokenRegistry.GetEntryOrThrow(selectedTokenView.TokenDefinition.TokenId);

            var tokenIntent = await tokenRegistryEntry.Visuals.IntentPicker.PickIntent(visualContext, Match.ClientIndex);

            // Tell the server our intended play
            var message = new TokenIntentMessage
            {
                TokenId = selectedTokenView.TokenDefinition.TokenId,
                InstanceId = selectedTokenView.TokenInstance.InstanceId,
                Intent = tokenIntent
            };
            var response = await Match.RequestPlayToken(message);

            // TODO: Handle failure response
            Assert.True(response);
        }

        private async void OnTokenResolved(TokenResolutionMessage message)
        {
            var tokenView = SceneObjects
                .Player(message.SourceClientIndex).TokenViews
                .First(tv => tv.TokenInstance.InstanceId == message.TokenInstanceId);

            var visualContext = new TokenVisualContext(tokenView, SceneLayout, SceneObjects);

            var tokenRegistry = GameEntrypoint.Singleton.TokenRegistry;
            var tokenRegistryEntry = tokenRegistry.GetEntryOrThrow(message.TokenId);

            await tokenRegistryEntry.Visuals.Animator.Animate(
                Match.GameState,
                visualContext,
                message.SourceClientIndex,
                message.TokenInstanceId,
                message.Resolution,
                cts.Token);
        }
    }
}
