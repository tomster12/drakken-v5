using Drakken.Common.Utility;
using Drakken.Domain.Tokens;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;

namespace Drakken.Server
{
    public class GameServer : MonoBehaviour
    {
        public static GameServer Singleton { get; private set; }

        [Header("References")]
        [SerializeField] private UnityTransport transport;

        [Header("Config")]
        [SerializeField] private string hostAddress = "0.0.0.0";
        [SerializeField] private ushort hostPort = 7777;

        public ServerConnection Connection { get; private set; }
        public TokenRegistry TokenRegistry { get; private set; }
        private ServerMatch currentMatch;

        private void Awake()
        {
            Singleton = this;
            TokenRegistry = TokenRegistryBuilder.Build();
        }

        public void StartApplication()
        {
            Log.Info("Client", $"Starting game server at {hostAddress}:{hostPort}");

            var transport = (UnityTransport)NetworkManager.Singleton.NetworkConfig.NetworkTransport;
            
            transport.ConnectionData.Address = hostAddress;
            transport.ConnectionData.Port = hostPort;
            NetworkManager.Singleton.StartServer();

            Connection = new ServerConnection(this);
        }

        public ServerMatch GetOrCreateMatch()
        {
            return currentMatch ??= new ServerMatch(this);
        }

        public ServerMatch GetMatch(ulong matchId)
        {
            Assert.True(currentMatch?.MatchId == matchId, $"No match with matchId={matchId}");
            return currentMatch;
        }
        
        public ServerMatch GetMatchForClient(ulong clientId)
        {
            return currentMatch;
        }
    }
}
