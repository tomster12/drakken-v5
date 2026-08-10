using Drakken.Common.Utility;
using Drakken.Domain;
using Drakken.Domain.Networking;
using Drakken.Domain.Static;
using Drakken.Domain.Tokens;
using Drakken.Networking;
using Drakken.Server.Simulation;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

namespace Drakken.Server
{
    public class ServerMatch
    {
        private static ulong nextMatchId = 1;
        private static readonly TimeSpan draftingStartDelay = TimeSpan.FromSeconds(1);
        private static readonly TimeSpan playingStartDelay = TimeSpan.FromSeconds(1);

        private readonly TokenRegistry tokenRegistry;
        private IGameConnection Connection => GameEntrypoint.Singleton.Connection;

        public GameState GameState { get; private set; }
        public DiceSimulationWorld[] DiceWorlds { get; }
        private readonly ulong[] clientIds = new ulong[2];
        private readonly Dictionary<ulong, int> clientIdIndexAssignment = new();
        private readonly ulong matchId;
        private int connectedCount;
        private int startReadyCount = 0;
        private int discardReadyCount = 0;
        private int animatedTokenResolvedConfirmCount = 0;

        public bool IsMatch(ulong matchId)
            => this.matchId == matchId;

        private ulong GetOtherClientId(ulong clientId)
            => clientIds[0] == clientId ? clientIds[1] : clientIds[0];

        // -------------------------------- Setup

        public ServerMatch(TokenRegistry tokenRegistry)
        {
            this.tokenRegistry = tokenRegistry;
            matchId = nextMatchId++;
            GameState = new();

            DiceWorlds = new DiceSimulationWorld[2]
            {
                new($"Match-{matchId} Dice World P1", GameConstants.DiceTrayCenter(0), GameConstants.DiceTraySize, GameConstants.DiceTrayWallHeight),
                new($"Match-{matchId} Dice World P2", GameConstants.DiceTrayCenter(1), GameConstants.DiceTraySize, GameConstants.DiceTrayWallHeight),
            };

            Log.Info($"ServerMatch-{matchId}", $"Created new match");
        }

        public JoinMatchResponse OnClientRequestJoin(ulong clientId)
        {
            // Block if the match is started / or full
            if (GameState.Phase != GamePhase.NotStarted || connectedCount >= 2)
            {
                Log.Info($"ServerMatch-{matchId}", $"Rejected ClientId={clientId}");
                return new() { Success = false };
            }

            // Assign the client the next client ID
            int index = connectedCount++;
            clientIds[index] = clientId;
            clientIdIndexAssignment[clientId] = index;

            Log.Info($"ServerMatch-{matchId}", $"Accepted ClientId={clientId} as clientIndex={index}");

            if (connectedCount == 2)
            {
                Connection.Server_MessageMatchOtherPlayerJoined(clientIds[0]);
            }

            return new()
            {
                Success = true,
                MatchId = matchId,
                ClientIndex = (ulong)index
            };
        }

        public void OnClientReady(ulong clientId)
        {
            Assert.True(GameState.Phase == GamePhase.NotStarted);
            Log.Info($"ServerMatch-{matchId}", $"Client {clientId} ready ({startReadyCount + 1}/2)");

            startReadyCount++;

            // Let other player know they have readied up
            Connection.Server_MessageMatchOtherPlayerReady(GetOtherClientId(clientId));

            // When both readied up start drafting phase
            if (startReadyCount == 2)
            {
                StartDraftingPhase();
            }
        }

        // -------------------------------- Drafting

        private async void StartDraftingPhase()
        {
            // TODO: Allow coming to drafting after round end
            Assert.True(GameState.Phase == GamePhase.NotStarted);
            Assert.True(startReadyCount == 2);
            Log.Info($"ServerMatch-{matchId}", $"Match starting drafting phase");

            // Arbitrary delay before starting
            await Task.Delay(draftingStartDelay);

            GameState.Phase = GamePhase.Drafting;

            // Give each client a set of new dice, thrown into their physics table
            for (int p = 0; p < 2; p++)
            {
                List<DiceInstance> newDice = new();
                for (int d = 0; d < GameConstants.StandardDiceCount; d++)
                {
                    newDice.Add(DiceInstance.Create(sides: GameConstants.StandardDiceSideCount));
                }

                SimulateRollDice(p, newDice);
                GameState.Clients[p].Dice.AddRange(newDice);
            }

            DealDraftTokens();

            var diceTraces = ExtractDiceSimulationTraces();
            Connection.Server_BroadcastMatchStartDraftingPhase(clientIds, GameState, diceTraces);
        }

        private void DealDraftTokens()
        {
            Assert.True(GameState.Phase == GamePhase.Drafting);

            // Build a pool from all available token definitions and shuffle it
            var allTokenIds = new List<string>();
            foreach (var tokenDef in tokenRegistry.AllDefinitions)
            {
                for (int i = 0; i < GameConstants.MaxCountOfEachToken; i++)
                {
                    allTokenIds.Add(tokenDef.TokenId);
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
                    GameState.Clients[p].Tokens.Add(tokenInstance);
                }
            }
        }

        public void OnClientRequestDraftDiscard(ulong clientId, DraftDiscardMessage message, Action<bool> respond)
        {
            Assert.True(GameState.Phase == GamePhase.Drafting);
            Assert.True(clientIdIndexAssignment.TryGetValue(clientId, out int playerIndex));
            Log.Info($"ServerMatch-{matchId}", $"Player {clientId} discarded tokens");

            // Remove the discarded tokens
            var tokens = GameState.Clients[playerIndex].Tokens;
            foreach (var discardedId in message.DiscardedInstanceIds)
            {
                var tokenInstance = tokens.Find(t => t.InstanceId == discardedId);
                Assert.NotNull(tokenInstance);
                tokens.Remove(tokenInstance);
            }

            // Tell the client all is good before we move on
            respond(true);

            // Let other player know they have discard
            Connection.Server_MessageMatchOtherPlayerDiscarded(GetOtherClientId(clientId));

            // Start playing phase once everyone has readied up
            discardReadyCount++;
            if (discardReadyCount == 2)
            {
                StartPlayingPhase();
            }
        }

        // -------------------------------- Playing

        private async void StartPlayingPhase()
        {
            Assert.True(GameState.Phase == GamePhase.Drafting);
            Log.Info($"ServerMatch-{matchId}", $"Match starting playing phase");

            // Arbitrary delay before starting
            await Task.Delay(playingStartDelay);

            GameState.Phase = GamePhase.Playing;

            Connection.Server_BroadcastMatchStartPlayingPhase(clientIds, GameState);
        }

        public void OnClientRequestPlayToken(ulong clientId, TokenIntentMessage message, Action<bool> respond)
        {
            Assert.True(GameState.Phase == GamePhase.Playing);
            Assert.True(clientIdIndexAssignment.TryGetValue(clientId, out int sourceClientIndex));
            Assert.True(GameState.TurnClientIndex == sourceClientIndex);

            var tokenInstance = GameState.Clients[sourceClientIndex].Tokens.Find(t => t.InstanceId == message.InstanceId);
            if (tokenInstance == null || tokenInstance.TokenId != message.TokenId)
                throw new InvalidOperationException("Client attempted to play a token they do not own");

            // Calculate and apply resolution to game state
            var entry = tokenRegistry.GetEntryOrThrow(message.TokenId);
            var resolution = entry.Executor.Execute(GameState, message.Intent, sourceClientIndex);
            entry.Executor.Apply(GameState, resolution, sourceClientIndex);
            GameState.Clients[sourceClientIndex].Tokens.Remove(tokenInstance);

            respond(true);

            Connection.Server_BroadcastMatchTokenResolved(clientIds, new TokenResolutionMessage
            {
                TokenId = message.TokenId,
                TokenInstanceId = message.InstanceId,
                SourceClientIndex = sourceClientIndex,
                Resolution = resolution
            });
        }

        public void OnClientMessageTokenResolved(ulong clientId)
        {
            Assert.True(GameState.Phase == GamePhase.Playing);
            Assert.True(clientIdIndexAssignment.ContainsKey(clientId));
            Log.Info($"ServerMatch-{matchId}", $"Client {clientId} confirmed animated resolution ({animatedTokenResolvedConfirmCount + 1}/2)");

            // Wait until both clients have finished animating before advancing
            animatedTokenResolvedConfirmCount++;
            if (animatedTokenResolvedConfirmCount < 2) return;

            animatedTokenResolvedConfirmCount = 0;

            bool roundIsOver =
                GameState.Clients[0].Tokens.Count == 0 &&
                GameState.Clients[1].Tokens.Count == 0;

            if (roundIsOver)
            {
                EndRound();
            }
            else
            {
                AdvanceTurn();
            }
        }

        private void AdvanceTurn()
        {
            GameState.TurnClientIndex = 1 - GameState.TurnClientIndex;
            Log.Info($"ServerMatch-{matchId}", $"Advancing turn, next clientIndex={GameState.TurnClientIndex}");

            Connection.Server_BroadcastMatchNextTurn(clientIds, GameState);
        }

        private void EndRound()
        {
            Log.Info($"ServerMatch-{matchId}", $"Round {GameState.Round} complete, calculating winner");

            // Score the round based on each players dice total, ties award nothing
            var p0DiceTotal = GameState.Clients[0].GetDiceTotal();
            var p1DiceTotal = GameState.Clients[1].GetDiceTotal();

            if (p0DiceTotal > p1DiceTotal) GameState.Clients[0].Score++;
            else if (p1DiceTotal > p0DiceTotal) GameState.Clients[1].Score++;

            GameState.Round++;
            GameState.Phase = GamePhase.Drafting;
            discardReadyCount = 0;

            // Reroll each player's dice for the new round, resuming their existing physics table
            for (int p = 0; p < 2; p++)
            {
                SimulateRerollDice(p, GameState.Clients[p].Dice);
            }

            // Deal a fresh set of tokens
            DealDraftTokens();

            var diceTraces = ExtractDiceSimulationTraces();
            Connection.Server_BroadcastMatchNextRound(clientIds, GameState, diceTraces);
        }

        // -------------------------------- Dice Simulation

        private void SimulateRollDice(int clientIndex, List<DiceInstance> diceInstances)
        {
            var world = DiceWorlds[clientIndex];
            var trayCenter = GameConstants.DiceTrayCenter(clientIndex);
            var traySize = GameConstants.DiceTraySize;
            Vector3 spawnCorner = trayCenter + new Vector3(
                -traySize.x / 2f + 0.4f,
                2.0f,
                -traySize.z / 2f + 0.4f);

            for (int i = 0; i < diceInstances.Count; i++)
            {
                Vector3 spawnPos = spawnCorner + new Vector3(i * 0.5f, 0f, 0f);
                Vector3 throwVelocity = (trayCenter - spawnPos).normalized * GameConstants.DiceThrowImpulseSpeed;
                Vector3 torque = UnityEngine.Random.insideUnitSphere * GameConstants.DiceThrowTorque;

                world.Spawn(diceInstances[i], spawnPos, Quaternion.identity, throwVelocity, torque);
            }

            world.SimulateUntilAllSettled();
            world.FreezeAll();
        }

        private void SimulateRerollDice(int clientIndex, List<DiceInstance> diceInstances)
        {
            var world = DiceWorlds[clientIndex];

            foreach (var dice in diceInstances)
            {
                Vector3 impulse = Vector3.up * GameConstants.DiceThrowImpulseSpeed * 0.5f;
                Vector3 torque = UnityEngine.Random.insideUnitSphere * GameConstants.DiceThrowTorque;
                world.Wake(dice.InstanceId, impulse, torque);
            }

            world.SimulateUntilAllSettled();
            world.FreezeAll();
        }

        private MatchDiceSimulationTraces ExtractDiceSimulationTraces()
        {
            return new MatchDiceSimulationTraces
            {
                P1 = DiceWorlds[0].ExtractTraceSinceLastExtract(),
                P2 = DiceWorlds[1].ExtractTraceSinceLastExtract(),
            };
        }
    }
}
