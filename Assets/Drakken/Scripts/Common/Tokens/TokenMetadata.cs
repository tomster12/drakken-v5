using Unity.Netcode;

namespace Drakken.Common.Tokens
{
    public class TokenMetadata : INetworkSerializable
    {
        public int Uid;
        public string Name;
        public string Description;

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref Uid);
            serializer.SerializeValue(ref Name);
            serializer.SerializeValue(ref Description);
        }
    }
}
