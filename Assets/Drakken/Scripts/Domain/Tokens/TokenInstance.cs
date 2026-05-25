using Drakken.Domain.Tokens;
using Unity.Netcode;

namespace Drakken.Domain
{
    public class TokenInstance : INetworkSerializable
    {
        public static int nextUid;
        public int Uid;
        public int DefinitionUid;

        public static TokenInstance Get(TokenDefinition definition)
        {
            return new TokenInstance
            {
                Uid = nextUid++,
                DefinitionUid = definition.Uid
            };
        }

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref Uid);
            serializer.SerializeValue(ref DefinitionUid);
        }
    }
}
