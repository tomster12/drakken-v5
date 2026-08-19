using System.Threading.Tasks;
using Drakken.Gameplay.Tokens.Logic;

namespace Drakken.Gameplay.Tokens.Implementation.Common
{
    public class EmptyTokenIntent : TokenIntent { }

    public class EmptyTokenIntentPicker : TokenIntentPicker<EmptyTokenIntent>
    {
        protected override Task<EmptyTokenIntent> PickIntent(TokenVisualContext context, int clientIndex)
            => Task.FromResult(new EmptyTokenIntent());
    }
}
