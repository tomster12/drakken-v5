using System;
using System.Threading;
using System.Threading.Tasks;
using Drakken.Client;
using Drakken.Domain.Dice;

namespace Drakken.Domain.Tokens.Logic
{
    public interface ITokenLogic
    {
        Type IntentType { get; }
        Type ResolutionType { get; }

        TokenResolution Execute(GameState gameState, TokenIntent intent, int sourceClientIndex, DiceSimulationWorld diceWorld);
        void Apply(GameState gameState, TokenResolution resolution, int sourceClientIndex);

        Task Animate(
            ClientMatch match,
            TokenVisualContext visualContext,
            int sourceClientIndex,
            int tokenInstanceId,
            TokenResolution resolution,
            CancellationToken ct);
    }

    public abstract class TokenLogic<TIntent, TResolution> : ITokenLogic
        where TIntent : TokenIntent
        where TResolution : TokenResolution
    {
        Type ITokenLogic.IntentType => typeof(TIntent);
        Type ITokenLogic.ResolutionType => typeof(TResolution);

        public TokenResolution Execute(GameState gameState, TokenIntent intent, int sourceClientIndex, DiceSimulationWorld diceWorld)
            => Execute(gameState, (TIntent)intent, sourceClientIndex, diceWorld);

        public void Apply(GameState gameState, TokenResolution resolution, int sourceClientIndex)
        {
            var typedResolution = (TResolution)resolution;

            foreach (var trace in typedResolution.Traces)
            {
                trace?.ApplyEffects(gameState, sourceClientIndex);
            }

            Apply(gameState, typedResolution, sourceClientIndex);
        }

        public Task Animate(
            ClientMatch match,
            TokenVisualContext visualContext,
            int sourceClientIndex,
            int tokenInstanceId,
            TokenResolution resolution,
            CancellationToken ct)
        {
            return Animate(match, visualContext, sourceClientIndex, tokenInstanceId, (TResolution)resolution, ct);
        }

        protected abstract TResolution Execute(GameState gameState, TIntent intent, int sourceClientIndex, DiceSimulationWorld diceWorld);

        protected abstract void Apply(GameState gameState, TResolution resolution, int sourceClientIndex);

        protected abstract Task Animate(
            ClientMatch match,
            TokenVisualContext visualContext,
            int sourceClientIndex,
            int tokenInstanceId,
            TResolution resolution,
            CancellationToken ct);
    }
}
