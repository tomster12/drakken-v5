using Unity.Netcode;

namespace Drakken.Domain
{
    public class DiceInstance : INetworkSerializable
    {
        public static int nextUid;
        public int Uid;
        public int Sides;
        public int Value;

        public static DiceInstance Create(int sides)
        {
            return new()
            {
                Uid = nextUid++,
                Sides = sides
            };
        }

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
