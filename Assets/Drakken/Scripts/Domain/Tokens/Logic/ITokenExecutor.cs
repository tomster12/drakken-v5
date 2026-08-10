using Drakken.Domain.Dice;

namespace Drakken.Domain.Tokens.Logic
{
    public interface ITokenExecutor
    {
        TokenResolution Execute(GameState gameState, TokenIntent intent, int sourceClientIndex, DiceSimulationWorld diceWorld);
        void Apply(GameState gameState, TokenResolution resolution, int sourceClientIndex);
    }

    public abstract class TokenExecutor<TIntent, TResolution> : ITokenExecutor
        where TIntent : TokenIntent
        where TResolution : TokenResolution
    {
        public TokenResolution Execute(GameState gameState, TokenIntent intent, int sourceClientIndex, DiceSimulationWorld diceWorld)
            => Execute(gameState, (TIntent)intent, sourceClientIndex, diceWorld);

        public void Apply(GameState gameState, TokenResolution resolution, int sourceClientIndex)
            => Apply(gameState, (TResolution)resolution, sourceClientIndex);

        protected abstract TResolution Execute(GameState gameState, TIntent intent, int sourceClientIndex, DiceSimulationWorld diceWorld);

        protected abstract void Apply(GameState gameState, TResolution resolution, int sourceClientIndex);
    }
}
