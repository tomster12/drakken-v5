using Drakken.Gameplay.Simulation;
using Drakken.Gameplay.Tokens.Implementation.Common;
using Drakken.Gameplay.Tokens.Logic;
using Unity.Netcode;
using UnityEngine;
using Drakken.Domain;

namespace Drakken.Gameplay.Tokens.Implementation
{
    public class BolsterOutcome : EventResolution
    {
        public DiceInstance AddedDiceInstance;

        public override void NetworkSerialize<T>(BufferSerializer<T> serializer)
        {
            if (serializer.IsReader) AddedDiceInstance = new DiceInstance();
            serializer.SerializeValue(ref AddedDiceInstance);
        }
    }

    public class BolsterTokenLogic : TokenLogic<EmptyTokenIntent, BolsterOutcome>
    {
        private const float TossUpwardMin = 4.5f;
        private const float TossUpwardMax = 6f;
        private const float TossSideways = 0.6f;
        private const float TossTorque = 20f;
        private const float SpawnHeight = 1f;

        public override int EffectId => TokenEventIds.Bolster;

        protected override TokenResolution Execute(
            GameState gameState,
            EmptyTokenIntent intent,
            int sourceClientIndex,
            GameSimulationWorld world)
        {
            var client = gameState.Clients[sourceClientIndex];

            // The bolster dice always shows 1 on every face for now, upgradeable later
            var bolsterDice = DiceInstance.Create(sides: 4);
            foreach (var face in bolsterDice.Faces) face.Value = 1;
            bolsterDice.DiceEffects.Add(DiceEffectIds.Bolster);

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

            world.RecordEvent(EffectId, EventKind.Token, bolsterDice.InstanceId, bolsterDice.CurrentSide, new BolsterOutcome
            {
                AddedDiceInstance = bolsterDice.Clone(),
            });

            return new TokenResolution(world.EndSession());
        }

        protected override void Apply(GameState gameState, BolsterOutcome outcome, int clientIndex, int sourceInstanceId)
        {
            gameState.Clients[clientIndex].Dice.Add(outcome.AddedDiceInstance);
        }
    }
}
