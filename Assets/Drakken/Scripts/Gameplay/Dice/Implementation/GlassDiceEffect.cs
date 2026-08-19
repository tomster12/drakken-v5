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
            diceWorld.RemoveDice(dice.InstanceId);
            diceWorld.RecordEvent(EventId, EventKind.Dice, dice.InstanceId, dice.CurrentSide, new EmptyEventResolution());

            return false;
        }

        protected override void Apply(GameState gameState, EmptyEventResolution resolution, int clientIndex, int sourceInstanceId)
        {
            gameState.Clients[clientIndex].Dice.RemoveAll(d => d.InstanceId == sourceInstanceId);
        }
    }
}
