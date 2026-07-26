using System;
using System.Threading.Tasks;
using Drakken.Client;
using Drakken.Common;
using Drakken.Domain;
using Drakken.Domain.Networking;
using Drakken.Server;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;

namespace Drakken.Networking
{
    public interface IGameConnection
    {
        void StartServer(GameServer server, string address, ushort port);
        void StartClient(GameClient client, string address, ushort port);
        void AddClientListeners(Action<ulong> OnConnected, Action<ulong> OnDisconnected);
        void RemoveClientListeners(Action<ulong> OnConnected, Action<ulong> OnDisconnected);

        Task<JoinMatchResponse> Client_RequestJoinMatch();
        void Client_MessageMatchClientReady(ulong matchId);
        void Server_BroadcastMatchStartDraftingPhase(ulong[] clientIds, GameState gameState);
        void Client_MessageMatchDraftDiscard(ulong matchId, DraftDiscardMessage message);
        void Server_BroadcastMatchStartPlayingPhase(ulong[] clientIds, GameState gameState);
    }

    public class GameConnection : NetworkBehaviour, IGameConnection
    {
        private const string JoinMatchTaskName = "JoinMatch";

        private GameServer server;
        private GameClient client;
        private readonly TaskManager tasks = new();

        public void StartServer(GameServer server, string address, ushort port)
        {
            this.server = server;

            var transport = (UnityTransport)NetworkManager.Singleton.NetworkConfig.NetworkTransport;
            transport.ConnectionData.Address = address;
            transport.ConnectionData.Port = port;
            NetworkManager.Singleton.StartServer();
        }

        public void StartClient(GameClient client, string address, ushort port)
        {
            this.client = client;

            var transport = (UnityTransport)NetworkManager.Singleton.NetworkConfig.NetworkTransport;
            transport.ConnectionData.Address = address;
            transport.ConnectionData.Port = port;
            NetworkManager.Singleton.StartClient();
        }

        public void AddClientListeners(Action<ulong> OnConnected, Action<ulong> OnDisconnected)
        {
            NetworkManager.Singleton.OnClientConnectedCallback += OnConnected;
            NetworkManager.Singleton.OnClientDisconnectCallback += OnDisconnected;
        }
        
        public void RemoveClientListeners(Action<ulong> OnConnected, Action<ulong> OnDisconnected)
        {
            NetworkManager.Singleton.OnClientConnectedCallback -= OnConnected;
            NetworkManager.Singleton.OnClientDisconnectCallback -= OnDisconnected;
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

        public void Client_MessageMatchClientReady(ulong matchId)
            => C2S_MessageMatchClientReady_Rpc(matchId);

        [Rpc(SendTo.Server)]
        private void C2S_MessageMatchClientReady_Rpc(ulong matchId, RpcParams rpc = default)
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
        private void S2C_BroadcastMatchStartDraftingPhase_Rpc(GameState gameState, RpcParams rpc = default)
        {
            var match = client.Match;
            match.OnStartDraftingPhase(gameState);
        }

        public void Client_MessageMatchDraftDiscard(ulong matchId, DraftDiscardMessage message)
            => C2S_MessageMatchDraftDiscard_Rpc(matchId, message);

        [Rpc(SendTo.Server)]
        private void C2S_MessageMatchDraftDiscard_Rpc(ulong matchId, DraftDiscardMessage message, RpcParams rpc = default)
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
        private void S2C_BroadcastMatchStartPlayingPhase_Rpc(GameState gameState, RpcParams rpc = default)
        {
            var match = client.Match;
            match.OnStartPlayingPhase(gameState);
        }
    }
}
