using System.Threading.Tasks;
using Drakken.Domain.Tokens.Logic;

namespace Drakken.Domain.Tokens.Implementation.Common
{
    public class EmptyTokenIntent : TokenIntent { }

    public class EmptyTokenIntentPicker : TokenIntentPicker<EmptyTokenIntent>
    {
        protected override Task<EmptyTokenIntent> PickIntent(TokenVisualContext context, int clientIndex)
            => Task.FromResult(new EmptyTokenIntent());
    }
}
