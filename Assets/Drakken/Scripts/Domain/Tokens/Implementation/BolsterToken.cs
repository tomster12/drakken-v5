using System.Collections.Generic;
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
    public class BolsterTokenResolution : TokenResolution
    {
        public List<int> BolsteredInstanceIds = new();
        public List<int> NewFaceValues = new();

        public override void NetworkSerialize<T>(BufferSerializer<T> serializer)
        {
            base.NetworkSerialize(serializer);
            serializer.SerializeList(ref BolsteredInstanceIds);
            serializer.SerializeList(ref NewFaceValues);
        }
    }

    public class BolsterTokenExecutor : TokenExecutor<EmptyTokenIntent, BolsterTokenResolution>
    {
        private const int BolsterCount = 3;

        protected override BolsterTokenResolution Execute(
            GameState gameState,
            EmptyTokenIntent intent,
            int sourceClientIndex,
            DiceSimulationWorld diceWorld)
        {
            var client = gameState.Clients[sourceClientIndex];

            int count = Mathf.Min(BolsterCount, client.Dice.Count);
            var bolsteredIndices = Enumerable.Range(0, client.Dice.Count)
                .OrderBy(_ => Random.value)
                .Take(count)
                .ToList();

            var bolsteredInstanceIds = new List<int>();
            var newFaceValues = new List<int>();

            foreach (var index in bolsteredIndices)
            {
                var dice = client.Dice[index];

                bolsteredInstanceIds.Add(dice.InstanceId);
                newFaceValues.Add(dice.Value + 1);
            }

            return new BolsterTokenResolution
            {
                BolsteredInstanceIds = bolsteredInstanceIds,
                NewFaceValues = newFaceValues,
            };
        }

        protected override void Apply(GameState gameState, BolsterTokenResolution resolution, int sourceClientIndex)
        {
            Assert.True(resolution.BolsteredInstanceIds.Count == resolution.NewFaceValues.Count);

            var client = gameState.Clients[sourceClientIndex];

            for (int i = 0; i < resolution.BolsteredInstanceIds.Count; i++)
            {
                var dice = client.Dice.Find(d => d.InstanceId == resolution.BolsteredInstanceIds[i]);
                Assert.True(dice != null);

                dice.Faces[dice.CurrentSide].Value = resolution.NewFaceValues[i];
            }
        }
    }

    public class BolsterTokenAnimator : TokenAnimator<BolsterTokenResolution>
    {
        private static readonly Color HighlightColor = new(1f, 0.82f, 0.3f);
        private const float HighlightDuration = 0.9f;
        private const float LabelRiseHeight = 0.6f;

        protected override async Task Animate(
            ClientMatch match,
            TokenVisualContext visualContext,
            int sourceClientIndex,
            int tokenInstanceId,
            BolsterTokenResolution resolution,
            CancellationToken ct)
        {
            await Task.Delay(250);

            var shrinkTokenTask = visualContext.TokenView.AnimateShrink(0.6f, ct);

            var sourcePlayerObjects = visualContext.Client.SceneObjects.Player(sourceClientIndex);

            var bolsterTasks = new List<Task>();

            for (int i = 0; i < resolution.BolsteredInstanceIds.Count; i++)
            {
                int instanceId = resolution.BolsteredInstanceIds[i];

                if (!sourcePlayerObjects.DiceViews.TryGetValue(instanceId, out var diceView)) continue;

                // Bump the printed value shown on the dice's current face and flag it as
                // picked, without moving the dice at all.
                diceView.RefreshFaceLabels();

                bolsterTasks.Add(diceView.FlashHighlight(HighlightColor, HighlightDuration, ct));
                bolsterTasks.Add(visualContext.Client.Vfx.SpawnFloatingLabel(
                    "+1",
                    HighlightColor,
                    diceView.transform.position + Vector3.up * LabelRiseHeight,
                    Quaternion.Euler(90f, 0f, 0f),
                    ct));
            }

            await Task.WhenAll(bolsterTasks);
            await shrinkTokenTask;

            visualContext.Client.UI.UpdateDiceTotal(match.ClientIndex, sourceClientIndex);
        }
    }
}
