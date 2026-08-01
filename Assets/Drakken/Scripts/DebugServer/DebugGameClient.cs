using Drakken.Client;
using System.Threading.Tasks;

namespace Drakken.DebugServer
{
    public class DebugGameClient : IGameClient
    {
        public ClientMatch Match { get; private set; }

        public void SetMatch(ClientMatch clientMatch)
        {
            Match = clientMatch;
        }

        public Task<bool> Connect()
        {
            return Task.FromResult(true);
        }

        public Task<bool> JoinMatch()
        {
            return Task.FromResult(true);
        }
    }
}
