using Drakken.Gameplay.Simulation;
using Drakken.Gameplay.Tokens.Implementation.Common;
using Drakken.Gameplay.Tokens.Logic;
using Unity.Netcode;
using UnityEngine;
using Drakken.Domain;

namespace Drakken.Gameplay.Tokens.Implementation
{
    public class GlassOutcome : EventResolution
    {
        public DiceInstance AddedDiceInstance;

        public override void NetworkSerialize<T>(BufferSerializer<T> serializer)
        {
            if (serializer.IsReader) AddedDiceInstance = new DiceInstance();
            serializer.SerializeValue(ref AddedDiceInstance);
        }
    }

    public class GlassTokenLogic : TokenLogic<EmptyTokenIntent, GlassOutcome>
    {
        private const float TossUpwardMin = 4.5f;
        private const float TossUpwardMax = 6f;
        private const float TossSideways = 0.6f;
        private const float TossTorque = 20f;
        private const float SpawnHeight = 1f;

        public override int EffectId => TokenEventIds.Glass;

        protected override TokenResolution Execute(
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

            world.Simulate(untilAllSettled: true);

            world.FreezeAllDice();

            world.RecordEvent(EffectId, EventKind.Token, glassDice.InstanceId, glassDice.CurrentSide, new GlassOutcome
            {
                AddedDiceInstance = glassDice.Clone(),
            });

            return new TokenResolution(world.EndSession());
        }

        protected override void Apply(GameState gameState, GlassOutcome outcome, int clientIndex, int sourceInstanceId)
        {
            gameState.Clients[clientIndex].Dice.Add(outcome.AddedDiceInstance);
        }
    }
}
