using System.Threading.Tasks;
using Drakken.Domain;

namespace Drakken.Client
{
    public class ClientState
    {
        protected GameClient client;
        protected ClientMatch Match => client.Match;
        protected GameState GameState => Match.GameState;

        public void Init(GameClient client)
        {
            this.client = client;
        }

        public virtual Task Enter() => Task.CompletedTask;

        public virtual Task Update() => Task.CompletedTask;
    }
}