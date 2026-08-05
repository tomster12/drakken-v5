using System.Collections;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Drakken.Common.Utility;
using Drakken.Domain.Tokens.Logic;
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
        protected override ParasiteTokenResolution Execute(GameState gameState, ParasiteTokenIntent intent, int sourceClientIndex)
        {
            int opponentIndex = 1 - sourceClientIndex;
            var opponent = gameState.Clients[opponentIndex];

            // Validate the users choice
            var targetDice = opponent.Dice.Find(d => d.InstanceId == intent.TargetDiceId);
            Assert.NotNull(targetDice, $"ParasiteExecutor: no dice with id={intent.TargetDiceId} on opponent");

            return new ParasiteTokenResolution
            {
                TargetDiceId = intent.TargetDiceId
            };
        }

        protected override void Apply(GameState gameState, ParasiteTokenResolution resolution, int sourceClientIndex)
        {
            int opponentIndex = 1 - sourceClientIndex;
            var opponent = gameState.Clients[opponentIndex];

            var targetDice = opponent.Dice.Find(d => d.InstanceId == resolution.TargetDiceId);

            targetDice.Effects.Add(new DiceEffect
            {
                EffectId = "parasite",
                SourceClientIndex = sourceClientIndex,
            });
        }
    }

    public class ParasiteTokenAnimator : TokenAnimator<ParasiteTokenResolution>
    {
        protected override async Task Animate(
            GameState gameState,
            TokenVisualContext visualContext,
            int sourceClientIndex,
            int tokenInstanceId,
            ParasiteTokenResolution resolution,
            CancellationToken ct)
        {
            int opponentIndex = 1 - sourceClientIndex;
            // var targetView = context.GetDiceView(opponentIndex, resolution.TargetDiceId);

            // var projectile = context.SpawnProjectile("Parasite", sourcePos, targetPos);

            await Task.Delay(500);

            // if (targetView != null)
            // {
            //     Log.Info("ParasiteAnimator", $"Attaching parasite to dice id={resolution.TargetDiceId}");

            //     // targetView.AttachEffect("ParasiteEffect");
            // }

            await Task.Delay(200);
        }
    }
}