using System;
using System.Threading;
using System.Threading.Tasks;
using Drakken.Domain;

namespace Drakken.Gameplay.Simulation
{
    public interface IEventLogic
    {
        int EffectId { get; }
        Type ResolutionType { get; }

        // Apply must be an absolute/idempotent state set (never a relative delta - the same
        // event may be the "current" state of an object that a later event also rewrites
        // wholesale) and must no-op safely if its target no longer exists in this gameState.
        void Apply(GameState gameState, EventResolution resolution, int clientIndex, int sourceInstanceId);
        Task Animate(EventAnimateContext ctx, EventResolution resolution, int sourceInstanceId, CancellationToken ct);
    }
}
