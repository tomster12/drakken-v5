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
        public TokenRegistry TokenRegistry { get; }
    }

    public class GameServer : MonoBehaviour, IGameServer
    {
        [Header("References")]
        [SerializeField] private UnityTransport transport;

        public TokenRegistry TokenRegistry { get; private set; }
        private ServerMatch currentMatch;

        private void Awake()
        {
            TokenRegistry = TokenRegistryBuilder.BuildRegistry();
        }

        public void StartApplication()
        {
            var config = NetworkConfigLoader.Load();

            Log.Info("Server", $"Starting game server at {config.address}:{config.port}");

            GameEntrypoint.Singleton.Connection.StartServer(this, config.address, config.port);
        }

        public JoinMatchResponse OnRequestJoinMatch(ulong clientId)
        {
            currentMatch ??= new ServerMatch(TokenRegistry);

            return currentMatch.OnClientRequestJoin(clientId);
        }

        public ServerMatch GetMatch(ulong matchId)
        {
            Assert.True(currentMatch.IsMatch(matchId), $"No match with matchId={matchId}");
            return currentMatch;
        }
    }
}
