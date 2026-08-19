using System.Collections.Generic;
using Unity.Netcode;

namespace Drakken.Domain.Networking
{
    public struct TokenResolutionMessage : INetworkSerializable
    {
        public bool Success;
        public string TokenId;
        public int TokenInstanceId;
        public int SourceClientIndex;
        public List<GameSimulationTrace> Traces;

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref Success);
            serializer.SerializeValue(ref TokenId);
            serializer.SerializeValue(ref TokenInstanceId);
            serializer.SerializeValue(ref SourceClientIndex);
            serializer.SerializeList(ref Traces);
        }
    }
}
