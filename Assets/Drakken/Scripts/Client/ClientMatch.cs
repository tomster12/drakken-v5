using Drakken.Domain;
using Drakken.Common.Utility;
using System;
using Drakken.Domain.Networking;
using System.Threading.Tasks;
using System.Linq;

namespace Drakken.Client
{
    public class ClientMatch
    {
        public Action OnOtherPlayerJoined = delegate { };
        public Action OnOtherPlayerReady = delegate { };
        public Action OnDraftingPhaseStarted = delegate { };
        public Action OnOtherPlayerDiscarded = delegate { };
        public Action OnPlayingPhaseStarted = delegate { };

        public GameState GameState { get; private set; }
        public ulong MatchId { get; private set; }
        public int ClientIndex { get; private set; }

        public bool IsOpJoined { get; private set; }
        public bool IsOpReady { get; private set; }
        public bool IsReady { get; private set; }
        public bool IsOpDiscarded { get; private set; }
        public bool IsMyTurn => GameState?.TurnClientIndex == ClientIndex;
        public int OpClientIndex => 1 - ClientIndex;

        // -------------------------------- Setup

        public ClientMatch(ulong matchId, int clientIndex)
        {
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
            GameEntrypoint.Singleton.Connection.Client_MessageMatchClientReady(MatchId);
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

        public void OnServerStartDraftingPhase(GameState gameState)
        {
            // TODO: Allow coming to drafting after round end
            Assert.True(GameState.Phase == GamePhase.NotStarted);
            Log.Info($"ClientMatch-{MatchId}", $"Match started drafting phase");

            GameState = gameState;
            OnDraftingPhaseStarted.Invoke();
        }

        public async Task<bool> RequestDraftDiscard(DraftDiscardMessage message)
        {
            Assert.True(GameState.Phase == GamePhase.Drafting);
            Log.Info($"ClientMatch-{MatchId}", $"Sending draft discard");

            var response = await GameEntrypoint.Singleton.Connection.Client_RequestMatchDraftDiscard(MatchId, message);

            // Update game state with removed tokens
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
            OnOtherPlayerDiscarded.Invoke();
        }

        // -------------------------------- Playing

        public void OnServerStartPlayingPhase(GameState gameState)
        {
            Assert.True(GameState.Phase == GamePhase.Drafting);
            Log.Info($"ClientMatch-{MatchId}", $"Match started prafting phase");

            GameState = gameState;
            OnPlayingPhaseStarted.Invoke();
        }
    }
}