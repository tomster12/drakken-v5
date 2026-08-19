using System.Collections.Generic;
using Drakken.Domain;
using Drakken.Gameplay.Simulation;

namespace Drakken.Gameplay.Dice.Logic
{
    public class DiceEffectExecuteContext
    {
        public readonly DiceInstance SettledDice;
        public readonly List<DiceInstance> CandidatePool;
        public readonly GameSimulationWorld World;

        // The specific face this dispatch is about - the landed side for Execute(),
        // or the particular non-landed face carrying the effect for OnMiss()
        public readonly int Side;

        public DiceEffectExecuteContext(DiceInstance settledDice, List<DiceInstance> candidatePool, GameSimulationWorld world, int side)
        {
            SettledDice = settledDice;
            CandidatePool = candidatePool;
            World = world;
            Side = side;
        }
    }
}
