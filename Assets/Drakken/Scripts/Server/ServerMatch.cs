using Drakken.Common.Utility;
using Drakken.Domain;
using Drakken.Domain.Networking;
using Drakken.Domain.Static;
using Drakken.Domain.Tokens;
using Drakken.Networking;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.Mathematics;

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
        private readonly ulong[] clientIds = new ulong[2];
        private readonly Dictionary<ulong, int> clientIdIndexAssignment = new();
        private readonly ulong matchId;
        private int connectedCount;
        private int startReadyCount = 0;
        private int discardReadyCount = 0;

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

            // Give each client a set of new dice
            for (int p = 0; p < 2; p++)
            {
                for (int d = 0; d < GameConstants.StandardDiceCount; d++)
                {
                    var dice = DiceInstance.Create(sides: GameConstants.StandardDiceSideCount);
                    dice.Roll();
                    GameState.Clients[p].Dice.Add(dice);
                }
            }

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

            Connection.Server_BroadcastMatchStartDraftingPhase(clientIds, GameState);
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
                BeginPlayingPhase();
            }
        }

        // -------------------------------- Playing

        private async void BeginPlayingPhase()
        {
            Assert.True(GameState.Phase == GamePhase.Drafting);
            Log.Info($"ServerMatch-{matchId}", $"Match starting playing phase");

            // Arbitrary delay before starting
            await Task.Delay(playingStartDelay);

            GameState.Phase = GamePhase.Playing;

            // TODO: Remove, just for debug connection
            GameState.TurnClientIndex = 1;

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

            // TODO
        }
    }
}
