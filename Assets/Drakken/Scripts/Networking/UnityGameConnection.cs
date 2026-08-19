using System;
using System.Threading.Tasks;
using Drakken.Client;
using Drakken.Utility;
using Drakken.Domain;
using Drakken.Domain.Networking;
using Drakken.Networking;
using Drakken.Server;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;

namespace Drakken.Networking
{
    public class UnityGameConnection : NetworkBehaviour, IGameConnection
    {
        public static UnityGameConnection Singleton { get; private set; }
        private IGameServer Server => GameEntrypoint.Singleton.Server;
        private IGameClient Client => GameEntrypoint.Singleton.Client;
        private readonly TaskManager tasks = new();

        private void Awake()
        {
            Singleton = this;
        }

        public void StartServer(string address, ushort port)
        {
            var transport = (UnityTransport)NetworkManager.Singleton.NetworkConfig.NetworkTransport;
            transport.ConnectionData.Address = address;
            transport.ConnectionData.Port = port;
            NetworkManager.Singleton.StartServer();
        }

        public void StartClient(string address, ushort port)
        {
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
            var response = Server.OnRequestJoinMatch(clientId);
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
            Client.Match.OnServerOtherPlayerJoined();
        }

        public void Client_MessageMatchClientReady(ulong matchId)
        {
            C2S_MessageMatchClientReady_Rpc(matchId);
        }

        [Rpc(SendTo.Server)]
        private void C2S_MessageMatchClientReady_Rpc(ulong matchId, RpcParams rpc = default)
        {
            var clientId = rpc.Receive.SenderClientId;
            Server.GetMatch(matchId).OnClientReady(clientId);
        }

        public void Server_MessageMatchOtherPlayerReady(ulong clientId)
        {
            S2C_MessageMatchOtherPlayerReady_Rpc(RpcTarget.Single(clientId, RpcTargetUse.Temp));
        }

        [Rpc(SendTo.SpecifiedInParams)]
        private void S2C_MessageMatchOtherPlayerReady_Rpc(RpcParams rpc = default)
        {
            Client.Match.OnServerOtherPlayerReady();
        }

        // -------------------------------- Drafting

        public void Server_BroadcastMatchStartDraftingPhase(ulong[] clientIds, GameState gameState, GameSimulationMatchTrace trace)
        {
            S2C_BroadcastMatchStartDraftingPhase_Rpc(gameState, trace, RpcTarget.Group(clientIds, RpcTargetUse.Temp));
        }

        [Rpc(SendTo.SpecifiedInParams)]
        private void S2C_BroadcastMatchStartDraftingPhase_Rpc(GameState gameState, GameSimulationMatchTrace trace, RpcParams rpc = default)
        {
            Client.Match.OnServerStartDraftingPhase(gameState, trace);
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
            Server.GetMatch(matchId).OnClientRequestDraftDiscard(clientId, message, (response) =>
                S2C_RespondMatchDraftDiscard_Rpc(requestId, response, RpcTarget.Single(clientId, RpcTargetUse.Temp)));
        }

        [Rpc(SendTo.SpecifiedInParams)]
        private void S2C_RespondMatchDraftDiscard_Rpc(ulong requestId, bool response, RpcParams rpc = default)
        {
            tasks.Complete(requestId, response);
        }

        public void Server_MessageMatchOtherPlayerDiscarded(ulong clientId)
        {
            S2C_MessageMatchOtherPlayerDiscarded_Rpc(RpcTarget.Single(clientId, RpcTargetUse.Temp));
        }

        [Rpc(SendTo.SpecifiedInParams)]
        private void S2C_MessageMatchOtherPlayerDiscarded_Rpc(RpcParams rpc = default)
        {
            Client.Match.OnServerOtherPlayerDiscarded();
        }

        // -------------------------------- Playing

        public void Server_BroadcastMatchStartPlayingPhase(ulong[] clientIds, GameState gameState)
        {
            S2C_BroadcastMatchStartPlayingPhase_Rpc(gameState, RpcTarget.Group(clientIds, RpcTargetUse.Temp));
        }

        [Rpc(SendTo.SpecifiedInParams)]
        private void S2C_BroadcastMatchStartPlayingPhase_Rpc(GameState gameState, RpcParams rpc = default)
        {
            Client.Match.OnServerStartPlayingPhase(gameState);
        }

        public Task<bool> Client_RequestMatchPlayToken(ulong matchId, TokenIntentMessage message)
        {
            var (requestId, task) = tasks.Create<bool>();
            C2S_RequestMatchPlayToken_Rpc(matchId, requestId, message);
            return task;
        }

        [Rpc(SendTo.Server)]
        private void C2S_RequestMatchPlayToken_Rpc(ulong matchId, ulong requestId, TokenIntentMessage message, RpcParams rpc = default)
        {
            var clientId = rpc.Receive.SenderClientId;
            Server.GetMatch(matchId).OnClientRequestPlayToken(clientId, message, (response) =>
                S2C_RespondMatchPlayToken_Rpc(requestId, response, RpcTarget.Single(clientId, RpcTargetUse.Temp)));
        }

        [Rpc(SendTo.SpecifiedInParams)]
        private void S2C_RespondMatchPlayToken_Rpc(ulong requestId, bool response, RpcParams rpc = default)
        {
            tasks.Complete(requestId, response);
        }

        public void Server_BroadcastMatchTokenResolved(ulong[] clientIds, TokenResolutionMessage message)
        {
            S2C_BroadcastMatchTokenResolved_Rpc(message, RpcTarget.Group(clientIds, RpcTargetUse.Temp));
        }

        [Rpc(SendTo.SpecifiedInParams)]
        private void S2C_BroadcastMatchTokenResolved_Rpc(TokenResolutionMessage message, RpcParams rpc = default)
        {
            Client.Match.OnServerTokenResolved(message);
        }

        public Task Client_MessageMatchAnimatedTokenResolved(ulong matchId)
        {
            C2S_MessageMatchAnimatedTokenResolved_Rpc(matchId);
            return Task.CompletedTask;
        }

        [Rpc(SendTo.Server)]
        private void C2S_MessageMatchAnimatedTokenResolved_Rpc(ulong matchId, RpcParams rpc = default)
        {
            var clientId = rpc.Receive.SenderClientId;
            Server.GetMatch(matchId).OnClientMessageTokenResolved(clientId);
        }

        public void Server_BroadcastMatchNextTurn(ulong[] clientIds, GameState gameState)
        {
            S2C_BroadcastMatchNextTurn_Rpc(gameState, RpcTarget.Group(clientIds, RpcTargetUse.Temp));
        }

        [Rpc(SendTo.SpecifiedInParams)]
        private void S2C_BroadcastMatchNextTurn_Rpc(GameState gameState, RpcParams rpc = default)
        {
            Client.Match.OnServerNextTurn(gameState);
        }

        public void Server_BroadcastMatchNextRound(ulong[] clientIds, GameState gameState, GameSimulationMatchTrace trace)
        {
            S2C_BroadcastMatchNextRound_Rpc(gameState, trace, RpcTarget.Group(clientIds, RpcTargetUse.Temp));
        }

        [Rpc(SendTo.SpecifiedInParams)]
        private void S2C_BroadcastMatchNextRound_Rpc(GameState gameState, GameSimulationMatchTrace trace, RpcParams rpc = default)
        {
            Client.Match.OnServerNextRound(gameState, trace);
        }
    }
}