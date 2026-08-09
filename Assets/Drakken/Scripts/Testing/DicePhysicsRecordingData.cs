using System.Collections.Generic;
using Drakken.Domain;
using UnityEngine;

namespace Drakken.Testing
{
    public class DicePhysicsReplayTrace
    {
        public List<DiceInstanceRecord> dice;
        public List<DicePhysicsTickSnapshot> tickSnapshots;
        public float FixedTimestep;
    }

    public struct DiceInstanceRecord
    {
        public DiceInstance Instance;
        public Vector3 spawnPosition;
    }

    public struct DicePhysicsTickSnapshot
    {
        public int tick;
        public List<DicePhysicsInstanceSnapshot> InstanceSnapshots;
    }

    public struct DicePhysicsInstanceSnapshot
    {
        public int DiceInstanceId;
        public Vector3 Position;
        public Quaternion Rotation;
    }
}
