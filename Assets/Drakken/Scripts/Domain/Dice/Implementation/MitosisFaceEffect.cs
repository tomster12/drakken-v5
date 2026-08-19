using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Drakken.Domain.Dice.Logic;
using Drakken.Domain.Tokens.Logic;
using Drakken.Utility;
using Unity.Netcode;
using UnityEngine;

namespace Drakken.Domain.Dice.Implementation
{
    public class MitosisSplitResolution : EffectResolution
    {
        public bool DidSplit;
        public DiceInstance ChildA;
        public DiceInstance ChildB;

        public override void NetworkSerialize<T>(BufferSerializer<T> serializer)
        {
            serializer.SerializeValue(ref DidSplit);

            if (!DidSplit) return;

            if (serializer.IsReader)
            {
                ChildA = new DiceInstance();
                ChildB = new DiceInstance();
            }

            serializer.SerializeValue(ref ChildA);
            serializer.SerializeValue(ref ChildB);
        }
    }
    
    public class MitosisFaceEffect : FaceEffectLogic<MitosisSplitResolution>
    {
        public const int MinSides = 4;
        private const int MaxTotalDice = 32; // shouldn't hit this but just in case
        private const float LiftHeight = 1f;
        private const float LiftDuration = 0.35f;
        private const float SplitUpwardSpeed = 4f;
        private const float SplitOutwardSpeed = 1.5f;
        private const float SplitTorque = 5f;

        public override int EffectId => FaceEffectIds.MitosisMark;

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

        private void Split(DiceSimulationWorld world, DiceInstance parent, Vector3 liftedPosition, int childSides)
        {
            world.RemoveDice(parent.InstanceId);

            Vector3 outward = new(Random.Range(-1f, 1f), 0f, Random.Range(-1f, 1f));
            outward = outward.sqrMagnitude > 0.0001f ? outward.normalized : Vector3.right;

            var (halfA, halfB) = SplitFacesRandomly(parent.Faces);
            var childA = DiceInstance.CreateFromRetainedFaces(childSides, halfA);
            var childB = DiceInstance.CreateFromRetainedFaces(childSides, halfB);

            if (childSides > MinSides)
            {
                MarkRandomHalf(childA);
                MarkRandomHalf(childB);
            }

            world.SpawnDice(
                childA, liftedPosition, Random.rotationUniform,
                outward * SplitOutwardSpeed + Vector3.up * SplitUpwardSpeed,
                Random.insideUnitSphere * SplitTorque);

            world.SpawnDice(
                childB, liftedPosition, Random.rotationUniform,
                -outward * SplitOutwardSpeed + Vector3.up * SplitUpwardSpeed,
                Random.insideUnitSphere * SplitTorque);

            world.RecordEffectEvent(EffectId, isFaceEffect: true, parent.InstanceId, new MitosisSplitResolution
            {
                DidSplit = true,
                ChildA = childA,
                ChildB = childB,
            });
        }

        public override void OnMiss(DiceEffectExecuteContext ctx)
        {
            var dice = ctx.SettledDice;

            bool wasMarked = dice.Faces.Any(f => f.FaceEffects.Contains(FaceEffectIds.MitosisMark));
            if (!wasMarked) return;

            // Landed on an unmarked face - the marks are spent, clear them rather than leaving
            // them to persist indefinitely until it eventually lands on one
            foreach (var face in dice.Faces)
            {
                face.FaceEffects = face.FaceEffects
                    .Where(e => e != FaceEffectIds.MitosisMark)
                    .ToList();
            }

            ctx.World.RecordEffectEvent(EffectId, isFaceEffect: true, dice.InstanceId, new MitosisSplitResolution
            {
                DidSplit = false,
            });
        }

        protected override void Apply(GameState gameState, MitosisSplitResolution resolution, int clientIndex, int sourceInstanceId)
        {
            var client = gameState.Clients[clientIndex];
            int index = client.Dice.FindIndex(d => d.InstanceId == sourceInstanceId);
            if (index < 0) return;

            if (resolution.DidSplit)
            {
                client.Dice.RemoveAt(index);
                client.Dice.InsertRange(index, new[] { resolution.ChildA, resolution.ChildB });
                return;
            }

            foreach (var face in client.Dice[index].Faces)
            {
                face.FaceEffects = face.FaceEffects
                    .Where(e => e != FaceEffectIds.MitosisMark)
                    .ToList();
            }
        }

        protected override Task Animate(EffectAnimateContext ctx, MitosisSplitResolution resolution, int sourceInstanceId, CancellationToken ct)
            => Task.CompletedTask;

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
