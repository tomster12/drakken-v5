using Drakken.Domain;
using Drakken.Gameplay.Dice.Logic;
using Drakken.Gameplay.Simulation;
using UnityEngine;

namespace Drakken.Gameplay.Dice.Implementation
{
    public class GlassDiceEffect : DiceEffectLogic<EmptyEventResolution>
    {
        public override int EventId => DiceEffectIds.Glass;

        public override bool TryModify(DiceInstance dice, GameSimulationWorld diceWorld)
        {
            diceWorld.WakeDice(dice.InstanceId, Vector3.zero, Vector3.zero);

            // RemoveDice records its own GameState-updating RemoveDice event
            diceWorld.RemoveDice(dice.InstanceId);

            return false;
        }

        protected override void Apply(GameState gameState, EmptyEventResolution resolution, int clientIndex) { }
    }
}
