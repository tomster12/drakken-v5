using System;
using Drakken.Client.GameObjects;
using Drakken.Common.Utility;
using UnityEngine;

namespace Drakken
{
    [CreateAssetMenu(menuName = "Drakken/Asset Database")]
    public class DrakkenAssetDatabase : ScriptableObject
    {
        [Header("Prefabs")]
        public TokenView TokenViewPrefab;
        public DiceView DiceViewPrefab;
        [SerializeField] private TokenPrefabEntry[] tokenMeshPrefabs;

        public GameObject GetTokenPrefab(string tokenId)
        {
            foreach (var entry in tokenMeshPrefabs)
            {
                if (entry.TokenId == tokenId)
                    return entry.Prefab;
            }

            Log.Warning("DrakkenAssetDatabase", $"No prefab found for tokenId='{tokenId}'");
            return null;
        }
    }

    [Serializable]
    public struct TokenPrefabEntry
    {
        public string TokenId;
        public GameObject Prefab;
    }
}