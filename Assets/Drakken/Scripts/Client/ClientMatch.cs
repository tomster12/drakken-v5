using Drakken.Domain;
using Drakken.Common.Utility;
using Drakken.Networking;
using System.Threading.Tasks;
using System;

namespace Drakken.Client
{
    public class ClientMatch
    {
        public Action DraftingPhaseStarted = delegate { };
        public Action PlayingPhaseStarted = delegate { };

        public ulong MatchId { get; }
        public int ClientIndex { get; }
        public GameState GameState { get; private set; }
        public bool IsReadiedUp { get; private set; }
        public bool IsStarted { get; private set; }
        public bool IsMyTurn => GameState?.TurnClientIndex == ClientIndex;

        private readonly GameClient client;
        private Task currentResolutionTask;

        public ClientMatch(GameClient client, JoinMatchResponse response)
        {
            this.client = client;
            MatchId = response.MatchId;
            ClientIndex = (int)response.ClientIndex;

            Log.Info($"ClientMatch-{MatchId}", $"Joined match as clientIndex={ClientIndex}");
        }

        public void SetReady()
        {
            Assert.False(IsReadiedUp);
            
            IsReadiedUp = true;
            client.Connection.MessageMatchReady();
        }

        public void OnStartDraftingPhase(GameState state)
        {
            Assert.False(IsStarted);
            IsStarted = true;

            Log.Info($"ClientMatch-{MatchId}", $"Match started Drafting phase");
            GameState = state;
            DraftingPhaseStarted.Invoke();
        }

        public void OnStartPlayingPhase(GameState state)
        {
            Assert.True(IsStarted);

            Log.Info($"ClientMatch-{MatchId}", $"Match started Playing phase");
            GameState = state;
            PlayingPhaseStarted.Invoke();
        }

        /*
        public void OnStartTurn(int activeClientIndex)
        {
            GameState.TurnClientIndex = activeClientIndex;
            TurnStarted.Invoke(activeClientIndex);

            Log.Info("ClientMatch", $"Start turn (activeClientIndex={activeClientIndex}, isMyTurn={IsMyTurn})");
        }

        public void OnEndRound(int p0Score, int p1Score)
        {
            GameState.Clients[0].Score = p0Score;
            GameState.Clients[1].Score = p1Score;
            RoundEnded.Invoke(p0Score, p1Score);
        }

        public void PlayToken(TokenInstance token, TokenIntent intent)
        {
            Assert.False(IsMyTurn, "Tried to play token out of turn");

            string intentJson = JsonUtility.ToJson(intent);

            var intentMsg = new TokenIntentMessage
            {
                TokenId = token.TokenId,
                InstanceId = token.InstanceId,
                IntentJson = intentJson,
            };

            client.Connection.SendMatchPlayToken(intentMsg);
        }

        public void OnPlayTokenResolved(TokenResolutionMessage resolutionMsg)
        {
            var resolution = client.TokenRegistry.DeserialiseResolution(
                resolutionMsg.TokenId,
                resolutionMsg.ResolutionJson
            );

            var visualContext = BuildVisualContext();

            var animator = client.TokenRegistry.GetAnimator(resolutionMsg.TokenId);

            var task = animator.Animate(resolution, visualContext, resolutionMsg.SourceClientIndex);

            task = task.ContinueWith(r => { Log.Info("ClientMatch", "Animation complete"); });

            currentResolutionTask = task;
        }

        private TokenVisualContext BuildVisualContext()
        {
            return new(
                ClientIndex,
                Vector3.zero
            );
        }
        */
    }
}
