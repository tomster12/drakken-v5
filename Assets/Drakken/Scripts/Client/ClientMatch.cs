using Drakken.Domain;
using Drakken.Common.Utility;
using Drakken.Networking;
using System;
using Drakken.Domain.Networking;

namespace Drakken.Client
{
    public class ClientMatch
    {
        public Action DraftingPhaseStarted = delegate { };
        public Action PlayingPhaseStarted = delegate { };

        public ulong MatchId { get; private set; }
        public int ClientIndex { get; private set; }
        public GameState GameState { get; private set; }
        public bool IsReadiedUp { get; private set; }
        public bool IsStarted { get; private set; }
        public bool IsMyTurn => GameState?.TurnClientIndex == ClientIndex;

        public ClientMatch(ulong matchId, int clientIndex)
        {
            MatchId = matchId;
            ClientIndex = clientIndex;

            Log.Info($"ClientMatch-{MatchId}", $"Joined match as clientIndex={ClientIndex}");
        }

        public void SetReady()
        {
            Assert.False(IsReadiedUp);

            Log.Info($"ClientMatch-{MatchId}", $"Ready up");

            IsReadiedUp = true;

            GameEntrypoint.Singleton.Connection.Client_MessageMatchClientReady(MatchId);
        }

        public void OnStartDraftingPhase(GameState gameState)
        {
            Assert.False(IsStarted);

            Log.Info($"ClientMatch-{MatchId}", $"Match started Drafting phase");

            IsStarted = true;
            GameState = gameState;
            DraftingPhaseStarted.Invoke();
        }

        public void SendDraftDiscard(DraftDiscardMessage message)
        {
            Log.Info($"ClientMatch-{MatchId}", $"Sending draft discard");

            GameEntrypoint.Singleton.Connection.Client_MessageMatchDraftDiscard(MatchId, message);
        }

        public void OnStartPlayingPhase(GameState gameState)
        {
            Assert.True(IsStarted);

            Log.Info($"ClientMatch-{MatchId}", $"Match started Playing phase");

            GameState = gameState;
            PlayingPhaseStarted.Invoke();
        }
    }
}
