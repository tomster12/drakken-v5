using Unity.Netcode;

namespace Drakken.Domain.Tokens
{
    public class TokenInstance : INetworkSerializable
    {
        private static int nextInstanceId = 1;
        public int InstanceId;
        public string TokenId;

        public static TokenInstance Create(string tokenId)
        {
            return new()
            {
                InstanceId = nextInstanceId++,
                TokenId = tokenId
            };
        }

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref InstanceId);
            serializer.SerializeValue(ref TokenId);
        }

        public TokenInstance Clone()
        {
            return new TokenInstance
            {
                InstanceId = InstanceId,
                TokenId = TokenId,
            };
        }
    }
}
