using Drakken.Common.Utility;
using Unity.Netcode.Transports.UTP;
using UnityEngine;

namespace Drakken
{
    public class Server : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] public UnityTransport transport;

        [Header("Config")]
        [SerializeField] public string hostAddress = "0.0.0.0";
        [SerializeField] public ushort hostPort = 7777;

        private GameServer server;

        public void Init()
        {
            server = new GameServer();
            server.Host(hostAddress, hostPort);
        }
    }
}
