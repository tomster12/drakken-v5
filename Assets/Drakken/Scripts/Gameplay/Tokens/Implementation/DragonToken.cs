using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Drakken.Client;
using Drakken.Client.World;
using Drakken.Utility;
using Drakken.Gameplay.Simulation;
using Drakken.Gameplay.Tokens.Implementation.Common;
using Drakken.Gameplay.Tokens.Logic;
using Unity.Netcode;
using UnityEngine;
using Drakken.Domain;
using Drakken.Domain.Networking;

namespace Drakken.Gameplay.Tokens.Implementation
{
    public class DragonSummary : TokenSummary
    {
        public int ReplaceCount;
        public List<int> ReplacedInstanceIds = new();

        public override void NetworkSerialize<T>(BufferSerializer<T> serializer)
        {
            serializer.SerializeValue(ref ReplaceCount);
            serializer.SerializeList(ref ReplacedInstanceIds);
        }
    }

    public class DragonTokenLogic : TokenLogic<EmptyTokenIntent, DragonSummary>
    {
        private const float LiftHeight = 1.2f;
        private const float LiftDuration = 0.5f;
        private const float LiftSpinTurns = 2f;
        private const float HoverDuration = 1f;
        private const float HoverSpinTurns = 1.5f;

        private static readonly Color HighlightColor = Colors.Hex("#f8827e");
        private const float HighlightDuration = 0.9f;
        private const float D4OffsetDistance = 1.5f;

        protected override (List<GameSimulationTrace> Traces, DragonSummary Summary) ExecuteToken(
            GameState gameState,
            EmptyTokenIntent intent,
            int sourceClientIndex,
            GameSimulationWorld world)
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

            var summary = new DragonSummary { ReplaceCount = replaceCount };

            world.BeginSession(client.Dice);

            // Lift each replaced dice into the air with a spin
            var liftDriveIds = new List<string>();
            var liftPositions = new Dictionary<int, Vector3>();
            foreach (var index in replacedIndices)
            {
                int diceInstanceId = client.Dice[index].InstanceId;

                var (startPosition, startRotation) = world.GetDicePose(diceInstanceId);
                liftPositions[index] = startPosition + Vector3.up * LiftHeight;
                Vector3 spinAxis = Random.insideUnitSphere.normalized;

                liftDriveIds.Add(world.DriveDice(
                    diceInstanceId,
                    LiftDuration,
                    t => Vector3.Lerp(startPosition, liftPositions[index], Easing.EaseOutCubic(t)),
                    t => startRotation * Quaternion.AngleAxis(LiftSpinTurns * 360f * t, spinAxis)));
            }

            world.Simulate(untilDrivesComplete: liftDriveIds);

            // Now try modify each lifted dice to a D8
            var addedDice = new List<DiceInstance>();
            var hoverDriveIds = new List<string>();
            foreach (var replacedIndex in replacedIndices)
            {
                var replacedDice = client.Dice[replacedIndex];

                if (!TokenExecutionLogic.TryModify(replacedDice, world)) continue;

                // Successfully modified so remove old dice, add new dice
                world.RemoveDice(replacedDice.InstanceId);

                var newDice = DiceInstance.Create(sides: 8);
                addedDice.Add(newDice);
                summary.ReplacedInstanceIds.Add(replacedDice.InstanceId);

                Vector3 hoverPosition = liftPositions[replacedIndex];
                Quaternion spawnRotation = Random.rotationUniform;
                Vector3 spinAxis = Random.insideUnitSphere.normalized;
                // RemoveDice/SpawnDice above already recorded the GameState-updating events
                world.SpawnDice(newDice, hoverPosition, spawnRotation);

                hoverDriveIds.Add(world.DriveDice(
                    newDice.InstanceId,
                    HoverDuration,
                    _ => hoverPosition,
                    t => spawnRotation * Quaternion.AngleAxis(HoverSpinTurns * 360f * Easing.EaseOutCubic(t), spinAxis)));
            }

            if (hoverDriveIds.Count > 0)
            {
                world.Simulate(untilDrivesComplete: hoverDriveIds);
            }

            // All changes + hovers finished, drop dice back down to finish
            foreach (var newDice in addedDice)
            {
                world.WakeDice(newDice.InstanceId);
            }

            world.Simulate(untilAllSettled: true);

            world.FreezeAllDice();

            return (new List<GameSimulationTrace> { world.EndSession() }, summary);
        }

        public override async Task AnimateToken(
            ClientMatch match,
            TokenVisualContext visualContext,
            int sourceClientIndex,
            int tokenInstanceId,
            List<GameSimulationTrace> traces,
            DragonSummary summary,
            CancellationToken ct)
        {
            var sourcePlayerObjects = visualContext.Client.SceneObjects.Player(sourceClientIndex);
            var tokenView = visualContext.TokenView.transform;

            // Spawn a D4 next to the token showing how many dice it's about to replace
            var d4Instance = DiceInstance.Create(sides: 4, currentSide: summary.ReplaceCount - 1);
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
            foreach (var instanceId in summary.ReplacedInstanceIds)
            {
                Assert.True(sourcePlayerObjects.DiceViews.TryGetValue(instanceId, out var diceView));
                tasks.Add(diceView.FlashHighlight(HighlightColor, HighlightDuration, ct));
            }

            await Task.Delay(500);

            // Replay the simulation with the lift / change / hover
            await sourcePlayerObjects.SimReplayer.Play(
                traces[0], ct, sourcePlayerObjects);

            await Task.WhenAll(tasks);

            visualContext.Client.UI.UpdateDiceTotal(match.ClientIndex, sourceClientIndex);
        }
    }
}
