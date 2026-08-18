using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Drakken.Client;
using Drakken.Domain.Dice;
using Drakken.Domain.Tokens.Implementation.Common;
using Drakken.Domain.Tokens.Logic;
using Unity.Netcode;

namespace Drakken.Domain.Tokens.Implementation
{
    public class BlankTokenResolution : TokenResolution
    {
        public override IEnumerable<DiceSimulationTraces> Traces => Array.Empty<DiceSimulationTraces>();

        public override void NetworkSerialize<T>(BufferSerializer<T> serializer)
        {
            // TODO
        }
    }

    public class BlankTokenExecutor : TokenExecutor<EmptyTokenIntent, BlankTokenResolution>
    {
        protected override BlankTokenResolution Execute(GameState gameState, EmptyTokenIntent intent, int sourceClientIndex, DiceSimulationWorld diceWorld)
        {
            // TODO

            return new BlankTokenResolution { };
        }

        protected override void Apply(GameState gameState, BlankTokenResolution resolution, int sourceClientIndex)
        {
            // TODO
        }
    }

    public class BlankTokenAnimator : TokenAnimator<BlankTokenResolution>
    {
        protected override async Task Animate(
            ClientMatch match,
            TokenVisualContext visualContext,
            int sourceClientIndex,
            int tokenInstanceId,
            BlankTokenResolution resolution,
            CancellationToken ct)
        {
            // TODO
        }
    }
}
