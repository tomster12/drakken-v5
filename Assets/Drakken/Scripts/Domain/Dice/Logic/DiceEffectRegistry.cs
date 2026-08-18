using System.Collections.Generic;
using Drakken.Domain.Dice.Implementation;

namespace Drakken.Domain.Dice.Logic
{
    public static class DiceEffectRegistry
    {
        private static readonly Dictionary<int, IDiceEffectLogic> byId = new()
        {
            [DiceEffectIds.Glass] = new GlassDiceEffect(),
            [DiceEffectIds.Bolster] = new BolsterDiceEffect(),
        };

        public static IDiceEffectLogic Get(int effectId)
            => byId.TryGetValue(effectId, out var effect) ? effect : null;
    }

    public static class FaceEffectRegistry
    {
        private static readonly Dictionary<int, IFaceEffectLogic> byId = new()
        {
            [FaceEffectIds.MitosisMark] = new MitosisFaceEffect(),
        };

        public static IFaceEffectLogic Get(int effectId)
            => byId.TryGetValue(effectId, out var effect) ? effect : null;
    }

    public static class EffectRegistry
    {
        public static IEffectLogic Get(int effectId, bool isFaceEffect)
            => isFaceEffect ? FaceEffectRegistry.Get(effectId) : DiceEffectRegistry.Get(effectId);
    }
}
