using System.Collections.Generic;
using Drakken.Domain;
using Drakken.Networking;
using Unity.Netcode;

namespace Drakken.Server
{
    public class ServerConnection
    {
        public static ServerConnection Singleton { get; private set; }
        private readonly GameServer server;

        public ServerConnection(GameServer server)
        {
            this.server = server;
            Singleton = this;
        }

        public void OnRequestJoinMatch(ulong clientId)
            => server.OnRequestJoinMatch(clientId);

        public void RespondJoinMatch(JoinMatchResponse res, ulong clientId)
            => GameConnection.Singleton.RespondJoinMatchRpc(res,
                GameConnection.Singleton.RpcTarget.Single(clientId, RpcTargetUse.Temp));

        public void OnMessageMatchClientReady(ulong matchId, ulong clientId)
            => server.GetMatch(matchId).OnClientReady(clientId);

        public void MessageMatchStarted(GameState gameState, ulong[] clientIds)
            => GameConnection.Singleton.MessageMatchStartedRpc(gameState,
                GameConnection.Singleton.RpcTarget.Group(clientIds, RpcTargetUse.Temp));
    }
}
