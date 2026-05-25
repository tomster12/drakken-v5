using UnityEngine;

namespace Drakken.Domain.Tokens
{
    [CreateAssetMenu]
    public class TokenDefinition : ScriptableObject
    {
        public int TokenId;
        public string DisplayName;
        public string Description;
        public Rarity Rarity;
        public Sprite Artwork;
        public bool RequiresTarget;
        public TargetOwner TargetOwner;
    }
}