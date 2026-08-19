using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Drakken.Domain;
using Unity.Netcode;

namespace Drakken.Gameplay.Simulation
{
    // Events not owned by any specific dice/face ability or token - reusable simulation
    // primitives that any effect can record, such as adding a freshly created dice to GameState.
    public static class CommonEventIds
    {
        public const int AddDice = 1;
    }

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

        public void ApplyEvent(GameState gameState, EventResolution resolution, int clientIndex, int sourceInstanceId)
            => gameState.Clients[clientIndex].Dice.Add(((AddDiceResolution)resolution).AddedDiceInstance);

        public Task AnimateEvent(EventAnimateContext ctx, EventResolution resolution, int sourceInstanceId, CancellationToken ct)
            => Task.CompletedTask;
    }

    public static class CommonEventRegistry
    {
        private static readonly Dictionary<int, IEventLogic> byId = new()
        {
            [CommonEventIds.AddDice] = new AddDiceEventLogic(),
        };

        public static IEventLogic Get(int eventId)
            => byId.TryGetValue(eventId, out var logic) ? logic : null;
    }
}
