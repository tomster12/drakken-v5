using System.Threading.Tasks;
using Drakken.Networking;
using Drakken.Domain;

namespace Drakken.Client
{
    public class ClientConnection
    {
        public static ClientConnection Singleton { get; private set; }

        private readonly GameClient client;
        private TaskCompletionSource<JoinMatchResponse> joinMatchTask;

        public ClientConnection(GameClient client)
        {
            this.client = client;
            Singleton = this;
        }

        public Task<JoinMatchResponse> RequestJoinMatch()
        {
            joinMatchTask = new();
            GameConnection.Singleton.RequestJoinMatchRpc();
            return joinMatchTask.Task;
        }

        public void OnRespondJoinMatch(JoinMatchResponse response)
            => joinMatchTask.TrySetResult(response);

        public void MessageReadyInMatch(ulong matchId)
            => GameConnection.Singleton.MessageMatchClientReadyRpc(matchId);

        public void OnMessageMatchStarted(GameState gameState)
            => client.Match.OnGameStarted(gameState);
    }
}
