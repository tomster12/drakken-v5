using Drakken.Client;
using Drakken.Client.World;

namespace Drakken.Domain.Tokens.Logic
{
    public class TokenVisualContext
    {
        public GameClient Client { get; set; }
        public TokenView TokenView { get; set; }
    }
}