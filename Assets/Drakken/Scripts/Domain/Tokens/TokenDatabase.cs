using Drakken.Domain.Tokens.Implementation;
using System.Collections.Generic;

namespace Drakken.Domain.Tokens
{
    public class TokenData
    {
        public TokenDefinition Definition { get; set; }
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

            Register(new TokenDefinition
            {
                Name = "Dragon Token",
                Description = "A token representing a dragon that can roll dice."
            }, new DragonToken());

            isInitialized = true;
        }

        private static void Register(TokenDefinition data, ITokenImplementation implementation)
        {
            data.Uid = nextUid++;
            tokenDatas[data.Uid] = new TokenData { Definition = data, Implementation = implementation };
        }
    }
}
