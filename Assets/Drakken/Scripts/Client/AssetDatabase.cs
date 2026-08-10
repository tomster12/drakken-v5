using System;
using Drakken.Client.World;
using Drakken.Common.Utility;
using TMPro;
using UnityEngine;

namespace Drakken.Client
{
    [CreateAssetMenu(menuName = "Drakken/Asset Database")]
    public class AssetDatabase : ScriptableObject
    {
        [Header("Prefabs")]
        [SerializeField] public TokenView TokenViewPrefab;
        [SerializeField] public GameObject EmptyTokenMeshPrefab;
        [SerializeField] private TokenPrefabEntry[] tokenMeshPrefabs;

        [Header("Materials")]
        [SerializeField] public Material DiceMeshMaterial;

        [Header("Fonts")]
        [SerializeField] public TMP_FontAsset DiceFaceLabelFont;
        [SerializeField] public Material DiceFaceLabelMaterial;

        public GameObject GetTokenMeshPrefabById(string tokenId)
        {
            foreach (var entry in tokenMeshPrefabs)
            {
                if (entry.TokenId == tokenId)
                    return entry.Prefab;
            }

            Log.Warning("AssetDatabase", $"No prefab found for tokenId='{tokenId}'");
            return null;
        }

        [Serializable]
        public struct TokenPrefabEntry
        {
            public string TokenId;
            public GameObject Prefab;
        }
    }
}
