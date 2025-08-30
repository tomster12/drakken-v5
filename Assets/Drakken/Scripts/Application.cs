using System.Linq;
using UnityEngine;
using Unity.Multiplayer.Playmode;
using Drakken.Common.Utility;
using Drakken.ApplicationPlayer;
using Drakken.ApplicationServer;

namespace Drakken
{
    internal class Application : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Player player;
        [SerializeField] private Server server;

        private void Start()
        {
            var isServer = UnityEngine.Application.isBatchMode || CurrentPlayer.ReadOnlyTags().Contains("Server");
            if (isServer)
            {
                Log.Info("Application", "Running as server");
                server.StartApplication();
            }
            else
            {
                Log.Info("Application", "Running as player");
                player.StartApplication();
            }
        }
    }
}
