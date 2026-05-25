using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

namespace Drakken.Domain
{
    public class DiceInstance : INetworkSerializable
    {
        private static int nextId = 1;
        public int Id;
        public int Sides;
        public int Value;
        public List<DiceEffect> Effects = new();

        public static DiceInstance Create(int sides)
        {
            return new()
            {
                Id = nextId++,
                Sides = sides
            };
        }

        public void Roll()
        {
            Value = Random.Range(1, Sides + 1);
        }

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref Id);
            serializer.SerializeValue(ref Sides);
            serializer.SerializeValue(ref Value);
            serializer.SerializeList(Effects);
        }
    }

}
