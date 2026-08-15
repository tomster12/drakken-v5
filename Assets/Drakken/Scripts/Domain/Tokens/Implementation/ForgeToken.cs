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
        public bool DidMerge;
        public DiceInstance MergedDiceInstance;
        public DiceSimulationTraces LiftTrace;
        public DiceSimulationTraces MergeTrace;

        public override void NetworkSerialize<T>(BufferSerializer<T> serializer)
        {
            base.NetworkSerialize(serializer);
            serializer.SerializeValue(ref FirstInstanceId);
            serializer.SerializeValue(ref SecondInstanceId);

            if (serializer.IsReader)
            {
                LiftTrace = new DiceSimulationTraces();
                MergeTrace = new DiceSimulationTraces();
            }
            serializer.SerializeValue(ref LiftTrace);
            serializer.SerializeValue(ref MergeTrace);

            serializer.SerializeValue(ref DidMerge);
            if (DidMerge)
            {
                if (serializer.IsReader) MergedDiceInstance = new DiceInstance();
                serializer.SerializeValue(ref MergedDiceInstance);
            }
        }
    }

    public class ForgeTokenExecutor : TokenExecutor<PickDiceTokenIntent, ForgeTokenResolution>
    {
        private const float FlightDuration = 0.5f;
        private const float FlightLiftHeight = 1.3f;
        private const float FlightSideGap = 0.9f;
        private const float FlightArchHeight = 0.6f;
        private const float ForgePopUpwardSpeed = 1.2f;
        private const float ForgePopHorizontalSpeed = 0.3f;
        private const float ForgePopTorque = 4f;

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

            var resolution = new ForgeTokenResolution
            {
                FirstInstanceId = firstDice.InstanceId,
                SecondInstanceId = secondDice.InstanceId,
            };

            // Drive the 2 dice up next to each other in the midpoint
            diceWorld.BeginSession(client.Dice);

            var (firstStartPos, firstStartRot) = diceWorld.GetDicePose(firstDice.InstanceId);
            var (secondStartPos, secondStartRot) = diceWorld.GetDicePose(secondDice.InstanceId);

            Vector3 liftedMidpoint = (firstStartPos + secondStartPos) / 2f + Vector3.up * FlightLiftHeight;
            Vector3 sideDirection = secondStartPos - firstStartPos;
            sideDirection = sideDirection.sqrMagnitude > 0.0001f ? sideDirection.normalized : Vector3.right;

            Vector3 firstTarget = liftedMidpoint - sideDirection * (FlightSideGap / 2f);
            Vector3 secondTarget = liftedMidpoint + sideDirection * (FlightSideGap / 2f);

            Vector3 firstControlPosition = Vector3.Lerp(firstStartPos, firstTarget, 0.5f) + Vector3.up * FlightArchHeight;
            Vector3 secondControlPosition = Vector3.Lerp(secondStartPos, secondTarget, 0.5f) + Vector3.up * FlightArchHeight;

            var firstArc = AnimationCurves.QuadraticBezier(firstStartPos, firstControlPosition, firstTarget);
            var secondArc = AnimationCurves.QuadraticBezier(secondStartPos, secondControlPosition, secondTarget);

            var flightDriveIds = new List<string>
            {
                diceWorld.DriveDice(
                    firstDice.InstanceId, FlightDuration,
                    t => firstArc(Easing.EaseOutCubic(t)),
                    _ => firstStartRot),
                diceWorld.DriveDice(
                    secondDice.InstanceId, FlightDuration,
                    t => secondArc(Easing.EaseOutCubic(t)),
                    _ => secondStartRot),
            };

            diceWorld.Simulate(untilDrivesComplete: flightDriveIds);

            resolution.LiftTrace = diceWorld.EndSession();

            // Now try and merge the 2 dice together
            diceWorld.BeginSession(client.Dice);

            bool firstCanModify = TokenExecutionLogic.TryModify(firstDice, diceWorld, resolution);
            bool secondCanModify = TokenExecutionLogic.TryModify(secondDice, diceWorld, resolution);

            if (!firstCanModify || !secondCanModify)
            {
                resolution.DidMerge = false;

                // One of the 2 failed so fling the other away
                var returnDriveIds = new List<string>();

                if (firstCanModify)
                {
                    var returnArc = AnimationCurves.QuadraticBezier(firstTarget, firstControlPosition, firstStartPos);
                    returnDriveIds.Add(diceWorld.DriveDice(
                        firstDice.InstanceId, FlightDuration,
                        t => returnArc(Easing.EaseOutCubic(t)),
                        _ => firstStartRot));
                }

                if (secondCanModify)
                {
                    var returnArc = AnimationCurves.QuadraticBezier(secondTarget, secondControlPosition, secondStartPos);
                    returnDriveIds.Add(diceWorld.DriveDice(
                        secondDice.InstanceId, FlightDuration,
                        t => returnArc(Easing.EaseOutCubic(t)),
                        _ => secondStartRot));
                }

                if (returnDriveIds.Count > 0)
                {
                    diceWorld.Simulate(untilDrivesComplete: returnDriveIds);
                }
            }

            else
            {
                resolution.DidMerge = true;

                // Merge into a new dice, with sides = (v1 + v2) rounded up, minimum 4
                int mergedSides = TokenExecutionLogic.RoundUpToEven(firstDice.Value + secondDice.Value);
                mergedSides = Mathf.Max(mergedSides, 4);

                var mergedDice = DiceInstance.Create(sides: mergedSides);
                mergedDice.Roll();

                diceWorld.RemoveDice(firstDice.InstanceId);
                diceWorld.RemoveDice(secondDice.InstanceId);

                // Pop the forged dice straight up with a small random horizontal drift and spin, then let it fall
                Quaternion mergedRotation = Random.rotationUniform;
                Vector3 popImpulse = Vector3.up * ForgePopUpwardSpeed
                    + new Vector3(Random.Range(-ForgePopHorizontalSpeed, ForgePopHorizontalSpeed), 0f, Random.Range(-ForgePopHorizontalSpeed, ForgePopHorizontalSpeed));
                Vector3 popTorque = Random.insideUnitSphere * ForgePopTorque;

                diceWorld.SpawnDice(mergedDice, liftedMidpoint, mergedRotation, popImpulse, popTorque);

                diceWorld.Simulate(untilAllSettled: true);

                resolution.MergedDiceInstance = mergedDice;
            }

            diceWorld.FreezeAllDice();
            resolution.MergeTrace = diceWorld.EndSession();

            return resolution;
        }

        protected override void Apply(GameState gameState, ForgeTokenResolution resolution, int sourceClientIndex)
        {
            if (!resolution.DidMerge) return;

            var client = gameState.Clients[sourceClientIndex];

            int firstIndex = client.Dice.FindIndex(d => d.InstanceId == resolution.FirstInstanceId);
            int secondIndex = client.Dice.FindIndex(d => d.InstanceId == resolution.SecondInstanceId);
            Assert.True(firstIndex >= 0 && secondIndex >= 0);

            // Keep the merged dice at the lower of the two original slots, preserving stable ordering
            int insertIndex = Mathf.Min(firstIndex, secondIndex);
            client.Dice.RemoveAll(d => d.InstanceId == resolution.FirstInstanceId || d.InstanceId == resolution.SecondInstanceId);
            client.Dice.Insert(insertIndex, resolution.MergedDiceInstance);
        }
    }

    public class ForgeTokenAnimator : TokenAnimator<ForgeTokenResolution>
    {
        private const float PullTogetherDuration = 0.35f;
        private const float PullTogetherArchHeight = 0.5f;

        protected override async Task Animate(
            ClientMatch match,
            TokenVisualContext visualContext,
            int sourceClientIndex,
            int tokenInstanceId,
            ForgeTokenResolution resolution,
            CancellationToken ct)
        {
            var sourcePlayerObjects = visualContext.Client.SceneObjects.Player(sourceClientIndex);

            // Shrink the token out the way
            await visualContext.TokenView.AnimateShrinkAfter(0.5f, ct);

            // Replay the lift simulation
            await sourcePlayerObjects.DiceSimReplayer.Play(
                resolution.LiftTrace, sourcePlayerObjects, ct);

            if (resolution.DidMerge)
            {
                var firstDiceView = sourcePlayerObjects.DiceViews[resolution.FirstInstanceId];
                var secondDiceView = sourcePlayerObjects.DiceViews[resolution.SecondInstanceId];

                // Do a final client-side animation to merge the 2 dice
                Vector3 firstStartPosition = firstDiceView.transform.position;
                Vector3 secondStartPosition = secondDiceView.transform.position;
                Vector3 midpoint = (firstStartPosition + secondStartPosition) / 2f;

                Vector3 firstControlPosition = Vector3.Lerp(firstStartPosition, midpoint, 0.5f) + Vector3.up * PullTogetherArchHeight;
                Vector3 secondControlPosition = Vector3.Lerp(secondStartPosition, midpoint, 0.5f) + Vector3.up * PullTogetherArchHeight;

                var firstArc = AnimationCurves.QuadraticBezier(firstStartPosition, firstControlPosition, midpoint);
                var secondArc = AnimationCurves.QuadraticBezier(secondStartPosition, secondControlPosition, midpoint);

                await Task.WhenAll(
                    firstDiceView.Animator.Play(AnimationSequenceBuilder.Start()
                        .Next(
                            AnimationTracks.PositionFunc(PullTogetherDuration, firstDiceView.transform, firstArc, Easing.EaseInOutQuad),
                            AnimationTracks.LocalScale(PullTogetherDuration, firstDiceView.transform, firstDiceView.transform.localScale, Vector3.zero, Easing.EaseInCubic))
                        .Build(), ct),
                    secondDiceView.Animator.Play(AnimationSequenceBuilder.Start()
                        .Next(
                            AnimationTracks.PositionFunc(PullTogetherDuration, secondDiceView.transform, secondArc, Easing.EaseInOutQuad),
                            AnimationTracks.LocalScale(PullTogetherDuration, secondDiceView.transform, secondDiceView.transform.localScale, Vector3.zero, Easing.EaseInCubic))
                        .Build(), ct));

                // Both dice have already shrunk to nothing at the midpoint, so just clean them up
                GameObject.Destroy(firstDiceView.gameObject);
                GameObject.Destroy(secondDiceView.gameObject);
                sourcePlayerObjects.DiceViews.Remove(resolution.FirstInstanceId);
                sourcePlayerObjects.DiceViews.Remove(resolution.SecondInstanceId);
            }

            // Replay the final merge simulation (either merge or just drop)
            await sourcePlayerObjects.DiceSimReplayer.Play(
                resolution.MergeTrace, sourcePlayerObjects, ct);

            visualContext.Client.UI.UpdateDiceTotal(match.ClientIndex, sourceClientIndex);
        }
    }
}
