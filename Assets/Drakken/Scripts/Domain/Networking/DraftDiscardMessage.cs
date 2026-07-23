using Unity.Netcode;

namespace Drakken.Domain.Networking
{
    public struct DraftDiscardMessage : INetworkSerializable
    {
        public int DiscardInstanceId0;
        public int DiscardInstanceId1;

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref DiscardInstanceId0);
            serializer.SerializeValue(ref DiscardInstanceId1);
        }
    }
}
