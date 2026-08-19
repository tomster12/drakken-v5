using System;
using System.Collections.Generic;
using Drakken.Gameplay.Simulation;
using Drakken.Domain.Networking;
using Unity.Netcode;
using UnityEngine;

namespace Drakken.Domain
{
    public class GameSimulationTrace : INetworkSerializable
    {
        public float FixedTimestep;
        public List<DiceSessionTrace> Dice = new();
        public List<SimulationEvent> Events = new();

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref FixedTimestep);
            serializer.SerializeList(ref Dice);
            serializer.SerializeList(ref Events);
        }

        public void ApplyEffects(GameState gameState, int clientIndex)
        {
            foreach (var evt in Events)
            {
                var logic = EventRegistry.Get(evt.EventId, evt.Kind);
                logic?.ApplyEvent(gameState, evt.Resolution, clientIndex, evt.SourceInstanceId);
            }
        }

        public static void ApplyAll(List<GameSimulationTrace> traces, GameState gameState, int clientIndex)
        {
            foreach (var trace in traces)
            {
                trace?.ApplyEffects(gameState, clientIndex);
            }
        }
    }

    public class DiceSessionTrace : INetworkSerializable
    {
        public DiceInstance Instance = new();
        public List<DicePoseTrace> PoseTraces = new();
        public List<DiceSettleEvent> SettleEvents = new();
        public int SpawnTick;
        public int RemoveTick = -1;

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref Instance);
            serializer.SerializeValue(ref SpawnTick);
            serializer.SerializeValue(ref RemoveTick);
            serializer.SerializeList(ref PoseTraces);
            serializer.SerializeList(ref SettleEvents);
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

    public class DiceSettleEvent : INetworkSerializable
    {
        public int Tick;
        public int Side;

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref Tick);
            serializer.SerializeValue(ref Side);
        }
    }

    public class SimulationEvent : INetworkSerializable
    {
        public int EventId;
        public EventKind Kind;
        public int Tick;
        public int SourceInstanceId;
        public int Side;
        public EventResolution Resolution;

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref EventId);
            serializer.SerializeValue(ref Kind);
            serializer.SerializeValue(ref Tick);
            serializer.SerializeValue(ref SourceInstanceId);
            serializer.SerializeValue(ref Side);

            if (serializer.IsReader)
            {
                var logic = EventRegistry.Get(EventId, Kind);
                Resolution = (EventResolution)Activator.CreateInstance(logic.ResolutionType);
            }

            Resolution.NetworkSerialize(serializer);
        }
    }
}
