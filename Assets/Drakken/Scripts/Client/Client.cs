using Drakken.Common.Utility;
using System.Threading.Tasks;
using UnityEngine;

namespace Drakken
{
    public class Client : MonoBehaviour
    {
        [Header("Config")]
        [SerializeField] public string serverAddress = "0.0.0.0";
        [SerializeField] public ushort serverPort = 7777;

        private GameClient gameClient;

        public async Task Init()
        {
            gameClient = new GameClient();

            var res = await gameClient.Connect(serverAddress, serverPort);
            if (!res)
            {
                Log.Error("Client", "Failed to connect to server");
                return;
            }

            await gameClient.RequestJoinGame();
        }
    }
}
