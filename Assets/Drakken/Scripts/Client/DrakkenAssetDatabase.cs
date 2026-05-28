using Drakken.Client.GameObjects;
using UnityEngine;

namespace Drakken
{
    [CreateAssetMenu(menuName = "Drakken/Asset Database")]
    public class DrakkenAssetDatabase : ScriptableObject
    {
        [Header("Prefabs")]
        public TokenView TokenPrefab;
        public DiceView DicePrefab;

        public Sprite GetTokenMesh(string tokenId)
        {
            return null;
        }
    }
}
