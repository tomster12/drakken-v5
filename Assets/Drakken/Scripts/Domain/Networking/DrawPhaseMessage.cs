using Drakken.Common.Utility;
using Drakken.Domain.Tokens;
using System.Collections.Generic;
using Unity.Netcode;

namespace Drakken.Domain.Networking
{
    public struct DrawPhaseMessage : INetworkSerializable
    {
        public List<TokenInstance> DealtTokens;
        public int OpponentTokenCount;

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeList(DealtTokens);
            serializer.SerializeValue(ref OpponentTokenCount);
        }
    }
}
