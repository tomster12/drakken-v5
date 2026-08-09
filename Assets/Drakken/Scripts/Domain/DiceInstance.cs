using System.Collections.Generic;
using System.Linq;
using Drakken.Common.Utility;
using Drakken.Domain.Networking;
using Unity.Netcode;
using UnityEngine;

namespace Drakken.Domain
{
    public class DiceInstanceFace : INetworkSerializable
    {
        public int Value;

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref Value);
        }

        public DiceInstanceFace Clone()
        {
            return new DiceInstanceFace { Value = Value };
        }
    }

    public class DiceInstance : INetworkSerializable
    {
        private static int nextInstanceId = 1;
        public int InstanceId;
        public int Sides;
        public int Value;
        public List<DiceInstanceFace> Faces;

        public static DiceInstance Create(int sides, int value = 0)
        {
            return new()
            {
                InstanceId = nextInstanceId++,
                Sides = sides,
                Value = value,
                Faces = CreateDefaultFaces(sides)
            };
        }

        // Index i holds the printed value for face i+1 (matching DiceMeshFactory's face ordering).
        private static List<DiceInstanceFace> CreateDefaultFaces(int sides)
        {
            List<DiceInstanceFace> faces = new(sides);
            for (int value = 1; value <= sides; value++)
            {
                faces.Add(new DiceInstanceFace { Value = value });
            }
            return faces;
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
            serializer.SerializeList(ref Faces);
        }

        public DiceInstance Clone()
        {
            return new DiceInstance
            {
                InstanceId = InstanceId,
                Sides = Sides,
                Value = Value,
                Faces = Faces?.Select(f => f.Clone()).ToList()
            };
        }
    }
}
