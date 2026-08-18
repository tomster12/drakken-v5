using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Drakken.Client;
using Drakken.Common.Utility;
using Drakken.Domain.Dice;
using Drakken.Domain.Dice.Effects;
using Drakken.Domain.Networking;
using Drakken.Domain.Tokens.Implementation.Common;
using Drakken.Domain.Tokens.Logic;
using Unity.Netcode;
using UnityEngine;

namespace Drakken.Domain.Tokens.Implementation
{
    public class MitosisTokenResolution : TokenResolution
    {
        public int OriginalInstanceId;
        public List<DiceInstance> FinalDiceInstances = new();
        public DiceSimulationTraces DiceTrace = new();

        public override void NetworkSerialize<T>(BufferSerializer<T> serializer)
        {
            base.NetworkSerialize(serializer);
            serializer.SerializeValue(ref OriginalInstanceId);
            serializer.SerializeList(ref FinalDiceInstances);
            serializer.SerializeValue(ref DiceTrace);
        }
    }

    public class MitosisTokenExecutor : TokenExecutor<PickDiceTokenIntent, MitosisTokenResolution>
    {
        // Initial roll toss
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

            // Find the selected dice
            int originalInstanceId = intent.TargetDiceInstanceIds[0];
            var targetDice = client.Dice.Find(d => d.InstanceId == originalInstanceId);
            Assert.NotNull(targetDice);

            var resolution = new MitosisTokenResolution { OriginalInstanceId = originalInstanceId };

            diceWorld.BeginSession(resolution, client.Dice);

            // If the targeted dice can't be modified we cancel early
            if (!TokenExecutionLogic.TryModify(targetDice, diceWorld, resolution))
            {
                resolution.DiceTrace = diceWorld.EndSession();
                return resolution;
            }

            resolution.FinalDiceInstances.Add(targetDice);
            MitosisFaceEffectLogic.MarkRandomHalf(targetDice);

            // Wake and toss the selected dice
            Vector3 initialTossImpulse = new(
                Random.Range(-TossSideways, TossSideways),
                Random.Range(TossUpwardMin, TossUpwardMax),
                Random.Range(-TossSideways, TossSideways));

            diceWorld.WakeDice(targetDice.InstanceId, initialTossImpulse, Random.insideUnitSphere * TossTorque);

            // Every settle on a marked face lifts and splits via MitosisFaceEffectLogic
            diceWorld.Simulate(untilAllSettled: true);

            diceWorld.FreezeAllDice();
            resolution.DiceTrace = diceWorld.EndSession();

            // Remove all marks at the end of this tokens resolution
            foreach (var dice in resolution.FinalDiceInstances)
            {
                foreach (var face in dice.Faces)
                {
                    face.FaceEffects = face.FaceEffects
                        .Where(e => e != FaceEffectIds.MitosisMark)
                        .ToList();
                }
            }

            return resolution;
        }

        protected override void Apply(GameState gameState, MitosisTokenResolution resolution, int sourceClientIndex)
        {
            // If the modification failed, then exit early
            if (resolution.FinalDiceInstances.Count == 0) return;

            var client = gameState.Clients[sourceClientIndex];

            int index = client.Dice.FindIndex(d => d.InstanceId == resolution.OriginalInstanceId);
            Assert.True(index >= 0);

            client.Dice.RemoveAt(index);
            client.Dice.InsertRange(index, resolution.FinalDiceInstances);
        }
    }

    public class MitosisTokenAnimator : TokenAnimator<MitosisTokenResolution>
    {
        protected override async Task Animate(
            ClientMatch match,
            TokenVisualContext visualContext,
            int sourceClientIndex,
            int tokenInstanceId,
            MitosisTokenResolution resolution,
            CancellationToken ct)
        {
            var sourcePlayerObjects = visualContext.Client.SceneObjects.Player(sourceClientIndex);

            // Shrink the token out the way
            await visualContext.TokenView.AnimateShrinkAfter(0.5f, ct);

            // Replay the full simulation
            await sourcePlayerObjects.DiceSimReplayer.Play(
                resolution.DiceTrace, ct, sourcePlayerObjects, resolution.SideEffectsValueChanges);

            visualContext.Client.UI.UpdateDiceTotal(match.ClientIndex, sourceClientIndex);
        }
    }
}
