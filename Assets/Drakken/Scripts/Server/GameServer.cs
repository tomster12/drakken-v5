using Drakken.Utility;
using Drakken.Config;
using Drakken.Gameplay.Tokens;
using Drakken.Domain.Networking;
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
        private GameEntrypoint entrypoint;

        public void Init(GameEntrypoint entrypoint)
        {
            this.entrypoint = entrypoint;
            entrypoint.TokenRegistry = TokenRegistryBuilder.BuildServerRegistry();
        }

        public void StartApplication()
        {
            var config = NetworkConfigLoader.Load();

            Log.Info("Server", $"Starting game server at {config.address}:{config.port}");

            entrypoint.Connection.StartServer(config.address, config.port);
        }

        public JoinMatchResponse OnRequestJoinMatch(ulong clientId)
        {
            currentMatch ??= new ServerMatch(entrypoint.TokenRegistry, entrypoint.Connection, entrypoint.SceneLayout.Dice);

            return currentMatch.OnClientRequestJoin(clientId);
        }

        public ServerMatch GetMatch(ulong matchId)
        {
            Assert.True(currentMatch.IsMatch(matchId), $"No match with matchId={matchId}");
            return currentMatch;
        }
    }
}
