using Drakken.Client.GameObjects;
using Drakken.Common.Utility;
using System.Threading.Tasks;

namespace Drakken.Client.States
{
    public class ConnectingClientState : ClientState
    {
        private SceneLayout Layout => GameEntrypoint.Singleton.Scene;

        public override async Task Enter()
        {
            Layout.Connecting.JoinButton.Clicked += OnJoinClicked;
            Layout.Connecting.JoinButton.Interactable = true;
            Layout.Connecting.JoinButton.gameObject.SetActive(true);

            Layout.Connecting.ReadyButton.Clicked += OnReadyClicked;
            Layout.Connecting.ReadyButton.Interactable = false;
            Layout.Connecting.ReadyButton.gameObject.SetActive(false);

            Log.Info("ConnectingClientState", "Not connected.");
        }

        public override async Task Exit()
        {
            if (client.Match != null) client.Match.DraftingPhaseStarted -= OnGameStarted;

            Layout.Connecting.JoinButton.Clicked -= OnJoinClicked;
            Layout.Connecting.JoinButton.gameObject.SetActive(false);

            Layout.Connecting.ReadyButton.Clicked -= OnReadyClicked;
            Layout.Connecting.ReadyButton.gameObject.SetActive(false);
        }

        private async void OnJoinClicked()
        {
            if (client.IsConnected) return;

            Log.Info("ConnectingClientState", "Connecting...");

            Layout.Connecting.JoinButton.Interactable = false;

            var connected = await client.Connect();
            if (!connected)
            {
                Log.Info("ConnectingClientState", "Connection failed.");
                Layout.Connecting.JoinButton.Interactable = true;
                return;
            }

            Log.Info("ConnectingClientState", "Connected. Joining match...");

            var joined = await client.JoinMatch();
            if (!joined)
            {
                Log.Info("ConnectingClientState", "Failed to join match.");
                Layout.Connecting.JoinButton.Interactable = true;
                return;
            }

            Log.Info("ConnectingClientState", "Joined. Ready up!");

            Layout.Connecting.ReadyButton.gameObject.SetActive(true);
            Layout.Connecting.ReadyButton.Interactable = true;

            client.Match.DraftingPhaseStarted += OnGameStarted;
        }

        private void OnReadyClicked()
        {
            Assert.False(client.Match.IsReadiedUp);

            Layout.Connecting.ReadyButton.Interactable = false;

            client.Match.SetReady();
        }

        private async void OnGameStarted()
        {
            await client.GotoState(new DraftingClientState());
        }
    }
}
