using Drakken.Common;
using Drakken.Common.Utility;
using System.Threading.Tasks;
using Unity.Netcode.Transports.UTP;
using Unity.Netcode;
using UnityEngine;

namespace Drakken
{
    public class Client : MonoBehaviour
    {
        public static Client Singleton { get; private set; }

        [Header("Config")]
        [SerializeField] public string serverAddress = "0.0.0.0";
        [SerializeField] public ushort serverPort = 7777;

        private bool isConnecting = false;

        public async Task Init()
        {
            Singleton = this;

            var res = await Connect();
            if (!res)
            {
                Log.Error("Client", "Failed to connect to server");
                return;
            }

            await RequestJoinGame();
        }

        public Task<bool> Connect()
        {
            Log.Info("Client", $"Connecting to game server at {serverAddress}:{serverPort}");
            Assert.True(!isConnecting, "Client is already connecting");

            isConnecting = true;
            var tcs = new TaskCompletionSource<bool>();

            void OnConnected(ulong clientID)
            {
                NetworkManager.Singleton.OnClientConnectedCallback -= OnConnected;
                NetworkManager.Singleton.OnClientDisconnectCallback -= OnDisconnected;
                Log.Info("Client", $"Client connected ({clientID})");
                isConnecting = false;
                tcs.TrySetResult(true);
            }

            void OnDisconnected(ulong clientID)
            {
                NetworkManager.Singleton.OnClientConnectedCallback -= OnConnected;
                NetworkManager.Singleton.OnClientDisconnectCallback -= OnDisconnected;
                Log.Info("Client", $"Client disconnected ({clientID})");
                isConnecting = false;
                tcs.TrySetResult(false);
            }

            NetworkManager.Singleton.OnClientConnectedCallback += OnConnected;
            NetworkManager.Singleton.OnClientDisconnectCallback += OnDisconnected;

            var transport = (UnityTransport)NetworkManager.Singleton.NetworkConfig.NetworkTransport;
            transport.ConnectionData.Address = serverAddress;
            transport.ConnectionData.Port = serverPort;
            NetworkManager.Singleton.StartClient();

            return tcs.Task;
        }

        public async Task<bool> RequestJoinGame()
        {
            Log.Info("Client", "Requesting to join game...");
            var response = await Networking.Singleton.RequestJoinGameAsync();
            Log.Info("Client", $"Join game request response: {response}");
            return response;
        }
    }
}
