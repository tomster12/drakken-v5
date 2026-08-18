using System.Collections.Generic;
using Drakken.Domain.Dice;
using Drakken.Domain.Dice.Effects;

namespace Drakken.Domain.Tokens.Logic
{
    public static class TokenExecutionLogic
    {
        public static bool TryModify(DiceInstance dice, DiceSimulationWorld diceWorld, TokenResolution resolution)
        {
            bool allowed = true;

            // Every dice effect gets a chance to block / react to the attempted modification
            foreach (var effectId in new List<int>(dice.DiceEffects))
            {
                var effect = DiceEffectRegistry.Get(effectId);
                
                if (effect != null && !effect.TryModify(dice, diceWorld, resolution))
                    allowed = false;
            }

            return allowed;
        }

        public static int RoundUpToEven(int n) => n % 2 == 0 ? n : n + 1;
    }
}
