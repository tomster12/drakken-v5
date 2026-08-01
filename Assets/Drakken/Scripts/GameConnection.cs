using System;
using System.Threading.Tasks;
using Drakken.Client;
using Drakken.Common;
using Drakken.Domain;
using Drakken.Domain.Networking;
using Drakken.Server;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;

namespace Drakken
{
    public interface IGameConnection
    {
        void StartServer(IGameServer server, string address, ushort port);
        void StartClient(IGameClient client, string address, ushort port);
        void AddClientListeners(Action<ulong> OnConnected, Action<ulong> OnDisconnected);
        void RemoveClientListeners(Action<ulong> OnConnected, Action<ulong> OnDisconnected);

        Task<JoinMatchResponse> Client_RequestJoinMatch();
        void Server_MessageMatchOtherPlayerJoined(ulong clientId);
        void Client_MessageMatchClientReady(ulong matchId);
        void Server_MessageMatchOtherPlayerReady(ulong clientId);
        void Server_BroadcastMatchStartDraftingPhase(ulong[] clientIds, GameState gameState);
        Task<bool> Client_RequestMatchDraftDiscard(ulong matchId, DraftDiscardMessage message);
        void Server_BroadcastMatchStartPlayingPhase(ulong[] clientIds, GameState gameState);
    }

    public class GameConnection : NetworkBehaviour, IGameConnection
    {
        private IGameServer server;
        private IGameClient client;
        private readonly TaskManager tasks = new();

        public void StartServer(IGameServer server, string address, ushort port)
        {
            this.server = server;

            var transport = (UnityTransport)NetworkManager.Singleton.NetworkConfig.NetworkTransport;
            transport.ConnectionData.Address = address;
            transport.ConnectionData.Port = port;
            NetworkManager.Singleton.StartServer();
        }

        public void StartClient(IGameClient client, string address, ushort port)
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

        // -------------------------------- Setup

        public Task<JoinMatchResponse> Client_RequestJoinMatch()
        {
            var (requestId, task) = tasks.Create<JoinMatchResponse>();
            C2S_RequestJoinMatch_Rpc(requestId);
            return task;
        }

        [Rpc(SendTo.Server)]
        private void C2S_RequestJoinMatch_Rpc(ulong requestId, RpcParams rpc = default)
        {
            var clientId = rpc.Receive.SenderClientId;
            var response = server.OnRequestJoinMatch(clientId);
            S2C_RespondJoinMatch_Rpc(requestId, response, RpcTarget.Single(clientId, RpcTargetUse.Temp));
        }

        [Rpc(SendTo.SpecifiedInParams)]
        private void S2C_RespondJoinMatch_Rpc(ulong requestId, JoinMatchResponse response, RpcParams rpc = default)
        {
            tasks.Complete(requestId, response);
        }

        public void Server_MessageMatchOtherPlayerJoined(ulong clientId)
        {
            S2C_MessageMatchOtherPlayerJoined_Rpc(RpcTarget.Single(clientId, RpcTargetUse.Temp));
        }

        [Rpc(SendTo.SpecifiedInParams)]
        private void S2C_MessageMatchOtherPlayerJoined_Rpc(RpcParams rpc = default)
        {
            client.Match.OnServerOtherPlayerJoined();
        }

        public void Client_MessageMatchClientReady(ulong matchId)
        {
            C2S_MessageMatchClientReady_Rpc(matchId);
        }

        [Rpc(SendTo.Server)]
        private void C2S_MessageMatchClientReady_Rpc(ulong matchId, RpcParams rpc = default)
        {
            var clientId = rpc.Receive.SenderClientId;
            server.GetMatch(matchId).OnClientReady(clientId);
        }

        public void Server_MessageMatchOtherPlayerReady(ulong clientId)
        {
            S2C_MessageMatchOtherPlayerReady_Rpc(RpcTarget.Single(clientId, RpcTargetUse.Temp));
        }

        [Rpc(SendTo.SpecifiedInParams)]
        private void S2C_MessageMatchOtherPlayerReady_Rpc(RpcParams rpc = default)
        {
            client.Match.OnServerOtherPlayerReady();
        }

        // -------------------------------- Drafting

        public void Server_BroadcastMatchStartDraftingPhase(ulong[] clientIds, GameState gameState)
        {
            S2C_BroadcastMatchStartDraftingPhase_Rpc(gameState, RpcTarget.Group(clientIds, RpcTargetUse.Temp));
        }

        [Rpc(SendTo.SpecifiedInParams)]
        private void S2C_BroadcastMatchStartDraftingPhase_Rpc(GameState gameState, RpcParams rpc = default)
        {
            client.Match.OnServerStartDraftingPhase(gameState);
        }

        public Task<bool> Client_RequestMatchDraftDiscard(ulong matchId, DraftDiscardMessage message)
        {
            var (requestId, task) = tasks.Create<bool>();
            C2S_RequestMatchDraftDiscard_Rpc(matchId, requestId, message);
            return task;
        }

        [Rpc(SendTo.Server)]
        private void C2S_RequestMatchDraftDiscard_Rpc(ulong matchId, ulong requestId, DraftDiscardMessage message, RpcParams rpc = default)
        {
            var clientId = rpc.Receive.SenderClientId;
            server.GetMatch(matchId).OnClientRequestDraftDiscard(clientId, message, (response) =>
                S2C_RespondMatchDraftDiscard_Rpc(requestId, response, RpcTarget.Single(clientId, RpcTargetUse.Temp)));
        }

        [Rpc(SendTo.SpecifiedInParams)]
        private void S2C_RespondMatchDraftDiscard_Rpc(ulong requestId, bool response, RpcParams rpc = default)
        {
            tasks.Complete(requestId, response);
        }

        // -------------------------------- Playing

        public void Server_BroadcastMatchStartPlayingPhase(ulong[] clientIds, GameState gameState)
        {
            S2C_BroadcastMatchStartPlayingPhase_Rpc(gameState, RpcTarget.Group(clientIds, RpcTargetUse.Temp));
        }

        [Rpc(SendTo.SpecifiedInParams)]
        private void S2C_BroadcastMatchStartPlayingPhase_Rpc(GameState gameState, RpcParams rpc = default)
        {
            client.Match.OnServerStartPlayingPhase(gameState);
        }
    }
}
