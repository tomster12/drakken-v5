using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Drakken.Client;
using Drakken.Domain.Dice;
using Drakken.Domain.Networking;
using Drakken.Domain.Tokens.Implementation.Common;
using Drakken.Domain.Tokens.Logic;
using Drakken.Utility;
using Unity.Netcode;
using UnityEngine;

namespace Drakken.Domain.Tokens.Implementation
{
    public class MitosisTokenResolution : TokenResolution
    {
        public List<int> OriginalInstanceIds = new();
        public List<DiceInstance> FinalDiceInstances = new();
        public DiceSimulationTraces DiceTrace = new();

        public override void NetworkSerialize<T>(BufferSerializer<T> serializer)
        {
            base.NetworkSerialize(serializer);
            serializer.SerializeList(ref OriginalInstanceIds);
            serializer.SerializeList(ref FinalDiceInstances);
            serializer.SerializeValue(ref DiceTrace);
        }
    }

    public class MitosisTokenExecutor : TokenExecutor<EmptyTokenIntent, MitosisTokenResolution>
    {
        private const int MinSides = 4;

        // Initial roll toss
        private const float TossUpwardMin = 5.5f;
        private const float TossUpwardMax = 7.5f;
        private const float TossSideways = 0.6f;
        private const float TossTorque = 20f;

        // Lift a high-rolling dice up before it splits, to telegraph what's about to happen
        private const float LiftHeight = 1f;
        private const float LiftDuration = 0.35f;

        // Split when mitosis happens
        private const float SplitUpwardSpeed = 4f;
        private const float SplitOutwardSpeed = 1.5f;
        private const float SplitTorque = 5f;

        // Shouldn't hit this but just in case
        private const int MaxTotalDice = 32;

        protected override MitosisTokenResolution Execute(
            GameState gameState,
            EmptyTokenIntent intent,
            int sourceClientIndex,
            DiceSimulationWorld diceWorld)
        {
            var client = gameState.Clients[sourceClientIndex];

            var originalInstanceIds = client.Dice.ConvertAll(d => d.InstanceId);
            var liveInstances = new Dictionary<int, DiceInstance>();
            var pendingInstanceIds = new HashSet<int>();

            diceWorld.BeginSession();

            int totalDiceCount = client.Dice.Count;

            foreach (var dice in client.Dice)
            {
                liveInstances[dice.InstanceId] = dice;
                pendingInstanceIds.Add(dice.InstanceId);

                // Wake and toss every dice
                Vector3 tossImpulse = new(
                    Random.Range(-TossSideways, TossSideways),
                    Random.Range(TossUpwardMin, TossUpwardMax),
                    Random.Range(-TossSideways, TossSideways));

                diceWorld.WakeDice(dice.InstanceId, tossImpulse, Random.insideUnitSphere * TossTorque);
            }

            // Dice currently being lifted up to telegraph a split, keyed by their drive id
            var liftDrives = new Dictionary<string, (int SettledId, int ChildSides)>();

            while (pendingInstanceIds.Count > 0 || liftDrives.Count > 0)
            {
                // Simulate until either a dice settles or a lift finishes
                var result = diceWorld.Simulate(
                    untilAnySettled: pendingInstanceIds.Count > 0,
                    untilDrivesComplete: liftDrives.Count > 0 ? liftDrives.Keys : null);

                if (result.TimedOut) break;

                foreach (var settledId in result.SettledInstanceIds)
                {
                    if (!pendingInstanceIds.Remove(settledId)) continue;

                    int side = diceWorld.PeekDiceSide(settledId);
                    int sides = liveInstances[settledId].Sides;

                    // Check if it is a "high roll" e.g. top 1/3rd
                    int topThirdCount = Mathf.Max(1, Mathf.RoundToInt(sides / 3f));
                    bool isHighRoll = (side + 1) > sides - topThirdCount;

                    // Settled without splitting, lock in its final value and leave it alive
                    if (!isHighRoll || totalDiceCount + 2 > MaxTotalDice)
                    {
                        liveInstances[settledId].CurrentSide = side;
                        continue;
                    }

                    // Otherwise lift it into the air to show it's about to split
                    var (startPosition, startRotation) = diceWorld.GetDicePose(settledId);
                    Vector3 liftedPosition = startPosition + Vector3.up * LiftHeight;

                    string driveId = diceWorld.DriveDice(
                        settledId, LiftDuration,
                        t => Vector3.Lerp(startPosition, liftedPosition, Easing.EaseOutCubic(t)),
                        _ => startRotation);

                    int childSides = Mathf.Max(MinSides, Mathf.CeilToInt(sides / 2f));
                    liftDrives[driveId] = (settledId, childSides);
                }

                foreach (var completedDriveId in result.CompletedDriveIds)
                {
                    if (!liftDrives.TryGetValue(completedDriveId, out var lift)) continue;
                    liftDrives.Remove(completedDriveId);

                    var (liftedPosition, _) = diceWorld.GetDicePose(lift.SettledId);
                    diceWorld.RemoveDice(lift.SettledId);
                    liveInstances.Remove(lift.SettledId);

                    Vector3 outward = new(Random.Range(-1f, 1f), 0f, Random.Range(-1f, 1f));
                    outward = outward.sqrMagnitude > 0.0001f ? outward.normalized : Vector3.right;

                    var childA = DiceInstance.Create(lift.ChildSides);
                    var childB = DiceInstance.Create(lift.ChildSides);

                    // Fling the two children apart sideways in opposite directions, still with some lift
                    diceWorld.SpawnDice(
                        childA, liftedPosition, Random.rotationUniform,
                        outward * SplitOutwardSpeed + Vector3.up * SplitUpwardSpeed,
                        Random.insideUnitSphere * SplitTorque);

                    diceWorld.SpawnDice(
                        childB, liftedPosition, Random.rotationUniform,
                        -outward * SplitOutwardSpeed + Vector3.up * SplitUpwardSpeed,
                        Random.insideUnitSphere * SplitTorque);

                    liveInstances[childA.InstanceId] = childA;
                    liveInstances[childB.InstanceId] = childB;
                    pendingInstanceIds.Add(childA.InstanceId);
                    pendingInstanceIds.Add(childB.InstanceId);

                    totalDiceCount++;
                }
            }

            diceWorld.FreezeAllDice();
            var trace = diceWorld.EndSession();

            return new MitosisTokenResolution
            {
                OriginalInstanceIds = originalInstanceIds,
                FinalDiceInstances = new List<DiceInstance>(liveInstances.Values),
                DiceTrace = trace,
            };
        }

        protected override void Apply(GameState gameState, MitosisTokenResolution resolution, int sourceClientIndex)
        {
            var client = gameState.Clients[sourceClientIndex];

            client.Dice.Clear();
            client.Dice.AddRange(resolution.FinalDiceInstances);
        }
    }

    public class MitosisTokenAnimator : TokenAnimator<MitosisTokenResolution>
    {
        protected override async Task Animate(
            ClientMatch match,
            TokenVisualContext visualContext,
            int sourceClientIndex,
            int tokenInstanceId,
            MitosisTokenResolution resolution,
            CancellationToken ct)
        {
            await Task.Delay(250);

            // Give players a moment to read the token before it shrinks out of the way
            var shrinkTokenTask = visualContext.TokenView.AnimateShrink(1f, ct);

            var sourcePlayerObjects = visualContext.Client.SceneObjects.Player(sourceClientIndex);

            // Replay simulation
            var newDiceViews = await sourcePlayerObjects.DiceSimReplayer.Play(
                visualContext.Client.Assets, visualContext.Client, resolution.DiceTrace, sourcePlayerObjects, ct);

            // All original IDs are replaced by new dice views
            foreach (var originalId in resolution.OriginalInstanceIds)
            {
                sourcePlayerObjects.DiceViews.Remove(originalId);
            }

            foreach (var (instanceId, diceView) in newDiceViews)
            {
                sourcePlayerObjects.DiceViews[instanceId] = diceView;
            }

            await shrinkTokenTask;

            visualContext.Client.UI.UpdateDiceTotal(match.ClientIndex, sourceClientIndex);

            await Task.Delay(100);
        }
    }
}
