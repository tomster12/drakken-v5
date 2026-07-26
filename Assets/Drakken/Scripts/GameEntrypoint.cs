using System.Linq;
using UnityEngine;

using Drakken.Common.Utility;
using Drakken.Client;
using Drakken.Server;
using System;
using Drakken.Client.GameObjects;
using Drakken.Networking;

namespace Drakken
{
    internal class GameEntrypoint : MonoBehaviour
    {
        public static GameEntrypoint Singleton { get; private set; }

        public GameClient Client => client;
        public GameServer Server => server;
        public SceneLayout Scene => scene;
        public IGameConnection Connection => resolvedConnection;
        public bool DebugPreventApplication => debugPreventApplication;
        public bool DebugPreventConnection => debugPreventConnection;

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
            resolvedConnection = debugPreventConnection ? new DebugGameConnection() : connection;
        }

        private async void Start()
        {
            if (DebugPreventApplication) return;

            var isServer =
                UnityEngine.Application.isBatchMode ||
                Unity.Multiplayer.PlayMode.CurrentPlayer.Tags.Contains("Server");

            if (isServer)
            {
                Log.Info("Application", "Running as server");
                server.StartApplication();
            }
            else
            {
                Log.Info("Application", "Running as client");
                await client.StartApplication();
            }
        }
    }
}
