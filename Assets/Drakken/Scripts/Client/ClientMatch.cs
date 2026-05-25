using Drakken.Domain;
using Drakken.Common.Utility;
using Drakken.Networking;
using UnityEngine.Events;

namespace Drakken.Client
{
    public class ClientMatch
    {
        public UnityAction GameStarted = delegate { };

        private readonly GameClient client;
        public ulong clientIndex { get; private set; }
        public GameState GameState { get; private set; }
        public ulong MatchId { get; private set; }
        public bool IsReady { get; private set; } = false;
        public bool IsStarted { get; private set; } = false;

        public ClientMatch(GameClient client, JoinMatchResponse response)
        {
            Assert.True(response.Success);
            this.client = client;
            MatchId = response.MatchId;
            clientIndex = response.ClientIndex;
            IsReady = false;
            IsStarted = false;
            Log.Info("ClientMatch", $"Joined match matchId={response.MatchId} with clientIndex={clientIndex}");
        }

        public void SetReady()
        {
            Assert.False(IsReady);
            IsReady = true;
            client.Connection.MessageReadyInMatch(MatchId);
        }

        public void OnGameStarted(GameState gameState)
        {
            Assert.True(IsReady && !IsStarted);
            GameState = gameState;
            IsStarted = true;
            Log.Info("ClientMatch", $"Game started");
            GameStarted.Invoke();
        }
    }
}