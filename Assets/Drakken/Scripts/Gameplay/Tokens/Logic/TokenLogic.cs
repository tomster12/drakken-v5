using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Drakken.Client;
using Drakken.Domain;
using Drakken.Gameplay.Simulation;

namespace Drakken.Gameplay.Tokens.Logic
{
    public interface ITokenLogic
    {
        Type IntentType { get; }
        Type SummaryType { get; }

        (List<GameSimulationTrace> Traces, TokenSummary Summary) ExecuteToken(
            GameState gameState, TokenIntent intent, int sourceClientIndex, GameSimulationWorld world);

        Task AnimateToken(
            ClientMatch match,
            TokenVisualContext visualContext,
            int sourceClientIndex,
            int tokenInstanceId,
            List<GameSimulationTrace> traces,
            TokenSummary summary,
            CancellationToken ct);
    }

    public abstract class TokenLogic<TIntent, TSummary> : ITokenLogic
        where TIntent : TokenIntent
        where TSummary : TokenSummary, new()
    {
        Type ITokenLogic.IntentType => typeof(TIntent);
        Type ITokenLogic.SummaryType => typeof(TSummary);

        (List<GameSimulationTrace> Traces, TokenSummary Summary) ITokenLogic.ExecuteToken(
            GameState gameState, TokenIntent intent, int sourceClientIndex, GameSimulationWorld world)
        {
            var (traces, summary) = ExecuteToken(gameState, (TIntent)intent, sourceClientIndex, world);
            return (traces, summary);
        }

        protected abstract (List<GameSimulationTrace> Traces, TSummary Summary) ExecuteToken(
            GameState gameState, TIntent intent, int sourceClientIndex, GameSimulationWorld world);

        Task ITokenLogic.AnimateToken(
            ClientMatch match,
            TokenVisualContext visualContext,
            int sourceClientIndex,
            int tokenInstanceId,
            List<GameSimulationTrace> traces,
            TokenSummary summary,
            CancellationToken ct)
            => AnimateToken(match, visualContext, sourceClientIndex, tokenInstanceId, traces, (TSummary)summary, ct);

        public virtual async Task AnimateToken(
            ClientMatch match,
            TokenVisualContext visualContext,
            int sourceClientIndex,
            int tokenInstanceId,
            List<GameSimulationTrace> traces,
            TSummary summary,
            CancellationToken ct)
        {
            var sourcePlayerObjects = visualContext.Client.SceneObjects.Player(sourceClientIndex);

            await visualContext.TokenView.AnimateShrinkAfter(0.5f, ct);

            foreach (var trace in traces)
            {
                await sourcePlayerObjects.SimReplayer.Play(trace, ct, sourcePlayerObjects);
            }

            visualContext.Client.UI.UpdateDiceTotal(match.ClientIndex, sourceClientIndex);
        }
    }

    public abstract class TokenLogic<TIntent> : TokenLogic<TIntent, EmptyTokenSummary>
        where TIntent : TokenIntent
    { }
}
