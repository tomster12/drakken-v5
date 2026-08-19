using Drakken.Presentation;
using Drakken.Gameplay.Simulation;
using Drakken.Gameplay.Tokens.Implementation.Common;
using Drakken.Gameplay.Tokens.Logic;
using Drakken.Domain;

namespace Drakken.Gameplay.Tokens.Implementation
{
    public class BlankOutcome : EventResolution { }

    public class BlankTokenLogic : TokenLogic<EmptyTokenIntent, BlankOutcome>
    {
        public override int EffectId => TokenEventIds.Blank;

        protected override TokenResolution Execute(GameState gameState, EmptyTokenIntent intent, int sourceClientIndex, GameSimulationWorld world)
        {
            // TODO

            return new TokenResolution();
        }

        protected override void Apply(GameState gameState, BlankOutcome outcome, int clientIndex, int sourceInstanceId)
        {
            // TODO
        }
    }
}
