using System;
using System.Collections.Generic;
using Drakken.Common.Utility;
using UnityEngine;

namespace Drakken.Domain.Tokens
{
    public class TokenRegistry
    {
        private readonly Dictionary<string, TokenRegistryEntry> entries = new();

        public IEnumerable<TokenDefinition> AllDefinitions
        {
            get
            {
                foreach (var entry in entries.Values)
                {
                    yield return entry.Definition;
                }
            }
        }

        internal void Register(
            TokenDefinition definition,
            ITokenExecutor executor,
            Type intentType,
            Type resolutionType,
            TokenVisuals visuals = null)
        {
            Assert.NotNullOrEmpty(definition.TokenId, "TokenDefinition must have a non-empty TokenId");

            entries[definition.TokenId] = new TokenRegistryEntry(definition, executor, intentType, resolutionType, visuals);
        }

        public TokenDefinition GetDefinition(string tokenId)
            => GetEntryOrThrow(tokenId).Definition;

        public bool TryGetDefinition(string tokenId, out TokenDefinition definition)
        {
            if (entries.TryGetValue(tokenId, out var entry))
            {
                definition = entry.Definition;
                return true;
            }
            definition = default;
            return false;
        }

        public ITokenExecutor GetExecutor(string tokenId)
            => GetEntryOrThrow(tokenId).Executor;

        public TokenIntent DeserialiseIntent(string tokenId, string json)
            => (TokenIntent)JsonUtility.FromJson(json, GetEntryOrThrow(tokenId).IntentType);

        public TokenResolution DeserialiseResolution(string tokenId, string json)
            => (TokenResolution)JsonUtility.FromJson(json, GetEntryOrThrow(tokenId).ResolutionType);

        public ITokenAnimator GetAnimator(string tokenId)
        {
            var visuals = GetEntryOrThrow(tokenId).Visuals;
            if (visuals != null) return visuals.Animator;
            throw new KeyNotFoundException($"No animator registered for tokenId='{tokenId}'. Was the registry built with visuals?");
        }

        public GameObject GetMeshPrefab(string tokenId)
        {
            var visuals = GetEntryOrThrow(tokenId).Visuals;
            if (visuals != null) return visuals.MeshPrefab;
            throw new KeyNotFoundException($"No mesh prefab registered for tokenId='{tokenId}'. Was the registry built with visuals?");
        }

        public bool TryGetMeshPrefab(string tokenId, out GameObject meshPrefab)
        {
            if (entries.TryGetValue(tokenId, out var entry) && entry.Visuals != null)
            {
                meshPrefab = entry.Visuals.MeshPrefab;
                return true;
            }
            meshPrefab = null;
            return false;
        }

        private TokenRegistryEntry GetEntryOrThrow(string tokenId)
        {
            if (entries.TryGetValue(tokenId, out var entry)) return entry;
            throw new KeyNotFoundException($"No token registered for tokenId='{tokenId}'");
        }
    }

    internal sealed class TokenRegistryEntry
    {
        public readonly TokenDefinition Definition;
        public readonly ITokenExecutor Executor;
        public readonly Type IntentType;
        public readonly Type ResolutionType;
        public readonly TokenVisuals Visuals;

        public TokenRegistryEntry(
            TokenDefinition definition,
            ITokenExecutor executor,
            Type intentType,
            Type resolutionType,
            TokenVisuals visuals)
        {
            Definition = definition;
            Executor = executor;
            IntentType = intentType;
            ResolutionType = resolutionType;
            Visuals = visuals;
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
