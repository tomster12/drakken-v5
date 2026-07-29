using Drakken.Client.World;
using Drakken.Common.Utility;
using System.Threading.Tasks;

namespace Drakken.Client.States
{
    public class TitleClientState : ClientState
    {
        private SceneLayout Layout => GameEntrypoint.Singleton.Scene;

        public override async Task Enter(ClientStateType fromStateType)
        {
            // Initialise this scenes objects
            Layout.Title.Title.SetActive(true);

            Layout.Title.JoinButton.Clicked += OnJoinClicked;
            Layout.Title.JoinButton.Interactable = true;
            Layout.Title.JoinButton.gameObject.SetActive(true);

            Layout.Title.ReadyButton.Clicked += OnReadyClicked;
            Layout.Title.ReadyButton.Interactable = false;
            Layout.Title.ReadyButton.gameObject.SetActive(true);

            Layout.Title.OptionsButton.Interactable = true;
            Layout.Title.OptionsButton.gameObject.SetActive(true);

            Layout.Title.ExitButton.Clicked += OnExitClicked;
            Layout.Title.ExitButton.Interactable = true;
            Layout.Title.ExitButton.gameObject.SetActive(true);

            Layout.Title.Clutter.SetActive(true);

            // Move camera into position
            GameEntrypoint.Singleton.Client.Camera.SetTarget(Layout.Title.CameraPosition);
        }

        public override async Task Exit(ClientStateType toStateType)
        {
            if (client.Match != null) client.Match.DraftingPhaseStarted -= OnDraftingPhaseStarted;

            // Disable this scenes specific objects
            Layout.Title.Title.SetActive(false);

            Layout.Title.JoinButton.Clicked -= OnJoinClicked;
            Layout.Title.JoinButton.gameObject.SetActive(false);

            Layout.Title.ReadyButton.Clicked -= OnReadyClicked;
            Layout.Title.ReadyButton.gameObject.SetActive(false);

            Layout.Title.OptionsButton.gameObject.SetActive(false);

            Layout.Title.ExitButton.Clicked -= OnExitClicked;
            Layout.Title.ExitButton.gameObject.SetActive(false);

            Layout.Title.Clutter.SetActive(false);
        }

        private async void OnJoinClicked()
        {
            if (client.IsConnected) return;

            Log.Info("ConnectingClientState", "Connecting...");
            Layout.Title.JoinButton.Interactable = false;

            // Connect to the server
            var connected = await client.Connect();
            if (!connected)
            {
                Log.Info("ConnectingClientState", "Connection failed.");
                Layout.Title.JoinButton.Interactable = true;
                return;
            }

            Log.Info("ConnectingClientState", "Connected. Joining match...");

            // Now straight away try join a match
            var joined = await client.JoinMatch();
            if (!joined)
            {
                Log.Info("ConnectingClientState", "Failed to join match.");
                Layout.Title.JoinButton.Interactable = true;
                return;
            }

            Log.Info("ConnectingClientState", "Joined. Ready up!");

            // Everything was successful so we are in a match
            // Prepare for the client to ready up and wait to move to draft
            Layout.Title.ReadyButton.Interactable = true;
            client.Match.DraftingPhaseStarted += OnDraftingPhaseStarted;
        }

        private void OnReadyClicked()
        {
            Assert.False(client.Match.IsReadiedUp);

            Layout.Title.ReadyButton.Interactable = false;

            client.Match.SetReady();
        }

        private void OnExitClicked()
        {
            GameEntrypoint.Singleton.Quit();
        }

        private async void OnDraftingPhaseStarted()
        {
            await client.GotoState(ClientStateType.Drafting);
        }
    }
}
