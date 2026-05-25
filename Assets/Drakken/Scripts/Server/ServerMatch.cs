using Drakken.Common.Utility;
using Unity.Netcode;
using Drakken.Domain;
using Drakken.Networking;

namespace Drakken.Server
{

    public class ServerMatch
    {
        public static ulong nextMatchId = 1;

        private GameServer server;
        public ulong MatchId { get; private set; }
        private ulong[] clientClientIds;
        private ulong connectedClientCount;
        private ulong readyClientCount;
        private GameState gameState;
        private bool isStarted;
        private ClientRpcParams broadcastRpcParams;

        public ServerMatch(GameServer server)
        {
            this.server = server;
            MatchId = nextMatchId++;
            clientClientIds = new ulong[2];
            connectedClientCount = 0;
            readyClientCount = 0;
            isStarted = false;
            Log.Info($"ServerMatch-{MatchId}", $"Created new match");
        }

        public void OnRequestJoinMatch(ulong clientId)
        {
            JoinMatchResponse response;

            if (isStarted || connectedClientCount >= 2)
            {
                response = new() { Success = false };
                Log.Info($"ServerMatch-{MatchId}", $"Denied client clientId={clientId} join request, match is full");
                server.Connection.RespondJoinMatch(response, clientId);
                return;
            }

            ulong clientIndex = connectedClientCount++;
            clientClientIds[clientIndex] = clientId;
            response = new() { Success = true, MatchId = MatchId, ClientIndex = clientIndex };
            Log.Info($"ServerMatch-{MatchId}", $"Accepted client clientId={clientId} join request, assigned clientIndex={response.ClientIndex}");
            server.Connection.RespondJoinMatch(response, clientId);
        }

        public void OnClientReady(ulong clientId)
        {
            Assert.False(isStarted);
            Log.Info($"ServerMatch-{MatchId}", $"Client clientId={clientId} is ready");
            readyClientCount++;
            if (readyClientCount == 2) StartGame();
        }

        private void StartGame()
        {
            Assert.True(connectedClientCount == 2 && readyClientCount == 2 && !isStarted);
            Log.Info($"ServerMatch-{MatchId}", $"All clients are ready, starting match...");

            gameState = new();

            for (int i = 0; i < 2; i++)
            {
                for (int d = 0; d < 4; d++)
                {
                    DiceInstance die = DiceInstance.Create(sides: 6);
                    die.Roll();

                    gameState.Clients[i].Dice.Add(die);
                }
            }

            isStarted = true;

            server.Connection.MessageMatchStarted(gameState, clientClientIds);
        }
    }
}
