using Drakken.Common.Utility;
using System.Threading.Tasks;
using Unity.Netcode.Transports.UTP;
using Unity.Netcode;
using UnityEngine;
using Drakken.ApplicationNetworking;
using UnityEngine.Events;
using Drakken.Common.Data;

namespace Drakken.ApplicationPlayer
{
    public class PlayerState
    {
        protected Player player;
        protected PlayerMatch Match => player.Match;
        protected GameState GameState => Match.GameState;

        public void Init(Player player)
        {
            this.player = player;
        }

        public virtual Task Enter() => Task.CompletedTask;

        public virtual Task Update() => Task.CompletedTask;
    }

    public class PlayerStateConnecting : PlayerState
    {
        public override async Task Enter()
        {
            Assert.True(!player.IsConnecting, "Player is already connecting");
            Assert.True(!player.IsInMatch, "Player is already in a match");

            if (!player.IsConnected)
            {
                if (!await player.Connect())
                {
                    Log.Error("PlayerStateConnecting", "Failed to connect to server");
                    return;
                }
            }

            if (!await player.JoinMatch())
            {
                Log.Error("PlayerStateConnecting", "Failed to join match");
                return;
            }

            player.Match.OnGameStartedEvt += async () => await player.GotoState(player.StatePlaying);

            player.Match.SetReady();
        }
    }

    public class PlayerStatePlaying : PlayerState
    {
        public override Task Enter()
        {
            Log.Info("PlayerStatePlaying", "Entered playing state");

            var allDice = GameState.Players[Match.playerIndex].Dice;
            for (int i = 0; i < allDice.Count; i++)
            {
                var dice = allDice[i];
                Log.Info("PlayerStatePlaying", $"Player dice {i}: sides={dice.Sides} value={dice.Value}");
            }

            return Task.CompletedTask;
        }
    }

    public class PlayerMatch
    {
        public UnityAction OnGameStartedEvt = delegate { };

        private Player player;
        private ulong matchId;
        public ulong playerIndex { get; private set; }
        public GameState GameState { get; private set; }
        private bool isReady;
        private bool isStarted;

        public PlayerMatch(Player player, JoinMatchResponse response)
        {
            Assert.True(response.Success);
            this.player = player;
            matchId = response.MatchId;
            playerIndex = response.PlayerIndex;
            isReady = false;
            isStarted = false;
            Log.Info("PlayerMatch", $"Joined match matchId={response.MatchId} with playerIndex={playerIndex}");
        }

        public void SetReady()
        {
            Assert.False(isReady);
            isReady = true;
            player.NetworkingRouter.MessageReadyInMatch(matchId);
        }

        public void OnGameStarted(GameState gameState)
        {
            Assert.True(isReady && !isStarted);
            GameState = gameState;
            isStarted = true;
            Log.Info("PlayerMatch", $"Game started");
            OnGameStartedEvt.Invoke();
        }
    }

    public class Player : MonoBehaviour
    {
        public static Player Singleton { get; private set; }

        [Header("Config")]
        [SerializeField] public string serverAddress = "0.0.0.0";
        [SerializeField] public ushort serverPort = 7777;

        public PlayerStateConnecting StateConnecting { get; private set; }
        public PlayerStatePlaying StatePlaying { get; private set; }
        public PlayerNetworkingRouter NetworkingRouter { get; private set; } = null;
        public PlayerState State { get; private set; } = null;
        public PlayerMatch Match { get; private set; } = null;
        public bool IsConnecting { get; private set; } = false;
        public bool IsConnected => NetworkManager.Singleton.IsConnectedClient;
        public bool IsInMatch => Match != null;

        private void Awake()
        {
            Singleton = this;
        }

        public async Task StartApplication()
        {
            StateConnecting = new PlayerStateConnecting();
            StatePlaying = new PlayerStatePlaying();
            StateConnecting.Init(this);
            StatePlaying.Init(this);

            await GotoState(StateConnecting);
        }

        public async Task GotoState(PlayerState state)
        {
            State = state;
            await State.Enter();
        }

        public Task<bool> Connect()
        {
            Assert.True(!IsConnecting && !IsConnected);
            Log.Info("Player", $"Connecting to game server at {serverAddress}:{serverPort}...");

            IsConnecting = true;
            NetworkingRouter = new PlayerNetworkingRouter(this);
            var tcs = new TaskCompletionSource<bool>();

            void OnConnected(ulong clientId)
            {
                NetworkManager.Singleton.OnClientConnectedCallback -= OnConnected;
                NetworkManager.Singleton.OnClientDisconnectCallback -= OnDisconnected;
                Log.Info("Player", $"Player connected clientId={clientId}");
                IsConnecting = false;
                tcs.TrySetResult(true);
            }

            void OnDisconnected(ulong clientId)
            {
                NetworkManager.Singleton.OnClientConnectedCallback -= OnConnected;
                NetworkManager.Singleton.OnClientDisconnectCallback -= OnDisconnected;
                Log.Info("Player", $"Player disconnected clientId={clientId}");
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
            Log.Info("Player", "Requesting to join match...");

            var response = await NetworkingRouter.RequestJoinMatch();

            if (response.Success)
            {
                Match = new PlayerMatch(this, response);
                return true;
            }

            return false;
        }
    }

    public class PlayerNetworkingRouter
    {
        public static PlayerNetworkingRouter Singleton { get; private set; }
        private readonly Player player;
        private TaskCompletionSource<JoinMatchResponse> joinMatchTask;

        public PlayerNetworkingRouter(Player player)
        {
            this.player = player;
            Singleton = this;
        }

        public Task<JoinMatchResponse> RequestJoinMatch()
        {
            joinMatchTask = new();
            Networking.Singleton.RequestJoinMatch_ServerRpc();
            return joinMatchTask.Task;
        }

        public void OnRespondJoinMatch(JoinMatchResponse response)
            => joinMatchTask.TrySetResult(response);

        public void MessageReadyInMatch(ulong matchId)
            => Networking.Singleton.MessageReadyInMatch_ServerRpc(matchId, NetworkManager.Singleton.LocalClientId);

        public void OnMessageGameStarted(GameState gameState)
            => player.Match.OnGameStarted(gameState);
    }
}
