using Drakken.Client;
using Drakken.Server;
using Drakken.Domain;
using Unity.Netcode;

namespace Drakken.Networking
{
    public class GameConnection : NetworkBehaviour
    {
        public static GameConnection Singleton { get; private set; }

        private void Awake()
        {
            Singleton = this;
        }

        [Rpc(SendTo.Server)]
        public void RequestJoinMatchRpc(RpcParams rpcParams = default)
            => ServerConnection.Singleton.OnRequestJoinMatch(rpcParams.Receive.SenderClientId);

        [Rpc(SendTo.SpecifiedInParams)]
        public void RespondJoinMatchRpc(JoinMatchResponse response, RpcParams rpcParams = default)
            => ClientConnection.Singleton.OnRespondJoinMatch(response);

        [Rpc(SendTo.Server)]
        public void MessageMatchClientReadyRpc(ulong matchId, RpcParams rpcParams = default)
            => ServerConnection.Singleton.OnMessageMatchClientReady(matchId, rpcParams.Receive.SenderClientId);

        [Rpc(SendTo.SpecifiedInParams)]
        public void MessageMatchStartedRpc(GameState gameState, RpcParams rpcParams = default)
            => ClientConnection.Singleton.OnMessageMatchStarted(gameState);
    }
}
