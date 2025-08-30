using Drakken.Common.Utility;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;
using Drakken.ApplicationNetworking;
using Drakken.Common.Data;

namespace Drakken.ApplicationServer
{
    public class ServerMatch
    {
        public static ulong nextMatchId = 1;

        private Server server;
        public ulong MatchId { get; private set; }
        private ulong[] playerClientIds;
        private ulong connectedPlayerCount;
        private ulong readyPlayerCount;
        private GameState gameState;
        private bool isStarted;
        private ClientRpcParams broadcastRpcParams;

        public ServerMatch(Server server)
        {
            this.server = server;
            MatchId = nextMatchId++;
            playerClientIds = new ulong[2];
            connectedPlayerCount = 0;
            readyPlayerCount = 0;
            isStarted = false;
            Log.Info($"ServerMatch-{MatchId}", $"Created new match");
        }

        public void OnRequestJoinMatch(ulong clientId)
        {
            JoinMatchResponse response;

            if (isStarted || connectedPlayerCount >= 2)
            {
                response = new() { Success = false };
                Log.Info($"ServerMatch-{MatchId}", $"Denied player clientId={clientId} join request, match is full");
                server.NetworkingRouter.RespondJoinMatch(response, Networking.ToClient(clientId));
                return;
            }

            ulong playerIndex = connectedPlayerCount++;
            playerClientIds[playerIndex] = clientId;
            response = new() { Success = true, MatchId = MatchId, PlayerIndex = playerIndex };
            Log.Info($"ServerMatch-{MatchId}", $"Accepted player clientId={clientId} join request, assigned playerIndex={response.PlayerIndex}");
            server.NetworkingRouter.RespondJoinMatch(response, Networking.ToClient(clientId));
        }

        public void OnReadyInMatch(ulong clientId)
        {
            Assert.False(isStarted);
            Log.Info($"ServerMatch-{MatchId}", $"Player clientId={clientId} is ready");
            readyPlayerCount++;
            if (readyPlayerCount == 2) StartGame();
        }

        private void StartGame()
        {
            Assert.True(connectedPlayerCount == 2 && readyPlayerCount == 2 && !isStarted);
            Log.Info($"ServerMatch-{MatchId}", $"All players are ready, starting match...");

            gameState = new();

            for (int i = 0; i < 2; i++)
            {
                for (int d = 0; d < 4; d++)
                {
                    DiceInstance die = new() { Sides = 6 };
                    die.Roll();
                    gameState.Players[i].Dice.Add(die);
                }
            }

            isStarted = true;

            broadcastRpcParams = Networking.ToClients(playerClientIds);
            server.NetworkingRouter.MessageGameStarted(gameState, broadcastRpcParams);
        }
    }

    public class Server : MonoBehaviour
    {
        public static Server Singleton { get; private set; }

        [Header("References")]
        [SerializeField] public UnityTransport transport;

        [Header("Config")]
        [SerializeField] public string hostAddress = "0.0.0.0";
        [SerializeField] public ushort hostPort = 7777;

        public ServerNetworkingRouter NetworkingRouter { get; private set; } = null;

        private ServerMatch currentMatch;

        private void Awake()
        {
            Singleton = this;
        }

        public void StartApplication()
        {
            Log.Info("Server", $"Starting game server at {hostAddress}:{hostPort}");

            NetworkingRouter = new ServerNetworkingRouter(this);

            UnityTransport transport = NetworkManager.Singleton.NetworkConfig.NetworkTransport as UnityTransport;
            transport.ConnectionData.Address = hostAddress;
            transport.ConnectionData.Port = hostPort;
            NetworkManager.Singleton.StartServer();
        }

        public void OnRequestJoinMatch(ulong playerId)
        {
            currentMatch ??= new ServerMatch(this);
            currentMatch.OnRequestJoinMatch(playerId);
        }

        public ServerMatch GetMatch(ulong matchId)
        {
            if (currentMatch != null && currentMatch.MatchId == matchId) return currentMatch;
            Log.Error("Server", $"No match found with matchId={matchId}");
            return null;
        }
    }

    public class ServerNetworkingRouter
    {
        public static ServerNetworkingRouter Singleton { get; private set; }
        private readonly Server server;

        public ServerNetworkingRouter(Server server)
        {
            this.server = server;
            Singleton = this;
        }

        public void OnRequestJoinMatch(ulong clientId)
            => server.OnRequestJoinMatch(clientId);

        public void RespondJoinMatch(JoinMatchResponse res, ClientRpcParams clients)
            => Networking.Singleton.RespondJoinMatch_ClientRpc(res, clients);

        public void OnMessageReadyInMatch(ulong matchId, ulong clientId)
            => server.GetMatch(matchId).OnReadyInMatch(clientId);

        public void MessageGameStarted(GameState gameState, ClientRpcParams clients)
            => Networking.Singleton.MessageGameStarted_ClientRpc(gameState, clients);
    }
}
