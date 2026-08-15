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
        {
            var typedResolution = (TResolution)resolution;

            // Handle dice resolution side effects
            // Currently just ensure we remove any dice that were removed (e.g. Glass)
            if (typedResolution.SideEffectsDestroyedDiceInstanceIds.Count > 0)
            {
                gameState.Clients[sourceClientIndex].Dice.RemoveAll(
                    d => typedResolution.SideEffectsDestroyedDiceInstanceIds.Contains(d.InstanceId));
            }

            Apply(gameState, typedResolution, sourceClientIndex);
        }

        protected abstract TResolution Execute(GameState gameState, TIntent intent, int sourceClientIndex, DiceSimulationWorld diceWorld);

        protected abstract void Apply(GameState gameState, TResolution resolution, int sourceClientIndex);
    }
}
