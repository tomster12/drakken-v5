using System.Collections.Generic;
using Unity.Netcode;

namespace Drakken.Domain.Networking
{
    public struct DraftDiscardMessage : INetworkSerializable
    {
        public List<int> DiscardedInstanceIds;

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeList(DiscardedInstanceIds);
        }
    }
}
