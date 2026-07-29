using Drakken.Common.Utility;
using Drakken.Domain;
using Drakken.Domain.Networking;
using Drakken.Domain.Static;
using Drakken.Domain.Tokens;
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
            // Block if the match is started / or full
            if (gameState.Phase != GamePhase.NotStarted || connectedCount >= 2)
            {
                return new() { Success = false };
            }

            // Assign the client the next client ID
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

            // When both readied up start drafting phase
            if (startReadyCount == 2)
            {
                StartDraftingPhase();
            }
        }

        private void StartDraftingPhase()
        {
            Assert.True(gameState.Phase == GamePhase.NotStarted);

            // Start game into drafting phase
            Log.Info($"ServerMatch-{matchId}", $"Starting game");
            gameState.Phase = GamePhase.Drafting;

            // Give each client a set of new dice
            for (int p = 0; p < 2; p++)
            {
                for (int d = 0; d < GameConstants.StandardDiceCount; d++)
                {
                    var dice = DiceInstance.Create(sides: GameConstants.StandardDiceSideCount);
                    dice.Roll();
                    gameState.Clients[p].Dice.Add(dice);
                }
            }

            // Build a pool from all available token definitions and shuffle it
            var allTokenIds = new List<string>();
            foreach (var def in server.TokenRegistry.AllDefinitions)
            {
                for (int i = 0; i < GameConstants.MaxCountOfEachToken; i++)
                {
                    allTokenIds.Add(def.TokenId);
                }
            }

            allTokenIds.ShuffleInplace();

            // Deal all the draft tokens to each player
            for (int p = 0; p < 2; p++)
            {
                for (int i = 0; i < GameConstants.DraftingTokenCount; i++)
                {
                    var draftedIdIndex = (p * GameConstants.DraftingTokenCount + i) % allTokenIds.Count;
                    var tokenId = allTokenIds[draftedIdIndex];
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

            foreach (var discardedId in message.DiscardedInstanceIds)
            {
                var tokenInstance = hand.Find(t => t.InstanceId == discardedId);
                Assert.NotNull(tokenInstance);
                hand.Remove(tokenInstance);
            }

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
