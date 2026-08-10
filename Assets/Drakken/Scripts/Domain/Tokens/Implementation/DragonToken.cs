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
using Unity.Netcode;
using UnityEngine;

namespace Drakken.Domain.Tokens.Implementation
{
    public class DragonTokenResolution : TokenResolution
    {
        public int D3Roll;
        public List<int> ReplacedIndices = new();
        public List<DiceInstance> AddedDiceInstances = new();
        public DiceSimulationTraces DiceTrace = new();

        public override void NetworkSerialize<T>(BufferSerializer<T> serializer)
        {
            base.NetworkSerialize(serializer);
            serializer.SerializeValue(ref D3Roll);
            serializer.SerializeList(ref ReplacedIndices);
            serializer.SerializeList(ref AddedDiceInstances);
            serializer.SerializeValue(ref DiceTrace);
        }
    }

    public class DragonTokenExecutor : TokenExecutor<EmptyTokenIntent, DragonTokenResolution>
    {
        private const float SpawnY = 1.5f;
        private const float ThrowY = 5f;
        private const float DiceThrowImpulseSpeed = 5f;
        private const float DiceThrowTorque = 30f;

        protected override DragonTokenResolution Execute(GameState gameState, EmptyTokenIntent intent, int sourceClientIndex, DiceSimulationWorld diceWorld)
        {
            var client = gameState.Clients[sourceClientIndex];

            // Roll a D3 to determine how many dice to replace
            int replaceCount = Random.Range(1, 4);
            replaceCount = Mathf.Min(replaceCount, client.Dice.Count);

            // Select random indices to replace
            var replacedIndices = Enumerable.Range(0, client.Dice.Count)
                .OrderBy(_ => Random.value)
                .Take(replaceCount)
                .ToList();

            var trayCenter = GameEntrypoint.Singleton.SceneLayout.Dice.Player(sourceClientIndex).transform.position;

            diceWorld.BeginSession();

            // Remove the replaced dice from the physical world and throw in their replacements
            var addedDice = new List<DiceInstance>();
            foreach (var replacedIndex in replacedIndices)
            {
                diceWorld.RemoveDice(client.Dice[replacedIndex].InstanceId);

                var newDice = DiceInstance.Create(sides: 8);

                Vector3 spawnPos = trayCenter + new Vector3(Random.Range(-0.3f, 0.3f), SpawnY, Random.Range(-0.3f, 0.3f));
                Quaternion spawnRotation = Random.rotationUniform;
                Vector3 throwTarget = trayCenter + Vector3.up * ThrowY * Random.Range(0.6f, 1.4f);
                Vector3 throwVelocity = (throwTarget - spawnPos).normalized * DiceThrowImpulseSpeed;
                Vector3 throwTorque = Random.insideUnitSphere * DiceThrowTorque;

                diceWorld.SpawnDice(newDice, spawnPos, spawnRotation, throwVelocity, throwTorque);
                addedDice.Add(newDice);
            }

            diceWorld.SimulateUntilAllSettled();
            var trace = diceWorld.EndSession();

            return new DragonTokenResolution
            {
                D3Roll = replaceCount,
                ReplacedIndices = replacedIndices,
                AddedDiceInstances = addedDice,
                DiceTrace = trace,
            };
        }

        protected override void Apply(GameState gameState, DragonTokenResolution resolution, int sourceClientIndex)
        {
            Assert.True(resolution.ReplacedIndices.Count == resolution.AddedDiceInstances.Count);
            Assert.True(resolution.D3Roll == resolution.AddedDiceInstances.Count);

            var client = gameState.Clients[sourceClientIndex];

            for (int i = 0; i < resolution.ReplacedIndices.Count; i++)
            {
                var index = resolution.ReplacedIndices[i];
                Assert.True(index >= 0 && index < client.Dice.Count);
                client.Dice[index] = resolution.AddedDiceInstances[i];
            }
        }
    }

    public class DragonTokenAnimator : TokenAnimator<DragonTokenResolution>
    {
        private const float D3OffsetDistance = 1.4f;

        protected override async Task Animate(
            ClientMatch match,
            TokenVisualContext visualContext,
            int sourceClientIndex,
            int tokenInstanceId,
            DragonTokenResolution resolution,
            CancellationToken ct)
        {
            await Task.Delay(250);

            var sourcePlayerObjects = visualContext.SceneObjects.Player(sourceClientIndex);

            // Spawn a D3 next to the token showing how many dice it's about to replace
            // TODO: Fix D3 reliance
            var d3Instance = DiceInstance.Create(sides: 3, value: resolution.D3Roll);
            var d3View = DiceView.Create(visualContext.Assets, d3Instance);

            var tokenTransform = visualContext.TokenView.transform;
            Vector3 d3Pos = tokenTransform.position + tokenTransform.right * D3OffsetDistance;
            d3View.transform.SetPositionAndRotation(d3Pos, tokenTransform.rotation);

            // await d3View.AnimateRoll(ct, durationMultiplier: 0.7f);
            await Task.Delay(200);

            await d3View.AnimateShrinkAndDestroy(ct);

            // Shrink and remove each replaced dice
            List<Task> removeDiceAnimationTasks = new();
            foreach (int removedDiceIndex in resolution.ReplacedIndices)
            {
                removeDiceAnimationTasks.Add(sourcePlayerObjects.DiceViews[removedDiceIndex].AnimateShrinkAndDestroy(ct));
                sourcePlayerObjects.DiceViews[removedDiceIndex] = null;
            }
            await Task.WhenAll(removeDiceAnimationTasks);

            await Task.Delay(100);

            // Replay dice simulation
            var viewsByInstanceId = await sourcePlayerObjects.DiceSimReplayer.Play(
                visualContext.Assets, resolution.DiceTrace, sourcePlayerObjects, ct);

            // Slot the newly rolled dice into their replaced positions
            // Be careful to rebind the dice view to the official dice instance
            for (int i = 0; i < resolution.ReplacedIndices.Count; i++)
            {
                var addedInstance = resolution.AddedDiceInstances[i];
                var newDiceView = viewsByInstanceId[addedInstance.InstanceId];
                newDiceView.Rebind(addedInstance);
                sourcePlayerObjects.DiceViews[resolution.ReplacedIndices[i]] = newDiceView;
            }

            visualContext.ClientUI.UpdateDiceTotal(match.ClientIndex, sourceClientIndex);

            await Task.Delay(100);
        }
    }
}
