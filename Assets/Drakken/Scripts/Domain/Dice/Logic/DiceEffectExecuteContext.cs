using System.Collections.Generic;

namespace Drakken.Domain.Dice.Logic
{
    public class DiceEffectExecuteContext
    {
        public readonly DiceInstance SettledDice;
        public readonly List<DiceInstance> CandidatePool;
        public readonly DiceSimulationWorld World;

        public DiceEffectExecuteContext(DiceInstance settledDice, List<DiceInstance> candidatePool, DiceSimulationWorld world)
        {
            SettledDice = settledDice;
            CandidatePool = candidatePool;
            World = world;
        }
    }
}
