using System;
using System.Collections.Generic;
using Drakken.Common.Utility;
using UnityEngine;

namespace Drakken.Domain.Tokens
{
    public class TokenRegistry
    {
        private readonly Dictionary<string, TokenDefinition> definitions = new();
        private readonly Dictionary<string, ITokenExecutor> executors = new();
        private readonly Dictionary<string, ITokenAnimator> animators = new();
        private readonly Dictionary<string, (Type intentType, Type resolutionType)> messageTypes = new();
        public IEnumerable<TokenDefinition> AllDefinitions => definitions.Values;

        public void Register(
            TokenDefinition definition,
            ITokenExecutor executor,
            ITokenAnimator animator,
            Type intentType,
            Type resolutionType)
        {
            Assert.NotNullOrEmpty(definition.TokenId, "TokenDefinition must have a non-empty TokenId");

            definitions[definition.TokenId] = definition;
            executors[definition.TokenId] = executor;
            animators[definition.TokenId] = animator;
            messageTypes[definition.TokenId] = (intentType, resolutionType);
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

        public ITokenAnimator GetAnimator(string tokenId)
        {
            if (animators.TryGetValue(tokenId, out var anim)) return anim;
            throw new KeyNotFoundException($"No animator registered for tokenId='{tokenId}'");
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
    }
}
