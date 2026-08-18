using System.Threading;
using System.Threading.Tasks;
using Drakken.Domain.Dice.Logic;
using UnityEngine;

namespace Drakken.Domain.Dice.Implementation
{
    public class GlassDiceEffect : DiceEffectLogic<EmptyEffectResolution>
    {
        public override int EffectId => DiceEffectIds.Glass;

        public override bool TryModify(DiceInstance dice, DiceSimulationWorld diceWorld)
        {
            diceWorld.WakeDice(dice.InstanceId, Vector3.zero, Vector3.zero);
            diceWorld.RemoveDice(dice.InstanceId);
            diceWorld.RecordEffectOccurrence(EffectId, isFaceEffect: false, dice.InstanceId, new EmptyEffectResolution());

            return false;
        }

        protected override void Apply(GameState gameState, EmptyEffectResolution resolution, int clientIndex, int sourceInstanceId)
        {
            gameState.Clients[clientIndex].Dice.RemoveAll(d => d.InstanceId == sourceInstanceId);
        }

        protected override Task Animate(EffectAnimateContext ctx, EmptyEffectResolution resolution, int sourceInstanceId, CancellationToken ct)
            => Task.CompletedTask;
    }
}
