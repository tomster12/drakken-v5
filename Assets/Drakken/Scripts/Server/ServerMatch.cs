using Drakken.Common.Utility;
using Drakken.Domain;
using Drakken.Domain.Networking;
using Drakken.Domain.Tokens;
using Drakken.Networking;
using System.Collections.Generic;
using UnityEngine;

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
            var allIds = new List<string>();
            foreach (var def in server.TokenRegistry.AllDefinitions)
            {
                for (int i = 0; i < 3; i++)
                {
                    allIds.Add(def.TokenId);
                }
            }

            Shuffle(allIds);

            // Deal 6 to each player, store in gameState hand as "dealt" (pre-discard)
            for (int p = 0; p < 2; p++)
            {
                for (int i = 0; i < 6; i++)
                {
                    string tokenId = allIds[(p * 6 + i) % allIds.Count];
                    var instance = TokenInstance.Create(tokenId);
                    gameState.Clients[p].Hand.Add(instance);
                }
            }

            GameConnection.Singleton.Server_BroadcastMatchStartDraftingPhase(clientIds, gameState);
        }

        public void OnDraftDiscard(ulong clientId, DraftDiscardMessage msg)
        {
            Assert.True(gameState.Phase == GamePhase.Drafting);

            if (!clientIndexAssignment.TryGetValue(clientId, out int playerIndex)) return;

            var hand = gameState.Clients[playerIndex].Hand;
            var discard0 = hand.Find(t => t.InstanceId == msg.DiscardInstanceId0);
            var discard1 = hand.Find(t => t.InstanceId == msg.DiscardInstanceId1);

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
            GameConnection.Singleton.Server_BroadcastMatchStartPlayingPhase(clientIds, gameState);
        }

        /*
        public void OnPlayToken(ulong clientId, TokenIntentMessage intentMsg)
        {
            if (!clientIndexAssignment.TryGetValue(clientId, out int sourceClientIndex))
            {
                Log.Error($"ServerMatch-{MatchId}", $"Unknown sender {clientId}");
                return;
            }

            if (gameState.TurnClientIndex != sourceClientIndex)
            {
                Log.Error($"ServerMatch-{MatchId}", $"ClientIndex={sourceClientIndex} tried to play out of turn");
                return;
            }

            Log.Info($"ServerMatch-{MatchId}", $"ClientIndex={sourceClientIndex} played token {intentMsg.TokenId}");

            TokenIntent intent = server.TokenRegistry.DeserialiseIntent(
                intentMsg.TokenId,
                intentMsg.IntentJson
            );

            var tokenExecutor = server.TokenRegistry.GetExecutor(intentMsg.TokenId);

            var resolution = tokenExecutor.Execute(gameState, intent, sourceClientIndex);

            string resolutionJson = JsonUtility.ToJson(resolution);

            var resolutionMsg = new TokenResolutionMessage
            {
                TokenId = intentMsg.TokenId,
                SourceClientIndex = sourceClientIndex,
                ResolutionJson = resolutionJson,
            };

            server.Connection.BroadcastMatchPlayTokenResolved(resolutionMsg, clientIds);

            AdvanceTurn();
        }

        public void OnPassTurn(ulong clientId)
        {
            AdvanceTurn();
        }

        private void AdvanceTurn()
        {
            gameState.TurnClientIndex = 1 - gameState.TurnClientIndex;
            gameState.Turn++;
            server.Connection.BroadcastMatchStartTurn(gameState.TurnClientIndex, clientIds);
        }
        */

        private static void Shuffle<T>(List<T> list)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
        }

        public bool IsMatch(ulong matchId)
        {
            return this.matchId == matchId;
        }
    }
}
