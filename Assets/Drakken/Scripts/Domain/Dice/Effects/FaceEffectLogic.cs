namespace Drakken.Domain.Dice.Effects
{
    public abstract class FaceEffectLogic
    {
        public abstract int EffectId { get; }

        public virtual void OnSettled(DiceEffectSettleContext ctx) { }
    }
}
