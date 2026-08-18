using System.Collections.Generic;
using Drakken.Domain.Dice;
using Drakken.Domain.Dice.Logic;

namespace Drakken.Domain.Tokens.Logic
{
    public static class TokenExecutionLogic
    {
        public static bool TryModify(DiceInstance dice, DiceSimulationWorld diceWorld)
        {
            bool allowed = true;

            foreach (var effectId in new List<int>(dice.DiceEffects))
            {
                var effect = DiceEffectRegistry.Get(effectId);

                if (effect != null && !effect.TryModify(dice, diceWorld))
                    allowed = false;
            }

            return allowed;
        }

        public static int RoundUpToEven(int n) => n % 2 == 0 ? n : n + 1;
    }
}
