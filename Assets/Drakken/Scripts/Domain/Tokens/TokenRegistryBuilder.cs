using Drakken.Domain.Tokens.Implementation;

namespace Drakken.Domain.Tokens
{
    public static class TokenRegistryBuilder
    {
        public static TokenRegistry Build()
        {
            var db = new TokenRegistry();

            db.Register(
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
                new DragonTokenAnimator(),
                typeof(DragonTokenIntent),
                typeof(DragonTokenResolution)
            );

            db.Register(
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
                new ParasiteTokenAnimator(),
                typeof(ParasiteTokenIntent),
                typeof(ParasiteTokenResolution)
            );

            return db;
        }
    }
}
