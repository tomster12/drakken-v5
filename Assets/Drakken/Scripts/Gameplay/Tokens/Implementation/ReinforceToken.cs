using System.Collections.Generic;
using System.Linq;
using Drakken.Utility;
using Drakken.Presentation;
using Drakken.Gameplay.Simulation;
using Drakken.Gameplay.Tokens.Implementation.Common;
using Drakken.Gameplay.Tokens.Logic;
using Unity.Netcode;
using UnityEngine;
using Drakken.Domain;

namespace Drakken.Gameplay.Tokens.Implementation
{
    public class ReinforceResolution : EventResolution
    {
        public int OriginalInstanceId;
        public bool DiceExpanded;
        public DiceInstance FinalDiceInstance;

        public override void NetworkSerialize<T>(BufferSerializer<T> serializer)
        {
            serializer.SerializeValue(ref OriginalInstanceId);
            serializer.SerializeValue(ref DiceExpanded);

            if (serializer.IsReader) FinalDiceInstance = new DiceInstance();
            serializer.SerializeValue(ref FinalDiceInstance);
        }
    }

    public class ReinforceTokenLogic : TokenLogic<PickDiceTokenIntent, ReinforceResolution>
    {
        private const int FaceIncrease = 2;
        private const int SidesIncrease = 2;

        private const float LiftHeight = 1f;
        private const float LiftDuration = 0.5f;
        private const float ExtraSpinTurns = 1f;
        private const float HoverDuration = 0.5f;

        private const float RollTorque = 12f;
        private const float PopUpwardSpeed = 3f;

        public override int EventId => 6;

        protected override List<GameSimulationTrace> ExecuteToken(
            GameState gameState,
            PickDiceTokenIntent intent,
            int sourceClientIndex,
            GameSimulationWorld world)
        {
            Assert.True(intent.TargetDiceInstanceIds != null && intent.TargetDiceInstanceIds.Count == 1);

            var client = gameState.Clients[sourceClientIndex];

            // Find selected dice
            int originalInstanceId = intent.TargetDiceInstanceIds[0];
            var targetDice = client.Dice.Find(d => d.InstanceId == originalInstanceId);
            Assert.NotNull(targetDice);

            world.BeginSession(client.Dice);

            // Ensure we can modify the dice
            if (!TokenExecutionLogic.TryModify(targetDice, world))
            {
                return new List<GameSimulationTrace> { world.EndSession() };
            }

            // Calculate if we are expanding the sides
            int oldSides = targetDice.Sides;
            int oldIndex = targetDice.CurrentSide;
            int newIndex = oldIndex + FaceIncrease;
            bool diceExpanded = newIndex >= oldSides;
            int newSides = diceExpanded ? oldSides + SidesIncrease : oldSides;

            // Start the animation of the dice
            var (startPosition, startRotation) = world.GetDicePose(targetDice.InstanceId);
            Vector3 liftedPosition = startPosition + Vector3.up * LiftHeight;

            DiceInstance finalDice;

            if (!diceExpanded)
            {
                // Fits on the same dice - float up, spin directly onto the higher face,
                // hover, then place back down where it started
                Quaternion targetRotation = GetFaceUpRotation(oldSides, newIndex);
                Quaternion rotationDelta = targetRotation * Quaternion.Inverse(startRotation);
                rotationDelta.ToAngleAxis(out float deltaAngle, out Vector3 spinAxis);
                if (spinAxis.sqrMagnitude < 0.0001f) spinAxis = Vector3.up;
                float totalSpinAngle = deltaAngle + 360f * ExtraSpinTurns;

                string spinDriveId = world.DriveDice(
                    targetDice.InstanceId, LiftDuration,
                    t => Vector3.Lerp(startPosition, liftedPosition, Easing.EaseOutCubic(t)),
                    t => Quaternion.AngleAxis(totalSpinAngle * t, spinAxis) * startRotation);

                world.Simulate(untilDrivesComplete: new[] { spinDriveId });
                world.Simulate(forSeconds: HoverDuration);

                string returnDriveId = world.DriveDice(
                    targetDice.InstanceId, LiftDuration,
                    t => Vector3.Lerp(liftedPosition, startPosition, Easing.EaseInOutCubic(t)),
                    _ => targetRotation);

                world.Simulate(untilDrivesComplete: new[] { returnDriveId });

                targetDice.CurrentSide = newIndex;
                finalDice = targetDice;
            }
            else
            {
                // Exceeds the max face - float up, hover, then swap to the bigger dice and
                // give it a big roll and a small pop upward for a genuine reroll
                string liftDriveId = world.DriveDice(
                    targetDice.InstanceId, LiftDuration,
                    t => Vector3.Lerp(startPosition, liftedPosition, Easing.EaseOutCubic(t)),
                    _ => startRotation);

                world.Simulate(untilDrivesComplete: new[] { liftDriveId });

                world.RemoveDice(targetDice.InstanceId);

                var retainedValues = targetDice.Faces.Select(f => f.Value).ToList();
                var newDice = DiceInstance.CreateFromRetainedFaces(newSides, retainedValues);

                Vector3 rollTorque = Random.insideUnitSphere.normalized * RollTorque;

                world.SpawnDice(newDice, liftedPosition, startRotation, Vector3.up * PopUpwardSpeed, rollTorque);

                world.Simulate(untilAllSettled: true);

                finalDice = newDice;
            }

            world.FreezeAllDice();

            world.RecordEvent(EventId, EventKind.Token, originalInstanceId, finalDice.CurrentSide, new ReinforceResolution
            {
                OriginalInstanceId = originalInstanceId,
                DiceExpanded = diceExpanded,
                FinalDiceInstance = finalDice.Clone(),
            });

            return new List<GameSimulationTrace> { world.EndSession() };
        }

        protected override void ApplyEvent(GameState gameState, ReinforceResolution Resolution, int clientIndex, int sourceInstanceId)
        {
            var client = gameState.Clients[clientIndex];

            int index = client.Dice.FindIndex(d => d.InstanceId == Resolution.OriginalInstanceId);
            if (index < 0) return;
            client.Dice[index] = Resolution.FinalDiceInstance;
        }

        private static Quaternion GetFaceUpRotation(int sides, int faceIndex)
        {
            var shapeInstance = DiceInstance.Create(sides);
            var mesh = DiceMeshFactory.Create(shapeInstance);
            Quaternion rotation = DiceMeshFactory.GetRotationForSide(mesh.Faces, faceIndex);
            GameObject.Destroy(mesh.GameObject);
            return rotation;
        }
    }
}
