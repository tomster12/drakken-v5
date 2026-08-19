using System.Threading.Tasks;

namespace Drakken.Gameplay.Tokens.Logic
{
    public interface ITokenIntentPicker
    {
        Task<TokenIntent> PickIntent(TokenVisualContext context, int clientIndex);
    }

    public abstract class TokenIntentPicker<TIntent> : ITokenIntentPicker
        where TIntent : TokenIntent
    {
        async Task<TokenIntent> ITokenIntentPicker.PickIntent(TokenVisualContext context, int clientIndex)
            => await PickIntent(context, clientIndex);

        protected abstract Task<TIntent> PickIntent(TokenVisualContext context, int clientIndex);
    }
}
