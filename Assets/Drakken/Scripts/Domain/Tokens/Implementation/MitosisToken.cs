using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Drakken.Client;
using Drakken.Common.Utility;
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
        public int OriginalInstanceId;
        public List<DiceInstance> FinalDiceInstances = new();
        public DiceSimulationTraces DiceTrace = new();

        public override void NetworkSerialize<T>(BufferSerializer<T> serializer)
        {
            base.NetworkSerialize(serializer);
            serializer.SerializeValue(ref OriginalInstanceId);
            serializer.SerializeList(ref FinalDiceInstances);
            serializer.SerializeValue(ref DiceTrace);
        }
    }

    public class MitosisTokenExecutor : TokenExecutor<PickDiceTokenIntent, MitosisTokenResolution>
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
            PickDiceTokenIntent intent,
            int sourceClientIndex,
            DiceSimulationWorld diceWorld)
        {
            Assert.True(intent.TargetDiceInstanceIds != null && intent.TargetDiceInstanceIds.Count == 1);

            var client = gameState.Clients[sourceClientIndex];

            // Find the selected dice
            int originalInstanceId = intent.TargetDiceInstanceIds[0];
            var targetDice = client.Dice.Find(d => d.InstanceId == originalInstanceId);
            Assert.NotNull(targetDice);

            var resolution = new MitosisTokenResolution { OriginalInstanceId = originalInstanceId };

            diceWorld.BeginSession(client.Dice);

            // If the targeted dice can't be modified we cancel early
            if (!TokenExecutionLogic.TryModify(targetDice, diceWorld, resolution))
            {
                resolution.DiceTrace = diceWorld.EndSession();
                return resolution;
            }

            // Keep track of "live" dice instances            
            var liveInstances = new Dictionary<int, DiceInstance>();
            var pendingInstanceIds = new HashSet<int>();
            int totalDiceCount = client.Dice.Count;

            // Start with the selected dice and mark half at random
            liveInstances[targetDice.InstanceId] = targetDice;
            pendingInstanceIds.Add(targetDice.InstanceId);
            MarkRandomHalf(targetDice);

            // Wake and toss the selected dice
            Vector3 initialTossImpulse = new(
                Random.Range(-TossSideways, TossSideways),
                Random.Range(TossUpwardMin, TossUpwardMax),
                Random.Range(-TossSideways, TossSideways));

            diceWorld.WakeDice(targetDice.InstanceId, initialTossImpulse, Random.insideUnitSphere * TossTorque);

            // The main mitosis loop until all is finished
            var liftDrives = new Dictionary<string, (int SettledId, int ChildSides)>();
            while (pendingInstanceIds.Count > 0 || liftDrives.Count > 0)
            {
                // Simulate until either a dice settles or a lift finishes
                var result = diceWorld.Simulate(
                    untilAnySettled: pendingInstanceIds.Count > 0,
                    untilDrivesComplete: liftDrives.Count > 0 ? liftDrives.Keys : null);

                if (result.TimedOut) break;

                // Process any dice that landed and settled
                foreach (var settledId in result.SettledInstanceIds)
                {
                    Assert.True(pendingInstanceIds.Remove(settledId));
                    var settledDice = liveInstances[settledId];
                    int sides = settledDice.Sides;

                    // If it was a pending D4 then skip
                    if (sides <= 4) continue;

                    // Check if it landed with a marked face
                    int side = diceWorld.PeekDiceSide(settledId);
                    bool isHit = settledDice.Faces[side].FaceEffects.Contains(FaceEffectIds.MitosisMark);

                    // Settled without splitting
                    if (!isHit || totalDiceCount + 2 > MaxTotalDice)
                    {
                        settledDice.CurrentSide = side;
                        continue;
                    }

                    // Otherwise lift it into the air to split
                    var (startPosition, startRotation) = diceWorld.GetDicePose(settledId);
                    Vector3 liftedPosition = startPosition + Vector3.up * LiftHeight;

                    string driveId = diceWorld.DriveDice(
                        settledId, LiftDuration,
                        t => Vector3.Lerp(startPosition, liftedPosition, Easing.EaseOutCubic(t)),
                        _ => startRotation);

                    int childSides = Mathf.Max(MinSides, TokenExecutionLogic.RoundUpToEven(sides / 2 + 1));
                    liftDrives[driveId] = (settledId, childSides);
                }

                // Process any finished lift drive
                foreach (var completedDriveId in result.CompletedDriveIds)
                {
                    Assert.True(liftDrives.TryGetValue(completedDriveId, out var lift));
                    liftDrives.Remove(completedDriveId);

                    // So now we can remove the dice view to split
                    var (liftedPosition, _) = diceWorld.GetDicePose(lift.SettledId);
                    var parentFaces = liveInstances[lift.SettledId].Faces;
                    diceWorld.RemoveDice(lift.SettledId);
                    liveInstances.Remove(lift.SettledId);

                    Vector3 outward = new(Random.Range(-1f, 1f), 0f, Random.Range(-1f, 1f));
                    outward = outward.sqrMagnitude > 0.0001f ? outward.normalized : Vector3.right;

                    // Create new dice with split retained faces
                    var (halfA, halfB) = SplitFacesRandomly(parentFaces);
                    var childA = DiceInstance.CreateFromRetainedFaces(lift.ChildSides, halfA);
                    var childB = DiceInstance.CreateFromRetainedFaces(lift.ChildSides, halfB);

                    // Only mark if they have >4 faces, but still add to pending
                    if (lift.ChildSides > 4)
                    {
                        MarkRandomHalf(childA);
                        MarkRandomHalf(childB);
                    }

                    // Fling the 2 new split dice apart and start tracking
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
            resolution.DiceTrace = diceWorld.EndSession();

            // Remove all mitosis marks from all dice
            foreach (var dice in liveInstances.Values)
            {
                foreach (var face in dice.Faces)
                {
                    face.FaceEffects = face.FaceEffects
                        .Where(e => e != FaceEffectIds.MitosisMark)
                        .ToList();
                }
            }

            resolution.FinalDiceInstances = new List<DiceInstance>(liveInstances.Values);

            return resolution;
        }

        private static void MarkRandomHalf(DiceInstance dice)
        {
            // Remove existing mitosis marks
            foreach (var face in dice.Faces)
            {
                face.FaceEffects = face.FaceEffects
                    .Where(e => e != FaceEffectIds.MitosisMark)
                    .ToList();
            }

            var indices = new List<int>(dice.Sides);
            for (int i = 0; i < dice.Sides; i++) indices.Add(i);

            for (int i = indices.Count - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                (indices[i], indices[j]) = (indices[j], indices[i]);
            }

            int markCount = dice.Sides / 2;
            for (int i = 0; i < markCount; i++)
            {
                dice.Faces[indices[i]].FaceEffects.Add(FaceEffectIds.MitosisMark);
            }
        }

        private static (List<int> HalfA, List<int> HalfB) SplitFacesRandomly(List<DiceInstanceFace> faces)
        {
            var values = faces.ConvertAll(f => f.Value);

            for (int i = values.Count - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                (values[i], values[j]) = (values[j], values[i]);
            }

            int half = values.Count / 2;
            return (values.GetRange(0, half), values.GetRange(half, values.Count - half));
        }

        protected override void Apply(GameState gameState, MitosisTokenResolution resolution, int sourceClientIndex)
        {
            // If the modification failed, then exit early
            if (resolution.FinalDiceInstances.Count == 0) return;

            var client = gameState.Clients[sourceClientIndex];

            int index = client.Dice.FindIndex(d => d.InstanceId == resolution.OriginalInstanceId);
            Assert.True(index >= 0);

            client.Dice.RemoveAt(index);
            client.Dice.InsertRange(index, resolution.FinalDiceInstances);
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
            var sourcePlayerObjects = visualContext.Client.SceneObjects.Player(sourceClientIndex);

            // Shrink the token out the way
            await visualContext.TokenView.AnimateShrinkAfter(0.5f, ct);

            // Replay the full simulation
            await sourcePlayerObjects.DiceSimReplayer.Play(
                resolution.DiceTrace, sourcePlayerObjects, ct);

            visualContext.Client.UI.UpdateDiceTotal(match.ClientIndex, sourceClientIndex);
        }
    }
}
