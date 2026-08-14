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
            int replaceCount = Random.Range(1, Mathf.Min(5, client.Dice.Count + 1));
            replaceCount = Mathf.Min(replaceCount, client.Dice.Count);

            // Select random dice to replace
            var replacedIndices = Enumerable.Range(0, client.Dice.Count)
                .OrderBy(_ => Random.value)
                .Take(replaceCount)
                .ToList();

            var replacedInstanceIds = replacedIndices
                .Select(i => client.Dice[i].InstanceId)
                .ToList();

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

            // Swap each lifted dice for a D8, spin it place, then drop
            var addedDice = new List<DiceInstance>();
            var hoverDriveIds = new List<string>();
            foreach (var replacedIndex in replacedIndices)
            {
                diceWorld.RemoveDice(client.Dice[replacedIndex].InstanceId);

                var newDice = DiceInstance.Create(sides: 8);
                addedDice.Add(newDice);

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

            diceWorld.Simulate(untilDrivesComplete: hoverDriveIds);

            foreach (var newDice in addedDice)
            {
                diceWorld.WakeDice(newDice.InstanceId, Vector3.zero, Vector3.zero);
            }

            diceWorld.Simulate(untilAllSettled: true);

            diceWorld.FreezeAllDice();
            var trace = diceWorld.EndSession();

            return new DragonTokenResolution
            {
                ReplaceCount = replaceCount,
                ReplacedInstanceIds = replacedInstanceIds,
                AddedDiceInstances = addedDice,
                DiceTrace = trace,
            };
        }

        protected override void Apply(GameState gameState, DragonTokenResolution resolution, int sourceClientIndex)
        {
            Assert.True(resolution.ReplacedInstanceIds.Count == resolution.AddedDiceInstances.Count);
            Assert.True(resolution.ReplaceCount == resolution.AddedDiceInstances.Count);

            var client = gameState.Clients[sourceClientIndex];

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
            await Task.Delay(250);

            var sourcePlayerObjects = visualContext.Client.SceneObjects.Player(sourceClientIndex);

            // Spawn a D4 next to the token showing how many dice it's about to replace
            var d4Instance = DiceInstance.Create(sides: 4, currentSide: resolution.ReplaceCount - 1);
            var d4View = DiceView.Create(visualContext.Client.Assets, d4Instance, scale: 1.2f);

            var token = visualContext.TokenView.transform;
            Vector3 d4Pos = token.position + token.right * D4OffsetDistance;
            d4View.transform.SetPositionAndRotation(d4Pos, token.rotation);

            await d4View.AnimateRoll(ct, durationMultiplier: 1.4f);

            // Immediately shrink token out of the way after the roll
            var shrinkTokenTask = visualContext.TokenView.AnimateShrink(0f, ct);

            // Highlight each of the dice views
            List<Task> flashTasks = new();
            foreach (var instanceId in resolution.ReplacedInstanceIds)
            {
                Assert.True(sourcePlayerObjects.DiceViews.TryGetValue(instanceId, out var diceView));
                flashTasks.Add(diceView.FlashHighlight(HighlightColor, HighlightDuration, ct));
            }

            await Task.Delay(500);
            await d4View.AnimateShrinkAndDestroy(ct);

            await Task.WhenAll(flashTasks);

            // Replay full physical animation
            var newDiceViews = await sourcePlayerObjects.DiceSimReplayer.Play(
                visualContext.Client.Assets, visualContext.Client, resolution.DiceTrace, sourcePlayerObjects, ct);

            await shrinkTokenTask;

            // Swap out the replaced dice for the newly rolled ones keyed by instance id
            for (int i = 0; i < resolution.ReplacedInstanceIds.Count; i++)
            {
                var addedInstance = resolution.AddedDiceInstances[i];
                var newDiceView = newDiceViews[addedInstance.InstanceId];
                sourcePlayerObjects.DiceViews.Remove(resolution.ReplacedInstanceIds[i]);
                sourcePlayerObjects.DiceViews[addedInstance.InstanceId] = newDiceView;
            }

            visualContext.Client.UI.UpdateDiceTotal(match.ClientIndex, sourceClientIndex);

            await Task.Delay(100);
        }
    }
}
