using System.Collections.Generic;
using System.Linq;
using Drakken.Domain.Networking;
using Unity.Netcode;
using UnityEngine;

namespace Drakken.Domain
{
    public static class FaceEffectIds
    {
        public const int MitosisMark = 1;
    }

    public static class DiceEffectIds
    {
        public const int Glass = 1;
    }

    public class DiceInstanceFace : INetworkSerializable
    {
        public int Value;
        public List<int> FaceEffects = new();

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref Value);
            serializer.SerializeList(ref FaceEffects);
        }

        public DiceInstanceFace Clone()
        {
            return new DiceInstanceFace { Value = Value, FaceEffects = new List<int>(FaceEffects) };
        }
    }

    public class DiceInstance : INetworkSerializable
    {
        private static int nextInstanceId = 1;
        public int InstanceId;
        public int Sides;
        public int CurrentSide;
        public List<DiceInstanceFace> Faces;
        public List<int> DiceEffects = new();

        public int Value => Faces[CurrentSide].Value;
        public bool CanBeModified => !DiceEffects.Contains(DiceEffectIds.Glass);

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

        public static DiceInstance CreateFromRetainedFaces(int sides, List<int> retainedValues)
        {
            List<DiceInstanceFace> faces = new(sides);
            foreach (int value in retainedValues)
            {
                faces.Add(new DiceInstanceFace { Value = value });
            }
            while (faces.Count < sides)
            {
                faces.Add(new DiceInstanceFace { Value = sides });
            }

            return new()
            {
                InstanceId = nextInstanceId++,
                Sides = sides,
                CurrentSide = 0,
                Faces = faces
            };
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
            serializer.SerializeList(ref DiceEffects);
        }

        public DiceInstance Clone()
        {
            return new DiceInstance
            {
                InstanceId = InstanceId,
                Sides = Sides,
                CurrentSide = CurrentSide,
                Faces = Faces?.Select(f => f.Clone()).ToList(),
                DiceEffects = new List<int>(DiceEffects)
            };
        }
    }
}
