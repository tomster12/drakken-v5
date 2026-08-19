using System.Collections.Generic;
using Drakken.Presentation;
using Drakken.Gameplay.Dice.Logic;
using Drakken.Gameplay.Simulation;
using Drakken.Domain;

namespace Drakken.Gameplay.Tokens.Logic
{
    public static class TokenExecutionLogic
    {
        public static bool TryModify(DiceInstance dice, GameSimulationWorld simWorld)
        {
            bool allowed = true;

            foreach (var effectId in new List<int>(dice.DiceEffects))
            {
                var effect = DiceEffectRegistry.Get(effectId);

                if (effect != null && !effect.TryModify(dice, simWorld))
                    allowed = false;
            }

            return allowed;
        }

        public static int RoundUpToEven(int n) => n % 2 == 0 ? n : n + 1;
    }
}
