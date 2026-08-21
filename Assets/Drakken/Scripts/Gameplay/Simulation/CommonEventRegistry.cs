using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Drakken.Domain;
using Drakken.Domain.Networking;
using Unity.Netcode;

namespace Drakken.Gameplay.Simulation
{
    // Events not owned by any specific dice/face ability or token - reusable simulation
    // primitives that any effect can record, such as adding a freshly created dice to GameState.
    public static class CommonEventIds
    {
        public const int AddDice = 1;
        public const int RemoveDice = 2;
        public const int SetFaceEffects = 3;
    }

    // Recorded automatically by GameSimulationWorld.SpawnDice - identity is added to GameState
    // the moment a dice enters the simulation, not something callers need to record by hand.
    public class AddDiceResolution : EventResolution
    {
        public DiceInstance AddedDiceInstance;

        public override void NetworkSerialize<T>(BufferSerializer<T> serializer)
        {
            if (serializer.IsReader) AddedDiceInstance = new DiceInstance();
            serializer.SerializeValue(ref AddedDiceInstance);
        }
    }

    public class AddDiceEventLogic : IEventLogic
    {
        public int EventId => CommonEventIds.AddDice;
        public Type ResolutionType => typeof(AddDiceResolution);

        public void ApplyEvent(GameState gameState, EventResolution resolution, int clientIndex)
            => gameState.Clients[clientIndex].Dice.Add(((AddDiceResolution)resolution).AddedDiceInstance);

        public Task AnimateEvent(EventAnimateContext ctx, EventResolution resolution, CancellationToken ct)
            => Task.CompletedTask;
    }

    // Recorded automatically by GameSimulationWorld.RemoveDice - identity is dropped from
    // GameState the moment a dice leaves the simulation, not something callers need to record by
    // hand. A token/effect that swaps one dice for another (Dragon, Forge, Reinforce, Mitosis
    // split, ...) just calls RemoveDice + SpawnDice and gets both halves of the swap for free.
    public class RemoveDiceResolution : EventResolution
    {
        public int InstanceId;

        public override void NetworkSerialize<T>(BufferSerializer<T> serializer)
            => serializer.SerializeValue(ref InstanceId);
    }

    public class RemoveDiceEventLogic : IEventLogic
    {
        public int EventId => CommonEventIds.RemoveDice;
        public Type ResolutionType => typeof(RemoveDiceResolution);

        public void ApplyEvent(GameState gameState, EventResolution resolution, int clientIndex)
            => gameState.Clients[clientIndex].Dice.RemoveAll(d => d.InstanceId == ((RemoveDiceResolution)resolution).InstanceId);

        public Task AnimateEvent(EventAnimateContext ctx, EventResolution resolution, CancellationToken ct)
            => Task.CompletedTask;
    }

    // Sets which faces of a single existing dice carry a given face-effect id. Replace=true clears
    // every face currently carrying the effect before marking FaceIndices (a bulk "these faces are
    // now marked" call); Replace=false only removes the effect from FaceIndices (a "this specific
    // face's mark is spent" call). Covers both ends of Mitosis's marking without a bespoke event.
    public class SetFaceEffectsResolution : EventResolution
    {
        public int SourceInstanceId;
        public int EffectId;
        public bool Replace;
        public List<int> FaceIndices = new();

        public override void NetworkSerialize<T>(BufferSerializer<T> serializer)
        {
            serializer.SerializeValue(ref SourceInstanceId);
            serializer.SerializeValue(ref EffectId);
            serializer.SerializeValue(ref Replace);
            serializer.SerializeList(ref FaceIndices);
        }
    }

    public class SetFaceEffectsEventLogic : IEventLogic
    {
        public int EventId => CommonEventIds.SetFaceEffects;
        public Type ResolutionType => typeof(SetFaceEffectsResolution);

        public void ApplyEvent(GameState gameState, EventResolution resolution, int clientIndex)
        {
            var r = (SetFaceEffectsResolution)resolution;
            var dice = gameState.Clients[clientIndex].Dice.Find(d => d.InstanceId == r.SourceInstanceId);
            if (dice == null) return;

            if (r.Replace)
            {
                foreach (var face in dice.Faces) face.FaceEffects.Remove(r.EffectId);
                foreach (int faceIndex in r.FaceIndices) dice.Faces[faceIndex].FaceEffects.Add(r.EffectId);
            }
            else
            {
                foreach (int faceIndex in r.FaceIndices) dice.Faces[faceIndex].FaceEffects.Remove(r.EffectId);
            }
        }

        public Task AnimateEvent(EventAnimateContext ctx, EventResolution resolution, CancellationToken ct)
            => Task.CompletedTask;
    }

    public static class CommonEventRegistry
    {
        private static readonly Dictionary<int, IEventLogic> byId = new()
        {
            [CommonEventIds.AddDice] = new AddDiceEventLogic(),
            [CommonEventIds.RemoveDice] = new RemoveDiceEventLogic(),
            [CommonEventIds.SetFaceEffects] = new SetFaceEffectsEventLogic(),
        };

        public static IEventLogic Get(int eventId)
            => byId.TryGetValue(eventId, out var logic) ? logic : null;
    }
}
