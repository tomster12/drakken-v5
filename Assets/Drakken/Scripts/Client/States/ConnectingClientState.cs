using Drakken.Client.GameObjects;
using Drakken.Common.Utility;
using System.Threading.Tasks;

namespace Drakken.Client.States
{
    public class ConnectingClientState : ClientState
    {
        private SceneLayout Layout => SceneLayout.Singleton;

        public override async Task Enter()
        {
            Layout.Connecting.JoinButton.Clicked += OnJoinClicked;
            Layout.Connecting.ReadyButton.Clicked += OnReadyClicked;

            Layout.Connecting.JoinButton.Interactable = true;
            Layout.Connecting.JoinButton.gameObject.SetActive(true);
            
            Layout.Connecting.ReadyButton.Interactable = false;
            Layout.Connecting.ReadyButton.gameObject.SetActive(false);

            Log.Info("ConnectingClientState", "Not connected.");
        }

        public override async Task Exit()
        {
            Layout.Connecting.JoinButton.Clicked -= OnJoinClicked;
            Layout.Connecting.ReadyButton.Clicked -= OnReadyClicked;
            if (client.Match != null) client.Match.DraftingPhaseStarted -= OnGameStarted;

            Layout.Connecting.JoinButton.gameObject.SetActive(false);
            Layout.Connecting.ReadyButton.gameObject.SetActive(false);
        }

        private async void OnJoinClicked()
        {
            if (client.IsConnected) return;

            Log.Info("ConnectingClientState", "Connecting...");

            if (!await client.Connect())
            {
                Log.Info("ConnectingClientState", "Connection failed.");
                return;
            }

            Log.Info("ConnectingClientState", "Connected. Joining match...");

            if (!await client.JoinMatch())
            {
                Log.Info("ConnectingClientState", "Failed to join match.");
                return;
            }

            Log.Info("ConnectingClientState", "Joined. Ready up!");

            client.Match.DraftingPhaseStarted += OnGameStarted;
            
            Layout.Connecting.JoinButton.Interactable = false;
            Layout.Connecting.ReadyButton.gameObject.SetActive(true);
            Layout.Connecting.ReadyButton.Interactable = true;
        }

        private void OnReadyClicked()
        {
            Assert.False(client.Match.IsReadiedUp);
            
            Log.Info("ConnectingClientState", "Waiting for opponent...");
            Layout.Connecting.ReadyButton.Interactable = false;
            client.Match.SetReady();
        }

        private async void OnGameStarted()
        {
            await client.GotoState(new DraftingClientState());
        }
    }
}
