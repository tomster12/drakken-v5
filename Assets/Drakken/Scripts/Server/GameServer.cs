using Drakken.Common.Utility;
using Drakken.Config;
using Drakken.Domain.Networking;
using Drakken.Domain.Tokens;
using Unity.Netcode.Transports.UTP;
using UnityEngine;

namespace Drakken.Server
{
    public interface IGameServer
    {
        JoinMatchResponse OnRequestJoinMatch(ulong clientId);
        ServerMatch GetMatch(ulong matchId);
    }

    public class GameServer : MonoBehaviour, IGameServer
    {
        [Header("References")]
        [SerializeField] private UnityTransport transport;

        private ServerMatch currentMatch;

        private void Awake()
        {
            GameEntrypoint.Singleton.TokenRegistry = TokenRegistryBuilder.BuildServerRegistry();
        }

        public void StartApplication()
        {
            var config = NetworkConfigLoader.Load();

            Log.Info("Server", $"Starting game server at {config.address}:{config.port}");

            GameEntrypoint.Singleton.Connection.StartServer(config.address, config.port);
        }

        public JoinMatchResponse OnRequestJoinMatch(ulong clientId)
        {
            currentMatch ??= new ServerMatch(GameEntrypoint.Singleton.TokenRegistry);

            return currentMatch.OnClientRequestJoin(clientId);
        }

        public ServerMatch GetMatch(ulong matchId)
        {
            Assert.True(currentMatch.IsMatch(matchId), $"No match with matchId={matchId}");
            return currentMatch;
        }
    }
}
