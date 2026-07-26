namespace Drakken.Domain.Tokens
{
    public interface ITokenExecutor
    {
        TokenResolution Execute(GameState gameState, TokenIntent intent, int sourceClientIndex);
    }

    public abstract class TokenExecutor<TIntent, TResolution> : ITokenExecutor
        where TIntent : TokenIntent
        where TResolution : TokenResolution
    {
        public TokenResolution Execute(GameState gameState, TokenIntent intent, int sourceClientIndex)
            => Execute(gameState, (TIntent)intent, sourceClientIndex);

        protected abstract TResolution Execute(GameState gameState, TIntent intent, int sourceClientIndex);
    }
}
