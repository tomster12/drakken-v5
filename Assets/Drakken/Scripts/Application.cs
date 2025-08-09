using System.Linq;
using UnityEngine;
using Unity.Multiplayer.Playmode;
using Drakken.Common.Utility;

namespace Drakken
{
    public class Application : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Client client;
        [SerializeField] private Server server;

        private void Start()
        {
            var isServer = UnityEngine.Application.isBatchMode || CurrentPlayer.ReadOnlyTags().Contains("Server");
            if (isServer) StartServer();
            else StartClient();
        }

        private void StartClient()
        {
            Log.Info("Application", "Running as client");
            client.Init();
        }

        private void StartServer()
        {
            Log.Info("Application", "Running as server");
            server.Init();
        }
    }
}
