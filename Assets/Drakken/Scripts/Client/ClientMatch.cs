using Drakken.Domain;
using Drakken.Utility;
using System;
using Drakken.Networking;
using System.Threading.Tasks;
using System.Linq;
using Drakken.Gameplay.Tokens;
using Drakken.Domain.Networking;

namespace Drakken.Client
{
    public class ClientMatch
    {
        public Action OnOtherPlayerJoined = delegate { };
        public Action OnOtherPlayerReady = delegate { };
        public Action OnDraftingPhaseStarted = delegate { };
        public Action OnDraftingOtherPlayerDiscarded = delegate { };
        public Action OnPlayingPhaseStarted = delegate { };
        public Action<TokenResolutionMessage> OnTokenResolved = delegate { };
        public Action OnNextTurnStarted = delegate { };
        public Action OnRoundEnded = delegate { };

        private readonly TokenRegistry tokenRegistry;
        private readonly IGameConnection connection;
        public GameState GameState { get; private set; }
        public GameSimulationMatchTrace LastDiceTraces { get; private set; }
        public ulong MatchId { get; private set; }
        public int ClientIndex { get; private set; }
        public int OpClientIndex => 1 - ClientIndex;
        public bool IsOpJoined { get; private set; }
        public bool IsOpReady { get; private set; }
        public bool IsReady { get; private set; }
        public bool IsOpDiscarded { get; private set; }
        public bool IsMyTurn => GameState?.TurnClientIndex == ClientIndex;

        // -------------------------------- Setup

        public ClientMatch(TokenRegistry tokenRegistry, IGameConnection connection, ulong matchId, int clientIndex)
        {
            this.tokenRegistry = tokenRegistry;
            this.connection = connection;
            MatchId = matchId;
            ClientIndex = clientIndex;
            GameState = new();

            IsOpJoined = clientIndex == 1;

            Log.Info($"ClientMatch-{MatchId}", $"Joined match as clientIndex={ClientIndex}");
        }

        public void SetReady()
        {
            Assert.False(IsReady);
            Assert.True(GameState.Phase == GamePhase.NotStarted);

            Log.Info($"ClientMatch-{MatchId}", $"Ready up");

            IsReady = true;
            connection.Client_MessageMatchClientReady(MatchId);
        }

        public void OnServerOtherPlayerJoined()
        {
            Assert.False(IsOpJoined);
            Assert.True(GameState.Phase == GamePhase.NotStarted);

            Log.Info($"ClientMatch-{MatchId}", "OnServerOtherPlayerJoined");

            IsOpJoined = true;
            OnOtherPlayerJoined.Invoke();
        }

        public void OnServerOtherPlayerReady()
        {
            Assert.False(IsOpReady);
            Assert.True(GameState.Phase == GamePhase.NotStarted);

            Log.Info($"ClientMatch-{MatchId}", "OnServerOtherPlayerReady");

            IsOpReady = true;
            OnOtherPlayerReady.Invoke();
        }

        // -------------------------------- Drafting

        public void OnServerStartDraftingPhase(GameState gameState, GameSimulationMatchTrace trace)
        {
            Assert.True(GameState.Phase == GamePhase.NotStarted);

            Log.Info($"ClientMatch-{MatchId}", $"Match started drafting phase");

            GameState = gameState;
            LastDiceTraces = trace;

            OnDraftingPhaseStarted.Invoke();
        }

        public async Task<bool> RequestDraftDiscard(DraftDiscardMessage message)
        {
            Assert.True(GameState.Phase == GamePhase.Drafting);

            Log.Info($"ClientMatch-{MatchId}", $"Sending draft discard");

            var response = await connection.Client_RequestMatchDraftDiscard(MatchId, message);

            GameState.Clients[ClientIndex].Tokens = GameState.Clients[ClientIndex].Tokens
                .Where(t => !message.DiscardedInstanceIds.Contains(t.InstanceId))
                .ToList();

            return response;
        }

        public void OnServerOtherPlayerDiscarded()
        {
            Assert.False(IsOpDiscarded);
            Assert.True(GameState.Phase == GamePhase.Drafting);

            Log.Info($"ClientMatch-{MatchId}", "OnServerOtherPlayerDiscarded");

            IsOpDiscarded = true;

            OnDraftingOtherPlayerDiscarded.Invoke();
        }

        // -------------------------------- Playing

        public void OnServerStartPlayingPhase(GameState gameState)
        {
            Assert.True(GameState.Phase == GamePhase.Drafting);

            Log.Info($"ClientMatch-{MatchId}", $"Match started playing phase");

            GameState = gameState;

            OnPlayingPhaseStarted.Invoke();
        }

        public async Task<bool> RequestPlayToken(TokenIntentMessage message)
        {
            Assert.True(GameState.Phase == GamePhase.Playing);
            Log.Info($"ClientMatch-{MatchId}", $"Sending token intent");

            var response = await connection.Client_RequestMatchPlayToken(MatchId, message);

            return response;
        }

        public void OnServerTokenResolved(TokenResolutionMessage message)
        {
            Assert.True(GameState.Phase == GamePhase.Playing);

            Log.Info($"ClientMatch-{MatchId}", $"Token resolution received for TokenId={message.TokenId}");

            var entry = tokenRegistry.GetEntryOrThrow(message.TokenId);
            entry.Logic.Apply(GameState, message.Resolution, message.SourceClientIndex);
            GameState.Clients[message.SourceClientIndex].Tokens.RemoveAll(t => t.InstanceId == message.TokenInstanceId);

            OnTokenResolved.Invoke(message);
        }

        public async Task MessageAnimatedTokenResolved()
        {
            Assert.True(GameState.Phase == GamePhase.Playing);

            Log.Info($"ClientMatch-{MatchId}", $"Sending token animated");

            await connection.Client_MessageMatchAnimatedTokenResolved(MatchId);
        }

        public void OnServerNextTurn(GameState gameState)
        {
            Assert.True(GameState.Phase == GamePhase.Playing);

            Log.Info($"ClientMatch-{MatchId}", $"OnServerNextTurn, next clientIndex={gameState.TurnClientIndex}");

            GameState = gameState;

            OnNextTurnStarted.Invoke();
        }

        public void OnServerNextRound(GameState gameState, GameSimulationMatchTrace trace)
        {
            Assert.True(GameState.Phase == GamePhase.Playing);

            Log.Info($"ClientMatch-{MatchId}", $"OnServerNextRound, round={gameState.Round}");

            GameState = gameState;
            LastDiceTraces = trace;
            IsOpDiscarded = false;

            OnRoundEnded.Invoke();
        }
    }
}
