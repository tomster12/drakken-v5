using Drakken.Common.Utility;
using Drakken.Domain;
using Drakken.Domain.Tokens;
using Drakken.Networking;
using System.Collections.Generic;
using UnityEngine;

namespace Drakken.Server
{
    public class ServerMatch
    {
        private static ulong nextMatchId = 1;

        public ulong MatchId { get; }
        private readonly GameServer server;
        private GameState gameState;
        private readonly ulong[] clientIds = new ulong[2];
        private readonly Dictionary<ulong, int> clientIndexAssignment = new();
        private bool isStarted;
        private int connectedCount;
        private int startReadyCount = 0;
        private int discardReadyCount = 0;

        public ServerMatch(GameServer server)
        {
            this.server = server;
            MatchId = nextMatchId++;

            Log.Info($"ServerMatch-{MatchId}", $"Created new match");
        }

        public void OnRequestJoinMatch(ulong clientId)
        {
            if (isStarted || connectedCount >= 2)
            {
                server.Connection.RespondJoinMatch(new() { Success = false }, clientId);
                return;
            }

            int index = connectedCount++;
            clientIds[index] = clientId;
            clientIndexAssignment[clientId] = index;

            Log.Info($"ServerMatch-{MatchId}", $"ClientId={clientId} joined as clientIndex={index}");

            server.Connection.RespondJoinMatch(new()
            {
                Success = true,
                MatchId = MatchId,
                ClientIndex = (ulong)index
            }, clientId);
        }

        public void OnReady(ulong clientId)
        {
            startReadyCount++;

            Log.Info($"ServerMatch-{MatchId}", $"Client {clientId} ready ({startReadyCount}/2)");

            if (startReadyCount == 2) StartGame();
        }

        private void StartGame()
        {
            Log.Info($"ServerMatch-{MatchId}", $"Starting game");

            gameState = new GameState();
            isStarted = true;

            StartDrawPhase();
        }

        private void StartDrawPhase()
        {
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
                var dealt = new List<TokenInstance>();
                for (int i = 0; i < 6; i++)
                {
                    string tokenId = allIds[(p * 6 + i) % allIds.Count];
                    var instance = TokenInstance.Create(tokenId);
                    dealt.Add(instance);
                    gameState.Clients[p].Hand.Add(instance);
                }

                var msg = new DrawPhaseMessage
                {
                    DealtTokens = dealt,
                    OpponentTokenCount = 6,
                };

            }

            server.Connection.BroadcastMatchStartDraftingPhase(gameState, clientIds);
        }

        public void OnDraftDiscard(ulong clientId, DraftDiscardMessage msg)
        {
            if (!clientIndexAssignment.TryGetValue(clientId, out int playerIndex)) return;

            var hand = gameState.Clients[playerIndex].Hand;
            var discard0 = hand.Find(t => t.InstanceId == msg.DiscardInstanceId0);
            var discard1 = hand.Find(t => t.InstanceId == msg.DiscardInstanceId1);

            if (discard0 != null) hand.Remove(discard0);
            if (discard1 != null) hand.Remove(discard1);

            Log.Info($"ServerMatch-{MatchId}", $"Player {playerIndex} discarded 2, hand={hand.Count}");

            discardReadyCount++;
            if (discardReadyCount == 2) BeginTokenPhase();
        }
        
        private void BeginTokenPhase()
        {
            Log.Info($"ServerMatch-{MatchId}", "Both players drafted, starting token phase");
            server.Connection.BroadcastMatchStartTokenPhase(gameState, clientIds);
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
    }
}
