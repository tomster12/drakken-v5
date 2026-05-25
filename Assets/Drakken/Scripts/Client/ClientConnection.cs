using System.Threading.Tasks;
using Drakken.Networking;
using Drakken.Domain;
using Drakken.Domain.Tokens;
using UnityEngine;
using Drakken.Common;

namespace Drakken.Client
{
    public class ClientConnection
    {
        public static ClientConnection Singleton { get; private set; }
        private readonly GameClient client;
        private readonly TaskManager tasks = new();
        private const string JoinMatchTask = "JoinMatch";

        public ClientConnection(GameClient client)
        {
            Singleton = this;
            this.client = client;
        }

        // -------------------------------------- Match Setup

        public Task<JoinMatchResponse> RequestJoinMatch()
        {
            var task = tasks.Create<JoinMatchResponse>(JoinMatchTask);
            GameConnection.Singleton.RequestServerJoinMatchRpc();
            return task;
        }

        public void OnRespondJoinMatch(JoinMatchResponse response)
            => tasks.Complete(JoinMatchTask, response);

        public void SendMatchReady()
            => GameConnection.Singleton.MessageServerMatchClientReadyRpc();

        // -------------------------------------- Match Flow

        public void OnMatchStartDraftingPhase(GameState state)
            => client.Match?.OnStartDraftingPhase(state);

        public void OnMatchStartTokenPhase(GameState state)
            => client.Match?.OnStartTokenPhase(state);

        /*
        public void SendMatchPlayToken(TokenIntentMessage intentMsg)
            => GameConnection.Singleton.MessageServerMatchPlayTokenRpc(intentMsg);

        public void OnMatchPlayTokenResolved(TokenResolutionMessage resolution)
            => client.Match?.OnPlayTokenResolved(resolution);

        public void OnMatchStartTurn(int activeClientIndex)
            => client.Match?.OnStartTurn(activeClientIndex);

        public void OnMatchEndRound(int p0Score, int p1Score)
            => client.Match?.OnEndRound(p0Score, p1Score);
        */
    }
}
