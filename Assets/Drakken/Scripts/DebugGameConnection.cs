using System;
using System.Threading.Tasks;
using Drakken.Client;
using Drakken.Common.Utility;
using Drakken.Domain;
using Drakken.Domain.Networking;
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
        }

        public void StartClient(GameClient client, string address, ushort port)
        {
            this.client = client;
            onClientConnectedCallback?.Invoke(clientId);
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
            client.Match.OnStartDraftingPhase(DraftingGameState);
        }

        public void Client_MessageMatchDraftDiscard(ulong matchId, DraftDiscardMessage message)
        {
            client.Match.OnStartPlayingPhase(PlayingGameState);
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

        private static GameState DraftingGameState
        {
            get
            {
                return new()
                {
                    Clients = new GameStateClient[2] {
                        new()
                        {
                            Dice = new()
                            {
                                DiceInstance.Create(6).Roll(),
                                DiceInstance.Create(6).Roll(),
                                DiceInstance.Create(6).Roll(),
                                DiceInstance.Create(6).Roll()
                            },
                            Hand = new()
                            {
                                TokenInstance.Create("parasite"),
                                TokenInstance.Create("dragon"),
                                TokenInstance.Create("parasite"),
                                TokenInstance.Create("dragon"),
                                TokenInstance.Create("parasite"),
                                TokenInstance.Create("dragon"),
                            }
                        },
                        new()
                        {
                            Dice = new()
                            {
                                DiceInstance.Create(6).Roll(),
                                DiceInstance.Create(6).Roll(),
                                DiceInstance.Create(6).Roll(),
                                DiceInstance.Create(6).Roll()
                            },
                            Hand = new()
                            {
                                TokenInstance.Create("parasite"),
                                TokenInstance.Create("dragon"),
                                TokenInstance.Create("parasite"),
                                TokenInstance.Create("dragon"),
                                TokenInstance.Create("parasite"),
                                TokenInstance.Create("dragon"),
                            }
                        },
                    },
                    TurnClientIndex = 0,
                    Phase = GamePhase.Drafting,
                    Turn = 1,
                    Round = 1
                };
            }
        }

        private static GameState PlayingGameState
        {
            get
            {
                var state = DraftingGameState;

                state.Clients[0].Hand.RemoveRange(state.Clients[0].Hand.Count - 2, 2);
                state.Clients[1].Hand.RemoveRange(state.Clients[1].Hand.Count - 2, 2);

                state.Phase = GamePhase.Playing;

                return state;
            }
        }
    }
}
