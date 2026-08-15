using System.Threading;
using System.Threading.Tasks;
using Drakken.Client;
using Drakken.Common.Utility;
using Drakken.Domain.Dice;
using Drakken.Domain.Static;
using Drakken.Domain.Tokens.Implementation.Common;
using Drakken.Domain.Tokens.Logic;
using Unity.Netcode;
using UnityEngine;

namespace Drakken.Domain.Tokens.Implementation
{
    public class GlassTokenResolution : TokenResolution
    {
        public DiceInstance AddedDiceInstance;
        public DiceSimulationTraces DiceTrace = new();

        public override void NetworkSerialize<T>(BufferSerializer<T> serializer)
        {
            base.NetworkSerialize(serializer);
            serializer.SerializeValue(ref AddedDiceInstance);
            serializer.SerializeValue(ref DiceTrace);
        }
    }

    public class GlassTokenExecutor : TokenExecutor<EmptyTokenIntent, GlassTokenResolution>
    {
        private const float TossUpwardMin = 5.5f;
        private const float TossUpwardMax = 7.5f;
        private const float TossSideways = 0.6f;
        private const float TossTorque = 20f;
        private const float SpawnHeight = 1f;

        protected override GlassTokenResolution Execute(
            GameState gameState,
            EmptyTokenIntent intent,
            int sourceClientIndex,
            DiceSimulationWorld diceWorld)
        {
            var client = gameState.Clients[sourceClientIndex];
            Assert.True(client.Dice.Count > 0);

            // Value is always 7, regardless of which face it physically lands on
            var glassDice = DiceInstance.Create(sides: GameConstants.StandardDiceSideCount);
            foreach (var face in glassDice.Faces) face.Value = 7;
            glassDice.DiceEffects.Add(DiceEffectIds.Glass);

            diceWorld.BeginSession();

            // Toss it in above one of the player's existing dice - there's no dedicated
            // "gain a new dice" spawn point, so this anchors it somewhere already in the tray
            var (anchorPosition, _) = diceWorld.GetDicePose(client.Dice[0].InstanceId);
            Vector3 spawnPosition = anchorPosition + Vector3.up * SpawnHeight;

            Vector3 tossImpulse = new(
                Random.Range(-TossSideways, TossSideways),
                Random.Range(TossUpwardMin, TossUpwardMax),
                Random.Range(-TossSideways, TossSideways));

            diceWorld.SpawnDice(glassDice, spawnPosition, Random.rotationUniform, tossImpulse, Random.insideUnitSphere * TossTorque);

            diceWorld.Simulate(untilAllSettled: true);

            diceWorld.FreezeAllDice();
            var trace = diceWorld.EndSession();

            return new GlassTokenResolution
            {
                AddedDiceInstance = glassDice,
                DiceTrace = trace,
            };
        }

        protected override void Apply(GameState gameState, GlassTokenResolution resolution, int sourceClientIndex)
        {
            gameState.Clients[sourceClientIndex].Dice.Add(resolution.AddedDiceInstance);
        }
    }

    public class GlassTokenAnimator : TokenAnimator<GlassTokenResolution>
    {
        protected override async Task Animate(
            ClientMatch match,
            TokenVisualContext visualContext,
            int sourceClientIndex,
            int tokenInstanceId,
            GlassTokenResolution resolution,
            CancellationToken ct)
        {
            await Task.Delay(250);

            // Give players a moment to read the token before it shrinks out of the way
            var shrinkTokenTask = visualContext.TokenView.AnimateShrink(1f, ct);

            var sourcePlayerObjects = visualContext.Client.SceneObjects.Player(sourceClientIndex);

            // Replaying keeps sourcePlayerObjects.DiceViews in sync with the newly spawned dice
            await sourcePlayerObjects.DiceSimReplayer.Play(
                visualContext.Client.Assets, visualContext.Client, resolution.DiceTrace, sourcePlayerObjects, ct);

            await shrinkTokenTask;

            visualContext.Client.UI.UpdateDiceTotal(match.ClientIndex, sourceClientIndex);

            await Task.Delay(100);
        }
    }
}
