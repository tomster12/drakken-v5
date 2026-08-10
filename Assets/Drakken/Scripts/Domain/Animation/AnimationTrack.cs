using System;

namespace Drakken.Domain.Animation
{
    public interface IAnimationTrack
    {
        float DurationSeconds { get; }
        void Apply(float t);
    }

    public class AnimationTrack<TValue> : IAnimationTrack
    {
        public float DurationSeconds { get; private set; }
        private readonly Func<float, TValue> getValueAtNormalisedTimeFunc;
        private readonly Action<TValue> applyValueFunc;

        public AnimationTrack(float durationSeconds, Func<float, TValue> getValueAtNormalisedTimeFunc, Action<TValue> applyValueFunc)
        {
            DurationSeconds = durationSeconds;
            this.getValueAtNormalisedTimeFunc = getValueAtNormalisedTimeFunc;
            this.applyValueFunc = applyValueFunc;
        }

        public void Apply(float normalisedTime)
        {
            applyValueFunc(getValueAtNormalisedTimeFunc(normalisedTime));
        }
    }
}
