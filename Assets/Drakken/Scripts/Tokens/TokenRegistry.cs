using Drakken.Tokens.Implementation;
using System;
using System.Collections.Generic;

namespace Drakken.Tokens
{
    public class TokenRegistryEntry
    {
        public TokenData Data { get; set; }
        public IToken Token { get; set; }
    }

    public static class TokenRegistry
    {
        private static readonly Dictionary<int, TokenRegistryEntry> entries = new();
        private static bool isInitialized = false;
        private static int nextUid = 1;

        private static void Register(TokenData data, IToken token)
        {
            data.Uid = nextUid++;
            entries[data.Uid] = new TokenRegistryEntry { Token = token, Data = data };
        }

        public static TokenRegistryEntry Get(int uid)
        {
            if (!isInitialized) Initialize();
            if (entries.TryGetValue(uid, out var token)) return token;
            throw new KeyNotFoundException($"Token '{uid}' not found");
        }

        public static void Initialize()
        {
            if (isInitialized) return;

            Register(new TokenData
            {
                Name = "Dragon Token",
                Description = "A token representing a dragon that can roll dice."
            }, new DragonToken());

            isInitialized = true;
        }
    }
}
