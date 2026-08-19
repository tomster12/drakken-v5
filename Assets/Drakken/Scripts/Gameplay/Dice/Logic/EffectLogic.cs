using System;
using System.Threading;
using System.Threading.Tasks;
using Drakken.Domain;
using Drakken.Gameplay.Simulation;

namespace Drakken.Gameplay.Dice.Logic
{
    public interface IDiceEffectLogic : IEventLogic
    {
        bool TryModify(DiceInstance dice, GameSimulationWorld diceWorld);
        void Execute(DiceEffectExecuteContext ctx);
    }

    public interface IFaceEffectLogic : IEventLogic
    {
        void Execute(DiceEffectExecuteContext ctx);

        // Called for every other face-effect instance present on the dice when a different face lands instead
        void OnMiss(DiceEffectExecuteContext ctx);
    }

    public abstract class DiceEffectLogic<TResolution> : IDiceEffectLogic
        where TResolution : EventResolution
    {
        public abstract int EffectId { get; }

        Type IEventLogic.ResolutionType => typeof(TResolution);

        public virtual bool TryModify(DiceInstance dice, GameSimulationWorld diceWorld) => true;

        public virtual void Execute(DiceEffectExecuteContext ctx) { }

        protected abstract void Apply(GameState gameState, TResolution resolution, int clientIndex, int sourceInstanceId);

        protected virtual Task Animate(EventAnimateContext ctx, TResolution resolution, int sourceInstanceId, CancellationToken ct) => Task.CompletedTask;

        void IEventLogic.Apply(GameState gameState, EventResolution resolution, int clientIndex, int sourceInstanceId)
            => Apply(gameState, (TResolution)resolution, clientIndex, sourceInstanceId);

        Task IEventLogic.Animate(EventAnimateContext ctx, EventResolution resolution, int sourceInstanceId, CancellationToken ct)
            => Animate(ctx, (TResolution)resolution, sourceInstanceId, ct);
    }

    public abstract class FaceEffectLogic<TResolution> : IFaceEffectLogic
        where TResolution : EventResolution
    {
        public abstract int EffectId { get; }
        Type IEventLogic.ResolutionType => typeof(TResolution);

        public virtual void Execute(DiceEffectExecuteContext ctx) { }

        public virtual void OnMiss(DiceEffectExecuteContext ctx) { }

        protected abstract void Apply(GameState gameState, TResolution resolution, int clientIndex, int sourceInstanceId);

        protected virtual Task Animate(EventAnimateContext ctx, TResolution resolution, int sourceInstanceId, CancellationToken ct) => Task.CompletedTask;

        void IEventLogic.Apply(GameState gameState, EventResolution resolution, int clientIndex, int sourceInstanceId)
            => Apply(gameState, (TResolution)resolution, clientIndex, sourceInstanceId);

        Task IEventLogic.Animate(EventAnimateContext ctx, EventResolution resolution, int sourceInstanceId, CancellationToken ct)
            => Animate(ctx, (TResolution)resolution, sourceInstanceId, ct);
    }
}
