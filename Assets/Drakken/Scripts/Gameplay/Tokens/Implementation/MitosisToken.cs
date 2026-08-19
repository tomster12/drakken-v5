using System.Collections.Generic;
using System.Linq;
using Drakken.Utility;
using Drakken.Presentation;
using Drakken.Gameplay.Dice.Implementation;
using Drakken.Gameplay.Simulation;
using Drakken.Gameplay.Tokens.Implementation.Common;
using Drakken.Gameplay.Tokens.Logic;
using Unity.Netcode;
using UnityEngine;
using Drakken.Domain;

namespace Drakken.Gameplay.Tokens.Implementation
{
    public class MitosisResolution : EventResolution
    {
        // Null when it split instead - the split is recorded as its own MitosisSplitResolution event
        public DiceInstance FinalTargetDice;

        public override void NetworkSerialize<T>(BufferSerializer<T> serializer)
        {
            bool hasFinalTargetDice = FinalTargetDice != null;
            serializer.SerializeValue(ref hasFinalTargetDice);
            if (hasFinalTargetDice)
            {
                if (serializer.IsReader) FinalTargetDice = new DiceInstance();
                serializer.SerializeValue(ref FinalTargetDice);
            }
        }
    }

    public class MitosisTokenLogic : TokenLogic<PickDiceTokenIntent, MitosisResolution>
    {
        private const float TossUpwardMin = 5.5f;
        private const float TossUpwardMax = 7.5f;
        private const float TossSideways = 0.6f;
        private const float TossTorque = 20f;

        public override int EventId => 3;

        protected override List<GameSimulationTrace> ExecuteToken(
            GameState gameState,
            PickDiceTokenIntent intent,
            int sourceClientIndex,
            GameSimulationWorld world)
        {
            Assert.True(intent.TargetDiceInstanceIds != null && intent.TargetDiceInstanceIds.Count == 1);

            var client = gameState.Clients[sourceClientIndex];

            int originalInstanceId = intent.TargetDiceInstanceIds[0];
            var targetDice = client.Dice.Find(d => d.InstanceId == originalInstanceId);
            Assert.NotNull(targetDice);

            world.BeginSession(client.Dice);

            if (!TokenExecutionLogic.TryModify(targetDice, world))
            {
                return new List<GameSimulationTrace> { world.EndSession() };
            }

            MitosisFaceEffect.MarkRandomHalf(targetDice);

            Vector3 initialTossImpulse = new(
                UnityEngine.Random.Range(-TossSideways, TossSideways),
                UnityEngine.Random.Range(TossUpwardMin, TossUpwardMax),
                UnityEngine.Random.Range(-TossSideways, TossSideways));

            world.WakeDice(targetDice.InstanceId, initialTossImpulse, UnityEngine.Random.insideUnitSphere * TossTorque);

            world.Simulate(untilAllSettled: true);

            // If it landed on an unmarked face, MitosisFaceEffect.OnMiss (dispatched from within
            // Simulate) already cleared the marks - if it split instead, it won't be in LiveInstances
            var finalTargetDice = world.LiveInstances
                .FirstOrDefault(d => d.InstanceId == originalInstanceId);

            world.FreezeAllDice();

            world.RecordEvent(EventId, EventKind.Token, originalInstanceId, targetDice.CurrentSide, new MitosisResolution
            {
                FinalTargetDice = finalTargetDice?.Clone(),
            });

            return new List<GameSimulationTrace> { world.EndSession() };
        }

        protected override void ApplyEvent(GameState gameState, MitosisResolution Resolution, int clientIndex, int sourceInstanceId)
        {
            if (Resolution.FinalTargetDice == null) return;

            var client = gameState.Clients[clientIndex];
            int index = client.Dice.FindIndex(d => d.InstanceId == Resolution.FinalTargetDice.InstanceId);
            if (index >= 0) client.Dice[index] = Resolution.FinalTargetDice;
        }
    }
}
