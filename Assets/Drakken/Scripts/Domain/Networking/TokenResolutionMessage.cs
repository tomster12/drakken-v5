using Drakken.Common.Utility;
using Unity.Netcode;

namespace Drakken.Networking
{
    public struct TokenResolutionMessage : INetworkSerializable
    {
        public string TokenId;
        public int SourceClientIndex;
        public string ResolutionJson;

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeString(ref TokenId);
            serializer.SerializeValue(ref SourceClientIndex);
            serializer.SerializeString(ref ResolutionJson);
        }
    }
}
