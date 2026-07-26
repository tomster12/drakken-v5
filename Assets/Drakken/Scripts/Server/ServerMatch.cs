using Drakken.Common.Utility;
using Drakken.Domain;
using Drakken.Domain.Networking;
using Drakken.Domain.Tokens;
using Drakken.Networking;
using System.Collections.Generic;

namespace Drakken.Server
{
    public class ServerMatch
    {
        private static ulong nextMatchId = 1;

        private readonly GameServer server;
        private readonly ulong matchId;
        private readonly GameState gameState;
        private readonly ulong[] clientIds = new ulong[2];
        private readonly Dictionary<ulong, int> clientIndexAssignment = new();
        private int connectedCount;
        private int startReadyCount = 0;
        private int discardReadyCount = 0;

        public ServerMatch(GameServer server)
        {
            this.server = server;
            matchId = nextMatchId++;
            gameState = new GameState();

            Log.Info($"ServerMatch-{matchId}", $"Created new match");
        }

        public JoinMatchResponse OnRequestJoin(ulong clientId)
        {
            if (gameState.Phase != GamePhase.NotStarted || connectedCount >= 2)
            {
                return new()
                {
                    Success = false
                };
            }

            int index = connectedCount++;
            clientIds[index] = clientId;
            clientIndexAssignment[clientId] = index;

            Log.Info($"ServerMatch-{matchId}", $"ClientId={clientId} joined as clientIndex={index}");

            return new()
            {
                Success = true,
                MatchId = matchId,
                ClientIndex = (ulong)index
            };
        }

        public void OnReady(ulong clientId)
        {
            Assert.True(gameState.Phase == GamePhase.NotStarted);

            startReadyCount++;

            Log.Info($"ServerMatch-{matchId}", $"Client {clientId} ready ({startReadyCount}/2)");

            if (startReadyCount == 2) StartDraftingPhase();
        }

        private void StartDraftingPhase()
        {
            Assert.True(gameState.Phase == GamePhase.NotStarted);

            // Start game into drafting phase
            Log.Info($"ServerMatch-{matchId}", $"Starting game");
            gameState.Phase = GamePhase.Drafting;

            // Give each client 4 new D6s
            for (int p = 0; p < 2; p++)
            {
                for (int d = 0; d < 4; d++)
                {
                    var dice = DiceInstance.Create(sides: 6);
                    dice.Roll();
                    gameState.Clients[p].Dice.Add(dice);
                }
            }

            // Build a pool from all available token definitions and shuffle it
            var allTokenIds = new List<string>();
            foreach (var def in server.TokenRegistry.AllDefinitions)
            {
                for (int i = 0; i < 3; i++)
                {
                    allTokenIds.Add(def.TokenId);
                }
            }

            allTokenIds.ShuffleInplace();

            // Deal 6 to each player, store in gameState hand as "dealt" (pre-discard)
            for (int p = 0; p < 2; p++)
            {
                for (int i = 0; i < 6; i++)
                {
                    string tokenId = allTokenIds[(p * 6 + i) % allTokenIds.Count];
                    var tokenInstance = TokenInstance.Create(tokenId);
                    gameState.Clients[p].Hand.Add(tokenInstance);
                }
            }

            GameEntrypoint.Singleton.Connection.Server_BroadcastMatchStartDraftingPhase(clientIds, gameState);
        }

        public void OnDraftDiscard(ulong clientId, DraftDiscardMessage message)
        {
            Assert.True(gameState.Phase == GamePhase.Drafting);

            if (!clientIndexAssignment.TryGetValue(clientId, out int playerIndex)) return;

            var hand = gameState.Clients[playerIndex].Hand;
            var discard0 = hand.Find(t => t.InstanceId == message.DiscardInstanceId0);
            var discard1 = hand.Find(t => t.InstanceId == message.DiscardInstanceId1);

            if (discard0 != null) hand.Remove(discard0);
            if (discard1 != null) hand.Remove(discard1);

            Log.Info($"ServerMatch-{matchId}", $"Player {playerIndex} discarded 2, hand={hand.Count}");

            discardReadyCount++;
            if (discardReadyCount == 2) BeginPlayingPhase();
        }

        private void BeginPlayingPhase()
        {
            Assert.True(gameState.Phase == GamePhase.Drafting);
            gameState.Phase = GamePhase.Playing;

            Log.Info($"ServerMatch-{matchId}", "Both players finished drafting, starting Playing phase");
            GameEntrypoint.Singleton.Connection.Server_BroadcastMatchStartPlayingPhase(clientIds, gameState);
        }

        public bool IsMatch(ulong matchId)
        {
            return this.matchId == matchId;
        }
    }
}
