using System.Collections.Generic;
using Drakken.Gameplay.Simulation;
using Drakken.Gameplay.Tokens.Implementation.Common;
using Drakken.Gameplay.Tokens.Logic;
using UnityEngine;
using Drakken.Domain;

namespace Drakken.Gameplay.Tokens.Implementation
{
    public class BolsterTokenLogic : TokenLogic<EmptyTokenIntent>
    {
        private const float TossUpwardMin = 4.5f;
        private const float TossUpwardMax = 6f;
        private const float TossSideways = 0.6f;
        private const float TossTorque = 20f;
        private const float SpawnHeight = 1f;

        protected override (List<GameSimulationTrace> Traces, EmptyTokenSummary Summary) ExecuteToken(
            GameState gameState,
            EmptyTokenIntent intent,
            int sourceClientIndex,
            GameSimulationWorld world)
        {
            var client = gameState.Clients[sourceClientIndex];

            // Dice with "Bolster" dice effect and 1 on every face
            var bolsterDice = DiceInstance.Create(sides: 4);
            foreach (var face in bolsterDice.Faces) face.Value = 1;
            bolsterDice.DiceEffects.Add(DiceEffectIds.Bolster);

            // Spawn dice into world with an impulse from the centre
            world.BeginSession(client.Dice);

            var trayPosition = world.Tray.position;
            Vector3 spawnPosition = trayPosition + Vector3.up * SpawnHeight;

            Vector3 tossImpulse = new(
                Random.Range(-TossSideways, TossSideways),
                Random.Range(TossUpwardMin, TossUpwardMax),
                Random.Range(-TossSideways, TossSideways));

            world.SpawnDice(bolsterDice, spawnPosition, Random.rotationUniform, tossImpulse, Random.insideUnitSphere * TossTorque);

            world.Simulate(untilAllSettled: true);

            world.FreezeAllDice();

            return (new List<GameSimulationTrace> { world.EndSession() }, new EmptyTokenSummary());
        }
    }
}
