using Drakken.Domain.Tokens.Logic;
using UnityEngine;

namespace Drakken.Domain.Dice.Effects
{
    public class GlassDiceEffectLogic : DiceEffectLogic
    {
        public override int EffectId => DiceEffectIds.Glass;

        public override bool TryModify(DiceInstance dice, DiceSimulationWorld diceWorld, TokenResolution resolution)
        {
            diceWorld.WakeDice(dice.InstanceId, Vector3.zero, Vector3.zero);
            diceWorld.RemoveDice(dice.InstanceId);

            // resolution is null when this fires outside of any token's session (e.g. a plain reroll)
            resolution?.SideEffectsDestroyedDiceInstanceIds.Add(dice.InstanceId);

            return false;
        }
    }
}
