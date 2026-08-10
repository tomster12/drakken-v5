using System;
using System.Threading.Tasks;
using Drakken.Domain;
using Drakken.Domain.Networking;

namespace Drakken.Networking
{
    public interface IGameConnection
    {
        void StartServer(string address, ushort port);
        void StartClient(string address, ushort port);
        void AddClientListeners(Action<ulong> OnConnected, Action<ulong> OnDisconnected);
        void RemoveClientListeners(Action<ulong> OnConnected, Action<ulong> OnDisconnected);

        Task<JoinMatchResponse> Client_RequestJoinMatch();
        void Server_MessageMatchOtherPlayerJoined(ulong clientId);
        void Client_MessageMatchClientReady(ulong matchId);
        void Server_MessageMatchOtherPlayerReady(ulong clientId);
        void Server_MessageMatchOtherPlayerDiscarded(ulong clientId);
        void Server_BroadcastMatchStartDraftingPhase(ulong[] clientIds, GameState gameState, MatchDiceSimulationTraces diceTraces);
        Task<bool> Client_RequestMatchDraftDiscard(ulong matchId, DraftDiscardMessage message);
        void Server_BroadcastMatchStartPlayingPhase(ulong[] clientIds, GameState gameState);
        Task<bool> Client_RequestMatchPlayToken(ulong matchId, TokenIntentMessage message);
        void Server_BroadcastMatchTokenResolved(ulong[] clientIds, TokenResolutionMessage message);
        Task Client_MessageMatchAnimatedTokenResolved(ulong matchId);
        void Server_BroadcastMatchNextTurn(ulong[] clientIds, GameState gameState);
        void Server_BroadcastMatchNextRound(ulong[] clientIds, GameState gameState, MatchDiceSimulationTraces diceTraces);
    }
}