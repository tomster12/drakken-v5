using Drakken.Common;
using Drakken.Common.Utility;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;

namespace Drakken
{
    public class GameServer
    {
        public static GameServer Singleton { get; private set; }

        public GameServer()
        {
            Singleton = this;
        }

        public void Host(string address, ushort port)
        {
            Log.Info("GameServer", $"Starting game server at {address}:{port}");

            UnityTransport transport = NetworkManager.Singleton.NetworkConfig.NetworkTransport as UnityTransport;
            transport.ConnectionData.Address = address;
            transport.ConnectionData.Port = port;
            NetworkManager.Singleton.StartServer();
        }

        public bool OnRequestJoinGame(ulong clientId)
        {
            Log.Info("GameServer", $"Client {clientId} requested to join the game");

            return true;
        }
    }
}
