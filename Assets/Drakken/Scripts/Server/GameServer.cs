using Drakken.Common.Utility;
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
        public static GameServer Singleton { get; private set; }

        [Header("References")]
        [SerializeField] private UnityTransport transport;

        [Header("Config")]
        [SerializeField] private string hostAddress = "0.0.0.0";
        [SerializeField] private ushort hostPort = 7777;

        public TokenRegistry TokenRegistry { get; private set; }
        private ServerMatch currentMatch;

        private void Awake()
        {
            Singleton = this;
            TokenRegistry = TokenRegistryBuilder.BuildRegistry();
        }

        public void StartApplication()
        {
            Log.Info("Client", $"Starting game server at {hostAddress}:{hostPort}");

            var transport = (UnityTransport)NetworkManager.Singleton.NetworkConfig.NetworkTransport;

            transport.ConnectionData.Address = hostAddress;
            transport.ConnectionData.Port = hostPort;
            NetworkManager.Singleton.StartServer();

            GameConnection.Singleton.SetServer(this);
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
