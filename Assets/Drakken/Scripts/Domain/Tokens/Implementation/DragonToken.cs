using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Drakken.Client;
using Drakken.Client.World;
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
    public class DragonTokenResolution : TokenResolution
    {
        public int ReplaceCount;
        public List<int> ReplacedInstanceIds = new();
        public List<DiceInstance> AddedDiceInstances = new();
        public DiceSimulationTraces DiceTrace = new();

        public override void NetworkSerialize<T>(BufferSerializer<T> serializer)
        {
            base.NetworkSerialize(serializer);
            serializer.SerializeValue(ref ReplaceCount);
            serializer.SerializeList(ref ReplacedInstanceIds);
            serializer.SerializeList(ref AddedDiceInstances);
            serializer.SerializeValue(ref DiceTrace);
        }
    }

    public class DragonTokenExecutor : TokenExecutor<EmptyTokenIntent, DragonTokenResolution>
    {
        private const float LiftHeight = 1.2f;
        private const float LiftDuration = 0.5f;
        private const float LiftSpinTurns = 2f;
        private const float HoverDuration = 1f;
        private const float HoverSpinTurns = 1.5f;

        protected override DragonTokenResolution Execute(
            GameState gameState,
            EmptyTokenIntent intent,
            int sourceClientIndex,
            DiceSimulationWorld diceWorld)
        {
            var client = gameState.Clients[sourceClientIndex];

            // Roll a D4 to determine how many dice to replace
            int replaceCount = Random.Range(1, 5);
            replaceCount = Mathf.Min(replaceCount, client.Dice.Count);

            // Select random dice to replace
            var replacedIndices = Enumerable.Range(0, client.Dice.Count)
                .OrderBy(_ => Random.value)
                .Take(replaceCount)
                .ToList();

            var resolution = new DragonTokenResolution
            {
                ReplaceCount = replaceCount
            };

            diceWorld.BeginSession();

            // Lift each replaced dice into the air with a spin
            var liftDriveIds = new List<string>();
            var liftPositions = new Dictionary<int, Vector3>();
            foreach (var index in replacedIndices)
            {
                int diceInstanceId = client.Dice[index].InstanceId;

                var (startPosition, startRotation) = diceWorld.GetDicePose(diceInstanceId);
                liftPositions[index] = startPosition + Vector3.up * LiftHeight;
                Vector3 spinAxis = Random.insideUnitSphere.normalized;

                liftDriveIds.Add(diceWorld.DriveDice(
                    diceInstanceId,
                    LiftDuration,
                    t => Vector3.Lerp(startPosition, liftPositions[index], Easing.EaseOutCubic(t)),
                    t => startRotation * Quaternion.AngleAxis(LiftSpinTurns * 360f * t, spinAxis)));
            }

            diceWorld.Simulate(untilDrivesComplete: liftDriveIds);

            // Now try modify each lifted dice to a D8
            var addedDice = new List<DiceInstance>();
            var hoverDriveIds = new List<string>();
            foreach (var replacedIndex in replacedIndices)
            {
                var replacedDice = client.Dice[replacedIndex];

                if (!TokenExecutionLogic.TryModify(replacedDice, diceWorld, resolution)) continue;

                // Successfully modified so remove old dice, add new dice
                diceWorld.RemoveDice(replacedDice.InstanceId);

                var newDice = DiceInstance.Create(sides: 8);
                addedDice.Add(newDice);
                resolution.ReplacedInstanceIds.Add(replacedDice.InstanceId);

                Vector3 hoverPosition = liftPositions[replacedIndex];
                Quaternion spawnRotation = Random.rotationUniform;
                Vector3 spinAxis = Random.insideUnitSphere.normalized;

                diceWorld.SpawnDice(newDice, hoverPosition, spawnRotation);

                hoverDriveIds.Add(diceWorld.DriveDice(
                    newDice.InstanceId,
                    HoverDuration,
                    _ => hoverPosition,
                    t => spawnRotation * Quaternion.AngleAxis(HoverSpinTurns * 360f * Easing.EaseOutCubic(t), spinAxis)));
            }

            if (hoverDriveIds.Count > 0)
            {
                diceWorld.Simulate(untilDrivesComplete: hoverDriveIds);
            }

            // All changes + hovers finished, drop dice back down to finish
            foreach (var newDice in addedDice)
            {
                diceWorld.WakeDice(newDice.InstanceId, Vector3.zero, Vector3.zero);
            }

            diceWorld.Simulate(untilAllSettled: true);

            diceWorld.FreezeAllDice();
            resolution.AddedDiceInstances = addedDice;
            resolution.DiceTrace = diceWorld.EndSession();

            return resolution;
        }

        protected override void Apply(
            GameState gameState,
            DragonTokenResolution resolution,
            int sourceClientIndex)
        {
            var client = gameState.Clients[sourceClientIndex];

            Assert.True(resolution.ReplacedInstanceIds.Count == resolution.AddedDiceInstances.Count);
            Assert.True(resolution.ReplaceCount == resolution.ReplacedInstanceIds.Count + resolution.SideEffectsDestroyedDiceInstanceIds.Count);

            // Directly overwrite replaced indices with new dice instances
            for (int i = 0; i < resolution.ReplacedInstanceIds.Count; i++)
            {
                var index = client.Dice.FindIndex(d => d.InstanceId == resolution.ReplacedInstanceIds[i]);
                Assert.True(index >= 0);
                client.Dice[index] = resolution.AddedDiceInstances[i];
            }
        }
    }

    public class DragonTokenAnimator : TokenAnimator<DragonTokenResolution>
    {
        private static readonly Color HighlightColor = Colors.Hex("#f8827e");
        private const float HighlightDuration = 0.9f;
        private const float D4OffsetDistance = 1.5f;

        protected override async Task Animate(
            ClientMatch match,
            TokenVisualContext visualContext,
            int sourceClientIndex,
            int tokenInstanceId,
            DragonTokenResolution resolution,
            CancellationToken ct)
        {
            var sourcePlayerObjects = visualContext.Client.SceneObjects.Player(sourceClientIndex);
            var tokenView = visualContext.TokenView.transform;

            // Spawn a D4 next to the token showing how many dice it's about to replace
            var d4Instance = DiceInstance.Create(sides: 4, currentSide: resolution.ReplaceCount - 1);
            var d4View = DiceView.Create(visualContext.Client.Assets, d4Instance, scale: 1.2f);
            Vector3 d4Pos = tokenView.position + tokenView.right * D4OffsetDistance;
            d4View.transform.SetPositionAndRotation(d4Pos, tokenView.rotation);

            await d4View.AnimateRoll(ct, durationMultiplier: 1.4f);

            await Task.Delay(250);

            List<Task> tasks = new()
            {
                // Shrink token and D4 out of the way after the roll
                visualContext.TokenView.AnimateShrinkAfter(0f, ct),
                d4View.AnimateShrinkAndDestroy(ct)
            };

            // Highlight each of the selected dice views
            foreach (var instanceId in resolution.ReplacedInstanceIds)
            {
                Assert.True(sourcePlayerObjects.DiceViews.TryGetValue(instanceId, out var diceView));
                tasks.Add(diceView.FlashHighlight(HighlightColor, HighlightDuration, ct));
            }

            await Task.Delay(500);

            // Replay the full simulation
            await sourcePlayerObjects.DiceSimReplayer.Play(
                visualContext.Client.Assets, visualContext.Client, resolution.DiceTrace, sourcePlayerObjects, ct);

            await Task.WhenAll(tasks);

            visualContext.Client.UI.UpdateDiceTotal(match.ClientIndex, sourceClientIndex);
        }
    }
}
