using Unity.Netcode;

namespace Drakken.Domain
{
    public class DiceEffect : INetworkSerializable
    {
        public string EffectId;
        public int SourceClientIndex;

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref EffectId);
            serializer.SerializeValue(ref SourceClientIndex);
        }
    }
}
