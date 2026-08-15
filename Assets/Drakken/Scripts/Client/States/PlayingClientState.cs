using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Drakken.Client.World;
using Drakken.Domain.Animation;
using Drakken.Common.Utility;
using Drakken.Networking;
using Drakken.Domain.Tokens.Logic;
using Drakken.Utility;
using UnityEngine;
using Drakken.Domain.Networking;

namespace Drakken.Client.States
{
    public class PlayingClientState : ClientState
    {
        private SceneLayout SceneLayout => client.SceneLayout;
        private SceneObjects SceneObjects => client.SceneObjects;
        private GamePlayerLayout MyGameLayout => SceneLayout.Game.Player(Match.ClientIndex);
        private ScenePlayerObjects MySceneObjects => SceneObjects.Player(Match.ClientIndex);

        // ------------------------------ Setup

        public override async Task Enter(ClientStateType fromType)
        {
            await base.Enter(fromType);

            Match.OnTokenResolved += OnTokenResolved;
            Match.OnNextTurnStarted += OnNextTurnStarted;
            Match.OnRoundEnded += OnRoundEnded;

            client.Camera.SetTarget(
                MyGameLayout.PlayingCameraPosition);

            StartRound();
        }

        public override async Task Exit(ClientStateType toType)
        {
            await base.Exit(toType);

            Match.OnTokenResolved -= OnTokenResolved;
            Match.OnNextTurnStarted -= OnNextTurnStarted;
            Match.OnRoundEnded -= OnRoundEnded;

            // If we are going back to title then clean up the token / dice views
            if (toType == ClientStateType.Title)
            {
                SceneObjects.OnDisconnect();
                client.UI.OnDisconnect();
            }
        }

        private async void StartRound()
        {
            List<Task> tasks = new();

            // Place each of my token into my row
            for (int i = 0; i < MySceneObjects.TokenViews.Length; i++)
            {
                var tokenView = MySceneObjects.TokenViews[i];

                tokenView.SetInteractionMode(TokenView.InteractionModeType.None);

                var targetPos = MySceneObjects.GetTokenRowIndexPosition(i);

                tasks.Add(tokenView.Animator.Play(
                    AnimationSequenceBuilder.Start()
                    .Next(
                        tokenView.CreateCurrentPositionAnimationTrack(
                            0.6f, AnimationCurves.Lerp(tokenView.transform.position, targetPos), Easing.EaseOutCubic),
                        AnimationTracks.Rotation(
                            0.4f, tokenView.transform, tokenView.transform.rotation, MyGameLayout.TokenRow.rotation, Easing.EaseOutCubic))
                    .Build(), cts.Token));
            }

            // Create each opponent token object in a row
            var opTokens = GameState.Clients[Match.OpClientIndex].Tokens;
            var opPlayerObjects = SceneObjects.Player(Match.OpClientIndex);
            var opTokenRow = SceneLayout.Game.Player(Match.OpClientIndex).TokenRow;

            opPlayerObjects.TokenViews = new TokenView[opTokens.Count];

            for (int i = 0; i < opTokens.Count; i++)
            {
                var tokenView = opPlayerObjects.SpawnTokenAtIndex(opTokens[i], i, hidden: true);

                tokenView.SetInteractionMode(TokenView.InteractionModeType.None);

                var targetPos = opPlayerObjects.GetTokenRowIndexPosition(i);
                var targetRot = opTokenRow.rotation;
                tokenView.SetPositionAndRotation(targetPos, targetRot);
            }

            StartTurn();
        }

        private async void OnNextTurnStarted()
        {
            await ReAlignTokens();

            StartTurn();
        }

        private async Task ReAlignTokens()
        {
            List<Task> tasks = new();

            // Re-align my tokens in the row
            for (int i = 0; i < MySceneObjects.TokenViews.Length; i++)
            {
                var tokenView = MySceneObjects.TokenViews[i];
                var targetPos = MySceneObjects.GetTokenRowIndexPosition(i);

                tasks.Add(tokenView.Animator.Play(AnimationSequenceBuilder.Start()
                    .Next(tokenView.CreateCurrentPositionAnimationTrack(
                        0.3f, AnimationCurves.Lerp(tokenView.transform.position, targetPos), Easing.EaseOutCubic))
                    .Build(), cts.Token));
            }

            // Re-align opponent tokens in the row
            var opPlayerObjects = SceneObjects.Player(Match.OpClientIndex);
            for (int i = 0; i < opPlayerObjects.TokenViews.Length; i++)
            {
                var tokenView = opPlayerObjects.TokenViews[i];
                var targetPos = opPlayerObjects.GetTokenRowIndexPosition(i);

                tasks.Add(tokenView.Animator.Play(AnimationSequenceBuilder.Start()
                    .Next(tokenView.CreateCurrentPositionAnimationTrack(
                        0.3f, AnimationCurves.Lerp(tokenView.transform.position, targetPos), Easing.EaseOutCubic))
                    .Build(), cts.Token));
            }

            await Task.WhenAll(tasks);
        }

        private void StartTurn()
        {
            // Interaction is locked while a token is resolving / animating
            // so release it for the new turn
            SetAllTokensInteractionLocked(false);
            SetAllDiceInteractionLocked(false);

            UpdateStatusUI();

            // Enable all tokens to be playable on your turn
            if (Match.ClientIndex == GameState.TurnClientIndex)
            {
                foreach (var tokenView in SceneObjects.Player(Match.ClientIndex).TokenViews)
                {
                    tokenView.SetInteractionMode(TokenView.InteractionModeType.Play);

                    tokenView.OnDragStarted.AddListener(OnTokenDragStarted);
                    tokenView.OnDragMoved.AddListener(OnTokenDragMoved);
                    tokenView.OnDragEnded.AddListener(OnTokenDragEnded);
                }
            }
        }

        private async void OnRoundEnded()
        {
            await client.GotoState(ClientStateType.Drafting);
        }

        // ------------------------------ Utility

        private void UpdateStatusUI()
        {
            var whoseTurn = Match.IsMyTurn ? "Your Turn" : "Opponent's Turn";
            client.UI.SetStatus($"Round {Match.GameState.Round}", whoseTurn);
            client.UI.UpdateScore(GameState.Clients[Match.ClientIndex].Score, GameState.Clients[Match.OpClientIndex].Score);
        }

        private void SetAllDiceInteractionLocked(bool interactionLocked)
        {
            foreach (var diceView in SceneObjects.P1.DiceViews.Values)
            {
                diceView.SetInteractionLocked(interactionLocked);
            }
            foreach (var diceView in SceneObjects.P2.DiceViews.Values)
            {
                diceView.SetInteractionLocked(interactionLocked);
            }
        }

        private void SetAllTokensInteractionLocked(bool interactionLocked)
        {
            foreach (var tokenView in SceneObjects.P1.TokenViews)
            {
                tokenView.SetInteractionLocked(interactionLocked);
            }
            foreach (var tokenView in SceneObjects.P2.TokenViews)
            {
                tokenView.SetInteractionLocked(interactionLocked);
            }
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
            int tokenIndex = Array.IndexOf(MySceneObjects.TokenViews, tokenView);
            var targetPos = MySceneObjects.GetTokenRowIndexPosition(tokenIndex);

            await tokenView.Animator.Play(AnimationSequenceBuilder.Start()
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
            await tokenView.Animator.Play(AnimationSequenceBuilder.Start()
                .Next(tokenView.CreateCurrentPositionAnimationTrack(
                    0.3f, AnimationCurves.Lerp(tokenView.transform.position, SceneLayout.Game.CentrePos.position), Easing.EaseOutCubic))
                .Build(), cts.Token);

            // Now setup the intent for the token
            var visualContext = GetVisualContext(tokenView);
            var tokenRegistry = client.TokenRegistry;
            var tokenRegistryEntry = tokenRegistry.GetEntryOrThrow(tokenView.TokenDefinition.TokenId);

            var tokenIntent = await tokenRegistryEntry.Visuals.IntentPicker.PickIntent(visualContext, Match.ClientIndex);

            // A null intent means the player cancelled the pick (e.g. clicked off), so return the token to hand
            if (tokenIntent == null)
            {
                await CancelPlayToken(tokenView);
                return;
            }

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

        private async Task CancelPlayToken(TokenView tokenView)
        {
            // Re-enable interaction on all my tokens now that the play was cancelled
            foreach (var otherTokenView in MySceneObjects.TokenViews)
            {
                otherTokenView.SetInteractionMode(TokenView.InteractionModeType.Play);
                otherTokenView.OnDragStarted.AddListener(OnTokenDragStarted);
                otherTokenView.OnDragMoved.AddListener(OnTokenDragMoved);
                otherTokenView.OnDragEnded.AddListener(OnTokenDragEnded);
            }

            await ReturnTokenToRow(tokenView);
        }

        private async void OnTokenResolved(TokenResolutionMessage message)
        {
            SetAllTokensInteractionLocked(true);
            SetAllDiceInteractionLocked(true);

            // A token has been played (ours or opponents)
            var tokenView = SceneObjects
                .Player(message.SourceClientIndex).TokenViews
                .First(tv => tv.TokenInstance.InstanceId == message.TokenInstanceId);

            // Animate to the centre and show what token was played
            var centreTargetPos = SceneLayout.Game.CentrePos.position;
            var centreTargetRot = Quaternion.Euler(-55f, Match.ClientIndex * 180f, 0);

            var builder = AnimationSequenceBuilder.Start()
                .Next(
                    tokenView.CreateCurrentPositionAnimationTrack(
                        0.6f, AnimationCurves.Lerp(tokenView.transform.position, centreTargetPos), Easing.EaseInOutCubic),
                    AnimationTracks.Rotation(
                        0.6f, tokenView.transform, tokenView.transform.rotation, centreTargetRot, Easing.EaseInOutCubic));

            // If it an opponent token then reveal when it is almost in the middle
            if (message.SourceClientIndex != Match.ClientIndex)
                builder.At(0.4f, () => tokenView.Reveal());

            await tokenView.Animator.Play(builder.Build(), cts.Token);

            // Now hand it over to the token to finish the animation
            var visualContext = GetVisualContext(tokenView);
            var tokenRegistry = client.TokenRegistry;
            var tokenRegistryEntry = tokenRegistry.GetEntryOrThrow(message.TokenId);

            await tokenRegistryEntry.Visuals.Animator.Animate(
                Match,
                visualContext,
                message.SourceClientIndex,
                message.TokenInstanceId,
                message.Resolution,
                cts.Token);

            // Remove the now-resolved token so it can't be interacted with again next turn
            var sourcePlayerObjects = SceneObjects.Player(message.SourceClientIndex);
            sourcePlayerObjects.TokenViews = sourcePlayerObjects.TokenViews
                .Where(tv => tv != tokenView)
                .ToArray();

            GameObject.Destroy(tokenView.gameObject);

            // Tell the server we have finished animating so it can advance the turn / round
            await Match.MessageAnimatedTokenResolved();
        }

        public TokenVisualContext GetVisualContext(TokenView tokenView) => new()
        {
            Client = client,
            TokenView = tokenView
        };
    }
}
