using System.Linq;
using UnityEngine;
using Drakken.Common.Utility;
using Drakken.Client;
using Drakken.Server;
using System;
using Drakken.Client.World;
using Drakken.DebugServer;

namespace Drakken
{
    internal class GameEntrypoint : MonoBehaviour
    {
        public static GameEntrypoint Singleton { get; private set; }

        public GameClient Client => client;
        public GameServer Server => server;
        public SceneLayout Scene => scene;
        public IGameConnection Connection => resolvedConnection;

        [Header("References")]
        [SerializeField] private GameClient client;
        [SerializeField] private GameServer server;
        [SerializeField] private SceneLayout scene;
        [SerializeField] private GameConnection connection;

        [Header("Debug")]
        [SerializeField] private bool debugPreventApplication = false;
        [SerializeField] private bool debugPreventConnection = false;

        private IGameConnection resolvedConnection;

        private void OnValidate()
        {
            Singleton = this;
        }

        private void Awake()
        {
            Singleton = this;
        }

        private async void Start()
        {
            if (debugPreventApplication) return;

            resolvedConnection = debugPreventConnection
                ? new DebugGameConnection()
                : connection;

            if (debugPreventConnection)
            {
                Log.Info("Application", "Starting game server due to debugPreventConnection");
                server.StartApplication();
            }

            var isServer =
                UnityEngine.Application.isBatchMode ||
                Unity.Multiplayer.PlayMode.CurrentPlayer.Tags.Contains("Server");

            if (isServer)
            {
                Log.Info("Application", "Starting game server application");
                server.StartApplication();
            }
            else
            {
                Log.Info("Application", "Starting game client application");
                await client.StartApplication();
            }
        }

        public void Quit()
        {
            Log.Info("Application", "Quitting...");

            if (Application.isEditor)
            {
                UnityEditor.EditorApplication.isPlaying = false;
            }
            else
            {
                Application.Quit();
            }
        }
    }
}
