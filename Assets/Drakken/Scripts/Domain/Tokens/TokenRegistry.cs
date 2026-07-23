using System;
using System.Collections.Generic;
using Drakken.Common.Utility;
using Unity.VisualScripting.Antlr3.Runtime;
using UnityEngine;

namespace Drakken.Domain.Tokens
{
    public class TokenRegistry
    {
        private readonly Dictionary<string, TokenDefinition> definitions = new();
        private readonly Dictionary<string, ITokenExecutor> executors = new();
        private readonly Dictionary<string, (Type intentType, Type resolutionType)> messageTypes = new();
        private readonly Dictionary<string, TokenVisuals> visuals = new();

        public IEnumerable<TokenDefinition> AllDefinitions => definitions.Values;

        internal void Register(
            TokenDefinition definition,
            ITokenExecutor executor,
            Type intentType,
            Type resolutionType,
            TokenVisuals visuals = null)
        {
            Assert.NotNullOrEmpty(definition.TokenId, "TokenDefinition must have a non-empty TokenId");

            this.definitions[definition.TokenId] = definition;
            this.executors[definition.TokenId] = executor;
            this.messageTypes[definition.TokenId] = (intentType, resolutionType);
            if (visuals != null) this.visuals[definition.TokenId] = visuals;
        }

        public TokenDefinition GetDefinition(string tokenId)
        {
            if (definitions.TryGetValue(tokenId, out var def)) return def;
            throw new KeyNotFoundException($"No token definition for tokenId='{tokenId}'");
        }

        public bool TryGetDefinition(string tokenId, out TokenDefinition def)
            => definitions.TryGetValue(tokenId, out def);

        public ITokenExecutor GetExecutor(string tokenId)
        {
            if (executors.TryGetValue(tokenId, out var exec)) return exec;
            throw new KeyNotFoundException($"No executor registered for tokenId='{tokenId}'");
        }

        public TokenIntent DeserialiseIntent(string tokenId, string json)
        {
            Assert.True(messageTypes.TryGetValue(tokenId, out var types), $"No types registered for tokenId='{tokenId}'");
            return (TokenIntent)JsonUtility.FromJson(json, types.intentType);
        }

        public TokenResolution DeserialiseResolution(string tokenId, string json)
        {
            Assert.True(messageTypes.TryGetValue(tokenId, out var types), $"No types registered for tokenId='{tokenId}'");
            return (TokenResolution)JsonUtility.FromJson(json, types.resolutionType);
        }

        public ITokenAnimator GetAnimator(string tokenId)
        {
            if (visuals.TryGetValue(tokenId, out var v)) return v.Animator;
            throw new KeyNotFoundException($"No animator registered for tokenId='{tokenId}'. Was the registry built with visuals?");
        }

        public GameObject GetMeshPrefab(string tokenId)
        {
            if (visuals.TryGetValue(tokenId, out var v)) return v.MeshPrefab;
            throw new KeyNotFoundException($"No mesh prefab registered for tokenId='{tokenId}'. Was the registry built with visuals?");
        }

        public bool TryGetMeshPrefab(string tokenId, out GameObject meshPrefab)
        {
            if (visuals.TryGetValue(tokenId, out var v)) { meshPrefab = v.MeshPrefab; return true; }
            meshPrefab = null;
            return false;
        }
    }

    public class TokenVisuals
    {
        public readonly ITokenAnimator Animator;
        public readonly GameObject MeshPrefab;

        public TokenVisuals(ITokenAnimator animator, GameObject meshPrefab)
        {
            Animator = animator;
            MeshPrefab = meshPrefab;
        }
    }
}
