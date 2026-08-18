using System.Collections.Generic;
using System.Linq;
using Drakken.Domain.Tokens.Implementation;
using Drakken.Domain.Tokens.Logic;
using Drakken.Utility;
using UnityEngine;

namespace Drakken.Domain.Dice.Effects
{
    // Marks never outlive the Mitosis token's own session (stripped before it returns), so this
    // effect only ever fires within that session and can safely assume ctx.Resolution is a
    // MitosisTokenResolution - unlike a permanent effect such as Bolster.
    public class MitosisFaceEffectLogic : FaceEffectLogic
    {
        public const int MinSides = 4;
        private const int MaxTotalDice = 32; // shouldn't hit this but just in case
        private const float LiftHeight = 1f;
        private const float LiftDuration = 0.35f;
        private const float SplitUpwardSpeed = 4f;
        private const float SplitOutwardSpeed = 1.5f;
        private const float SplitTorque = 5f;

        public override int EffectId => FaceEffectIds.MitosisMark;

        public override void OnSettled(DiceEffectSettleContext ctx)
        {
            var dice = ctx.SettledDice;

            // Pending split children start below the min size and can't split further
            if (dice.Sides <= MinSides) return;

            // CandidatePool is the world's own live dice, so this already reflects the running
            // total including anything spawned earlier in this same recursive chain
            if (ctx.CandidatePool.Count + 2 > MaxTotalDice) return;

            var (startPosition, startRotation) = ctx.World.GetDicePose(dice.InstanceId);
            Vector3 liftedPosition = startPosition + Vector3.up * LiftHeight;

            int childSides = Mathf.Max(MinSides, TokenExecutionLogic.RoundUpToEven(dice.Sides / 2 + 1));

            ctx.World.DriveDice(
                dice.InstanceId, LiftDuration,
                t => Vector3.Lerp(startPosition, liftedPosition, Easing.EaseOutCubic(t)),
                _ => startRotation,
                onComplete: () => Split(ctx, dice, liftedPosition, childSides));
        }

        private static void Split(DiceEffectSettleContext ctx, DiceInstance parent, Vector3 liftedPosition, int childSides)
        {
            var resolution = (MitosisTokenResolution)ctx.Resolution;

            ctx.World.RemoveDice(parent.InstanceId);
            resolution.FinalDiceInstances.Remove(parent);

            Vector3 outward = new(Random.Range(-1f, 1f), 0f, Random.Range(-1f, 1f));
            outward = outward.sqrMagnitude > 0.0001f ? outward.normalized : Vector3.right;

            var (halfA, halfB) = SplitFacesRandomly(parent.Faces);
            var childA = DiceInstance.CreateFromRetainedFaces(childSides, halfA);
            var childB = DiceInstance.CreateFromRetainedFaces(childSides, halfB);

            // Only mark if they have >4 faces, but still spawn either way
            if (childSides > MinSides)
            {
                MarkRandomHalf(childA);
                MarkRandomHalf(childB);
            }

            ctx.World.SpawnDice(
                childA, liftedPosition, Random.rotationUniform,
                outward * SplitOutwardSpeed + Vector3.up * SplitUpwardSpeed,
                Random.insideUnitSphere * SplitTorque);

            ctx.World.SpawnDice(
                childB, liftedPosition, Random.rotationUniform,
                -outward * SplitOutwardSpeed + Vector3.up * SplitUpwardSpeed,
                Random.insideUnitSphere * SplitTorque);

            resolution.FinalDiceInstances.Add(childA);
            resolution.FinalDiceInstances.Add(childB);
        }

        public static void MarkRandomHalf(DiceInstance dice)
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
    }
}
