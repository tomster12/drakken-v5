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
                    Description = "Roll a D3. Replace that many of your dice with freshly rolled D8s.",
                    Rarity = TokenRarity.Rare,
                    Categories = new[] { TokenCategory.DiceGrowth, TokenCategory.Chaos }
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
                    TokenId = "parasite",
                    DisplayName = "Parasite",
                    Description = "Attach to an opponent dice. At end of round, halve its value.",
                    Rarity = TokenRarity.Common,
                    Categories = new[] { TokenCategory.Attack, TokenCategory.Effect }
                },
                new ParasiteTokenExecutor(),
                typeof(PickDiceTokenIntent),
                typeof(ParasiteTokenResolution),
                !includeVisuals ? null : new TokenVisuals(
                    new ParasiteTokenAnimator(),
                    new PickDiceTokenIntentPicker(TargetOwner.Any),
                    prefabFactory?.Invoke("parasite")
                )
            );
        }
    }
}
