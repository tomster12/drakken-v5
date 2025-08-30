using Unity.Netcode;

namespace Drakken.Common.Tokens
{
    public class TokenInstance : INetworkSerializable
    {
        public static int nextUid;
        public int Uid;
        public int TokenUid;

        public static TokenInstance Get(TokenData tokenData)
        {
            return new TokenInstance
            {
                Uid = nextUid++,
                TokenUid = tokenData.Metadata.Uid
            };
        }

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref Uid);
            serializer.SerializeValue(ref TokenUid);
        }
    }
}
