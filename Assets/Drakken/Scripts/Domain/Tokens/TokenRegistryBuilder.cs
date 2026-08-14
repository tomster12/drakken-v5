using System;
using Drakken.Domain.Tokens.Implementation;
using Drakken.Domain.Tokens.Implementation.Common;
using UnityEngine;

namespace Drakken.Domain.Tokens
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
                    Rarity = TokenRarity.Rare,
                    Categories = new[] { TokenCategory.Transformation }
                },
                new DragonTokenExecutor(),
                typeof(EmptyTokenIntent),
                typeof(DragonTokenResolution),
                !includeVisuals ? null : new TokenVisuals(
                    new DragonTokenAnimator(),
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
                    Categories = new[] { TokenCategory.DiceGrowth }
                },
                new ForgeTokenExecutor(),
                typeof(PickDiceTokenIntent),
                typeof(ForgeTokenResolution),
                !includeVisuals ? null : new TokenVisuals(
                    new ForgeTokenAnimator(),
                    new PickDiceTokenIntentPicker(TargetOwner.Self, count: 2),
                    prefabFactory?.Invoke("forge")
                )
            );

            registry.Register(
                new TokenDefinition
                {
                    TokenId = "mitosis",
                    DisplayName = "Mitosis",
                    Description = "Reroll all dice. Any that land high (top third) split into 2 dice with half the sides (min 4), which also roll and can split again.",
                    Rarity = TokenRarity.Common,
                    Categories = new[] { TokenCategory.DiceGrowth, TokenCategory.Chaos }
                },
                new MitosisTokenExecutor(),
                typeof(EmptyTokenIntent),
                typeof(MitosisTokenResolution),
                !includeVisuals ? null : new TokenVisuals(
                    new MitosisTokenAnimator(),
                    new EmptyTokenIntentPicker(),
                    // Re-use assets for now
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
                new BolsterTokenExecutor(),
                typeof(EmptyTokenIntent),
                typeof(BolsterTokenResolution),
                !includeVisuals ? null : new TokenVisuals(
                    new BolsterTokenAnimator(),
                    new EmptyTokenIntentPicker(),
                    // Re-use assets for now
                    prefabFactory?.Invoke("forge")
                )
            );
        }
    }
}
