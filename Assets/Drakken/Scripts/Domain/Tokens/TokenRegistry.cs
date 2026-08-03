using System;
using System.Collections.Generic;
using Drakken.Common.Utility;
using Drakken.Domain.Tokens.Implementation;
using Drakken.Domain.Tokens.Logic;
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

        public TokenRegistryEntry GetEntryOrThrow(string tokenId)
        {
            if (!entries.TryGetValue(tokenId, out var entry))
                throw new InvalidOperationException($"No registry entry for TokenId={tokenId}");

            return entry;
        }
    }

    public class TokenRegistryEntry
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
        public readonly ITokenIntentPicker IntentPicker;
        public readonly GameObject MeshPrefab;

        public TokenVisuals(
            ITokenAnimator animator,
            ITokenIntentPicker intentPicker,
            GameObject meshPrefab)
        {
            Animator = animator;
            IntentPicker = intentPicker;
            MeshPrefab = meshPrefab;
        }
    }
}
