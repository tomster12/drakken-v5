using System.Threading.Tasks;
using Drakken.Client;
using Drakken.Common;
using Drakken.Domain;
using Drakken.Domain.Networking;
using Drakken.Server;
using Unity.Netcode;

namespace Drakken.Networking
{
    public class GameConnection : NetworkBehaviour
    {
        public static GameConnection Singleton { get; private set; }

        private const string JoinMatchTaskName = "JoinMatch";

        private GameServer server;
        private GameClient client;
        private readonly TaskManager tasks = new();

        private void Awake()
        {
            Singleton = this;
        }

        public void SetServer(GameServer server)
        {
            this.server = server;
        }

        public void SetClient(GameClient client)
        {
            this.client = client;
        }

        // -------------------------------- Match Setup

        public Task<JoinMatchResponse> Client_RequestJoinMatch()
        {
            var task = tasks.Create<JoinMatchResponse>(JoinMatchTaskName);
            C2S_RequestJoinMatch_Rpc();
            return task;
        }

        [Rpc(SendTo.Server)]
        private void C2S_RequestJoinMatch_Rpc(RpcParams rpc = default)
        {
            var clientId = rpc.Receive.SenderClientId;
            var response = server.OnRequestJoinMatch(clientId);
            S2C_RespondJoinMatch_Rpc(response, RpcTarget.Single(clientId, RpcTargetUse.Temp));
        }

        [Rpc(SendTo.SpecifiedInParams)]
        private void S2C_RespondJoinMatch_Rpc(JoinMatchResponse response, RpcParams rpc = default)
        {
            tasks.Complete(JoinMatchTaskName, response);
        }

        [Rpc(SendTo.Server)]
        public void C2S_MessageMatchClientReady_Rpc(ulong matchId, RpcParams rpc = default)
        {
            var clientId = rpc.Receive.SenderClientId;
            var match = server.GetMatch(matchId);
            match.OnReady(clientId);
        }

        // -------------------------------- Drafting Phase

        public void Server_BroadcastMatchStartDraftingPhase(ulong[] clientIds, GameState gameState)
        {
            var clientsTarget = RpcTarget.Group(clientIds, RpcTargetUse.Temp);
            S2C_BroadcastMatchStartDraftingPhase_Rpc(gameState, clientsTarget);
        }

        [Rpc(SendTo.SpecifiedInParams)]
        public void S2C_BroadcastMatchStartDraftingPhase_Rpc(GameState gameState, RpcParams rpc = default)
        {
            var match = client.Match;
            match.OnStartDraftingPhase(gameState);
        }

        [Rpc(SendTo.Server)]
        public void C2S_MessageMatchDraftDiscard_Rpc(ulong matchId, DraftDiscardMessage message, RpcParams rpc = default)
        {
            var match = server.GetMatch(matchId);
            match.OnDraftDiscard(rpc.Receive.SenderClientId, message);
        }

        // -------------------------------- Playing Phase

        public void Server_BroadcastMatchStartPlayingPhase(ulong[] clientIds, GameState gameState)
        {
            var clientsTarget = RpcTarget.Group(clientIds, RpcTargetUse.Temp);
            S2C_BroadcastMatchStartPlayingPhase_Rpc(gameState, clientsTarget);
        }

        [Rpc(SendTo.SpecifiedInParams)]
        public void S2C_BroadcastMatchStartPlayingPhase_Rpc(GameState gameState, RpcParams rpc = default)
        {
            var match = client.Match;
            match.OnStartPlayingPhase(gameState);
        }
    }
}
