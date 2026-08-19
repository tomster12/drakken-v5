using System.Collections.Generic;
using Drakken.Presentation;
using Drakken.Gameplay.Simulation;
using Drakken.Gameplay.Tokens.Implementation.Common;
using Drakken.Gameplay.Tokens.Logic;
using Drakken.Domain;

namespace Drakken.Gameplay.Tokens.Implementation
{
    public class BlankTokenLogic : TokenLogic<EmptyTokenIntent, EmptyEventResolution>
    {
        public override int EventId => 7;

        protected override List<GameSimulationTrace> ExecuteToken(GameState gameState, EmptyTokenIntent intent, int sourceClientIndex, GameSimulationWorld world)
        {
            // TODO

            return new List<GameSimulationTrace>();
        }

        protected override void ApplyEvent(GameState gameState, EmptyEventResolution resolution, int clientIndex, int sourceInstanceId)
        {
            // TODO
        }
    }
}
