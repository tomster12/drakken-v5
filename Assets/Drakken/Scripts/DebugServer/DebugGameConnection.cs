using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Drakken.Client;
using Drakken.Common;
using Drakken.Domain;
using Drakken.Domain.Networking;
using Drakken.Server;

namespace Drakken.DebugServer
{
    public class DebugGameConnection : IGameConnection
    {
        private static readonly ulong myClientId = 20;
        private static readonly ulong opClientId = 30;
        private static readonly TimeSpan opponentReactionDelay = TimeSpan.FromSeconds(1);

        private IGameServer server;
        private DebugGameClient opDebugGameClient;
        private readonly Dictionary<ulong, IGameClient> clientsById = new();
        private Action<ulong> onClientConnectedCallback;
        private readonly TaskManager tasks = new();

        public void StartServer(IGameServer server, string address, ushort port)
        {
            this.server = server;
        }

        public void StartClient(IGameClient client, string address, ushort port)
        {
            opDebugGameClient = new DebugGameClient();

            clientsById[myClientId] = client;
            clientsById[opClientId] = opDebugGameClient;

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
            var opJoinResponse = server.OnRequestJoinMatch(opClientId);
            opDebugGameClient.SetMatch(new ClientMatch(
                opJoinResponse.MatchId,
                (int)opJoinResponse.ClientIndex));

            var response = server.OnRequestJoinMatch(myClientId);
            return Task.FromResult(response);
        }

        public void Server_MessageMatchOtherPlayerJoined(ulong clientId)
        {
            var targetClient = clientsById[clientId];
            targetClient.Match.OnServerOtherPlayerJoined();
        }

        public void Client_MessageMatchClientReady(ulong matchId)
        {
            var match = server.GetMatch(matchId);
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
            var match = server.GetMatch(matchId);
            match.OnClientRequestDraftDiscard(myClientId, message, (response) =>
                tasks.Complete(requestId, response));

            ScheduleOpRequestMatchDraftDiscard(match);

            return task;
        }

        private async void ScheduleOpRequestMatchDraftDiscard(ServerMatch match)
        {
            await Task.Delay(opponentReactionDelay);

            var clientIndex = opDebugGameClient.Match.ClientIndex;
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

        public void Server_BroadcastMatchStartPlayingPhase(ulong[] clientIds, GameState gameState)
        {
            foreach (var clientId in clientIds)
            {
                clientsById[clientId].Match.OnServerStartPlayingPhase(gameState.Clone());
            }
        }
    }
}
