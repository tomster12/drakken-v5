using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Drakken.Common.Utility;
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
        public List<int> RemovedDiceIds = new();
        public List<DiceInstance> AddedDice = new();

        public override void NetworkSerialize<T>(BufferSerializer<T> serializer)
        {
            base.NetworkSerialize(serializer);
            serializer.SerializeValue(ref D3Roll);
            serializer.SerializeList(ref RemovedDiceIds);
            serializer.SerializeList(ref AddedDice);
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

            // Select the dice to remove
            var diceToReplace = client.Dice.OrderBy(_ => Random.value).Take(replaceCount).ToList();
            var removedDiceIds = diceToReplace.Select(d => d.InstanceId).ToList();

            // Add replacement D8s
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
                RemovedDiceIds = removedDiceIds,
                AddedDice = addedDice,
            };
        }

        protected override void Apply(GameState gameState, DragonTokenResolution resolution, int sourceClientIndex)
        {
            var client = gameState.Clients[sourceClientIndex];

            client.Dice.RemoveAll(d => resolution.RemovedDiceIds.Contains(d.InstanceId));
            client.Dice.AddRange(resolution.AddedDice);
        }
    }

    public class DragonTokenAnimator : TokenAnimator<DragonTokenResolution>
    {
        protected override async Task Animate(GameState gameState, DragonTokenResolution resolution, TokenVisualContext context, int sourceClientIndex)
        {
            Log.Info("DragonAnimator", "Spawning dragon VFX");

            // e.g. context.SpawnVFX("DragonRoar", context.BoardCenter);

            await Task.Delay(600);

            foreach (int removedDiceId in resolution.RemovedDiceIds)
            {
                // var diceView = context.GetDiceView(sourceClientIndex, removedDiceId);
                // Assert.NotNull(diceView);

                Log.Info("DragonAnimator", $"Shattering dice id={removedDiceId}");
                // diceView.PlayShatterAnimation();

                // TODO: Delete dice view

                await Task.Delay(300);
            }

            await Task.Delay(200);

            foreach (var newDice in resolution.AddedDice)
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
