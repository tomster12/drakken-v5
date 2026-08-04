using System;
using System.Collections.Generic;
using System.IO.Compression;
using System.Threading;
using System.Threading.Tasks;
using Drakken.Common.Utility;
using UnityEngine;

namespace Drakken.Client.World.Animation
{
    public class AnimationPlayer
    {
        public event Action OnAnimationCompleted;
        public bool IsAnimating { get; private set; }
        private CancellationTokenSource animationStoppingCts;
        private CancellationTokenSource animationCts;

        public async Task Play(AnimationSequence sequence, CancellationToken parentCt)
        {
            if (IsAnimating || animationCts != null)
                throw new InvalidOperationException("Cannot Play() animation while already animating");

            IsAnimating = true;

            animationStoppingCts = new();
            animationCts = CancellationTokenSource.CreateLinkedTokenSource(animationStoppingCts.Token, parentCt);
            var ct = animationCts.Token;

            try
            {
                // Keep track of completed tracks / fired callbacks
                var completedTrackIndices = new HashSet<int>();
                var firedCallbackIndices = new HashSet<int>();
                float elapsedSeconds = 0f;

                // Iterate until total duration at deltaTime increments
                while (elapsedSeconds < sequence.TotalDurationSeconds)
                {
                    if (ct.IsCancellationRequested) return;

                    elapsedSeconds += Time.deltaTime;

                    // Apply tracks and fire callbacks
                    ApplyTracksAtTime(sequence, elapsedSeconds, completedTrackIndices);
                    FireCallbacksAtTime(sequence, elapsedSeconds, firedCallbackIndices);

                    await Task.Yield();
                }

                if (ct.IsCancellationRequested) return;

                // Apply all tracks / callbacks at the end
                ApplyTracksAtTime(sequence, sequence.TotalDurationSeconds, completedTrackIndices);
                FireCallbacksAtTime(sequence, sequence.TotalDurationSeconds, firedCallbackIndices);
                IsAnimating = false;

                if (!ct.IsCancellationRequested)
                {
                    OnAnimationCompleted?.Invoke();
                }
            }
            finally
            {
                IsAnimating = false;

                animationStoppingCts.Dispose();
                animationCts.Dispose();

                animationStoppingCts = null;
                animationCts = null;
            }
        }

        private static void ApplyTracksAtTime(AnimationSequence sequence, float elapsedSeconds, HashSet<int> completedTrackIndices)
        {
            for (int i = 0; i < sequence.TimedTracks.Count; i++)
            {
                if (completedTrackIndices.Contains(i)) continue;

                // Play track when (start < time < end)
                var (startTimeSeconds, track) = sequence.TimedTracks[i];
                if (elapsedSeconds < startTimeSeconds) continue;

                float localElapsedSeconds = elapsedSeconds - startTimeSeconds;
                float normalizedTime = track.DurationSeconds <= 0f ? 1f : Mathf.Clamp01(localElapsedSeconds / track.DurationSeconds);
                track.Apply(normalizedTime);

                // Complete once when (time >= end)
                if (normalizedTime >= 1f)
                {
                    completedTrackIndices.Add(i);
                }
            }
        }

        private static void FireCallbacksAtTime(AnimationSequence sequence, float elapsedSeconds, HashSet<int> firedCallbackIndices)
        {
            for (int i = 0; i < sequence.TimedCallbacks.Count; i++)
            {
                if (firedCallbackIndices.Contains(i)) continue;

                // Trigger once we have gone past start
                var (startTimeSeconds, callback) = sequence.TimedCallbacks[i];
                if (elapsedSeconds < startTimeSeconds) continue;

                firedCallbackIndices.Add(i);
                callback();
            }
        }

        public void Stop()
        {
            animationStoppingCts?.Cancel();
        }

        public void Dispose()
        {
            animationStoppingCts?.Cancel();

            animationStoppingCts?.Dispose();
            animationCts?.Dispose();
        }
    }
}
