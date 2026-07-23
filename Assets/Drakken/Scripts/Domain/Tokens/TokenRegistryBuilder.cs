using System;
using Drakken.Domain.Tokens.Implementation;
using UnityEngine;

namespace Drakken.Domain.Tokens
{
    public static class TokenRegistryBuilder
    {
        public static TokenRegistry Build()
        {
            var registry = new TokenRegistry();
            RegisterAll(registry);
            return registry;
        }

        public static TokenRegistry BuildWithVisuals(Func<string, GameObject> prefabFactory)
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
                    Description = "Roll a D3. Replace that many of your dice with freshly rolled D8s.",
                    Rarity = TokenRarity.Rare,
                    Categories = new[] { TokenCategory.DiceGrowth, TokenCategory.Chaos },
                    TargetOwner = TargetOwner.None,
                    RequiresTarget = false
                },
                new DragonTokenExecutor(),
                typeof(DragonTokenIntent),
                typeof(DragonTokenResolution),
                !includeVisuals ? null : new TokenVisuals(
                    new DragonTokenAnimator(),
                    prefabFactory?.Invoke("dragon")
                )
            );

            registry.Register(
                new TokenDefinition
                {
                    TokenId = "parasite",
                    DisplayName = "Parasite",
                    Description = "Attach to an opponent dice. At end of round, halve its value.",
                    Rarity = TokenRarity.Common,
                    Categories = new[] { TokenCategory.Attack, TokenCategory.Effect },
                    TargetOwner = TargetOwner.Opponent,
                    RequiresTarget = true
                },
                new ParasiteTokenExecutor(),
                typeof(ParasiteTokenIntent),
                typeof(ParasiteTokenResolution),
                !includeVisuals ? null : new TokenVisuals(
                    new ParasiteTokenAnimator(),
                    prefabFactory?.Invoke("parasite")
                )
            );
        }
    }
}
