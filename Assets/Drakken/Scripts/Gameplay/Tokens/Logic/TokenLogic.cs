using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Drakken.Client;
using Drakken.Domain;
using Drakken.Gameplay.Simulation;

namespace Drakken.Gameplay.Tokens.Logic
{
    public interface ITokenLogic : IEventLogic
    {
        Type IntentType { get; }

        List<GameSimulationTrace> ExecuteToken(GameState gameState, TokenIntent intent, int sourceClientIndex, GameSimulationWorld world);

        Task AnimateToken(
            ClientMatch match,
            TokenVisualContext visualContext,
            int sourceClientIndex,
            int tokenInstanceId,
            List<GameSimulationTrace> traces,
            CancellationToken ct);
    }

    public abstract class TokenLogic<TIntent, TEventResolution> : ITokenLogic
        where TIntent : TokenIntent
        where TEventResolution : EventResolution
    {
        public abstract int EventId { get; }

        Type ITokenLogic.IntentType => typeof(TIntent);
        Type IEventLogic.ResolutionType => typeof(TEventResolution);

        // --------------------------------------------------- Token Lifetime

        public List<GameSimulationTrace> ExecuteToken(GameState gameState, TokenIntent intent, int sourceClientIndex, GameSimulationWorld world)
            => ExecuteToken(gameState, (TIntent)intent, sourceClientIndex, world);

        protected abstract List<GameSimulationTrace> ExecuteToken(GameState gameState, TIntent intent, int sourceClientIndex, GameSimulationWorld world);

        public virtual async Task AnimateToken(
            ClientMatch match,
            TokenVisualContext visualContext,
            int sourceClientIndex,
            int tokenInstanceId,
            List<GameSimulationTrace> traces,
            CancellationToken ct)
        {
            var sourcePlayerObjects = visualContext.Client.SceneObjects.Player(sourceClientIndex);

            await visualContext.TokenView.AnimateShrinkAfter(0.5f, ct);

            foreach (var trace in traces)
            {
                await sourcePlayerObjects.SimReplayer.Play(trace, ct, sourcePlayerObjects);
            }

            visualContext.Client.UI.UpdateDiceTotal(match.ClientIndex, sourceClientIndex);
        }

        // --------------------------------------------------- Event Lifetime

        void IEventLogic.ApplyEvent(GameState gameState, EventResolution resolution, int clientIndex, int sourceInstanceId)
            => ApplyEvent(gameState, (TEventResolution)resolution, clientIndex, sourceInstanceId);

        protected abstract void ApplyEvent(GameState gameState, TEventResolution resolution, int clientIndex, int sourceInstanceId);

        Task IEventLogic.AnimateEvent(EventAnimateContext ctx, EventResolution resolution, int sourceInstanceId, CancellationToken ct)
            => AnimateEvent(ctx, (TEventResolution)resolution, sourceInstanceId, ct);

        protected virtual Task AnimateEvent(EventAnimateContext ctx, TEventResolution resolution, int sourceInstanceId, CancellationToken ct)
            => Task.CompletedTask;
    }
}
