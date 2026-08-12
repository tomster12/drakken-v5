using Drakken.Client.World;
using Drakken.Common.Utility;
using Drakken.Domain;
using Drakken.Domain.Dice;
using Drakken.Domain.Networking;
using Drakken.Domain.Static;
using Drakken.Domain.Tokens;
using Drakken.Networking;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

namespace Drakken.Server
{
    public class ServerMatch
    {
        private static ulong nextMatchId = 1;
        private static readonly TimeSpan draftingStartDelay = TimeSpan.FromSeconds(0.5);
        private static readonly TimeSpan playingStartDelay = TimeSpan.FromSeconds(0.5);

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

            var diceLayout = GameEntrypoint.Singleton.SceneLayout.Dice;

            DiceWorlds = new DiceSimulationWorld[2]
            {
                new($"Match-{matchId} Dice World P1", diceLayout.P1),
                new($"Match-{matchId} Dice World P2", diceLayout.P2),
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
            Assert.True(GameState.Phase == GamePhase.NotStarted);
            Assert.True(startReadyCount == 2);
            Log.Info($"ServerMatch-{matchId}", $"Match starting drafting phase");

            // Arbitrary delay before starting
            await Task.Delay(draftingStartDelay);

            GameState.Phase = GamePhase.Drafting;

            DealGameDice();
            DealDraftTokens();

            var diceTraces = SimulateRollDice();

            Connection.Server_BroadcastMatchStartDraftingPhase(clientIds, GameState, diceTraces);
        }

        private void DealGameDice()
        {
            Assert.True(GameState.Phase == GamePhase.Drafting);

            // Give each client a set of new dice
            for (int p = 0; p < 2; p++)
            {
                List<DiceInstance> newDice = new();

                for (int d = 0; d < GameConstants.StandardDiceCount; d++)
                {
                    newDice.Add(DiceInstance.Create(sides: GameConstants.StandardDiceSideCount));
                }

                GameState.Clients[p].Dice.AddRange(newDice);
            }
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
            var resolution = entry.Executor.Execute(GameState, message.Intent, sourceClientIndex, DiceWorlds[sourceClientIndex]);
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

            DealDraftTokens();

            var diceTraces = SimulateRerollDice();
            Connection.Server_BroadcastMatchNextRound(clientIds, GameState, diceTraces);
        }

        // -------------------------------- Dice Simulation

        private const float diceRowEdgeMargin = 0.2f;
        private const float diceRowSpawnY = 1.5f;
        private const float diceThrowHeight = 8f;
        private const float diceThrowImpulseSpeed = 2.5f;
        private const float diceThrowTorque = 20f;
        private const float diceRiseIntoRowDuration = 0.35f;

        private readonly struct DiceRowSlot
        {
            public readonly Vector3 Position;
            public readonly Quaternion Rotation;

            public DiceRowSlot(Vector3 position, Quaternion rotation)
            {
                Position = position;
                Rotation = rotation;
            }
        }

        // Lay out where each dice in a set should sit in a row, spilling into new rows as needed
        private static List<DiceRowSlot> CalculateDiceRowLayout(int diceCount, DiceTray tray, int clientIndex)
        {
            var trayCenter = tray.transform.position;
            var traySize = tray.Size;

            float diceSpacing = DiceMeshFactory.BaseScale * 3.0f;
            float clientSideSignZ = clientIndex == 0 ? -1f : 1f;
            float rowWidth = traySize.x - diceRowEdgeMargin * 2f;
            int maxPerRow = Mathf.Max(1, Mathf.FloorToInt(rowWidth / diceSpacing));
            int rowCount = Mathf.Max(1, Mathf.CeilToInt((float)diceCount / maxPerRow));

            List<DiceRowSlot> slots = new(diceCount);
            for (int row = 0; row < rowCount; row++)
            {
                int rowDiceCount = diceCount / rowCount + (row < diceCount % rowCount ? 1 : 0);
                float slotZ = trayCenter.z + clientSideSignZ * (traySize.z / 2f - diceRowEdgeMargin - row * diceSpacing);

                for (int i = 0; i < rowDiceCount; i++)
                {
                    var slotX = trayCenter.x + (i - (rowDiceCount - 1) / 2f) * diceSpacing;
                    var slotPos = new Vector3(slotX, diceRowSpawnY, slotZ);
                    slots.Add(new DiceRowSlot(slotPos, UnityEngine.Random.rotationUniform));
                }
            }

            return slots;
        }

        // Calculate the impulse/torque to throw a dice from a row slot up into the tray
        private static (Vector3 Velocity, Vector3 Torque) CalculateDiceThrow(Vector3 fromPos, Vector3 trayCenter)
        {
            var throwTarget = trayCenter + Vector3.up * diceThrowHeight * UnityEngine.Random.Range(0.6f, 1.4f);
            var velocity = (throwTarget - fromPos).normalized * diceThrowImpulseSpeed;
            var torque = UnityEngine.Random.insideUnitSphere * diceThrowTorque;
            return (velocity, torque);
        }

        private MatchDiceSimulationTraces SimulateRollDice()
        {
            for (int p = 0; p < 2; p++)
            {
                var diceInstances = GameState.Clients[p].Dice;
                var tray = GameEntrypoint.Singleton.SceneLayout.Dice.Player(p);
                var trayCenter = tray.transform.position;
                var slots = CalculateDiceRowLayout(diceInstances.Count, tray, p);

                var world = DiceWorlds[p];
                world.BeginSession();

                for (int i = 0; i < diceInstances.Count; i++)
                {
                    var slot = slots[i];
                    var (throwVelocity, throwTorque) = CalculateDiceThrow(slot.Position, trayCenter);

                    // Spawn a new dice at its row slot, then throw it in
                    world.SpawnDice(diceInstances[i], slot.Position, slot.Rotation, throwVelocity, throwTorque);
                }

                world.Simulate(untilAllSettled: true);
                world.FreezeAllDice();
            }

            return new()
            {
                P1 = DiceWorlds[0].EndSession(),
                P2 = DiceWorlds[1].EndSession(),
            };
        }

        private MatchDiceSimulationTraces SimulateRerollDice()
        {
            for (int p = 0; p < 2; p++)
            {
                var diceInstances = GameState.Clients[p].Dice;
                var tray = GameEntrypoint.Singleton.SceneLayout.Dice.Player(p);
                var trayCenter = tray.transform.position;
                var slots = CalculateDiceRowLayout(diceInstances.Count, tray, p);

                var world = DiceWorlds[p];
                world.BeginSession();

                // Rise the existing dice up into the same row layout a fresh roll would start from
                List<string> riseDriveIds = new(diceInstances.Count);
                for (int i = 0; i < diceInstances.Count; i++)
                {
                    var dice = diceInstances[i];
                    var slot = slots[i];
                    var (startPos, startRot) = world.GetDicePose(dice.InstanceId);

                    riseDriveIds.Add(world.DriveKinematic(
                        dice.InstanceId,
                        diceRiseIntoRowDuration,
                        t => Vector3.Lerp(startPos, slot.Position, t),
                        t => Quaternion.Slerp(startRot, slot.Rotation, t)));
                }

                world.Simulate(untilDrivesComplete: riseDriveIds);

                // Now throw them the same way a fresh roll would
                for (int i = 0; i < diceInstances.Count; i++)
                {
                    var dice = diceInstances[i];
                    var slot = slots[i];
                    var (throwVelocity, throwTorque) = CalculateDiceThrow(slot.Position, trayCenter);
                    world.WakeDice(dice.InstanceId, throwVelocity, throwTorque);
                }

                world.Simulate(untilAllSettled: true);
                world.FreezeAllDice();
            }

            return new()
            {
                P1 = DiceWorlds[0].EndSession(),
                P2 = DiceWorlds[1].EndSession(),
            };
        }
    }
}
