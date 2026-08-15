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
        public DiceSimulationTraces DiceTrace = new();

        public override void NetworkSerialize<T>(BufferSerializer<T> serializer)
        {
            base.NetworkSerialize(serializer);
            serializer.SerializeList(ref BolsteredInstanceIds);
            serializer.SerializeList(ref NewFaceValues);
            serializer.SerializeValue(ref DiceTrace);
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

            // Pick the dice we want to bolster
            int count = Mathf.Min(BolsterCount, client.Dice.Count);
            var bolsteredIndices = Enumerable.Range(0, client.Dice.Count)
                .OrderBy(_ => Random.value)
                .Take(count)
                .ToList();

            var resolution = new BolsterTokenResolution();

            diceWorld.BeginSession();

            // Try apply bolster modification to each dice
            foreach (var index in bolsteredIndices)
            {
                var dice = client.Dice[index];

                if (!TokenExecutionLogic.TryModify(dice, diceWorld, resolution)) continue;

                resolution.BolsteredInstanceIds.Add(dice.InstanceId);
                resolution.NewFaceValues.Add(dice.Value + 1);
            }

            diceWorld.FreezeAllDice();
            resolution.DiceTrace = diceWorld.EndSession();

            return resolution;
        }

        protected override void Apply(GameState gameState, BolsterTokenResolution resolution, int sourceClientIndex)
        {
            Assert.True(resolution.BolsteredInstanceIds.Count == resolution.NewFaceValues.Count);

            var client = gameState.Clients[sourceClientIndex];

            // Apply the new face values to each bolstered dice
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
        private static readonly Color HighlightColor = Colors.Hex("#9cec92");
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
            var sourcePlayerObjects = visualContext.Client.SceneObjects.Player(sourceClientIndex);

            // Shrink the token out the way
            await visualContext.TokenView.AnimateShrinkAfter(0.5f, ct);

            // Replay simulation for any modification side effects
            await sourcePlayerObjects.DiceSimReplayer.Play(
                visualContext.Client.Assets, visualContext.Client, resolution.DiceTrace, sourcePlayerObjects, ct);

            // Play animation for bolstering each dice
            var bolsterTasks = new List<Task>();

            for (int i = 0; i < resolution.BolsteredInstanceIds.Count; i++)
            {
                int instanceId = resolution.BolsteredInstanceIds[i];
                Assert.True(sourcePlayerObjects.DiceViews.TryGetValue(instanceId, out var diceView));

                diceView.RefreshLabels();

                bolsterTasks.Add(diceView.FlashHighlight(HighlightColor, HighlightDuration, ct));
                bolsterTasks.Add(visualContext.Client.Vfx.SpawnFloatingLabel(
                    "+1",
                    HighlightColor,
                    diceView.transform.position + Vector3.up * LabelRiseHeight,
                    Quaternion.Euler(45f, visualContext.Client.Match.ClientIndex == 1 ? 180f : 0f, 0f),
                    ct));
            }

            await Task.WhenAll(bolsterTasks);

            visualContext.Client.UI.UpdateDiceTotal(match.ClientIndex, sourceClientIndex);
        }
    }
}
