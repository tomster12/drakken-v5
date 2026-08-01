using Drakken.Client.World;
using Drakken.Common.Utility;
using System.Threading.Tasks;
using static Drakken.Client.ClientUI;

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
            if (Match != null)
            {
                Match.OnDraftingPhaseStarted -= OnDraftingPhaseStarted;
                Match.OnOtherPlayerJoined -= OnOtherPlayerJoined;
                Match.OnOtherPlayerReady -= OnOtherPlayerReady;
            }

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

            Layout.Title.JoinButton.Interactable = false;

            // Connect to the server
            var connected = await client.Connect();
            if (!connected)
            {
                Layout.Title.JoinButton.Interactable = true;
                return;
            }

            // Now straight away try join a match
            var joined = await client.JoinMatch();
            if (!joined)
            {
                Layout.Title.JoinButton.Interactable = true;
                return;
            }

            // Setup UI
            client.UI.SetMyAvatar(AvatarState.Visible);

            if (Match.IsOpJoined)
            {
                client.UI.SetOpAvatar(AvatarState.Visible);
                client.UI.SetStatus("Lobby", "Waiting for both ready...");
            }
            else
            {
                client.UI.SetStatus("Lobby", "Waiting for an opponent...");
            }

            // Everything was successful so we are in a match
            // Prepare for the client to ready up and wait to move to draft
            Layout.Title.ReadyButton.Interactable = true;
            Match.OnDraftingPhaseStarted += OnDraftingPhaseStarted;
            Match.OnOtherPlayerJoined += OnOtherPlayerJoined;
            Match.OnOtherPlayerReady += OnOtherPlayerReady;
        }

        private async void OnOtherPlayerJoined()
        {
            client.UI.SetOpAvatar(AvatarState.Visible);
            client.UI.SetStatus("Lobby", "Waiting for both ready...");
        }

        private void OnReadyClicked()
        {
            Assert.False(Match.IsReady);

            Layout.Title.ReadyButton.Interactable = false;

            client.UI.SetMyAvatar(AvatarState.Ready);

            Match.SetReady();
        }

        private async void OnOtherPlayerReady()
        {
            client.UI.SetOpAvatar(AvatarState.Ready);

            if (Match.IsReady)
            {
                client.UI.SetStatus("Lobby", "Game starting...");
            }
        }

        private void OnExitClicked()
        {
            GameEntrypoint.Singleton.Quit();
        }

        private async void OnDraftingPhaseStarted()
        {
            client.UI.SetMyAvatar(AvatarState.Visible);
            client.UI.SetOpAvatar(AvatarState.Visible);

            await client.GotoState(ClientStateType.Drafting);
        }
    }
}
