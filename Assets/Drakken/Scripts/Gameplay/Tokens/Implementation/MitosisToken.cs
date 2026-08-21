using System.Collections.Generic;
using Drakken.Utility;
using Drakken.Gameplay.Dice.Implementation;
using Drakken.Gameplay.Simulation;
using Drakken.Gameplay.Tokens.Implementation.Common;
using Drakken.Gameplay.Tokens.Logic;
using Drakken.Domain;
using UnityEngine;

namespace Drakken.Gameplay.Tokens.Implementation
{
    public class MitosisTokenLogic : TokenLogic<PickDiceTokenIntent>
    {
        private const float TossUpwardMin = 5.5f;
        private const float TossUpwardMax = 7.5f;
        private const float TossSideways = 0.6f;
        private const float TossTorque = 20f;

        protected override (List<GameSimulationTrace> Traces, EmptyTokenSummary Summary) ExecuteToken(
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
                return (new List<GameSimulationTrace> { world.EndSession() }, new EmptyTokenSummary());
            }

            MitosisFaceEffect.MarkRandomHalf(world, targetDice);

            Vector3 initialTossImpulse = new(
                UnityEngine.Random.Range(-TossSideways, TossSideways),
                UnityEngine.Random.Range(TossUpwardMin, TossUpwardMax),
                UnityEngine.Random.Range(-TossSideways, TossSideways));

            world.WakeDice(targetDice.InstanceId, initialTossImpulse, UnityEngine.Random.insideUnitSphere * TossTorque);

            world.Simulate(untilAllSettled: true);

            world.FreezeAllDice();

            // No event needed here - marking, missing, and splitting each already record their own
            // correctly-timed event (see MitosisFaceEffect), and CurrentSide is kept in sync by the
            // dice's own DiceSettleEvent as it lands, whether it split or not
            return (new List<GameSimulationTrace> { world.EndSession() }, new EmptyTokenSummary());
        }
    }
}
