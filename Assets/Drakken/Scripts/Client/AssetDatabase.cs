using Drakken.Client.Views;
using Drakken.Domain;
using Drakken.Domain.Tokens;
using UnityEngine;

namespace Drakken
{
    [CreateAssetMenu(menuName = "Drakken/Asset Database")]
    public class DrakkenAssetDatabase : ScriptableObject
    {
        [Header("UI Panels")]
        public GameObject ConnectingPanel;
        public GameObject DraftPanel;
        public GameObject PlayingPanel;

        [Header("Prefabs")]
        public TokenView TokenPrefab;
        public DiceView DicePrefab;

        [Header("Token Sprites")]
        public TokenSpriteEntry[] TokenSprites;

        public Sprite GetTokenSprite(string tokenId)
        {
            foreach (var entry in TokenSprites)
                if (entry.TokenId == tokenId) return entry.Sprite;
            return null;
        }
    }

    [System.Serializable]
    public class TokenSpriteEntry
    {
        public string TokenId;
        public Sprite Sprite;
    }
}
