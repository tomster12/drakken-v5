using System.Threading;
using System.Threading.Tasks;
using Drakken.Client;

namespace Drakken.Domain.Tokens.Logic
{
    public interface ITokenAnimator
    {
        Task Animate(
            ClientMatch match,
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
            ClientMatch match,
            TokenVisualContext visualContext,
            int sourceClientIndex,
            int tokenInstanceId,
            TokenResolution resolution,
            CancellationToken ct)
        {
            return Animate(match, visualContext, sourceClientIndex, tokenInstanceId, (TResolution)resolution, ct);
        }

        protected abstract Task Animate(
            ClientMatch match,
            TokenVisualContext visualContext,
            int sourceClientIndex,
            int tokenInstanceId,
            TResolution resolution,
            CancellationToken ct);
    }
}
