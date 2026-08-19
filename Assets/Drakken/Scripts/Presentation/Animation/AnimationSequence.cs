using System;
using System.Collections.Generic;
using UnityEngine;

namespace Drakken.Presentation.Animation
{
    public class AnimationSequence
    {
        public IReadOnlyList<(float StartTimeSeconds, IAnimationTrack Track)> TimedTracks { get; }
        public IReadOnlyList<(float StartTimeSeconds, Action Callback)> TimedCallbacks { get; }
        public float TotalDurationSeconds { get; }

        public AnimationSequence(
            IReadOnlyList<(float StartTimeSeconds, IAnimationTrack Track)> timedTracks,
            IReadOnlyList<(float StartTimeSeconds, Action Callback)> timedCallbacks)
        {
            TimedTracks = timedTracks;
            TimedCallbacks = timedCallbacks;

            float totalDurationSeconds = 0f;
            foreach (var (startTimeSeconds, track) in timedTracks)
            {
                totalDurationSeconds = Mathf.Max(totalDurationSeconds, startTimeSeconds + track.DurationSeconds);
            }
            foreach (var (startTimeSeconds, callback) in timedCallbacks)
            {
                totalDurationSeconds = Mathf.Max(totalDurationSeconds, startTimeSeconds);
            }

            TotalDurationSeconds = totalDurationSeconds;
        }
    }

    public class AnimationSequenceBuilder
    {
        private readonly List<(float StartTimeSeconds, IAnimationTrack Track)> timedTracks = new();
        private readonly List<(float StartTimeSeconds, Action Callback)> timedCallbacks = new();
        private float cursorSeconds = 0f;

        public static AnimationSequenceBuilder Start() => new();

        public AnimationSequenceBuilder Next(params IAnimationTrack[] tracks)
        {
            foreach (var track in tracks)
            {
                timedTracks.Add((cursorSeconds, track));
            }

            float groupDurationSeconds = 0f;
            foreach (var track in tracks)
            {
                groupDurationSeconds = Mathf.Max(groupDurationSeconds, track.DurationSeconds);
            }

            cursorSeconds += groupDurationSeconds;
            return this;
        }

        public AnimationSequenceBuilder Next(params Action[] callbacks)
        {
            foreach (var callback in callbacks)
            {
                timedCallbacks.Add((cursorSeconds, callback));
            }
            return this;
        }

        public AnimationSequenceBuilder At(float startTimeSeconds, IAnimationTrack track)
        {
            timedTracks.Add((startTimeSeconds, track));
            return this;
        }

        public AnimationSequenceBuilder At(float startTimeSeconds, Action callback)
        {
            timedCallbacks.Add((startTimeSeconds, callback));
            return this;
        }

        public AnimationSequence Build()
        {
            return new AnimationSequence(timedTracks, timedCallbacks);
        }
    }
}