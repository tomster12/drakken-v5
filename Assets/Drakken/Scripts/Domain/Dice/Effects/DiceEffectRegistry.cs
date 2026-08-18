using System.Collections.Generic;

namespace Drakken.Domain.Dice.Effects
{
    public static class DiceEffectRegistry
    {
        private static readonly Dictionary<int, DiceEffectLogic> byId = new()
        {
            [DiceEffectIds.Glass] = new GlassDiceEffectLogic(),
            [DiceEffectIds.Bolster] = new BolsterDiceEffectLogic(),
        };

        public static DiceEffectLogic Get(int effectId)
            => byId.TryGetValue(effectId, out var effect) ? effect : null;
    }

    public static class FaceEffectRegistry
    {
        private static readonly Dictionary<int, FaceEffectLogic> byId = new()
        {
            [FaceEffectIds.MitosisMark] = new MitosisFaceEffectLogic(),
        };

        public static FaceEffectLogic Get(int effectId)
            => byId.TryGetValue(effectId, out var effect) ? effect : null;
    }
}
