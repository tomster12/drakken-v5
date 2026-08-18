using System.Linq;
using Drakken.Domain.Tokens.Logic;
using UnityEngine;

namespace Drakken.Domain.Dice.Effects
{
    // Permanent effect - fires any time a Bolster dice settles, no matter which token's
    // simulation caused it (its own toss, a reroll caught up in another token's effect, etc.)
    public class BolsterDiceEffectLogic : DiceEffectLogic
    {
        public override int EffectId => DiceEffectIds.Bolster;

        public override void OnSettled(DiceEffectSettleContext ctx)
        {
            int magnitude = ctx.SettledDice.Value;

            var targets = ctx.CandidatePool
                .Where(d => d.InstanceId != ctx.SettledDice.InstanceId)
                .OrderBy(_ => Random.value)
                .Take(magnitude);

            foreach (var dice in targets)
            {
                if (!TokenExecutionLogic.TryModify(dice, ctx.World, ctx.Resolution)) continue;

                int newValue = dice.Faces[dice.CurrentSide].Value + 1;
                dice.Faces[dice.CurrentSide].Value = newValue;

                ctx.Resolution?.SideEffectsValueChanges.Add(new DiceValueChange
                {
                    InstanceId = dice.InstanceId,
                    NewValue = newValue,
                    SourceInstanceId = ctx.SettledDice.InstanceId,
                });
            }
        }
    }
}
