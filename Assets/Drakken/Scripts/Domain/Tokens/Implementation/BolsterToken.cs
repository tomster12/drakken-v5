using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Drakken.Client;
using Drakken.Domain.Dice;
using Drakken.Domain.Networking;
using Drakken.Domain.Tokens.Implementation.Common;
using Drakken.Domain.Tokens.Logic;
using Unity.Netcode;
using UnityEngine;

namespace Drakken.Domain.Tokens.Implementation
{
    public class BolsterTokenResolution : TokenResolution
    {
        public DiceInstance AddedDiceInstance;
        public DiceSimulationTraces DiceTrace = new();

        public override IEnumerable<DiceSimulationTraces> Traces => new[] { DiceTrace };

        public override void NetworkSerialize<T>(BufferSerializer<T> serializer)
        {
            base.NetworkSerialize(serializer);
            if (serializer.IsReader) AddedDiceInstance = new DiceInstance();
            serializer.SerializeValue(ref AddedDiceInstance);
            serializer.SerializeValue(ref DiceTrace);
        }
    }

    public class BolsterTokenExecutor : TokenExecutor<EmptyTokenIntent, BolsterTokenResolution>
    {
        private const float TossUpwardMin = 4.5f;
        private const float TossUpwardMax = 6f;
        private const float TossSideways = 0.6f;
        private const float TossTorque = 20f;
        private const float SpawnHeight = 1f;

        protected override BolsterTokenResolution Execute(
            GameState gameState,
            EmptyTokenIntent intent,
            int sourceClientIndex,
            DiceSimulationWorld diceWorld)
        {
            var client = gameState.Clients[sourceClientIndex];

            // The bolster dice always shows 1 on every face for now, upgradeable later
            var bolsterDice = DiceInstance.Create(sides: 4);
            foreach (var face in bolsterDice.Faces) face.Value = 1;
            bolsterDice.DiceEffects.Add(DiceEffectIds.Bolster);

            var resolution = new BolsterTokenResolution { AddedDiceInstance = bolsterDice };

            diceWorld.BeginSession(client.Dice);

            var trayPosition = diceWorld.Tray.position;
            Vector3 spawnPosition = trayPosition + Vector3.up * SpawnHeight;

            Vector3 tossImpulse = new(
                Random.Range(-TossSideways, TossSideways),
                Random.Range(TossUpwardMin, TossUpwardMax),
                Random.Range(-TossSideways, TossSideways));

            diceWorld.SpawnDice(bolsterDice, spawnPosition, Random.rotationUniform, tossImpulse, Random.insideUnitSphere * TossTorque);

            diceWorld.Simulate(untilAllSettled: true);

            diceWorld.FreezeAllDice();
            resolution.DiceTrace = diceWorld.EndSession();

            return resolution;
        }

        protected override void Apply(
            GameState gameState,
            BolsterTokenResolution resolution,
            int sourceClientIndex)
        {
            gameState.Clients[sourceClientIndex].Dice.Add(resolution.AddedDiceInstance);
        }
    }

    public class BolsterTokenAnimator : TokenAnimator<BolsterTokenResolution>
    {
        protected override async Task Animate(
            ClientMatch match,
            TokenVisualContext visualContext,
            int sourceClientIndex,
            int tokenInstanceId,
            BolsterTokenResolution resolution,
            CancellationToken ct)
        {
            var sourcePlayerObjects = visualContext.Client.SceneObjects.Player(sourceClientIndex);

            await visualContext.TokenView.AnimateShrinkAfter(0.5f, ct);

            await sourcePlayerObjects.DiceSimReplayer.Play(
                resolution.DiceTrace, ct, sourcePlayerObjects);

            visualContext.Client.UI.UpdateDiceTotal(match.ClientIndex, sourceClientIndex);
        }
    }
}
