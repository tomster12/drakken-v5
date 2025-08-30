using Unity.Netcode;

namespace Drakken.Common.Data
{
    public class DiceInstance : INetworkSerializable
    {
        public int Uid;
        public int Sides;
        public int Value;

        public void Roll()
        {
            Value = UnityEngine.Random.Range(1, Sides + 1);
        }

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref Uid);
            serializer.SerializeValue(ref Sides);
            serializer.SerializeValue(ref Value);
        }
    }
}
