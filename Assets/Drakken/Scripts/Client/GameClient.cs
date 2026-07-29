using Drakken.Client.States;
using Drakken.Client.World;
using Drakken.Common.Utility;
using Drakken.Config;
using Drakken.Domain.Tokens;
using System.Threading.Tasks;
using UnityEngine;

namespace Drakken.Client
{
    public class GameClient : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private AssetDatabase assets;
        [SerializeField] private new CameraController camera;


        public ClientMatch Match { get; private set; }
        public TokenRegistry TokenRegistry { get; private set; }
        public AssetDatabase Assets => assets;
        public CameraController Camera => camera;

        private readonly TitleClientState titleState = new();
        private readonly DraftingClientState draftingState = new();
        private readonly PlayingClientState playingState = new();
        private ClientState currentState = null;
        private ClientStateType currentStateType = ClientStateType.None;
        public bool IsConnecting { get; private set; }
        public bool IsConnected { get; private set; }
        public bool IsInMatch => Match != null;
        public SceneShared Shared { get; private set; } = new();

        private void Awake()
        {
            TokenRegistry = TokenRegistryBuilder.BuildClientRegistry(assets.GetTokenPrefabById);

            titleState.Init(this);
            draftingState.Init(this);
            playingState.Init(this);
        }

        public async Task StartApplication()
        {
            await GotoState(ClientStateType.Title);
        }

        public async Task GotoState(ClientStateType nextStateType)
        {
            if (currentState != null) await currentState.Exit(nextStateType);

            ClientState newState = nextStateType switch
            {
                ClientStateType.Title => titleState,
                ClientStateType.Drafting => draftingState,
                ClientStateType.Playing => playingState,
                _ => throw new System.NotImplementedException(),
            };

            var oldStateType = currentStateType;

            currentState = newState;
            currentStateType = nextStateType;

            await newState.Enter(oldStateType);
        }

        public Task<bool> Connect()
        {
            Assert.True(!IsConnecting && !IsConnected);

            var config = NetworkConfigLoader.Load();

            Log.Info("Client", $"Connecting to game server at {config.address}:{config.port}...");

            // Listen to connect / disconnect events
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

            // Start networking client
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
