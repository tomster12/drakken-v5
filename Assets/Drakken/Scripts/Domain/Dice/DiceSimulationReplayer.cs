using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Drakken.Client;
using Drakken.Client.World;
using Drakken.Domain;
using UnityEngine;

namespace Drakken.Domain.Dice
{
    public class DiceSimulationReplayer
    {
        private AssetDatabase assets;
        private GameClient gameClient;

        public void Init(AssetDatabase assets, GameClient gameClient)
        {
            this.assets = assets;
            this.gameClient = gameClient;
        }

        public void Cleanup()
        {
            assets = null;
            gameClient = null;
        }

        public Task Play(DiceSimulationTraces trace, CancellationToken ct)
            => Play(trace, existingViews: null, ct);

        public async Task Play(
            DiceSimulationTraces trace,
            ScenePlayerObjects existingViews,
            CancellationToken ct)
        {
            var relevantTraces = trace.Dice.FindAll(diceTrace => diceTrace.PoseTraces.Count > 0);

            float maxTimeSeconds = 0f;
            foreach (var diceTrace in relevantTraces)
            {
                maxTimeSeconds = Mathf.Max(maxTimeSeconds, diceTrace.PoseTraces[^1].Tick * trace.FixedTimestep);
            }

            // Views currently on screen for this playback keyed by dice instance id
            // Created and destroyed as playback crosses each dice's SpawnTick / RemoveTick
            Dictionary<int, DiceView> liveViews = new();

            // Scrub through the simulation with interpolated frames
            float elapsedSeconds = 0f;
            while (elapsedSeconds < maxTimeSeconds)
            {
                if (ct.IsCancellationRequested) break;

                elapsedSeconds += Time.deltaTime;
                ApplyAtTime(relevantTraces, existingViews, liveViews, trace.FixedTimestep, elapsedSeconds);

                await Task.Yield();
            }

            // Apply with maxTimeSeconds at the end
            if (!ct.IsCancellationRequested)
            {
                ApplyAtTime(relevantTraces, existingViews, liveViews, trace.FixedTimestep, maxTimeSeconds);
            }

            // Once simulation has come to rest update effects and settled face
            if (!ct.IsCancellationRequested)
            {
                foreach (var diceTrace in relevantTraces)
                {
                    if (liveViews.TryGetValue(diceTrace.Instance.InstanceId, out var view))
                    {
                        // Update view with final game state dice instance
                        var finalInstance = view.DiceInstance;

                        // NOTE: Face effects are only refreshed on dice add, or on replay end
                        view.RefreshEffects(finalInstance);

                        _ = view.SetSettledFace(finalInstance, finalInstance.CurrentSide, ct);
                    }
                }
            }
        }

        private void ApplyAtTime(
            List<DiceSessionTrace> traces,
            ScenePlayerObjects existingViews,
            Dictionary<int, DiceView> liveViews,
            float fixedTimestep,
            float elapsedSeconds)
        {
            float rawTick = elapsedSeconds / fixedTimestep;

            // For each dice that is being traced
            foreach (var diceTrace in traces)
            {
                // Add / remove the dice view
                var diceView = EnsureLiveDiceView(diceTrace, rawTick, existingViews, liveViews);

                // Dice view is not required this tick
                if (diceView == null) continue;

                // Now sample and interpolate ticks
                var (lower, upper, t) = SampleAt(diceTrace.PoseTraces, rawTick);

                diceView.transform.SetPositionAndRotation(
                    Vector3.Lerp(lower.Position, upper.Position, t),
                    Quaternion.Slerp(lower.Rotation, upper.Rotation, t));
            }
        }

        private DiceView EnsureLiveDiceView(
            DiceSessionTrace diceTrace,
            float rawTick,
            ScenePlayerObjects existingViews,
            Dictionary<int, DiceView> liveViews)
        {
            // Check if we should exist and if we already have a view
            // We shouldn't exist if we haven't spawned, or have been removed
            bool shouldExist =
                rawTick >= diceTrace.SpawnTick &&
                (diceTrace.RemoveTick < 0 || rawTick < diceTrace.RemoveTick);

            int instanceId = diceTrace.Instance.InstanceId;

            // Use liveView and existingViews to find the diceView
            // This ensures the removal !shouldExist works on frame 0
            if (!liveViews.TryGetValue(instanceId, out var diceView))
            {
                diceView = existingViews?.FindDiceView(instanceId);
            }

            // We shouldn't have a view so remove if needed
            if (!shouldExist)
            {
                if (diceView != null)
                {
                    GameObject.Destroy(diceView.gameObject);
                    liveViews.Remove(instanceId);

                    // Ensure the passed in state is kept up to date
                    existingViews?.DiceViews.Remove(instanceId);
                }
                return null;
            }

            // Otherwise make sure this dice has a live view
            if (!liveViews.ContainsKey(instanceId))
            {
                if (diceView == null) diceView = DiceView.Create(assets, diceTrace.Instance, gameClient);
                liveViews[instanceId] = diceView;

                // Set to not settled and update face effect
                diceView.UnsetSettledFace();
                diceView.RefreshEffects(diceTrace.Instance);

                // Ensure the passed in state is kept up to date
                if (existingViews != null) existingViews.DiceViews[instanceId] = diceView;
            }

            return diceView;
        }

        private static (DicePoseTrace Lower, DicePoseTrace Upper, float T) SampleAt(List<DicePoseTrace> poses, float rawTick)
        {
            for (int i = 0; i < poses.Count - 1; i++)
            {
                if (rawTick <= poses[i + 1].Tick)
                {
                    float span = Mathf.Max(1, poses[i + 1].Tick - poses[i].Tick);
                    float t = Mathf.Clamp01((rawTick - poses[i].Tick) / span);
                    return (poses[i], poses[i + 1], t);
                }
            }

            var last = poses[^1];
            return (last, last, 0f);
        }
    }
}
