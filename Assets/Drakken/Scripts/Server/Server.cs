using Drakken.Common.Utility;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;

namespace Drakken
{
    public class Server : MonoBehaviour
    {
        public static Server Singleton { get; private set; }

        [Header("References")]
        [SerializeField] public UnityTransport transport;

        [Header("Config")]
        [SerializeField] public string hostAddress = "0.0.0.0";
        [SerializeField] public ushort hostPort = 7777;

        public void Init()
        {
            Singleton = this;
            Host();
        }

        public void Host()
        {
            Log.Info("Server", $"Starting game server at {hostAddress}:{hostPort}");

            UnityTransport transport = NetworkManager.Singleton.NetworkConfig.NetworkTransport as UnityTransport;
            transport.ConnectionData.Address = hostAddress;
            transport.ConnectionData.Port = hostPort;
            NetworkManager.Singleton.StartServer();
        }

        public bool OnRequestJoinGame(ulong clientId)
        {
            Log.Info("Server", $"Client {clientId} requested to join the game");

            return true;
        }
    }
}
