namespace Drakken.Domain.Tokens
{
    public interface ITokenExecutor
    {
        TokenResolution Execute(GameState state, TokenIntent intent, int sourceClientIndex);
    }

    public abstract class TokenExecutor<TIntent, TResolution> : ITokenExecutor
        where TIntent : TokenIntent
        where TResolution : TokenResolution
    {
        public TokenResolution Execute(GameState state, TokenIntent intent, int sourceClientIndex)
            => Execute(state, (TIntent)intent, sourceClientIndex);

        protected abstract TResolution Execute(GameState state, TIntent intent, int sourceClientIndex);
    }
}
