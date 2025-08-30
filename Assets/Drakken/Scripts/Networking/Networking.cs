using Drakken.ApplicationPlayer;
using Drakken.ApplicationServer;
using Drakken.Common.Data;
using Unity.Netcode;

namespace Drakken.ApplicationNetworking
{
    public class JoinMatchResponse : INetworkSerializable
    {
        public bool Success;
        public ulong MatchId;
        public ulong PlayerIndex;

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref Success);
            serializer.SerializeValue(ref MatchId);
            serializer.SerializeValue(ref PlayerIndex);
        }
    }

    public class Networking : NetworkBehaviour
    {
        public static Networking Singleton { get; private set; }

        private void Awake()
        {
            Singleton = this;
        }

        public static ClientRpcParams ToClient(ulong id) => new() { Send = new() { TargetClientIds = new[] { id } } };

        public static ClientRpcParams ToClients(ulong[] ids) => new() { Send = new() { TargetClientIds = ids } };

        [ServerRpc(RequireOwnership = false)]
        public void RequestJoinMatch_ServerRpc(ServerRpcParams rpcParams = default)
            => ServerNetworkingRouter.Singleton.OnRequestJoinMatch(rpcParams.Receive.SenderClientId);

        [ClientRpc]
        public void RespondJoinMatch_ClientRpc(JoinMatchResponse response, ClientRpcParams rpcParams = default)
            => PlayerNetworkingRouter.Singleton.OnRespondJoinMatch(response);

        [ServerRpc(RequireOwnership = false)]
        public void MessageReadyInMatch_ServerRpc(ulong matchId, ulong clientId, ServerRpcParams rpcParams = default)
            => ServerNetworkingRouter.Singleton.OnMessageReadyInMatch(matchId, clientId);

        [ClientRpc]
        public void MessageGameStarted_ClientRpc(GameState gameState, ClientRpcParams rpcParams = default)
            => PlayerNetworkingRouter.Singleton.OnMessageGameStarted(gameState);
    }
}
