using System.Collections.Generic;
using Drakken.Domain.Networking;
using Unity.Netcode;
using UnityEngine;

namespace Drakken.Domain
{
    public class DiceSimulationTraces : INetworkSerializable
    {
        public float FixedTimestep;
        public List<DiceSessionTrace> Dice = new();

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref FixedTimestep);
            serializer.SerializeList(ref Dice);
        }
    }

    public class DiceSessionTrace : INetworkSerializable
    {
        public DiceInstance Instance = new();
        public List<DicePoseTrace> PoseTraces = new();
        public int SpawnTick;
        public int RemoveTick = -1;

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref Instance);
            serializer.SerializeValue(ref SpawnTick);
            serializer.SerializeValue(ref RemoveTick);
            serializer.SerializeList(ref PoseTraces);
        }
    }

    public class DicePoseTrace : INetworkSerializable
    {
        public int Tick;
        public Vector3 Position;
        public Quaternion Rotation;

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref Tick);
            serializer.SerializeValue(ref Position);
            serializer.SerializeValue(ref Rotation);
        }
    }
}
