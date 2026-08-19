using System.Collections.Generic;
using Drakken.Gameplay.Simulation;
using Drakken.Gameplay.Tokens.Implementation.Common;
using Drakken.Gameplay.Tokens.Logic;
using UnityEngine;
using Drakken.Domain;

namespace Drakken.Gameplay.Tokens.Implementation
{
    public class GlassTokenLogic : TokenLogic<EmptyTokenIntent, EmptyEventResolution>
    {
        private const float TossUpwardMin = 4.5f;
        private const float TossUpwardMax = 6f;
        private const float TossSideways = 0.6f;
        private const float TossTorque = 20f;
        private const float SpawnHeight = 1f;

        public override int EventId => 5;

        protected override List<GameSimulationTrace> ExecuteToken(
            GameState gameState,
            EmptyTokenIntent intent,
            int sourceClientIndex,
            GameSimulationWorld world)
        {
            var client = gameState.Clients[sourceClientIndex];

            // Create a new glass dice
            var glassDice = DiceInstance.Create(sides: 6);
            foreach (var face in glassDice.Faces) face.Value = 7;
            glassDice.DiceEffects.Add(DiceEffectIds.Glass);

            world.BeginSession(client.Dice);

            // Spawn and toss the dice into the world
            var trayPosition = world.Tray.position;
            Vector3 spawnPosition = trayPosition + Vector3.up * SpawnHeight;

            Vector3 tossImpulse = new(
                Random.Range(-TossSideways, TossSideways),
                Random.Range(TossUpwardMin, TossUpwardMax),
                Random.Range(-TossSideways, TossSideways));

            world.SpawnDice(glassDice, spawnPosition, Random.rotationUniform, tossImpulse, Random.insideUnitSphere * TossTorque);

            // Add the dice to GameState before simulating, since anything triggered while
            // it settles must see it already present
            world.RecordEvent(CommonEventIds.AddDice, EventKind.Common, glassDice.InstanceId, glassDice.CurrentSide, new AddDiceResolution
            {
                AddedDiceInstance = glassDice.Clone(),
            });

            world.Simulate(untilAllSettled: true);

            world.FreezeAllDice();

            world.RecordEvent(EventId, EventKind.Token, glassDice.InstanceId, glassDice.CurrentSide, new EmptyEventResolution());

            return new List<GameSimulationTrace> { world.EndSession() };
        }

        protected override void ApplyEvent(GameState gameState, EmptyEventResolution resolution, int clientIndex, int sourceInstanceId) { }
    }
}
