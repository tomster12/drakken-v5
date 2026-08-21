using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Drakken.Client;
using Drakken.Client.World;
using Drakken.Utility;
using Drakken.Presentation.Animation;
using Drakken.Presentation;
using Drakken.Gameplay.Simulation;
using Drakken.Gameplay.Tokens.Implementation.Common;
using Drakken.Gameplay.Tokens.Logic;
using Unity.Netcode;
using UnityEngine;
using Drakken.Domain;

namespace Drakken.Gameplay.Tokens.Implementation
{
    public class ForgeSummary : TokenSummary
    {
        public int FirstInstanceId;
        public int SecondInstanceId;
        public bool DidMerge;

        public override void NetworkSerialize<T>(BufferSerializer<T> serializer)
        {
            serializer.SerializeValue(ref FirstInstanceId);
            serializer.SerializeValue(ref SecondInstanceId);
            serializer.SerializeValue(ref DidMerge);
        }
    }

    public class ForgeTokenLogic : TokenLogic<PickDiceTokenIntent, ForgeSummary>
    {
        private const float FlightDuration = 0.5f;
        private const float FlightLiftHeight = 1.3f;
        private const float FlightSideGap = 0.9f;
        private const float FlightArchHeight = 0.6f;
        private const float ForgePopUpwardSpeed = 1.2f;
        private const float ForgePopHorizontalSpeed = 0.3f;
        private const float ForgePopTorque = 4f;

        private const float PullTogetherDuration = 0.35f;
        private const float PullTogetherArchHeight = 0.5f;

        protected override (List<GameSimulationTrace> Traces, ForgeSummary Summary) ExecuteToken(GameState gameState, PickDiceTokenIntent intent, int sourceClientIndex, GameSimulationWorld world)
        {
            var client = gameState.Clients[sourceClientIndex];

            Assert.True(client.Dice.Count >= 2);
            Assert.True(intent.TargetDiceInstanceIds != null && intent.TargetDiceInstanceIds.Count == 2);
            Assert.True(intent.TargetDiceInstanceIds[0] != intent.TargetDiceInstanceIds[1]);

            var firstDice = client.Dice.Find(d => d.InstanceId == intent.TargetDiceInstanceIds[0]);
            var secondDice = client.Dice.Find(d => d.InstanceId == intent.TargetDiceInstanceIds[1]);

            Assert.NotNull(firstDice);
            Assert.NotNull(secondDice);

            var summary = new ForgeSummary
            {
                FirstInstanceId = firstDice.InstanceId,
                SecondInstanceId = secondDice.InstanceId,
            };

            // Drive the 2 dice up next to each other in the midpoint
            world.BeginSession(client.Dice);

            var (firstStartPos, firstStartRot) = world.GetDicePose(firstDice.InstanceId);
            var (secondStartPos, secondStartRot) = world.GetDicePose(secondDice.InstanceId);

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
                world.DriveDice(
                    firstDice.InstanceId, FlightDuration,
                    t => firstArc(Easing.EaseOutCubic(t)),
                    _ => firstStartRot),
                world.DriveDice(
                    secondDice.InstanceId, FlightDuration,
                    t => secondArc(Easing.EaseOutCubic(t)),
                    _ => secondStartRot),
            };

            world.Simulate(untilDrivesComplete: flightDriveIds);

            var liftTrace = world.EndSession();

            // Now try and merge the 2 dice together
            world.BeginSession(client.Dice);

            bool firstCanModify = TokenExecutionLogic.TryModify(firstDice, world);
            bool secondCanModify = TokenExecutionLogic.TryModify(secondDice, world);

            if (!firstCanModify || !secondCanModify)
            {
                summary.DidMerge = false;

                // Let each naturally drop
                if (firstCanModify) world.WakeDice(firstDice.InstanceId);
                if (secondCanModify) world.WakeDice(secondDice.InstanceId);

                world.Simulate(untilAllSettled: true);
            }

            else
            {
                summary.DidMerge = true;

                // Merge into a new dice, with sides = (v1 + v2) rounded up, minimum 4
                int mergedSides = TokenExecutionLogic.RoundUpToEven(firstDice.Value + secondDice.Value);
                mergedSides = Mathf.Max(mergedSides, 4);

                var mergedDice = DiceInstance.Create(sides: mergedSides);
                mergedDice.Roll();

                world.RemoveDice(firstDice.InstanceId);
                world.RemoveDice(secondDice.InstanceId);

                // Pop the forged dice straight up with a small random horizontal drift and spin, then let it fall
                Quaternion mergedRotation = Random.rotationUniform;
                Vector3 popImpulse = Vector3.up * ForgePopUpwardSpeed
                    + new Vector3(Random.Range(-ForgePopHorizontalSpeed, ForgePopHorizontalSpeed), 0f, Random.Range(-ForgePopHorizontalSpeed, ForgePopHorizontalSpeed));
                Vector3 popTorque = Random.insideUnitSphere * ForgePopTorque;

                // RemoveDice/SpawnDice above already recorded the GameState-updating events
                world.SpawnDice(mergedDice, liftedMidpoint, mergedRotation, popImpulse, popTorque);

                world.Simulate(untilAllSettled: true);
            }

            world.FreezeAllDice();

            return (new List<GameSimulationTrace> { liftTrace, world.EndSession() }, summary);
        }

        public override async Task AnimateToken(
            ClientMatch match,
            TokenVisualContext visualContext,
            int sourceClientIndex,
            int tokenInstanceId,
            List<GameSimulationTrace> traces,
            ForgeSummary summary,
            CancellationToken ct)
        {
            var sourcePlayerObjects = visualContext.Client.SceneObjects.Player(sourceClientIndex);

            // Shrink the token out the way
            await visualContext.TokenView.AnimateShrinkAfter(0.5f, ct);

            // Replay the lift simulation
            await sourcePlayerObjects.SimReplayer.Play(
                traces[0], ct, sourcePlayerObjects);

            if (summary.DidMerge)
            {
                var firstDiceView = sourcePlayerObjects.DiceViews[summary.FirstInstanceId];
                var secondDiceView = sourcePlayerObjects.DiceViews[summary.SecondInstanceId];

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
                sourcePlayerObjects.DiceViews.Remove(summary.FirstInstanceId);
                sourcePlayerObjects.DiceViews.Remove(summary.SecondInstanceId);
            }

            // Replay the final merge simulation (either merge or just drop)
            await sourcePlayerObjects.SimReplayer.Play(
                traces[1], ct, sourcePlayerObjects);

            visualContext.Client.UI.UpdateDiceTotal(match.ClientIndex, sourceClientIndex);
        }
    }
}
