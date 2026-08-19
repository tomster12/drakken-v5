using Drakken.Gameplay.Dice.Logic;
using Drakken.Gameplay.Tokens.Logic;

namespace Drakken.Gameplay.Simulation
{
    public static class EventRegistry
    {
        public static IEventLogic Get(int effectId, EventKind kind) => kind switch
        {
            EventKind.Dice => DiceEffectRegistry.Get(effectId),
            EventKind.Face => FaceEffectRegistry.Get(effectId),
            EventKind.Token => TokenEventRegistry.Get(effectId),
            _ => null,
        };
    }
}
