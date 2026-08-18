using Drakken.Domain.Tokens.Logic;

namespace Drakken.Domain.Dice.Effects
{
    public abstract class DiceEffectLogic
    {
        public abstract int EffectId { get; }

        public virtual bool TryModify(DiceInstance dice, DiceSimulationWorld diceWorld, TokenResolution resolution)
            => true;

        public virtual void OnSettled(DiceEffectSettleContext ctx) { }
    }
}
