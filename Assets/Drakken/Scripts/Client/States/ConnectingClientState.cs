using Drakken.Common.Utility;
using System.Threading.Tasks;

namespace Drakken.Client.States
{
    public class ConnectingClientState : ClientState
    {
        public override async Task Enter()
        {
            Assert.True(!client.IsConnected, "Client is already connecting");
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

            client.Match.MatchStarted += MatchStarted;

            client.Match.SetReady();
        }

        private async void MatchStarted()
        {
            await client.GotoState(new PlayingClientState());
        }
    }
}
