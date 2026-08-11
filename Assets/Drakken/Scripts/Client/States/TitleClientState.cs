using Drakken.Client.World;
using Drakken.Common.Utility;
using System.Threading.Tasks;
using static Drakken.Client.ClientUI;

namespace Drakken.Client.States
{
    public class TitleClientState : ClientState
    {
        private SceneLayout SceneLayout => GameEntrypoint.Singleton.Client.SceneLayout;

        public override async Task Enter(ClientStateType fromStateType)
        {
            // Initialise this scenes objects
            SceneLayout.Title.Title.SetActive(true);

            SceneLayout.Title.JoinButton.Clicked += OnJoinClicked;
            SceneLayout.Title.JoinButton.Interactable = true;
            SceneLayout.Title.JoinButton.gameObject.SetActive(true);

            SceneLayout.Title.ReadyButton.Clicked += OnReadyClicked;
            SceneLayout.Title.ReadyButton.Interactable = false;
            SceneLayout.Title.ReadyButton.gameObject.SetActive(true);

            SceneLayout.Title.OptionsButton.Interactable = true;
            SceneLayout.Title.OptionsButton.gameObject.SetActive(true);

            SceneLayout.Title.ExitButton.Clicked += OnExitClicked;
            SceneLayout.Title.ExitButton.Interactable = true;
            SceneLayout.Title.ExitButton.gameObject.SetActive(true);

            SceneLayout.Title.Clutter.SetActive(true);

            // Move camera into position
            GameEntrypoint.Singleton.Client.Camera.SetTarget(
                SceneLayout.Title.CameraPosition, snap: true);
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
            SceneLayout.Title.Title.SetActive(false);

            SceneLayout.Title.JoinButton.Clicked -= OnJoinClicked;
            SceneLayout.Title.JoinButton.gameObject.SetActive(false);

            SceneLayout.Title.ReadyButton.Clicked -= OnReadyClicked;
            SceneLayout.Title.ReadyButton.gameObject.SetActive(false);

            SceneLayout.Title.OptionsButton.gameObject.SetActive(false);

            SceneLayout.Title.ExitButton.Clicked -= OnExitClicked;
            SceneLayout.Title.ExitButton.gameObject.SetActive(false);

            SceneLayout.Title.Clutter.SetActive(false);
        }

        private async void OnJoinClicked()
        {
            if (client.IsConnected) return;

            SceneLayout.Title.JoinButton.Interactable = false;

            // Connect to the server
            var connected = await client.Connect();
            if (!connected)
            {
                SceneLayout.Title.JoinButton.Interactable = true;
                return;
            }

            // Now straight away try join a match
            var joined = await client.JoinMatch();
            if (!joined)
            {
                SceneLayout.Title.JoinButton.Interactable = true;
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
            SceneLayout.Title.ReadyButton.Interactable = true;
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

            SceneLayout.Title.ReadyButton.Interactable = false;

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
