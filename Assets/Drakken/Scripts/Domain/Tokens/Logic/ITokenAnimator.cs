using System.Threading;
using System.Threading.Tasks;

namespace Drakken.Domain.Tokens.Logic
{
    public interface ITokenAnimator
    {
        Task Animate(
            GameState gameState,
            TokenVisualContext visualContext,
            int sourceClientIndex,
            int tokenInstanceId,
            TokenResolution resolution,
            CancellationToken ct);
    }

    public abstract class TokenAnimator<TResolution> : ITokenAnimator
        where TResolution : TokenResolution
    {
        public Task Animate(
            GameState gameState,
            TokenVisualContext visualContext,
            int sourceClientIndex,
            int tokenInstanceId,
            TokenResolution resolution,
            CancellationToken ct)
        {
            return Animate(gameState, visualContext, sourceClientIndex, tokenInstanceId, (TResolution)resolution, ct);
        }


        protected abstract Task Animate(
            GameState gameState,
            TokenVisualContext visualContext,
            int sourceClientIndex,
            int tokenInstanceId,
            TResolution resolution,
            CancellationToken ct);
    }
}
