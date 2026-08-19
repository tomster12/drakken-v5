using System.Linq;
using UnityEngine;
using Drakken.Utility;
using Drakken.Client;
using Drakken.Client.World;
using Drakken.Server;
using System;
using Drakken.Networking;
using Drakken.Gameplay.Tokens;

namespace Drakken
{
    public class GameEntrypoint : MonoBehaviour
    {
        public static GameEntrypoint Singleton { get; private set; }

        public GameClient Client => client;
        public GameServer Server => server;
        public SceneLayout SceneLayout => sceneLayout;
        public IGameConnection Connection =>
            (IGameConnection)debugConnection
            ?? UnityGameConnection.Singleton;
        public TokenRegistry TokenRegistry { get; set; }

        [Header("References")]
        [SerializeField] private GameClient client;
        [SerializeField] private GameServer server;
        [SerializeField] private SceneLayout sceneLayout;

        [Header("Debug")]
        [SerializeField] private bool debugPreventApplication = false;
        [SerializeField] private bool debugUseFakeConnection = false;

        private DebugGameConnection debugConnection;

        private void OnValidate()
        {
            // Used for DebugBindRandom() in TokenView
            Singleton = this;
        }

        private void Awake()
        {
            Singleton = this;
        }

        private async void Start()
        {
            if (debugPreventApplication) return;

            var isServer =
                UnityEngine.Application.isBatchMode ||
                Unity.Multiplayer.PlayMode.CurrentPlayer.Tags.Contains("Server");

            Assert.False(debugUseFakeConnection && isServer);

            if (debugUseFakeConnection)
            {
                debugConnection = new(this);
                Destroy(UnityGameConnection.Singleton.gameObject);
            }

            if (debugUseFakeConnection)
            {
                Log.Info("Application", "Starting game server due to debugPreventConnection");
                server.Init(this);
                server.StartApplication();
            }

            if (isServer)
            {
                Log.Info("Application", "Starting game server application");
                server.Init(this);
                server.StartApplication();
            }
            else
            {
                Log.Info("Application", "Starting game client application");
                client.Init(this);
                await client.StartApplication();
            }
        }

        public void Quit()
        {
            Log.Info("Application", "Quitting...");

#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
