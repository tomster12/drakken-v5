using System.Collections.Generic;
using Drakken.Presentation;
using Drakken.Gameplay.Simulation;
using Drakken.Gameplay.Tokens.Implementation.Common;
using Drakken.Gameplay.Tokens.Logic;
using Drakken.Domain;

namespace Drakken.Gameplay.Tokens.Implementation
{
    public class BlankTokenLogic : TokenLogic<EmptyTokenIntent>
    {
        protected override (List<GameSimulationTrace> Traces, EmptyTokenSummary Summary) ExecuteToken(
            GameState gameState, EmptyTokenIntent intent, int sourceClientIndex, GameSimulationWorld world)
        {
            // TODO

            return (new List<GameSimulationTrace>(), new EmptyTokenSummary());
        }
    }
}
