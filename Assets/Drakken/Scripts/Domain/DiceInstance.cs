using System.Collections.Generic;
using System.Linq;
using Drakken.Common.Utility;
using Drakken.Domain.Networking;
using Unity.Netcode;
using UnityEngine;

namespace Drakken.Domain
{
    public class DiceInstance : INetworkSerializable
    {
        private static int nextInstanceId = 1;
        public int InstanceId;
        public int Sides;
        public int Value;

        public static DiceInstance Create(int sides, int value = 0)
        {
            return new()
            {
                InstanceId = nextInstanceId++,
                Sides = sides,
                Value = value
            };
        }

        public DiceInstance Roll()
        {
            Value = Random.Range(1, Sides + 1);
            return this;
        }

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref InstanceId);
            serializer.SerializeValue(ref Sides);
            serializer.SerializeValue(ref Value);
        }

        public DiceInstance Clone()
        {
            return new DiceInstance
            {
                InstanceId = InstanceId,
                Sides = Sides,
                Value = Value
            };
        }
    }
}
