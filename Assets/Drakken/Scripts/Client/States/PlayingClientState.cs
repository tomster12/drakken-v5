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
        private CancellationTokenSource cts = new();

        // ------------------------------ Setup

        public override async Task Enter(ClientStateType fromType)
        {
            cts = new();

            Match.OnTokenResolved += OnTokenResolved;

            GameEntrypoint.Singleton.Client.Camera
                .SetTarget(MyPlayingLayout.PlayingCameraPosition);

            SetupTokens();

            UpdateStatusUI();
        }

        public override async Task Exit(ClientStateType toStateType)
        {
            cts.Cancel();
            cts.Dispose();
            cts = null;

            Match.OnTokenResolved -= OnTokenResolved;

            // If we are going back to title then clean up the token / dice views
            if (toStateType == ClientStateType.Title)
            {
                SceneLayout.Game.OnDisconnect();
                SceneObjects.OnDisconnect();
                client.UI.OnDisconnect();
            }
        }

        public override void OnDestroy()
        {
            cts?.Cancel();
            cts?.Dispose();
        }

        private async void SetupTokens()
        {
            List<Task> tasks = new();

            // Place each of my token into my row
            var myTokenViews = SceneObjects.Player(Match.ClientIndex).TokenViews;
            for (int i = 0; i < myTokenViews.Length; i++)
            {
                var tokenView = myTokenViews[i];
                tokenView.SetInteractionMode(TokenView.InteractionModeType.None);

                // Animate directly into the row
                var targetPos = GetTokenRowIndexPosition(i);

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
            var opTokens = GameState.Clients[Match.OpClientIndex].Tokens;

            SceneObjects.Player(Match.OpClientIndex).TokenViews = new TokenView[opTokens.Count];
            var opTokenViews = SceneObjects.Player(Match.OpClientIndex).TokenViews;
            var opTokenRow = SceneLayout.Game.Player(Match.OpClientIndex).TokenRow;

            for (int i = 0; i < opTokens.Count; i++)
            {
                var tokenView = TokenView.Create(client.Assets, GameEntrypoint.Singleton.TokenRegistry, opTokens[i], hidden: true);
                Assert.NotNull(tokenView);

                opTokenViews[i] = tokenView;
                tokenView.SetInteractionMode(TokenView.InteractionModeType.None);

                // Place directly into the row
                float offset = (i - (opTokens.Count - 1) / 2f) * SceneLayout.TokenSpacing;
                Vector3 targetPos = opTokenRow.position + opTokenRow.right * offset;
                tokenView.SetPositionAndRotation(targetPos, opTokenRow.rotation);
            }

            StartTurn(GameState.TurnClientIndex);
        }

        private void StartTurn(int clientIndex)
        {
            // Enable all tokens to be playable
            foreach (var tokenView in SceneObjects.Player(clientIndex).TokenViews)
            {
                tokenView.SetInteractionMode(TokenView.InteractionModeType.Play);

                if (clientIndex == Match.ClientIndex)
                {
                    tokenView.OnDragStarted.AddListener(OnTokenDragStarted);
                    tokenView.OnDragMoved.AddListener(OnTokenDragMoved);
                    tokenView.OnDragEnded.AddListener(OnTokenDragEnded);
                }
            }
        }

        // ------------------------------ Main

        private void UpdateStatusUI()
        {
            var whoseTurn = Match.IsMyTurn ? "Your Turn" : "Opponent's Turn";
            client.UI.SetStatus($"Round {Match.GameState.Round}", whoseTurn);
        }

        private Vector3 GetTokenRowPosition(TokenView tokenView)
        {
            var myTokenViews = MySceneObjects.TokenViews;
            int tokenIndex = Array.IndexOf(myTokenViews, tokenView);
            return GetTokenRowIndexPosition(tokenIndex);
        }

        private Vector3 GetTokenRowIndexPosition(int index)
        {
            var myTokenViews = MySceneObjects.TokenViews;
            float offset = (index - (myTokenViews.Length - 1) / 2f) * SceneLayout.TokenSpacing;
            return MyPlayingLayout.TokenRow.position + MyPlayingLayout.TokenRow.right * offset;
        }

        // ------------------------------ Playing Tokens

        private bool IsWithinPlayDropRadius(TokenView tokenView)
        {
            return Vector3.Distance(tokenView.transform.position, SceneLayout.Game.CentrePos.position) <= SceneLayout.Game.PlayDropRadius;
        }

        private void OnTokenDragStarted(TokenView tokenView) { }

        private void OnTokenDragMoved(TokenView tokenView)
        {
            tokenView.SetPrimedToPlay(IsWithinPlayDropRadius(tokenView));
        }

        private async void OnTokenDragEnded(TokenView tokenView)
        {
            bool shouldPlay = IsWithinPlayDropRadius(tokenView);
            tokenView.SetPrimedToPlay(false);

            if (shouldPlay)
            {
                await PlayToken(tokenView);
            }
            else
            {
                await ReturnTokenToRow(tokenView);
            }
        }

        private async Task ReturnTokenToRow(TokenView tokenView)
        {
            Vector3 targetPos = GetTokenRowPosition(tokenView);

            await tokenView.Animator.Play(AnimationSequenceBuilder
                .Start()
                .Next(tokenView.CreateCurrentPositionAnimationTrack(
                    0.3f, AnimationCurves.Lerp(tokenView.transform.position, targetPos), Easing.EaseOutCubic))
                .Build(), cts.Token);
        }

        private async Task PlayToken(TokenView tokenView)
        {
            // We are playing a token, first disable interacting with all tokens
            foreach (var otherTokenView in MySceneObjects.TokenViews)
            {
                otherTokenView.SetInteractionMode(TokenView.InteractionModeType.None);
                otherTokenView.OnDragStarted.RemoveListener(OnTokenDragStarted);
                otherTokenView.OnDragMoved.RemoveListener(OnTokenDragMoved);
                otherTokenView.OnDragEnded.RemoveListener(OnTokenDragEnded);
            }

            // Settle the token into the centre before resolving its intent
            await tokenView.Animator.Play(AnimationSequenceBuilder
                .Start()
                .Next(tokenView.CreateCurrentPositionAnimationTrack(
                    0.3f, AnimationCurves.Lerp(tokenView.transform.position, SceneLayout.Game.CentrePos.position), Easing.EaseOutCubic))
                .Build(), cts.Token);

            // Now setup the intent for the token
            var visualContext = new TokenVisualContext(tokenView, SceneLayout, SceneObjects);
            var tokenRegistry = GameEntrypoint.Singleton.TokenRegistry;
            var tokenRegistryEntry = tokenRegistry.GetEntryOrThrow(tokenView.TokenDefinition.TokenId);

            var tokenIntent = await tokenRegistryEntry.Visuals.IntentPicker.PickIntent(visualContext, Match.ClientIndex);

            // Tell the server our intended play
            var response = await Match.RequestPlayToken(new()
            {
                TokenId = tokenView.TokenDefinition.TokenId,
                InstanceId = tokenView.TokenInstance.InstanceId,
                Intent = tokenIntent
            });

            // TODO: Handle failure response
            Assert.True(response);
        }

        private async void OnTokenResolved(TokenResolutionMessage message)
        {
            // A token has been played (ours or opponents)
            var tokenView = SceneObjects
                .Player(message.SourceClientIndex).TokenViews
                .First(tv => tv.TokenInstance.InstanceId == message.TokenInstanceId);

            // If it is the opponents token then reveal now
            if (message.SourceClientIndex != Match.ClientIndex)
                tokenView.Reveal();

            // Animate to the centre and show what token was played
            var centreTargetPos = SceneLayout.Game.CentrePos.position;
            var centreTargetRot = Quaternion.Euler(-55f, Match.ClientIndex * 180f, 0);

            await tokenView.Animator.Play(AnimationSequenceBuilder
                .Start()
                .Next(
                    tokenView.CreateCurrentPositionAnimationTrack(
                        0.6f, AnimationCurves.Lerp(tokenView.transform.position, centreTargetPos), Easing.EaseInOutCubic),
                    AnimationTracks.Rotation(
                        0.6f, tokenView.transform, tokenView.transform.rotation, centreTargetRot, Easing.EaseInOutCubic))
                .Build(), cts.Token);

            // Now hand it over to the token to finish the animation
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
