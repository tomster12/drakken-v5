using Drakken.Domain.Dice;
using UnityEngine;

namespace Drakken.Domain.Tokens.Logic
{
    public static class TokenExecutionLogic
    {
        public static bool TryModify(DiceInstance dice, DiceSimulationWorld diceWorld, TokenResolution resolution)
        {
            if (dice.CanBeModified) return true;

            // When a glass dice blocks a modification it is removed
            if (dice.DiceEffects.Contains(DiceEffectIds.Glass))
            {
                diceWorld.WakeDice(dice.InstanceId, Vector3.zero, Vector3.zero);
                diceWorld.RemoveDice(dice.InstanceId);
                resolution.SideEffectsDestroyedDiceInstanceIds.Add(dice.InstanceId);
            }

            return false;
        }

        public static int RoundUpToEven(int n) => n % 2 == 0 ? n : n + 1;
    }
}
