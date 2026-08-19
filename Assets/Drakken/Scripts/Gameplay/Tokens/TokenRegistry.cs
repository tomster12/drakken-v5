using System;
using System.Collections.Generic;
using Drakken.Utility;
using Drakken.Gameplay.Tokens.Implementation;
using Drakken.Gameplay.Tokens.Logic;
using UnityEngine;

namespace Drakken.Gameplay.Tokens
{
    public class TokenRegistry
    {
        // Used from SimulationEvent network serialization

        public static readonly TokenRegistry Shared = TokenRegistryBuilder.BuildServerRegistry();

        private readonly Dictionary<string, TokenRegistryEntry> entriesByTokenId = new();
        private readonly Dictionary<int, TokenRegistryEntry> entriesByEventId = new();

        public IEnumerable<TokenDefinition> AllDefinitions
        {
            get
            {
                foreach (var entry in entriesByTokenId.Values)
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

            var entry = new TokenRegistryEntry(definition, logic, visuals);
            entriesByTokenId[definition.TokenId] = entry;
            entriesByEventId[logic.EventId] = entry;
        }

        public TokenRegistryEntry GetEntryOrThrow(string tokenId)
        {
            if (!entriesByTokenId.TryGetValue(tokenId, out var entry))
                throw new InvalidOperationException($"No registry entry for TokenId={tokenId}");

            return entry;
        }

        public TokenRegistryEntry GetEntryByEventId(int eventId)
            => entriesByEventId.TryGetValue(eventId, out var entry) ? entry : null;
    }

    public class TokenRegistryEntry
    {
        public readonly TokenDefinition Definition;
        public readonly ITokenLogic Logic;
        public readonly TokenVisuals Visuals;

        public Type IntentType => Logic.IntentType;

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
