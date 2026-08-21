using System.Collections.Generic;
using System.Linq;
using Drakken.Gameplay.Dice.Logic;
using Drakken.Gameplay.Simulation;
using Drakken.Gameplay.Tokens.Logic;
using Drakken.Utility;
using Drakken.Domain;
using UnityEngine;

namespace Drakken.Gameplay.Dice.Implementation
{
    public class MitosisFaceEffect : FaceEffectLogic<EmptyEventResolution>
    {
        public const int MinSides = 4;
        private const int MaxTotalDice = 32; // shouldn't hit this but just in case
        private const float LiftHeight = 1f;
        private const float LiftDuration = 0.35f;
        private const float SplitUpwardSpeed = 4f;
        private const float SplitOutwardSpeed = 1.5f;
        private const float SplitTorque = 5f;

        // Not actually recorded as an event any more (see below) - kept only to satisfy
        // IFaceEffectLogic's landed/missed dispatch, which is keyed by this id
        public override int EventId => FaceEffectIds.MitosisMark;

        public override void Execute(DiceEffectExecuteContext ctx)
        {
            var dice = ctx.SettledDice;

            if (dice.Sides <= MinSides) return;
            if (ctx.CandidatePool.Count + 2 > MaxTotalDice) return;

            var (startPosition, startRotation) = ctx.World.GetDicePose(dice.InstanceId);
            Vector3 liftedPosition = startPosition + Vector3.up * LiftHeight;

            int childSides = Mathf.Max(MinSides, TokenExecutionLogic.RoundUpToEven(dice.Sides / 2 + 1));

            ctx.World.DriveDice(
                dice.InstanceId, LiftDuration,
                t => Vector3.Lerp(startPosition, liftedPosition, Easing.EaseOutCubic(t)),
                _ => startRotation,
                onComplete: () => Split(ctx.World, dice, liftedPosition, childSides));
        }

        private void Split(GameSimulationWorld world, DiceInstance parent, Vector3 liftedPosition, int childSides)
        {
            world.RemoveDice(parent.InstanceId);

            Vector3 outward = new(Random.Range(-1f, 1f), 0f, Random.Range(-1f, 1f));
            outward = outward.sqrMagnitude > 0.0001f ? outward.normalized : Vector3.right;

            var (halfA, halfB) = SplitFacesRandomly(parent.Faces);
            var childA = DiceInstance.CreateFromRetainedFaces(childSides, halfA);
            var childB = DiceInstance.CreateFromRetainedFaces(childSides, halfB);

            if (childSides > MinSides)
            {
                // Mark before spawning - SpawnDice snapshots the dice for its AddDice event, so the
                // marks need to already be in place for the client's replay to see them
                MarkRandomHalf(childA);
                MarkRandomHalf(childB);
            }

            // RemoveDice/SpawnDice above and below already record the GameState-updating events
            world.SpawnDice(
                childA, liftedPosition, Random.rotationUniform,
                outward * SplitOutwardSpeed + Vector3.up * SplitUpwardSpeed,
                Random.insideUnitSphere * SplitTorque);

            world.SpawnDice(
                childB, liftedPosition, Random.rotationUniform,
                -outward * SplitOutwardSpeed + Vector3.up * SplitUpwardSpeed,
                Random.insideUnitSphere * SplitTorque);
        }

        public override void OnMiss(DiceEffectExecuteContext ctx)
        {
            var dice = ctx.SettledDice;
            var face = dice.Faces[ctx.Side];

            if (!face.FaceEffects.Contains(FaceEffectIds.MitosisMark)) return;

            // This specific face's mark is spent since a different face landed - clear just this
            // one rather than leaving it to persist indefinitely until it eventually lands on it
            face.FaceEffects = face.FaceEffects
                .Where(e => e != FaceEffectIds.MitosisMark)
                .ToList();

            ctx.World.RecordEvent(CommonEventIds.SetFaceEffects, EventKind.Common, new SetFaceEffectsResolution
            {
                SourceInstanceId = dice.InstanceId,
                EffectId = FaceEffectIds.MitosisMark,
                Replace = false,
                FaceIndices = { ctx.Side },
            });
        }

        protected override void Apply(GameState gameState, EmptyEventResolution resolution, int clientIndex) { }

        // Marks a random half of the dice's faces and records an event so the client can animate
        // the marks appearing at the correct point in time, instead of only reflecting them once
        // the whole trace (including any later misses) has already resolved
        public static void MarkRandomHalf(GameSimulationWorld world, DiceInstance dice)
        {
            MarkRandomHalf(dice);

            var markedSides = new List<int>(dice.Sides / 2);
            for (int i = 0; i < dice.Faces.Count; i++)
            {
                if (dice.Faces[i].FaceEffects.Contains(FaceEffectIds.MitosisMark)) markedSides.Add(i);
            }

            world.RecordEvent(CommonEventIds.SetFaceEffects, EventKind.Common, new SetFaceEffectsResolution
            {
                SourceInstanceId = dice.InstanceId,
                EffectId = FaceEffectIds.MitosisMark,
                Replace = true,
                FaceIndices = markedSides,
            });
        }

        public static void MarkRandomHalf(DiceInstance dice)
        {
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
    }
}
