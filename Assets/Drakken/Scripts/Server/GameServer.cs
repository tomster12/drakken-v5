using Drakken.Common.Utility;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;

namespace Drakken.Server
{
    public class GameServer : MonoBehaviour
    {
        public static GameServer Singleton { get; private set; }

        [Header("References")]
        [SerializeField] public UnityTransport transport;

        [Header("Config")]
        [SerializeField] public string hostAddress = "0.0.0.0";
        [SerializeField] public ushort hostPort = 7777;

        public ServerConnection Connection { get; private set; } = null;
        private ServerMatch currentMatch;

        private void Awake()
        {
            Singleton = this;
        }

        public void StartApplication()
        {
            Log.Info("Server", $"Starting game server at {hostAddress}:{hostPort}");

            Connection = new ServerConnection(this);

            UnityTransport transport = NetworkManager.Singleton.NetworkConfig.NetworkTransport as UnityTransport;
            transport.ConnectionData.Address = hostAddress;
            transport.ConnectionData.Port = hostPort;
            NetworkManager.Singleton.StartServer();
        }

        public void OnRequestJoinMatch(ulong clientId)
        {
            currentMatch ??= new ServerMatch(this);
            currentMatch.OnRequestJoinMatch(clientId);
        }

        public ServerMatch GetMatch(ulong matchId)
        {
            if (currentMatch != null && currentMatch.MatchId == matchId) return currentMatch;
            Log.Error("Server", $"No match found with matchId={matchId}");
            return null;
        }
    }
}
