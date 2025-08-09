using Drakken.Common;
using Drakken.Common.Utility;
using System.Threading.Tasks;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;

namespace Drakken
{
    public class GameClient
    {
        public static GameClient Singleton { get; private set; }

        private bool isConnecting = false;

        public GameClient()
        {
            Singleton = this;
        }

        public Task<bool> Connect(string address, ushort port)
        {
            Log.Info("GameClient", $"Connecting to game server at {address}:{port}");
            Assert.True(!isConnecting, "GameClient is already connecting");

            isConnecting = true;
            var tcs = new TaskCompletionSource<bool>();

            void OnConnected(ulong clientID)
            {
                NetworkManager.Singleton.OnClientConnectedCallback -= OnConnected;
                NetworkManager.Singleton.OnClientDisconnectCallback -= OnDisconnected;
                Log.Info("GameClient", $"Client connected ({clientID})");
                isConnecting = false;
                tcs.TrySetResult(true);
            }

            void OnDisconnected(ulong clientID)
            {
                NetworkManager.Singleton.OnClientConnectedCallback -= OnConnected;
                NetworkManager.Singleton.OnClientDisconnectCallback -= OnDisconnected;
                Log.Info("GameClient", $"Client disconnected ({clientID})");
                isConnecting = false;
                tcs.TrySetResult(false);
            }

            NetworkManager.Singleton.OnClientConnectedCallback += OnConnected;
            NetworkManager.Singleton.OnClientDisconnectCallback += OnDisconnected;

            var transport = (UnityTransport)NetworkManager.Singleton.NetworkConfig.NetworkTransport;
            transport.ConnectionData.Address = address;
            transport.ConnectionData.Port = port;
            NetworkManager.Singleton.StartClient();

            return tcs.Task;
        }

        public async Task<bool> RequestJoinGame()
        {
            Log.Info("GameClient", "Requesting to join game...");
            var response = await GameNetwork.Singleton.RequestJoinGameAsync();
            Log.Info("GameClient", $"Join game request response: {response}");
            return response;
        }
    }
}
