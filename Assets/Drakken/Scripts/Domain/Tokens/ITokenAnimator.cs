using System.Threading.Tasks;

namespace Drakken.Domain.Tokens
{
    public interface ITokenAnimator
    {
        Task Animate(TokenResolution resolution, TokenVisualContext context, int sourceClientIndex);
    }

    public abstract class TokenAnimator<TResolution> : ITokenAnimator
        where TResolution : TokenResolution
    {
        public Task Animate(TokenResolution resolution, TokenVisualContext context, int sourceClientIndex)
            => Animate((TResolution)resolution, context, sourceClientIndex);

        protected abstract Task Animate(TResolution resolution, TokenVisualContext context, int sourceClientIndex);
    }
}
