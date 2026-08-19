using System;
using System.Threading;
using System.Threading.Tasks;
using Drakken.Domain;

namespace Drakken.Gameplay.Simulation
{
    public interface IEventLogic
    {
        int EventId { get; }
        Type ResolutionType { get; }

        void ApplyEvent(GameState gameState, EventResolution resolution, int clientIndex, int sourceInstanceId);

        Task AnimateEvent(EventAnimateContext ctx, EventResolution resolution, int sourceInstanceId, CancellationToken ct);
    }
}
