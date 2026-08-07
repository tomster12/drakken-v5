using System.Threading;
using System.Threading.Tasks;
using Drakken.Domain;

namespace Drakken.Client
{
    public class ClientState
    {
        protected ClientMatch Match => client.Match;
        protected GameState GameState => Match.GameState;
        protected GameClient client;
        protected CancellationTokenSource cts = new();

        public void Init(GameClient client)
        {
            this.client = client;
        }

        public virtual void OnDestroy()
        {
            cts?.Cancel();
            cts?.Dispose();
        }

        public virtual Task Enter(ClientStateType fromType)
        {
            cts = new();

            return Task.CompletedTask;
        }

        public virtual Task Exit(ClientStateType toType)
        {
            cts.Cancel();
            cts.Dispose();
            cts = null;

            return Task.CompletedTask;
        }

        public virtual Task Update() => Task.CompletedTask;
    }

    public enum ClientStateType
    { None, Title, Drafting, Playing };
}
