using Drakken.Common.Tokens.Implementation;
using System.Collections.Generic;

namespace Drakken.Common.Tokens
{
    public class TokenData
    {
        public TokenMetadata Metadata { get; set; }
        public ITokenImplementation Implementation { get; set; }
    }

    public static class TokenDatabase
    {
        private static readonly Dictionary<int, TokenData> tokenDatas = new();
        private static bool isInitialized = false;
        private static int nextUid = 1;

        public static TokenData Get(int uid)
        {
            if (!isInitialized) Initialize();
            if (tokenDatas.TryGetValue(uid, out var tokenData)) return tokenData;
            throw new KeyNotFoundException($"Token '{uid}' not found");
        }

        private static void Initialize()
        {
            if (isInitialized) return;

            Register(new TokenMetadata
            {
                Name = "Dragon Token",
                Description = "A token representing a dragon that can roll dice."
            }, new DragonToken());

            isInitialized = true;
        }

        private static void Register(TokenMetadata data, ITokenImplementation implementation)
        {
            data.Uid = nextUid++;
            tokenDatas[data.Uid] = new TokenData { Metadata = data, Implementation = implementation };
        }
    }
}
