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
    public class ReinforceTokenResolution : TokenResolution
    {
        public int OriginalInstanceId;
        public bool DiceExpanded;
        public DiceInstance FinalDiceInstance;
        public DiceSimulationTraces DiceTrace = new();

        public override void NetworkSerialize<T>(BufferSerializer<T> serializer)
        {
            base.NetworkSerialize(serializer);
            serializer.SerializeValue(ref OriginalInstanceId);
            serializer.SerializeValue(ref DiceExpanded);
            serializer.SerializeValue(ref DiceTrace);

            bool hasFinalDice = FinalDiceInstance != null;
            serializer.SerializeValue(ref hasFinalDice);
            if (hasFinalDice) serializer.SerializeValue(ref FinalDiceInstance);
        }
    }

    public class ReinforceTokenExecutor : TokenExecutor<PickDiceTokenIntent, ReinforceTokenResolution>
    {
        private const int FaceIncrease = 2;
        private const int SidesIncrease = 2;

        private const float LiftHeight = 1f;
        private const float LiftDuration = 0.5f;
        private const float ExtraSpinTurns = 1f;
        private const float HoverDuration = 0.5f;

        private const float RollTorque = 12f;
        private const float PopUpwardSpeed = 3f;

        protected override ReinforceTokenResolution Execute(
            GameState gameState,
            PickDiceTokenIntent intent,
            int sourceClientIndex,
            DiceSimulationWorld diceWorld)
        {
            Assert.True(intent.TargetDiceInstanceIds != null && intent.TargetDiceInstanceIds.Count == 1);

            var client = gameState.Clients[sourceClientIndex];

            // Find selected dice
            int originalInstanceId = intent.TargetDiceInstanceIds[0];
            var targetDice = client.Dice.Find(d => d.InstanceId == originalInstanceId);
            Assert.NotNull(targetDice);

            var resolution = new ReinforceTokenResolution { OriginalInstanceId = originalInstanceId };

            diceWorld.BeginSession(client.Dice);

            // Ensure we can modify the dice
            if (!TokenExecutionLogic.TryModify(targetDice, diceWorld, resolution))
            {
                resolution.DiceTrace = diceWorld.EndSession();
                return resolution;
            }

            // Calculate if we are expanding the sides
            int oldSides = targetDice.Sides;
            int oldIndex = targetDice.CurrentSide;
            int newIndex = oldIndex + FaceIncrease;
            bool diceExpanded = newIndex >= oldSides;
            int newSides = diceExpanded ? oldSides + SidesIncrease : oldSides;

            resolution.DiceExpanded = diceExpanded;

            // Start the animation of the dice
            var (startPosition, startRotation) = diceWorld.GetDicePose(targetDice.InstanceId);
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

                string spinDriveId = diceWorld.DriveDice(
                    targetDice.InstanceId, LiftDuration,
                    t => Vector3.Lerp(startPosition, liftedPosition, Easing.EaseOutCubic(t)),
                    t => Quaternion.AngleAxis(totalSpinAngle * t, spinAxis) * startRotation);

                diceWorld.Simulate(untilDrivesComplete: new[] { spinDriveId });
                diceWorld.Simulate(forSeconds: HoverDuration);

                string returnDriveId = diceWorld.DriveDice(
                    targetDice.InstanceId, LiftDuration,
                    t => Vector3.Lerp(liftedPosition, startPosition, Easing.EaseInOutCubic(t)),
                    _ => targetRotation);

                diceWorld.Simulate(untilDrivesComplete: new[] { returnDriveId });

                targetDice.CurrentSide = newIndex;
                finalDice = targetDice;
            }
            else
            {
                // Exceeds the max face - float up, hover, then swap to the bigger dice and
                // give it a big roll and a small pop upward for a genuine reroll
                string liftDriveId = diceWorld.DriveDice(
                    targetDice.InstanceId, LiftDuration,
                    t => Vector3.Lerp(startPosition, liftedPosition, Easing.EaseOutCubic(t)),
                    _ => startRotation);

                diceWorld.Simulate(untilDrivesComplete: new[] { liftDriveId });

                diceWorld.RemoveDice(targetDice.InstanceId);

                var retainedValues = targetDice.Faces.Select(f => f.Value).ToList();
                var newDice = DiceInstance.CreateFromRetainedFaces(newSides, retainedValues);

                Vector3 rollTorque = Random.insideUnitSphere.normalized * RollTorque;

                diceWorld.SpawnDice(newDice, liftedPosition, startRotation, Vector3.up * PopUpwardSpeed, rollTorque);

                diceWorld.Simulate(untilAllSettled: true);

                finalDice = newDice;
            }

            diceWorld.FreezeAllDice();
            resolution.FinalDiceInstance = finalDice;
            resolution.DiceTrace = diceWorld.EndSession();

            return resolution;
        }

        private static Quaternion GetFaceUpRotation(int sides, int faceIndex)
        {
            var shapeInstance = DiceInstance.Create(sides);
            var mesh = DiceMeshFactory.Create(shapeInstance);
            Quaternion rotation = DiceMeshFactory.GetRotationForSide(mesh.Faces, faceIndex);
            GameObject.Destroy(mesh.GameObject);
            return rotation;
        }

        protected override void Apply(
            GameState gameState,
            ReinforceTokenResolution resolution,
            int sourceClientIndex)
        {
            // If the modification failed then exit early
            if (resolution.FinalDiceInstance == null) return;

            var client = gameState.Clients[sourceClientIndex];

            int index = client.Dice.FindIndex(d => d.InstanceId == resolution.OriginalInstanceId);
            Assert.True(index >= 0);
            client.Dice[index] = resolution.FinalDiceInstance;
        }
    }

    public class ReinforceTokenAnimator : TokenAnimator<ReinforceTokenResolution>
    {
        protected override async Task Animate(
            ClientMatch match,
            TokenVisualContext visualContext,
            int sourceClientIndex,
            int tokenInstanceId,
            ReinforceTokenResolution resolution,
            CancellationToken ct)
        {
            var sourcePlayerObjects = visualContext.Client.SceneObjects.Player(sourceClientIndex);

            // Shrink the token out the way
            await visualContext.TokenView.AnimateShrinkAfter(0.5f, ct);

            // Replay the full simulation (float, spin, and dice swap if it occurred)
            await sourcePlayerObjects.DiceSimReplayer.Play(
                resolution.DiceTrace, sourcePlayerObjects, ct);

            visualContext.Client.UI.UpdateDiceTotal(match.ClientIndex, sourceClientIndex);
        }
    }
}
