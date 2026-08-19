using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Drakken.Client;
using Drakken.Common.Utility;
using Drakken.Domain.Dice;
using Drakken.Domain.Dice.Implementation;
using Drakken.Domain.Dice.Logic;
using Drakken.Domain.Networking;
using Drakken.Domain.Tokens.Implementation.Common;
using Drakken.Domain.Tokens.Logic;
using Unity.Netcode;
using UnityEngine;

namespace Drakken.Domain.Tokens.Implementation
{
    public class MitosisTokenResolution : TokenResolution
    {
        public DiceInstance FinalTargetDice;
        public DiceSimulationTraces DiceTrace = new();

        public override IEnumerable<DiceSimulationTraces> Traces => new[] { DiceTrace };

        public override void NetworkSerialize<T>(BufferSerializer<T> serializer)
        {
            base.NetworkSerialize(serializer);

            bool hasFinalTargetDice = FinalTargetDice != null;
            serializer.SerializeValue(ref hasFinalTargetDice);
            if (hasFinalTargetDice)
            {
                if (serializer.IsReader) FinalTargetDice = new DiceInstance();
                serializer.SerializeValue(ref FinalTargetDice);
            }

            serializer.SerializeValue(ref DiceTrace);
        }
    }

    public class MitosisTokenLogic : TokenLogic<PickDiceTokenIntent, MitosisTokenResolution>
    {
        private const float TossUpwardMin = 5.5f;
        private const float TossUpwardMax = 7.5f;
        private const float TossSideways = 0.6f;
        private const float TossTorque = 20f;

        protected override MitosisTokenResolution Execute(
            GameState gameState,
            PickDiceTokenIntent intent,
            int sourceClientIndex,
            DiceSimulationWorld diceWorld)
        {
            Assert.True(intent.TargetDiceInstanceIds != null && intent.TargetDiceInstanceIds.Count == 1);

            var client = gameState.Clients[sourceClientIndex];

            int originalInstanceId = intent.TargetDiceInstanceIds[0];
            var targetDice = client.Dice.Find(d => d.InstanceId == originalInstanceId);
            Assert.NotNull(targetDice);

            var resolution = new MitosisTokenResolution();

            diceWorld.BeginSession(client.Dice);

            if (!TokenExecutionLogic.TryModify(targetDice, diceWorld))
            {
                resolution.DiceTrace = diceWorld.EndSession();
                return resolution;
            }

            MitosisFaceEffect.MarkRandomHalf(targetDice);

            Vector3 initialTossImpulse = new(
                Random.Range(-TossSideways, TossSideways),
                Random.Range(TossUpwardMin, TossUpwardMax),
                Random.Range(-TossSideways, TossSideways));

            diceWorld.WakeDice(targetDice.InstanceId, initialTossImpulse, Random.insideUnitSphere * TossTorque);

            diceWorld.Simulate(untilAllSettled: true);

            // If it landed on an unmarked face, MitosisFaceEffect.OnMiss (dispatched from within
            // Simulate) already cleared the marks - if it split instead, it won't be in LiveInstances
            resolution.FinalTargetDice = diceWorld.LiveInstances
                .FirstOrDefault(d => d.InstanceId == originalInstanceId);

            diceWorld.FreezeAllDice();
            resolution.DiceTrace = diceWorld.EndSession();

            return resolution;
        }

        protected override void Apply(GameState gameState, MitosisTokenResolution resolution, int sourceClientIndex)
        {
            if (resolution.FinalTargetDice == null) return;

            var client = gameState.Clients[sourceClientIndex];
            int index = client.Dice.FindIndex(d => d.InstanceId == resolution.FinalTargetDice.InstanceId);
            if (index >= 0) client.Dice[index] = resolution.FinalTargetDice;
        }

        protected override async Task Animate(
            ClientMatch match,
            TokenVisualContext visualContext,
            int sourceClientIndex,
            int tokenInstanceId,
            MitosisTokenResolution resolution,
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
