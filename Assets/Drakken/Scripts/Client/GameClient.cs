using Drakken.Client.States;
using Drakken.Common.Utility;
using Drakken.Config;
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
        [Header("References")]
        [SerializeField] private AssetDatabase assets;

        public AssetDatabase Assets => assets;
        public ClientMatch Match { get; private set; }
        public TokenRegistry TokenRegistry { get; private set; }
        private ClientState currentState = null;
        public bool IsConnecting { get; private set; }
        public bool IsConnected { get; private set; }
        public bool IsInMatch => Match != null;

        private void Awake()
        {
            TokenRegistry = TokenRegistryBuilder.BuildClientRegistry(assets.GetTokenPrefabById);
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

            var config = NetworkConfigLoader.Load();

            Log.Info("Client", $"Connecting to game server at {config.address}:{config.port}...");

            IsConnecting = true;
            IsConnected = false;
            var tcs = new TaskCompletionSource<bool>();

            void OnConnected(ulong clientId)
            {
                Log.Info("Client", $"Connected as clientId={clientId}");
                IsConnecting = false;
                IsConnected = true;
                tcs.TrySetResult(true);
            }

            void OnDisconnected(ulong clientId)
            {
                Log.Info("Client", $"Disconnected as clientId={clientId}");
                GameEntrypoint.Singleton.Connection.RemoveClientListeners(OnConnected, OnDisconnected);
                IsConnecting = false;
                IsConnected = false;
                tcs.TrySetResult(false);
            }

            GameEntrypoint.Singleton.Connection.AddClientListeners(OnConnected, OnDisconnected);
            GameEntrypoint.Singleton.Connection.StartClient(this, config.address, config.port);

            return tcs.Task;
        }

        public async Task<bool> JoinMatch()
        {
            Assert.True(IsConnected && !IsInMatch);

            Log.Info("Client", "Requesting to join match...");

            var response = await GameEntrypoint.Singleton.Connection.Client_RequestJoinMatch();

            if (response.Success)
            {
                Match = new ClientMatch(response.MatchId, (int)response.ClientIndex);
                return true;
            }

            return false;
        }
    }
}
