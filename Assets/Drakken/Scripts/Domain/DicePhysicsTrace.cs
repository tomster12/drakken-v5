using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

namespace Drakken.Domain
{
    // One playback window recorded from a DicePhysicsWorld action (a draft roll, a round-start
    // reroll, a token's dice effect). Positions/rotations only - which face is "up" is decided once,
    // server-side, and carried on DiceTraceRecord.Instance.Value. Clients never re-derive it.
    public class DicePhysicsTrace : INetworkSerializable
    {
        public float FixedTimestep;
        public List<DiceTraceRecord> Dice = new();

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref FixedTimestep);

            if (serializer.IsReader)
            {
                int count = 0;
                serializer.SerializeValue(ref count);
                Dice = new(count);
                for (int i = 0; i < count; i++)
                {
                    DiceTraceRecord record = new();
                    record.NetworkSerialize(serializer);
                    Dice.Add(record);
                }
            }
            else
            {
                int count = Dice.Count;
                serializer.SerializeValue(ref count);
                for (int i = 0; i < count; i++)
                {
                    Dice[i].NetworkSerialize(serializer);
                }
            }
        }
    }

    public class DiceTraceRecord : INetworkSerializable
    {
        public DiceInstance Instance = new();
        public int ParentInstanceId; // 0 when this die wasn't split off another (instance ids start at 1)
        public int SpawnTick; // tick, relative to this trace, this die first appears at Poses[0]
        public List<DicePoseSample> Poses = new();
        public int RemoveTick = -1; // -1 when the die is still present at the end of this trace

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref Instance);
            serializer.SerializeValue(ref ParentInstanceId);
            serializer.SerializeValue(ref SpawnTick);
            serializer.SerializeValue(ref RemoveTick);

            if (serializer.IsReader)
            {
                int count = 0;
                serializer.SerializeValue(ref count);
                Poses = new(count);
                for (int i = 0; i < count; i++)
                {
                    int tick = 0;
                    Vector3 position = default;
                    Quaternion rotation = default;
                    serializer.SerializeValue(ref tick);
                    serializer.SerializeValue(ref position);
                    serializer.SerializeValue(ref rotation);
                    Poses.Add(new DicePoseSample { Tick = tick, Position = position, Rotation = rotation });
                }
            }
            else
            {
                int count = Poses.Count;
                serializer.SerializeValue(ref count);
                for (int i = 0; i < count; i++)
                {
                    int tick = Poses[i].Tick;
                    Vector3 position = Poses[i].Position;
                    Quaternion rotation = Poses[i].Rotation;
                    serializer.SerializeValue(ref tick);
                    serializer.SerializeValue(ref position);
                    serializer.SerializeValue(ref rotation);
                }
            }
        }
    }

    public struct DicePoseSample
    {
        public int Tick;
        public Vector3 Position;
        public Quaternion Rotation;
    }
}
