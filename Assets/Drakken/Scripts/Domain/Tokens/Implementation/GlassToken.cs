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
        private const float TossUpwardMin = 4.5f;
        private const float TossUpwardMax = 6f;
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

            // Create a new glass dice
            var glassDice = DiceInstance.Create(sides: 6);
            foreach (var face in glassDice.Faces) face.Value = 7;
            glassDice.DiceEffects.Add(DiceEffectIds.Glass);

            diceWorld.BeginSession();

            // Spawn and toss the dice into the world
            var trayPosition = diceWorld.Tray.position;
            Vector3 spawnPosition = trayPosition + Vector3.up * SpawnHeight;

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
            // Directly add new dice into the world
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
            var sourcePlayerObjects = visualContext.Client.SceneObjects.Player(sourceClientIndex);

            // Shrink the token out the way
            await visualContext.TokenView.AnimateShrinkAfter(0.5f, ct);

            // Replay the full simulation
            await sourcePlayerObjects.DiceSimReplayer.Play(
                visualContext.Client.Assets, visualContext.Client, resolution.DiceTrace, sourcePlayerObjects, ct);

            visualContext.Client.UI.UpdateDiceTotal(match.ClientIndex, sourceClientIndex);
        }
    }
}
