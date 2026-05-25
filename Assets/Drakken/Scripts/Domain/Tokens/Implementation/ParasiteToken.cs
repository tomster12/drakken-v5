using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using Drakken.Common.Utility;
using Unity.Netcode;
using UnityEngine;

namespace Drakken.Domain.Tokens.Implementation
{
    public class ParasiteTokenIntent : TokenIntent
    {
        public int TargetDiceId;

        public override void NetworkSerialize<T>(BufferSerializer<T> serializer)
        {
            serializer.SerializeValue(ref TargetDiceId);
        }
    }

    public class ParasiteTokenResolution : TokenResolution
    {
        public int TargetDiceId;

        public override void NetworkSerialize<T>(BufferSerializer<T> serializer)
        {
            serializer.SerializeValue(ref TargetDiceId);
        }
    }

    public class ParasiteTokenExecutor : TokenExecutor<ParasiteTokenIntent, ParasiteTokenResolution>
    {
        protected override ParasiteTokenResolution Execute(GameState state, ParasiteTokenIntent intent, int sourceClientIndex)
        {
            int opponentIndex = 1 - sourceClientIndex;
            var opponent = state.Clients[opponentIndex];

            // Validate the users choice
            var targetDice = opponent.Dice.Find(d => d.Id == intent.TargetDiceId);
            Assert.NotNull(targetDice, $"ParasiteExecutor: no dice with id={intent.TargetDiceId} on opponent");

            // Apply the effect to server-side state
            targetDice.Effects.Add(new DiceEffect
            {
                EffectId = "parasite",
                SourceClientIndex = sourceClientIndex,
            });

            return new ParasiteTokenResolution
            {
                TargetDiceId = intent.TargetDiceId
            };
        }
    }

    public class ParasiteTokenAnimator : TokenAnimator<ParasiteTokenResolution>
    {
        protected override async Task Animate(ParasiteTokenResolution resolution, TokenVisualContext context, int sourceClientIndex)
        {
            int opponentIndex = 1 - sourceClientIndex;
            var targetView = context.GetDiceView(opponentIndex, resolution.TargetDiceId);

            Log.Info("ParasiteAnimator", "Launching parasite projectile");

            // var projectile = context.SpawnProjectile("Parasite", sourcePos, targetPos);

            await Task.Delay(500);

            if (targetView != null)
            {
                Log.Info("ParasiteAnimator", $"Attaching parasite to dice id={resolution.TargetDiceId}");

                // targetView.AttachEffect("ParasiteEffect");
            }

            await Task.Delay(200);
        }
    }
}