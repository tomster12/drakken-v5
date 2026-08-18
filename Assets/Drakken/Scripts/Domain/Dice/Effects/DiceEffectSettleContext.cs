using System.Collections.Generic;
using Drakken.Domain.Tokens.Logic;

namespace Drakken.Domain.Dice.Effects
{
    // Passed to a dice/face effect whenever the dice it's attached to settles, no matter which
    // token's simulation session caused it. Effects act via World directly (spawn/remove/drive) -
    // the world's own body bookkeeping automatically keeps simulating and re-dispatching until
    // everything (including anything an effect spawns) comes to rest.
    public class DiceEffectSettleContext
    {
        public readonly DiceInstance SettledDice;
        public readonly List<DiceInstance> CandidatePool;
        public readonly DiceSimulationWorld World;

        // Null when the settle happened outside of any token's session (e.g. a plain reroll)
        public readonly TokenResolution Resolution;

        public DiceEffectSettleContext(
            DiceInstance settledDice,
            List<DiceInstance> candidatePool,
            DiceSimulationWorld world,
            TokenResolution resolution)
        {
            SettledDice = settledDice;
            CandidatePool = candidatePool;
            World = world;
            Resolution = resolution;
        }
    }
}
