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
            var client = gameState.Clients[sourceClientIndex];

            // Handle generic dice side effects - these can come from any settle-triggered dice/face
            // effect (e.g. Bolster, Glass), no matter which token's simulation caused them
            if (typedResolution.SideEffectsDestroyedDiceInstanceIds.Count > 0)
            {
                client.Dice.RemoveAll(d => typedResolution.SideEffectsDestroyedDiceInstanceIds.Contains(d.InstanceId));
            }

            foreach (var change in typedResolution.SideEffectsValueChanges)
            {
                var dice = client.Dice.Find(d => d.InstanceId == change.InstanceId);
                if (dice != null) dice.Faces[dice.CurrentSide].Value = change.NewValue;
            }

            Apply(gameState, typedResolution, sourceClientIndex);
        }

        protected abstract TResolution Execute(GameState gameState, TIntent intent, int sourceClientIndex, DiceSimulationWorld diceWorld);

        protected abstract void Apply(GameState gameState, TResolution resolution, int sourceClientIndex);
    }
}
