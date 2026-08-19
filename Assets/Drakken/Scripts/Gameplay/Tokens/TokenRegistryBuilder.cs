using System;
using Drakken.Gameplay.Tokens.Implementation;
using Drakken.Gameplay.Tokens.Implementation.Common;
using UnityEngine;

namespace Drakken.Gameplay.Tokens
{
    public static class TokenRegistryBuilder
    {
        public static TokenRegistry BuildServerRegistry()
        {
            var registry = new TokenRegistry();
            RegisterAll(registry);
            return registry;
        }

        public static TokenRegistry BuildClientRegistry(Func<string, GameObject> prefabFactory)
        {
            var registry = new TokenRegistry();
            RegisterAll(registry, prefabFactory);
            return registry;
        }

        private static void RegisterAll(TokenRegistry registry, Func<string, GameObject> prefabFactory = null)
        {
            bool includeVisuals = prefabFactory != null;

            registry.Register(
                new TokenDefinition
                {
                    TokenId = "dragon",
                    DisplayName = "Dragon",
                    Description = "Roll a D4, replace that many dice randomly with new D8s.",
                    Rarity = TokenRarity.Common,
                    Categories = new[] { TokenCategory.Transformation }
                },
                new DragonTokenLogic(),
                !includeVisuals ? null : new TokenVisuals(
                    new EmptyTokenIntentPicker(),
                    prefabFactory?.Invoke("dragon")
                )
            );

            registry.Register(
                new TokenDefinition
                {
                    TokenId = "forge",
                    DisplayName = "Forge",
                    Description = "Combine 2 dice, new dice sides has (D1 value + D2 value) rounded up.",
                    Rarity = TokenRarity.Common,
                    Categories = new[] { TokenCategory.Transformation }
                },
                new ForgeTokenLogic(),
                !includeVisuals ? null : new TokenVisuals(
                    new PickDiceTokenIntentPicker(TargetOwner.Self, count: 2),
                    prefabFactory?.Invoke("forge")
                )
            );

            registry.Register(
                new TokenDefinition
                {
                    TokenId = "mitosis",
                    DisplayName = "Mitosis",
                    Description = "Mark half the faces on a chosen dice. if it lands on any of these faces, split into 2 new dice (retaining faces) with (sides/2 + 1) rounded up. Repeat with the new dice.",
                    Rarity = TokenRarity.Common,
                    Categories = new[] { TokenCategory.Transformation, TokenCategory.Chaos }
                },
                new MitosisTokenLogic(),
                !includeVisuals ? null : new TokenVisuals(
                    new PickDiceTokenIntentPicker(TargetOwner.Self, count: 1),
                    prefabFactory?.Invoke("forge")
                )
            );

            registry.Register(
                new TokenDefinition
                {
                    TokenId = "bolster",
                    DisplayName = "Bolster",
                    Description = "Give 3 random dices current faces +1.",
                    Rarity = TokenRarity.Common,
                    Categories = new[] { TokenCategory.DiceGrowth }
                },
                new BolsterTokenLogic(),
                !includeVisuals ? null : new TokenVisuals(
                    new EmptyTokenIntentPicker(),
                    prefabFactory?.Invoke("forge")
                )
            );

            registry.Register(
                new TokenDefinition
                {
                    TokenId = "glass",
                    DisplayName = "Glass",
                    Description = "Gain a new glass dice whose value is always 7. If it is modified it breaks.",
                    Rarity = TokenRarity.Common,
                    Categories = new[] { TokenCategory.DiceGrowth }
                },
                new GlassTokenLogic(),
                !includeVisuals ? null : new TokenVisuals(
                    new EmptyTokenIntentPicker(),
                    prefabFactory?.Invoke("forge")
                )
            );

            registry.Register(
                new TokenDefinition
                {
                    TokenId = "reinforce",
                    DisplayName = "Reinforce",
                    Description = "Increase a dices roll by 2. If this exceeds its maximum increase its side count by 2, retaining sides, and reroll.",
                    Rarity = TokenRarity.Common,
                    Categories = new[] { TokenCategory.DiceGrowth }
                },
                new ReinforceTokenLogic(),
                !includeVisuals ? null : new TokenVisuals(
                    new PickDiceTokenIntentPicker(TargetOwner.Self, count: 1),
                    prefabFactory?.Invoke("forge")
                )
            );
        }
    }
}
