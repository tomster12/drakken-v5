using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Drakken.Client.World.Animation;
using Drakken.Common.Utility;
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
        public int D3Roll;
        public List<int> ReplacedIndices = new();
        public List<DiceInstance> AddedDiceInstances = new();

        public override void NetworkSerialize<T>(BufferSerializer<T> serializer)
        {
            base.NetworkSerialize(serializer);
            serializer.SerializeValue(ref D3Roll);
            serializer.SerializeList(ref ReplacedIndices);
            serializer.SerializeList(ref AddedDiceInstances);
        }
    }

    public class DragonTokenExecutor : TokenExecutor<EmptyTokenIntent, DragonTokenResolution>
    {
        protected override DragonTokenResolution Execute(GameState gameState, EmptyTokenIntent intent, int sourceClientIndex)
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

            // Create replacement D8s
            var addedDice = new List<DiceInstance>();
            for (int i = 0; i < replaceCount; i++)
            {
                var newDice = DiceInstance.Create(sides: 8);
                newDice.Roll();
                addedDice.Add(newDice);
            }

            return new DragonTokenResolution
            {
                D3Roll = replaceCount,
                ReplacedIndices = replacedIndices,
                AddedDiceInstances = addedDice,
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
        protected override async Task Animate(
            GameState gameState,
            TokenVisualContext visualContext,
            int sourceClientIndex,
            int tokenInstanceId,
            DragonTokenResolution resolution,
            CancellationToken ct)
        {
            // Spawn a new DiceView and roll to match the amount to replace

            await Task.Delay(500);

            // Delete each of the removed dice
            foreach (int removedDiceId in resolution.ReplacedIndices)
            {
                // var diceView = context.GetDiceView(sourceClientIndex, removedDiceId);
                // Assert.NotNull(diceView);
            }

            await Task.Delay(500);

            //
            foreach (var newDice in resolution.AddedDiceInstances)
            {
                Log.Info("DragonAnimator", $"Slamming in D8 id={newDice.InstanceId} value={newDice.Value}");

                // context.SpawnDiceView(resolution.SourceClientIndex, newDice);
                // diceView.PlayLandAnimation(newDice.Value);

                await Task.Delay(200);
            }

            await Task.Delay(100);
        }
    }
}
