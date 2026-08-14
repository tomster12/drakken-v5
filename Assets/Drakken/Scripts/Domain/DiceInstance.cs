using System.Collections.Generic;
using System.Linq;
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
        public int CurrentSide;
        public List<DiceInstanceFace> Faces;

        public int Value => Faces[CurrentSide].Value;

        public static DiceInstance Create(int sides, int currentSide = 0)
        {
            return new()
            {
                InstanceId = nextInstanceId++,
                Sides = sides,
                CurrentSide = currentSide,
                Faces = CreateDefaultFaces(sides)
            };
        }

        private static List<DiceInstanceFace> CreateDefaultFaces(int sides)
        {
            // Index i holds the printed value for side i+1 (matching DiceMeshFactory's face ordering).
            List<DiceInstanceFace> faces = new(sides);
            for (int value = 1; value <= sides; value++)
            {
                faces.Add(new DiceInstanceFace { Value = value });
            }
            return faces;
        }

        public DiceInstance Roll()
        {
            CurrentSide = Random.Range(0, Sides);
            return this;
        }

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref InstanceId);
            serializer.SerializeValue(ref Sides);
            serializer.SerializeValue(ref CurrentSide);
            serializer.SerializeList(ref Faces);
        }

        public DiceInstance Clone()
        {
            return new DiceInstance
            {
                InstanceId = InstanceId,
                Sides = Sides,
                CurrentSide = CurrentSide,
                Faces = Faces?.Select(f => f.Clone()).ToList()
            };
        }
    }
}
