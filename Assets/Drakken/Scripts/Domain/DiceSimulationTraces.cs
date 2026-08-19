using System;
using System.Collections.Generic;
using Drakken.Domain.Dice.Logic;
using Drakken.Domain.Networking;
using Unity.Netcode;
using UnityEngine;

namespace Drakken.Domain
{
    public class DiceSimulationTraces : INetworkSerializable
    {
        public float FixedTimestep;
        public List<DiceSessionTrace> Dice = new();
        public List<EffectEvent> EffectEvents = new();

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref FixedTimestep);
            serializer.SerializeList(ref Dice);
            serializer.SerializeList(ref EffectEvents);
        }

        public void ApplyEffects(GameState gameState, int clientIndex)
        {
            foreach (var occurrence in EffectEvents)
            {
                var effect = EffectRegistry.Get(occurrence.EffectId, occurrence.IsFaceEffect);
                effect?.Apply(gameState, occurrence.Resolution, clientIndex, occurrence.SourceInstanceId);
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

    public class EffectEvent : INetworkSerializable
    {
        public int EffectId;
        public bool IsFaceEffect;
        public int Tick;
        public int SourceInstanceId;
        public EffectResolution Resolution;

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref EffectId);
            serializer.SerializeValue(ref IsFaceEffect);
            serializer.SerializeValue(ref Tick);
            serializer.SerializeValue(ref SourceInstanceId);

            if (serializer.IsReader)
            {
                var effect = EffectRegistry.Get(EffectId, IsFaceEffect);
                Resolution = (EffectResolution)Activator.CreateInstance(effect.ResolutionType);
            }

            Resolution.NetworkSerialize(serializer);
        }
    }
}
