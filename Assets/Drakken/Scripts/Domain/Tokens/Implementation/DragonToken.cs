using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Drakken.Client;
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
            ClientMatch match,
            TokenVisualContext visualContext,
            int sourceClientIndex,
            int tokenInstanceId,
            DragonTokenResolution resolution,
            CancellationToken ct)
        {
            await Task.Delay(500);

            var sourcePlayerObjects = visualContext.SceneObjects.Player(sourceClientIndex);

            // Shrink each removed dice to localScale 0
            List<Task> removeDiceAnimationTasks = new();
            foreach (int removedDiceIndex in resolution.ReplacedIndices)
            {
                var diceView = sourcePlayerObjects.DiceViews[removedDiceIndex];
                removeDiceAnimationTasks.Add(diceView.Animator.Play(AnimationSequenceBuilder
                    .Start()
                    .Next(AnimationTracks.LocalScale(
                        0.3f, diceView.transform, diceView.transform.localScale, Vector3.zero, Easing.EaseInCubic))
                    .Build(), ct));
            }
            await Task.WhenAll(removeDiceAnimationTasks);

            await Task.Delay(200);

            // Create new dice at each of the position and roll sequentially
            Quaternion targetRot = Quaternion.Euler(0, match.ClientIndex * 180f, 0);

            for (int i = 0; i < resolution.ReplacedIndices.Count; i++)
            {
                var diceIndex = resolution.ReplacedIndices[i];

                sourcePlayerObjects.DestroyDiceAtIndex(diceIndex);

                var newDiceView = sourcePlayerObjects.SpawnDiceAtIndex(resolution.AddedDiceInstances[i], diceIndex, targetRot);
                await newDiceView.AnimateRoll(ct);

                await Task.Delay(200);
            }

            visualContext.ClientUI.UpdateDiceTotal(match.ClientIndex, sourceClientIndex);

            await Task.Delay(100);
        }
    }
}
