using System.Linq;
using UnityEngine;

using Drakken.Common.Utility;
using Drakken.Client;
using Drakken.Server;

namespace Drakken
{
    internal class GameEntrypoint : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private GameClient client;
        [SerializeField] private GameServer server;

        private void Start()
        {
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
                var _ = client.StartApplication();
            }
        }
    }
}

