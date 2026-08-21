using Drakken.Gameplay.Dice.Logic;

namespace Drakken.Gameplay.Simulation
{
    public static class EventRegistry
    {
        public static IEventLogic Get(int eventId, EventKind kind) => kind switch
        {
            EventKind.Dice => DiceEffectRegistry.Get(eventId),
            EventKind.Face => FaceEffectRegistry.Get(eventId),
            EventKind.Common => CommonEventRegistry.Get(eventId),
            _ => null,
        };
    }
}
