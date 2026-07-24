using Drakken.Client.States;
using Drakken.Common.Utility;
using Drakken.Domain.Tokens;
using Drakken.Networking;
using System.Threading.Tasks;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;

namespace Drakken.Client
{
    public class GameClient : MonoBehaviour
    {
        [Header("Config")]
        [SerializeField] private string serverAddress = "127.0.0.1";
        [SerializeField] private ushort serverPort = 7777;

        [Header("Assets")]
        [SerializeField] private AssetDatabase assets;

        public AssetDatabase Assets => assets;
        public ClientMatch Match { get; private set; }
        public TokenRegistry TokenRegistry { get; private set; }
        private ClientState currentState = null;
        public bool IsConnecting { get; private set; }
        public bool IsConnected => NetworkManager.Singleton.IsConnectedClient;
        public bool IsInMatch => Match != null;

        private void Awake()
        {
            TokenRegistry = TokenRegistryBuilder.BuildClientRegistry(assets.GetTokenPrefab);
        }

        public async Task StartApplication()
        {
            await GotoState(new ConnectingClientState());
        }

        public async Task GotoState(ClientState newState)
        {
            if (currentState != null) await currentState.Exit();
            currentState = newState;
            newState.Init(this);
            await newState.Enter();
        }

        public Task<bool> Connect()
        {
            Assert.True(!IsConnecting && !IsConnected);
            Log.Info("Client", $"Connecting to game server at {serverAddress}:{serverPort}...");

            IsConnecting = true;
            var tcs = new TaskCompletionSource<bool>();

            void OnConnected(ulong clientId)
            {
                Log.Info("Client", $"Connected as clientId={clientId}");

                NetworkManager.Singleton.OnClientConnectedCallback -= OnConnected;
                NetworkManager.Singleton.OnClientDisconnectCallback -= OnDisconnected;

                IsConnecting = false;
                GameConnection.Singleton.SetClient(this);
                tcs.TrySetResult(true);
            }

            void OnDisconnected(ulong clientId)
            {
                Log.Info("Client", $"Disconnected as clientId={clientId}");
                NetworkManager.Singleton.OnClientConnectedCallback -= OnConnected;
                NetworkManager.Singleton.OnClientDisconnectCallback -= OnDisconnected;

                IsConnecting = false;
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

        public async Task<bool> JoinMatch()
        {
            Assert.True(IsConnected && !IsInMatch);
            Log.Info("Client", "Requesting to join match...");

            var response = await GameConnection.Singleton.Client_RequestJoinMatch();

            if (response.Success)
            {
                Match = new ClientMatch(response.MatchId, (int)response.ClientIndex);
                return true;
            }

            return false;
        }
    }
}
