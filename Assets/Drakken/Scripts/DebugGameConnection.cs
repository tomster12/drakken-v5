using System;
using System.Linq;
using System.Threading.Tasks;
using Drakken.Client;
using Drakken.Common.Utility;
using Drakken.Domain;
using Drakken.Domain.Networking;
using Drakken.Domain.Static;
using Drakken.Domain.Tokens;
using Drakken.Server;

namespace Drakken.Networking
{
    public class DebugGameConnection : IGameConnection
    {
        // -------------------------------- Connection

        private GameClient client;
        private Action<ulong> onClientConnectedCallback;

        public void StartServer(GameServer server, string address, ushort port)
        {
            Initialise();
        }

        public void StartClient(GameClient client, string address, ushort port)
        {
            this.client = client;
            onClientConnectedCallback?.Invoke(clientId);
            Initialise();
        }

        public void AddClientListeners(Action<ulong> onClientConnected, Action<ulong> onClientDisconnected)
        {
            onClientConnectedCallback = onClientConnected;
        }

        public void RemoveClientListeners(Action<ulong> onClientConnected, Action<ulong> onClientDisconnected)
        {
            onClientConnectedCallback = null;
        }

        public Task<JoinMatchResponse> Client_RequestJoinMatch()
        {
            var joinMatchResponse = new JoinMatchResponse
            {
                Success = true,
                MatchId = matchId,
                ClientIndex = clientIndex
            };

            return Task.FromResult(joinMatchResponse);
        }

        public void Client_MessageMatchClientReady(ulong matchId)
        {
            client.Match.OnStartDraftingPhase(draftingGameState);
        }

        public void Client_MessageMatchDraftDiscard(ulong matchId, DraftDiscardMessage message)
        {
            client.Match.OnStartPlayingPhase(playingGameState);
        }

        public void Server_BroadcastMatchStartDraftingPhase(ulong[] clientIds, GameState gameState)
        {
        }

        public void Server_BroadcastMatchStartPlayingPhase(ulong[] clientIds, GameState gameState)
        {
        }

        // -------------------------------- Data

        private static readonly ulong matchId = 45;
        private static readonly ulong clientId = 103;
        private static readonly ulong clientIndex = 0;


        private void Initialise()
        {
            // Setup data randomisation
            var tokenRegistry = TokenRegistryBuilder.BuildRegistry();
            var shuffledTokenIds = tokenRegistry.AllDefinitions.Select(d => d.TokenId).ToList();

            GameStateClient GetRandomClient() =>
                new()
                {
                    Dice = Enumerable.Range(1, GameConstants.StandardDiceCount)
                        .Select(_ => DiceInstance.Create(GameConstants.StandardDiceSideCount).Roll())
                        .ToList(),

                    Hand = Enumerable.Range(1, GameConstants.DraftingTokenCount)
                        .Select(_ => TokenInstance.Create(shuffledTokenIds[UnityEngine.Random.Range(0, shuffledTokenIds.Count())]))
                        .ToList(),
                };

            // Setup drafting game state
            draftingGameState = new()
            {
                Clients = new GameStateClient[2]
                {
                        GetRandomClient(),
                        GetRandomClient()
                },
                TurnClientIndex = 0,
                Phase = GamePhase.Drafting,
                Turn = 1,
                Round = 1
            };

            // Setup playing game state
            // playingGameState = draftingGameState;

            // playingGameState.Clients[0].Hand.RemoveRange(playingGameState.Clients[0].Hand.Count - 2, 2);
            // playingGameState.Clients[1].Hand.RemoveRange(playingGameState.Clients[1].Hand.Count - 2, 2);

            // playingGameState.Phase = GamePhase.Playing;
        }

        private GameState draftingGameState;
        private GameState playingGameState;
    }
}
