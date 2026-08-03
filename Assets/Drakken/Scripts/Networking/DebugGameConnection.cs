using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Drakken.Client;
using Drakken.Common;
using Drakken.Domain;
using Drakken.Domain.Networking;
using Drakken.Server;

namespace Drakken.Networking
{
    public class DebugGameConnection : IGameConnection
    {
        private static readonly ulong myClientId = 20;
        private static readonly ulong opClientId = 30;
        private static readonly TimeSpan opponentReactionDelay = TimeSpan.FromSeconds(1);

        private IGameServer Server => GameEntrypoint.Singleton.Server;
        private readonly Dictionary<ulong, IGameClient> clientsById = new();
        private Action<ulong> onClientConnectedCallback;
        private DebugGameClient opClient;
        private readonly TaskManager tasks = new();

        public void StartServer(string address, ushort port)
        {
        }

        public void StartClient(string address, ushort port)
        {
            // Setup opponent debug client
            opClient = new DebugGameClient();
            clientsById[myClientId] = GameEntrypoint.Singleton.Client;
            clientsById[opClientId] = opClient;

            // Imediately tell the client to execute
            onClientConnectedCallback.Invoke(myClientId);
        }

        public void AddClientListeners(Action<ulong> onClientConnected, Action<ulong> onClientDisconnected)
        {
            onClientConnectedCallback = onClientConnected;
        }

        public void RemoveClientListeners(Action<ulong> onClientConnected, Action<ulong> onClientDisconnected)
        {
            onClientConnectedCallback = null;
        }

        // -------------------------------- Match Setup

        public Task<JoinMatchResponse> Client_RequestJoinMatch()
        {
            // make debug opponent join match first
            var opJoinResponse = Server.OnRequestJoinMatch(opClientId);
            opClient.SetMatch(new ClientMatch(
                GameEntrypoint.Singleton.TokenRegistry,
                opJoinResponse.MatchId,
                (int)opJoinResponse.ClientIndex));

            var response = Server.OnRequestJoinMatch(myClientId);
            return Task.FromResult(response);
        }

        public void Server_MessageMatchOtherPlayerJoined(ulong clientId)
        {
            var targetClient = clientsById[clientId];
            targetClient.Match.OnServerOtherPlayerJoined();
        }

        public void Client_MessageMatchClientReady(ulong matchId)
        {
            var match = Server.GetMatch(matchId);
            match.OnClientReady(myClientId);

            ScheduleOpClientReady(match);
        }

        private async void ScheduleOpClientReady(ServerMatch match)
        {
            await Task.Delay(opponentReactionDelay);
            match.OnClientReady(opClientId);
        }

        public void Server_MessageMatchOtherPlayerReady(ulong clientId)
        {
            var targetClient = clientsById[clientId];
            targetClient.Match.OnServerOtherPlayerReady();
        }

        public void Server_BroadcastMatchStartDraftingPhase(ulong[] clientIds, GameState gameState)
        {
            foreach (var clientId in clientIds)
            {
                clientsById[clientId].Match.OnServerStartDraftingPhase(gameState.Clone());
            }
        }

        public Task<bool> Client_RequestMatchDraftDiscard(ulong matchId, DraftDiscardMessage message)
        {
            var (requestId, task) = tasks.Create<bool>();
            var match = Server.GetMatch(matchId);
            match.OnClientRequestDraftDiscard(myClientId, message, (response) =>
                tasks.Complete(requestId, response));

            ScheduleOpRequestMatchDraftDiscard(match);

            return task;
        }

        private async void ScheduleOpRequestMatchDraftDiscard(ServerMatch match)
        {
            await Task.Delay(opponentReactionDelay);

            var clientIndex = opClient.Match.ClientIndex;
            var message = new DraftDiscardMessage
            {
                DiscardedInstanceIds = new()
                {
                    match.GameState.Clients[clientIndex].Tokens[0].InstanceId,
                    match.GameState.Clients[clientIndex].Tokens[1].InstanceId,
                }
            };

            match.OnClientRequestDraftDiscard(opClientId, message, (response) => { });
        }

        public void Server_MessageMatchOtherPlayerDiscarded(ulong clientId)
        {
            var targetClient = clientsById[clientId];
            targetClient.Match.OnServerOtherPlayerDiscarded();
        }

        public void Server_BroadcastMatchStartPlayingPhase(ulong[] clientIds, GameState gameState)
        {
            foreach (var clientId in clientIds)
            {
                clientsById[clientId].Match.OnServerStartPlayingPhase(gameState.Clone());
            }
        }

        public Task<bool> Client_RequestMatchPlayToken(ulong matchId, TokenIntentMessage message)
        {
            var (requestId, task) = tasks.Create<bool>();
            var match = Server.GetMatch(matchId);
            match.OnClientRequestPlayToken(myClientId, message, (response) =>
                tasks.Complete(requestId, response));

            return task;
        }

        public void Server_BroadcastMatchTokenResolved(ulong[] clientIds, TokenResolutionMessage message)
        {
            foreach (var clientId in clientIds)
            {
                clientsById[clientId].Match.OnServerTokenResolved(message);
            }
        }
    }

    public class DebugGameClient : IGameClient
    {
        public ClientMatch Match { get; private set; }

        public void SetMatch(ClientMatch clientMatch)
        {
            Match = clientMatch;
        }

        public Task<bool> Connect()
        {
            return Task.FromResult(true);
        }

        public Task<bool> JoinMatch()
        {
            return Task.FromResult(true);
        }
    }
}
