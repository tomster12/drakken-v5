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
            ITokenLogic logic,
            TokenVisuals visuals = null)
        {
            Assert.NotNullOrEmpty(definition.TokenId, "TokenDefinition must have a non-empty TokenId");

            entries[definition.TokenId] = new TokenRegistryEntry(definition, logic, visuals);
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
        public readonly ITokenLogic Logic;
        public readonly TokenVisuals Visuals;

        public Type IntentType => Logic.IntentType;
        public Type ResolutionType => Logic.ResolutionType;

        public TokenRegistryEntry(
            TokenDefinition definition,
            ITokenLogic logic,
            TokenVisuals visuals)
        {
            Definition = definition;
            Logic = logic;
            Visuals = visuals;
        }
    }

    public class TokenVisuals
    {
        public readonly ITokenIntentPicker IntentPicker;
        public readonly GameObject MeshPrefab;

        public TokenVisuals(
            ITokenIntentPicker intentPicker,
            GameObject meshPrefab)
        {
            IntentPicker = intentPicker;
            MeshPrefab = meshPrefab;
        }
    }
}
