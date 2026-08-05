using System.Threading.Tasks;
using Drakken.Domain;

namespace Drakken.Client
{
    public class ClientState
    {
        protected ClientMatch Match => client.Match;
        protected GameState GameState => Match.GameState;
        protected GameClient client;

        public void Init(GameClient client)
        {
            this.client = client;
        }

        public virtual Task Enter(ClientStateType fromType) => Task.CompletedTask;

        public virtual Task Exit(ClientStateType toType) => Task.CompletedTask;

        public virtual void OnDestroy() {}

        public virtual Task Update() => Task.CompletedTask;
    }

    public enum ClientStateType
    { None, Title, Drafting, Playing };
}
