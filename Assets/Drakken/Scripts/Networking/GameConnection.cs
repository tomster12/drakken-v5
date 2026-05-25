using Drakken.Client;
using Drakken.Domain;
using Drakken.Server;
using Unity.Netcode;

namespace Drakken.Networking
{
    public class GameConnection : NetworkBehaviour
    {
        public static GameConnection Singleton { get; private set; }

        private void Awake() => Singleton = this;

        // -------------------------------------- Match Setup

        [Rpc(SendTo.Server)]
        public void RequestServerJoinMatchRpc(RpcParams rpc = default)
            => ServerConnection.Singleton.OnRequestJoinMatch(rpc.Receive.SenderClientId);

        [Rpc(SendTo.SpecifiedInParams)]
        public void RespondClientJoinMatchRpc(JoinMatchResponse response, RpcParams rpc = default)
            => ClientConnection.Singleton.OnRespondJoinMatch(response);

        [Rpc(SendTo.Server)]
        public void MessageServerMatchClientReadyRpc(RpcParams rpc = default)
            => ServerConnection.Singleton.OnMatchReady(rpc.Receive.SenderClientId);

        // -------------------------------------- Match Flow

        [Rpc(SendTo.SpecifiedInParams)]
        public void BroadcastClientStartDraftingPhaseRpc(GameState gameState, RpcParams rpc = default)
            => ClientConnection.Singleton.OnMatchStartDraftingPhase(gameState);

        [Rpc(SendTo.Server)]
        public void MessageServerMatchDraftDiscardRpc(DraftDiscardMessage msg, RpcParams rpc = default)
            => ServerConnection.Singleton.OnMatchDraftDiscard(rpc.Receive.SenderClientId, msg);

        [Rpc(SendTo.SpecifiedInParams)]
        public void BroadcastMatchStartTokenPhaseRpc(GameState gameState, RpcParams rpc = default)
            => ClientConnection.Singleton.OnMatchStartTokenPhase(gameState);

        /*
        [Rpc(SendTo.Server)]
        public void MessageServerMatchPlayTokenRpc(TokenIntentMessage intentMsg, RpcParams rpc = default)
            => ServerConnection.Singleton.OnMatchPlayToken(rpc.Receive.SenderClientId, intentMsg);

        [Rpc(SendTo.SpecifiedInParams)]
        public void BroadcastClientsMatchPlayTokenResolvedRpc(TokenResolutionMessage resolution, RpcParams rpc = default)
            => ClientConnection.Singleton.OnMatchPlayTokenResolved(resolution);

        [Rpc(SendTo.SpecifiedInParams)]
        public void BroadcastClientsMatchStartTurnRpc(int activeClientIndex, RpcParams rpc = default)
            => ClientConnection.Singleton.OnMatchStartTurn(activeClientIndex);

        [Rpc(SendTo.SpecifiedInParams)]
        public void BroadcastClientsMatchEndRoundRpc(int p0Score, int p1Score, RpcParams rpc = default)
            => ClientConnection.Singleton.OnMatchEndRound(p0Score, p1Score);
        */
    }
}
