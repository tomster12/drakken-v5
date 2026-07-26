using Drakken.Common.Utility;
using Drakken.Config;
using Drakken.Domain.Networking;
using Drakken.Domain.Tokens;
using Drakken.Networking;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;

namespace Drakken.Server
{
    public class GameServer : MonoBehaviour
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

            Log.Info("Client", $"Starting game server at {config.address}:{config.port}");

            GameEntrypoint.Singleton.Connection.StartServer(this, config.address, config.port);
        }

        public JoinMatchResponse OnRequestJoinMatch(ulong clientId)
        {
            currentMatch ??= new ServerMatch(this);

            return currentMatch.OnRequestJoin(clientId);
        }

        public ServerMatch GetMatch(ulong matchId)
        {
            Assert.True(currentMatch.IsMatch(matchId), $"No match with matchId={matchId}");
            return currentMatch;
        }
    }
}
