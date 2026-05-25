using Drakken.Common.Utility;
using System.Threading.Tasks;
using Unity.Netcode.Transports.UTP;
using Unity.Netcode;
using UnityEngine;
using Drakken.Networking;
using UnityEngine.Events;
using Drakken.Domain;
using Drakken.Client.States;
using System.Collections.Generic;

namespace Drakken.Client
{
    public class GameClient : MonoBehaviour
    {
        public static GameClient Singleton { get; private set; }

        [Header("Config")]
        [SerializeField] public string serverAddress = "0.0.0.0";
        [SerializeField] public ushort serverPort = 7777;

        public bool IsConnecting { get; private set; } = false;
        public bool IsConnected { get; private set; } = false;
        public ClientConnection Connection { get; private set; } = null;
        public ClientMatch Match { get; private set; } = null;
        public bool IsInMatch => Match != null;

        private Dictionary<ClientStateType, ClientState> states;
        private ClientStateType currentStateType = ClientStateType.None;
        private ClientState currentState = null;

        private void Awake()
        {
            Singleton = this;
        }

        public async Task StartApplication()
        {
            states = new()
            {
              { ClientStateType.Connecting, new ClientConnectingState() },
              { ClientStateType.Playing, new ClientPlayingState() }
            };

            states[ClientStateType.Connecting].Init(this);
            states[ClientStateType.Playing].Init(this);

            currentState = null;
            currentStateType = ClientStateType.None;
            await GotoState(ClientStateType.Connecting);
        }

        public async Task GotoState(ClientStateType stateType)
        {
            Assert.False(currentStateType == stateType);
            Assert.False(stateType == ClientStateType.None);

            states.TryGetValue(stateType, out var newState);
            Assert.NotNull(newState);

            currentStateType = stateType;
            currentState = newState;

            await currentState.Enter();
        }

        public Task<bool> Connect()
        {
            Assert.True(!IsConnecting && !IsConnected);
            Log.Info("Client", $"Connecting to game server at {serverAddress}:{serverPort}...");

            IsConnecting = true;
            IsConnected = false;
            Connection = null;
            var tcs = new TaskCompletionSource<bool>();

            void OnConnected(ulong clientId)
            {
                Log.Info("Client", $"Client connected clientId={clientId}");

                NetworkManager.Singleton.OnClientConnectedCallback -= OnConnected;
                NetworkManager.Singleton.OnClientDisconnectCallback -= OnDisconnected;

                IsConnecting = false;
                IsConnected = true;
                Connection = new ClientConnection(this);
                tcs.TrySetResult(true);
            }

            void OnDisconnected(ulong clientId)
            {
                Log.Info("Client", $"Client disconnected clientId={clientId}");
                NetworkManager.Singleton.OnClientConnectedCallback -= OnConnected;
                NetworkManager.Singleton.OnClientDisconnectCallback -= OnDisconnected;

                IsConnecting = false;
                IsConnected = false;
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

            var response = await Connection.RequestJoinMatch();

            if (response.Success)
            {
                Match = new ClientMatch(this, response);
                return true;
            }

            return false;
        }
    }

    public enum ClientStateType { None, Connecting, Playing }
}
