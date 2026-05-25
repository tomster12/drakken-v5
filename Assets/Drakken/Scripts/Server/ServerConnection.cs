using System.Collections.Generic;
using Drakken.Domain;
using Drakken.Networking;
using Unity.Netcode;

namespace Drakken.Server
{
    public class ServerConnection
    {
        public static ServerConnection Singleton { get; private set; }
        private readonly GameServer server;

        public ServerConnection(GameServer server)
        {
            this.server = server;
            Singleton = this;
        }

        // -------------------------------------- Match Setup

        public void OnRequestJoinMatch(ulong clientId)
            => server.GetOrCreateMatch().OnRequestJoinMatch(clientId);

        public void RespondJoinMatch(JoinMatchResponse res, ulong clientId)
            => GameConnection.Singleton.RespondClientJoinMatchRpc(res,
                GameConnection.Singleton.RpcTarget.Single(clientId, RpcTargetUse.Temp));

        public void OnMatchReady(ulong clientId)
            => server.GetMatchForClient(clientId)?.OnReady(clientId);

        // -------------------------------------- Match Flow

        public void BroadcastMatchStartDraftingPhase(GameState state, ulong[] clientIds)
            => GameConnection.Singleton.BroadcastClientStartDraftingPhaseRpc(state,
                GameConnection.Singleton.RpcTarget.Group(clientIds, RpcTargetUse.Temp));

        public void OnMatchDraftDiscard(ulong clientId, DraftDiscardMessage msg)
            => server.GetMatchForClient(clientId)?.OnDraftDiscard(clientId, msg);

        public void BroadcastMatchStartTokenPhase(GameState state, ulong[] clientIds)
            => GameConnection.Singleton.BroadcastMatchStartTokenPhaseRpc(state,
                GameConnection.Singleton.RpcTarget.Group(clientIds, RpcTargetUse.Temp));

        /*
        public void OnMatchPlayToken(ulong clientId, TokenIntentMessage intentMsg)
            => server.GetMatchForClient(clientId)?.OnPlayToken(clientId, intentMsg);

        public void BroadcastMatchPlayTokenResolved(TokenResolutionMessage resolution, ulong[] clientIds)
            => GameConnection.Singleton.BroadcastClientsMatchPlayTokenResolvedRpc(resolution,
                GameConnection.Singleton.RpcTarget.Group(clientIds, RpcTargetUse.Temp));

        public void BroadcastMatchStartTurn(int activeClientIndex, ulong[] clientIds)
            => GameConnection.Singleton.BroadcastClientsMatchStartTurnRpc(activeClientIndex,
                GameConnection.Singleton.RpcTarget.Group(clientIds, RpcTargetUse.Temp));
        */
    }
}
