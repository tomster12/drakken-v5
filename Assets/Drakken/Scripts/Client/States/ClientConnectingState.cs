using Drakken.Common.Utility;
using System.Threading.Tasks;

namespace Drakken.Client.States
{
    public class ClientConnectingState : ClientState
    {
        public override async Task Enter()
        {
            Assert.True(!client.IsConnecting, "Client is already connecting");
            Assert.True(!client.IsInMatch, "Client is already in a match");

            if (!client.IsConnected)
            {
                if (!await client.Connect())
                {
                    Log.Error("ClientStateConnecting", "Failed to connect to server");
                    return;
                }
            }

            if (!await client.JoinMatch())
            {
                Log.Error("ClientStateConnecting", "Failed to join match");
                return;
            }

            client.Match.GameStarted += OnGameStarted;
        }

        private async void OnGameStarted()
        {
            await client.GotoState(ClientStateType.Playing);

            client.Match.SetReady();
        }
    }
}
