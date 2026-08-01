using System.Threading.Tasks;

namespace Drakken.Domain.Tokens
{
    public interface ITokenAnimator
    {
        Task Animate(GameState gameState, TokenResolution resolution, TokenVisualContext context, int sourceClientIndex);
    }

    public abstract class TokenAnimator<TResolution> : ITokenAnimator
        where TResolution : TokenResolution
    {
        public Task Animate(GameState gameState, TokenResolution resolution, TokenVisualContext context, int sourceClientIndex)
            => Animate(gameState, (TResolution)resolution, context, sourceClientIndex);

        protected abstract Task Animate(GameState gameState, TResolution resolution, TokenVisualContext context, int sourceClientIndex);
    }
}
