using System;
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

        TokenResolution Execute(GameState gameState, TokenIntent intent, int sourceClientIndex, GameSimulationWorld world);

        void Apply(GameState gameState, TokenResolution resolution, int sourceClientIndex);

        Task Animate(
            ClientMatch match,
            TokenVisualContext visualContext,
            int sourceClientIndex,
            int tokenInstanceId,
            TokenResolution resolution,
            CancellationToken ct);
    }

    public abstract class TokenLogic<TIntent, TOutcome> : ITokenLogic
        where TIntent : TokenIntent
        where TOutcome : EventResolution
    {
        public abstract int EffectId { get; }

        Type ITokenLogic.IntentType => typeof(TIntent);
        Type IEventLogic.ResolutionType => typeof(TOutcome);

        public TokenResolution Execute(GameState gameState, TokenIntent intent, int sourceClientIndex, GameSimulationWorld world)
            => Execute(gameState, (TIntent)intent, sourceClientIndex, world);

        public void Apply(GameState gameState, TokenResolution resolution, int sourceClientIndex)
        {
            foreach (var trace in resolution.Traces)
            {
                trace?.ApplyEffects(gameState, sourceClientIndex);
            }
        }

        public virtual async Task Animate(
            ClientMatch match,
            TokenVisualContext visualContext,
            int sourceClientIndex,
            int tokenInstanceId,
            TokenResolution resolution,
            CancellationToken ct)
        {
            var sourcePlayerObjects = visualContext.Client.SceneObjects.Player(sourceClientIndex);

            await visualContext.TokenView.AnimateShrinkAfter(0.5f, ct);

            foreach (var trace in resolution.Traces)
            {
                await sourcePlayerObjects.SimReplayer.Play(trace, ct, sourcePlayerObjects);
            }

            visualContext.Client.UI.UpdateDiceTotal(match.ClientIndex, sourceClientIndex);
        }

        protected abstract TokenResolution Execute(GameState gameState, TIntent intent, int sourceClientIndex, GameSimulationWorld world);

        protected abstract void Apply(GameState gameState, TOutcome outcome, int clientIndex, int sourceInstanceId);

        protected virtual Task Animate(EventAnimateContext ctx, TOutcome outcome, int sourceInstanceId, CancellationToken ct)
            => Task.CompletedTask;

        void IEventLogic.Apply(GameState gameState, EventResolution resolution, int clientIndex, int sourceInstanceId)
            => Apply(gameState, (TOutcome)resolution, clientIndex, sourceInstanceId);

        Task IEventLogic.Animate(EventAnimateContext ctx, EventResolution resolution, int sourceInstanceId, CancellationToken ct)
            => Animate(ctx, (TOutcome)resolution, sourceInstanceId, ct);
    }
}
