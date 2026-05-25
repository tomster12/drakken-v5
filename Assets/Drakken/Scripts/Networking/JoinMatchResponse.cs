using Unity.Netcode;

namespace Drakken.Networking
{
    public class JoinMatchResponse : INetworkSerializable
    {
        public bool Success;
        public ulong MatchId;
        public ulong ClientIndex;

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref Success);
            serializer.SerializeValue(ref MatchId);
            serializer.SerializeValue(ref ClientIndex);
        }
    }

}