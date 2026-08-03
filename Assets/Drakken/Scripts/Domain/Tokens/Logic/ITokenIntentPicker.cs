using System.Threading.Tasks;

namespace Drakken.Domain.Tokens.Logic
{
    public interface ITokenIntentPicker
    {
        Task<TokenIntent> PickIntent(TokenVisualContext context);
    }

    public abstract class TokenIntentPicker<TIntent> : ITokenIntentPicker
        where TIntent : TokenIntent
    {
        async Task<TokenIntent> ITokenIntentPicker.PickIntent(TokenVisualContext context)
            => await PickIntent(context);

        protected abstract Task<TIntent> PickIntent(TokenVisualContext context);
    }
}
