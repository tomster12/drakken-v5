using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Drakken.Client;
using Drakken.Client.World;
using Drakken.Client.World.Animation;
using Drakken.Common.Utility;
using Drakken.Domain.Tokens.Implementation.Common;
using Drakken.Domain.Tokens.Logic;
using Drakken.Utility;
using Unity.Netcode;
using UnityEngine;

namespace Drakken.Domain.Tokens.Implementation
{
    public class ForgeTokenResolution : TokenResolution
    {
        public int FirstIndex;
        public int SecondIndex;
        public DiceInstance CombinedDiceInstance;

        public override void NetworkSerialize<T>(BufferSerializer<T> serializer)
        {
            base.NetworkSerialize(serializer);
            serializer.SerializeValue(ref FirstIndex);
            serializer.SerializeValue(ref SecondIndex);
            serializer.SerializeValue(ref CombinedDiceInstance);
        }
    }

    public class ForgeTokenExecutor : TokenExecutor<EmptyTokenIntent, ForgeTokenResolution>
    {
        protected override ForgeTokenResolution Execute(GameState gameState, EmptyTokenIntent intent, int sourceClientIndex)
        {
            var client = gameState.Clients[sourceClientIndex];

            Assert.True(client.Dice.Count >= 2);

            // Randomly pick 2 dice to combine
            var indices = Enumerable.Range(0, client.Dice.Count)
                .OrderBy(_ => Random.value)
                .Take(2)
                .OrderBy(index => index)
                .ToList();

            int firstIndex = indices[0];
            int secondIndex = indices[1];

            // Combine into a single fresh dice with sides equal to the sum of the two values
            int combinedSides = client.Dice[firstIndex].Value + client.Dice[secondIndex].Value;
            var combinedDice = DiceInstance.Create(sides: combinedSides);
            combinedDice.Roll();

            return new ForgeTokenResolution
            {
                FirstIndex = firstIndex,
                SecondIndex = secondIndex,
                CombinedDiceInstance = combinedDice,
            };
        }

        protected override void Apply(GameState gameState, ForgeTokenResolution resolution, int sourceClientIndex)
        {
            var client = gameState.Clients[sourceClientIndex];

            Assert.True(resolution.FirstIndex >= 0 && resolution.FirstIndex < resolution.SecondIndex);
            Assert.True(resolution.SecondIndex < client.Dice.Count);

            client.Dice.RemoveAt(resolution.SecondIndex);
            client.Dice.RemoveAt(resolution.FirstIndex);
            client.Dice.Insert(resolution.FirstIndex, resolution.CombinedDiceInstance);
        }
    }

    public class ForgeTokenAnimator : TokenAnimator<ForgeTokenResolution>
    {
        private const float PullTogetherDuration = 0.35f;
        private const float ShiftDuration = 0.3f;

        protected override async Task Animate(
            ClientMatch match,
            TokenVisualContext visualContext,
            int sourceClientIndex,
            int tokenInstanceId,
            ForgeTokenResolution resolution,
            CancellationToken ct)
        {
            await Task.Delay(250);

            var sourcePlayerObjects = visualContext.SceneObjects.Player(sourceClientIndex);
            var oldDiceViews = sourcePlayerObjects.DiceViews;

            var firstDiceView = oldDiceViews[resolution.FirstIndex];
            var secondDiceView = oldDiceViews[resolution.SecondIndex];

            // Pull the two dice together to a shared midpoint
            Vector3 midpoint = (firstDiceView.transform.position + secondDiceView.transform.position) / 2f;

            await Task.WhenAll(
                firstDiceView.Animator.Play(AnimationSequenceBuilder
                    .Start()
                    .Next(AnimationTracks.Position(PullTogetherDuration, firstDiceView.transform, firstDiceView.transform.position, midpoint, Easing.EaseInOutQuad))
                    .Build(), ct),
                secondDiceView.Animator.Play(AnimationSequenceBuilder
                    .Start()
                    .Next(AnimationTracks.Position(PullTogetherDuration, secondDiceView.transform, secondDiceView.transform.position, midpoint, Easing.EaseInOutQuad))
                    .Build(), ct));

            await Task.Delay(100);

            // Merge them away, making room for the forged dice
            await Task.WhenAll(
                firstDiceView.AnimateShrinkAndDestroy(ct),
                secondDiceView.AnimateShrinkAndDestroy(ct));

            // Rebuild the dice row without the two merged dice, reserving a slot for the forged dice
            var newDiceViews = new DiceView[oldDiceViews.Length - 1];
            int writeIndex = 0;
            int forgedSlotIndex = -1;
            for (int i = 0; i < oldDiceViews.Length; i++)
            {
                if (i == resolution.FirstIndex)
                {
                    forgedSlotIndex = writeIndex;
                    writeIndex++;
                    continue;
                }
                if (i == resolution.SecondIndex) continue;

                newDiceViews[writeIndex] = oldDiceViews[i];
                writeIndex++;
            }
            sourcePlayerObjects.DiceViews = newDiceViews;

            // Slide the remaining dice into their new positions in the shortened row
            List<Task> shiftTasks = new();
            for (int i = 0; i < newDiceViews.Length; i++)
            {
                if (i == forgedSlotIndex) continue;

                var diceView = newDiceViews[i];
                Vector3 targetPosition = sourcePlayerObjects.GetDiceRowIndexPosition(i);
                shiftTasks.Add(diceView.Animator.Play(AnimationSequenceBuilder
                    .Start()
                    .Next(AnimationTracks.Position(ShiftDuration, diceView.transform, diceView.transform.position, targetPosition, Easing.EaseInOutQuad))
                    .Build(), ct));
            }
            await Task.WhenAll(shiftTasks);

            await Task.Delay(100);

            // Spawn the forged dice into its slot and roll it
            Quaternion targetRot = Quaternion.Euler(0, match.ClientIndex * 180f, 0);
            var forgedDiceView = sourcePlayerObjects.SpawnDiceAtIndex(resolution.CombinedDiceInstance, forgedSlotIndex, targetRot);
            await forgedDiceView.AnimateGrowThenRoll(ct, durationMultiplier: 0.7f);

            visualContext.ClientUI.UpdateDiceTotal(match.ClientIndex, sourceClientIndex);

            await Task.Delay(100);
        }
    }
}
