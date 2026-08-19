using System.Collections.Generic;
using Drakken.Gameplay.Simulation;
using Drakken.Gameplay.Tokens.Implementation;

namespace Drakken.Gameplay.Tokens.Logic
{
    public static class TokenEventIds
    {
        public const int Dragon = 1;
        public const int Forge = 2;
        public const int Mitosis = 3;
        public const int Bolster = 4;
        public const int Glass = 5;
        public const int Reinforce = 6;
        public const int Blank = 7;
    }

    public static class TokenEventRegistry
    {
        private static readonly Dictionary<int, IEventLogic> byId = new()
        {
            [TokenEventIds.Dragon] = new DragonTokenLogic(),
            [TokenEventIds.Forge] = new ForgeTokenLogic(),
            [TokenEventIds.Mitosis] = new MitosisTokenLogic(),
            [TokenEventIds.Bolster] = new BolsterTokenLogic(),
            [TokenEventIds.Glass] = new GlassTokenLogic(),
            [TokenEventIds.Reinforce] = new ReinforceTokenLogic(),
            [TokenEventIds.Blank] = new BlankTokenLogic(),
        };

        public static IEventLogic Get(int effectId)
            => byId.TryGetValue(effectId, out var effect) ? effect : null;
    }
}
