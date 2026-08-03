namespace Drakken.Domain.Tokens
{
    public enum TokenRarity { Common, Rare, Epic, Legendary }

    public enum TokenCategory { DiceGrowth, Attack, Effect, Chaos }

    public enum TargetOwner { None, Self, Opponent, Any }

    public class TokenDefinition
    {
        public string TokenId;
        public string DisplayName;
        public string Description;
        public TokenRarity Rarity;
        public TokenCategory[] Categories;
    }
}
