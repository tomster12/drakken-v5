using System;
using System.Threading;
using System.Threading.Tasks;

namespace Drakken.Domain.Dice.Logic
{
    public interface IEffectLogic
    {
        int EffectId { get; }
        Type ResolutionType { get; }

        void Apply(GameState gameState, EffectResolution resolution, int clientIndex, int sourceInstanceId);
        Task Animate(EffectAnimateContext ctx, EffectResolution resolution, int sourceInstanceId, CancellationToken ct);
    }

    public interface IDiceEffectLogic : IEffectLogic
    {
        bool TryModify(DiceInstance dice, DiceSimulationWorld diceWorld);
        void Execute(DiceEffectExecuteContext ctx);
    }

    public interface IFaceEffectLogic : IEffectLogic
    {
        void Execute(DiceEffectExecuteContext ctx);
    }

    public abstract class DiceEffect<TResolution> : IDiceEffectLogic
        where TResolution : EffectResolution
    {
        public abstract int EffectId { get; }

        Type IEffectLogic.ResolutionType => typeof(TResolution);

        public virtual bool TryModify(DiceInstance dice, DiceSimulationWorld diceWorld) => true;

        public virtual void Execute(DiceEffectExecuteContext ctx) { }

        protected abstract void Apply(GameState gameState, TResolution resolution, int clientIndex, int sourceInstanceId);

        protected abstract Task Animate(EffectAnimateContext ctx, TResolution resolution, int sourceInstanceId, CancellationToken ct);

        void IEffectLogic.Apply(GameState gameState, EffectResolution resolution, int clientIndex, int sourceInstanceId)
            => Apply(gameState, (TResolution)resolution, clientIndex, sourceInstanceId);

        Task IEffectLogic.Animate(EffectAnimateContext ctx, EffectResolution resolution, int sourceInstanceId, CancellationToken ct)
            => Animate(ctx, (TResolution)resolution, sourceInstanceId, ct);
    }

    public abstract class FaceEffectLogic<TResolution> : IFaceEffectLogic
        where TResolution : EffectResolution
    {
        public abstract int EffectId { get; }
        Type IEffectLogic.ResolutionType => typeof(TResolution);

        public virtual void Execute(DiceEffectExecuteContext ctx) { }

        protected abstract void Apply(GameState gameState, TResolution resolution, int clientIndex, int sourceInstanceId);
        protected abstract Task Animate(EffectAnimateContext ctx, TResolution resolution, int sourceInstanceId, CancellationToken ct);

        void IEffectLogic.Apply(GameState gameState, EffectResolution resolution, int clientIndex, int sourceInstanceId)
            => Apply(gameState, (TResolution)resolution, clientIndex, sourceInstanceId);

        Task IEffectLogic.Animate(EffectAnimateContext ctx, EffectResolution resolution, int sourceInstanceId, CancellationToken ct)
            => Animate(ctx, (TResolution)resolution, sourceInstanceId, ct);
    }
}
