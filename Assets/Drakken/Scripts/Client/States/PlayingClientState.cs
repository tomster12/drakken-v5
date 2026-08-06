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
        private GamePlayerLayout MyGameLayout => SceneLayout.Game.Player(Match.ClientIndex);
        private ScenePlayerObjects MySceneObjects => SceneObjects.Player(Match.ClientIndex);
        private CancellationTokenSource cts = new();

        // ------------------------------ Setup

        public override async Task Enter(ClientStateType fromType)
        {
            cts = new();

            Match.OnTokenResolved += OnTokenResolved;

            GameEntrypoint.Singleton.Client.Camera
                .SetTarget(MyGameLayout.PlayingCameraPosition);

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

                var targetPos = GetTokenRowIndexPosition(Match.ClientIndex, i);
                tasks.Add(tokenView.Animator.Play(AnimationSequenceBuilder
                    .Start()
                    .Next(
                        tokenView.CreateCurrentPositionAnimationTrack(
                            0.6f, AnimationCurves.Lerp(tokenView.transform.position, targetPos), Easing.EaseOutCubic),
                        AnimationTracks.Rotation(
                            0.4f, tokenView.transform, tokenView.transform.rotation, MyGameLayout.TokenRow.rotation, Easing.EaseOutCubic))
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
                opTokenViews[i] = tokenView;

                tokenView.SetInteractionMode(TokenView.InteractionModeType.None);

                var targetPos = GetTokenRowIndexPosition(Match.OpClientIndex, i);
                var targetRot = opTokenRow.rotation;
                tokenView.SetPositionAndRotation(targetPos, targetRot);
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

        private Vector3 GetTokenRowPosition(int clientIndex, TokenView tokenView)
        {
            var tokenViews = SceneObjects.Player(clientIndex).TokenViews;
            int tokenIndex = Array.IndexOf(tokenViews, tokenView);
            return GetTokenRowIndexPosition(clientIndex, tokenIndex);
        }

        private Vector3 GetTokenRowIndexPosition(int clientIndex, int tokenIndex)
        {
            var tokenViews = SceneObjects.Player(clientIndex).TokenViews;
            var tokenRow = SceneLayout.Game.Player(clientIndex).TokenRow;
            float offset = (tokenIndex - (tokenViews.Length - 1) / 2f) * SceneLayout.TokenSpacing;
            return tokenRow.position + tokenRow.right * offset;
        }

        private Vector3 GetDiceRowIndexPosition(int clientIndex, int diceIndex)
        {
            var diceViews = SceneObjects.Player(clientIndex).DiceViews;
            var diceRow = SceneLayout.Game.Player(clientIndex).DiceRow;
            float offset = (diceIndex - (diceViews.Length - 1) / 2f) * SceneLayout.DiceSpacing;
            return diceRow.position + diceRow.right * offset;
        }

        private void SetAllDiceInteractionLocked(bool interactionLocked)
        {
            foreach (var diceView in SceneObjects.Player(Match.ClientIndex).DiceViews)
                diceView.SetInteractionLocked(interactionLocked);

            foreach (var diceView in SceneObjects.Player(Match.OpClientIndex).DiceViews)
                diceView.SetInteractionLocked(interactionLocked);
        }

        private void SetAllTokensInteractionLocked(bool interactionLocked)
        {
            foreach (var tokenView in SceneObjects.Player(Match.ClientIndex).TokenViews)
                tokenView.SetInteractionLocked(interactionLocked);

            foreach (var tokenView in SceneObjects.Player(Match.OpClientIndex).TokenViews)
                tokenView.SetInteractionLocked(interactionLocked);
        }

        // ------------------------------ Playing Tokens

        private bool IsWithinPlayDropRadius(TokenView tokenView)
        {
            return Vector3.Distance(tokenView.transform.position, SceneLayout.Game.CentrePos.position) <= SceneLayout.Game.PlayDropRadius;
        }

        private void OnTokenDragStarted(TokenView tokenView)
        {
            SetAllDiceInteractionLocked(true);
        }

        private void OnTokenDragMoved(TokenView tokenView)
        {
            tokenView.SetPrimedToPlay(IsWithinPlayDropRadius(tokenView));
        }

        private async void OnTokenDragEnded(TokenView tokenView)
        {
            bool shouldPlay = IsWithinPlayDropRadius(tokenView);
            tokenView.SetPrimedToPlay(false);

            SetAllDiceInteractionLocked(false);

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
            Vector3 targetPos = GetTokenRowPosition(Match.ClientIndex, tokenView);

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
            var visualContext = GetVisualContext(tokenView);
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
            SetAllTokensInteractionLocked(true);
            SetAllDiceInteractionLocked(true);

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
            var visualContext = GetVisualContext(tokenView);
            var tokenRegistry = GameEntrypoint.Singleton.TokenRegistry;
            var tokenRegistryEntry = tokenRegistry.GetEntryOrThrow(message.TokenId);

            await tokenRegistryEntry.Visuals.Animator.Animate(
                Match,
                visualContext,
                message.SourceClientIndex,
                message.TokenInstanceId,
                message.Resolution,
                cts.Token);

            SetAllTokensInteractionLocked(false);
            SetAllDiceInteractionLocked(false);
        }

        public TokenVisualContext GetVisualContext(TokenView tokenView) => new()
        {
            Assets = client.Assets,
            SceneLayout = SceneLayout,
            SceneObjects = SceneObjects,
            ClientUI = client.UI,
            TokenView = tokenView,
            GetDiceRowIndexPosition = GetDiceRowIndexPosition
        };
    }
}

