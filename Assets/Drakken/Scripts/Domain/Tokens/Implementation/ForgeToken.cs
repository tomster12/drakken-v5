using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Drakken.Client;
using Drakken.Client.World;
using Drakken.Domain.Animation;
using Drakken.Common.Utility;
using Drakken.Domain.Dice;
using Drakken.Domain.Tokens.Implementation.Common;
using Drakken.Domain.Tokens.Logic;
using Drakken.Utility;
using Unity.Netcode;
using UnityEngine;

namespace Drakken.Domain.Tokens.Implementation
{
    public class ForgeTokenResolution : TokenResolution
    {
        public int FirstInstanceId;
        public int SecondInstanceId;
        public DiceInstance CombinedDiceInstance;

        public override void NetworkSerialize<T>(BufferSerializer<T> serializer)
        {
            base.NetworkSerialize(serializer);
            serializer.SerializeValue(ref FirstInstanceId);
            serializer.SerializeValue(ref SecondInstanceId);
            serializer.SerializeValue(ref CombinedDiceInstance);
        }
    }

    public class ForgeTokenExecutor : TokenExecutor<PickDiceTokenIntent, ForgeTokenResolution>
    {
        protected override ForgeTokenResolution Execute(GameState gameState, PickDiceTokenIntent intent, int sourceClientIndex, DiceSimulationWorld diceWorld)
        {
            var client = gameState.Clients[sourceClientIndex];

            Assert.True(client.Dice.Count >= 2);
            Assert.True(intent.TargetDiceInstanceIds != null && intent.TargetDiceInstanceIds.Count == 2);
            Assert.True(intent.TargetDiceInstanceIds[0] != intent.TargetDiceInstanceIds[1]);

            var firstDice = client.Dice.Find(d => d.InstanceId == intent.TargetDiceInstanceIds[0]);
            var secondDice = client.Dice.Find(d => d.InstanceId == intent.TargetDiceInstanceIds[1]);

            Assert.NotNull(firstDice);
            Assert.NotNull(secondDice);

            // Combine into a single fresh dice with sides equal to the sum of the two values
            int combinedSides = firstDice.Value + secondDice.Value;
            var combinedDice = DiceInstance.Create(sides: combinedSides);
            combinedDice.Roll();

            return new ForgeTokenResolution
            {
                FirstInstanceId = firstDice.InstanceId,
                SecondInstanceId = secondDice.InstanceId,
                CombinedDiceInstance = combinedDice,
            };
        }

        protected override void Apply(GameState gameState, ForgeTokenResolution resolution, int sourceClientIndex)
        {
            var client = gameState.Clients[sourceClientIndex];

            int firstIndex = client.Dice.FindIndex(d => d.InstanceId == resolution.FirstInstanceId);
            int secondIndex = client.Dice.FindIndex(d => d.InstanceId == resolution.SecondInstanceId);
            Assert.True(firstIndex >= 0 && secondIndex >= 0);

            // Keep the combined dice at the lower of the two original slots, preserving stable ordering
            int insertIndex = Mathf.Min(firstIndex, secondIndex);
            client.Dice.RemoveAll(d => d.InstanceId == resolution.FirstInstanceId || d.InstanceId == resolution.SecondInstanceId);
            client.Dice.Insert(insertIndex, resolution.CombinedDiceInstance);
        }
    }

    public class ForgeTokenAnimator : TokenAnimator<ForgeTokenResolution>
    {
        private const float PullTogetherDuration = 0.35f;
        private const float PullTogetherArchHeight = 1.5f;

        protected override async Task Animate(
            ClientMatch match,
            TokenVisualContext visualContext,
            int sourceClientIndex,
            int tokenInstanceId,
            ForgeTokenResolution resolution,
            CancellationToken ct)
        {
            await Task.Delay(250);

            var sourcePlayerObjects = visualContext.SceneObjects.Player(sourceClientIndex);

            var firstDiceView = sourcePlayerObjects.DiceViews[resolution.FirstInstanceId];
            var secondDiceView = sourcePlayerObjects.DiceViews[resolution.SecondInstanceId];

            // Pull the two dice together to a shared midpoint, arching up and across, shrinking away as they arrive
            Vector3 firstStartPosition = firstDiceView.transform.position;
            Vector3 secondStartPosition = secondDiceView.transform.position;
            Vector3 midpoint = (firstStartPosition + secondStartPosition) / 2f + Vector3.up * 1.0f;

            Vector3 firstControlPosition = Vector3.Lerp(firstStartPosition, midpoint, 0.5f) + Vector3.up * PullTogetherArchHeight;
            Vector3 secondControlPosition = Vector3.Lerp(secondStartPosition, midpoint, 0.5f) + Vector3.up * PullTogetherArchHeight;

            var firstArc = AnimationCurves.QuadraticBezier(firstStartPosition, firstControlPosition, midpoint);
            var secondArc = AnimationCurves.QuadraticBezier(secondStartPosition, secondControlPosition, midpoint);

            await Task.WhenAll(
                firstDiceView.Animator.Play(AnimationSequenceBuilder
                    .Start()
                    .Next(
                        AnimationTracks.PositionFunc(PullTogetherDuration, firstDiceView.transform, firstArc, Easing.EaseInOutQuad),
                        AnimationTracks.LocalScale(PullTogetherDuration, firstDiceView.transform, firstDiceView.transform.localScale, Vector3.zero, Easing.EaseInCubic))
                    .Build(), ct),
                secondDiceView.Animator.Play(AnimationSequenceBuilder
                    .Start()
                    .Next(
                        AnimationTracks.PositionFunc(PullTogetherDuration, secondDiceView.transform, secondArc, Easing.EaseInOutQuad),
                        AnimationTracks.LocalScale(PullTogetherDuration, secondDiceView.transform, secondDiceView.transform.localScale, Vector3.zero, Easing.EaseInCubic))
                    .Build(), ct));

            await Task.Delay(100);

            // Both dice have already shrunk to nothing at the midpoint, so just clean them up
            GameObject.Destroy(firstDiceView.gameObject);
            GameObject.Destroy(secondDiceView.gameObject);
            sourcePlayerObjects.DiceViews.Remove(resolution.FirstInstanceId);
            sourcePlayerObjects.DiceViews.Remove(resolution.SecondInstanceId);

            // Spawn the forged dice at the merge point and roll it
            Quaternion targetRot = Quaternion.Euler(0, match.ClientIndex * 180f, 0);
            var forgedDiceView = sourcePlayerObjects.SpawnDiceAt(resolution.CombinedDiceInstance, midpoint, targetRot);
            await forgedDiceView.AnimateGrow(ct);

            visualContext.ClientUI.UpdateDiceTotal(match.ClientIndex, sourceClientIndex);

            await Task.Delay(100);
        }
    }
}
