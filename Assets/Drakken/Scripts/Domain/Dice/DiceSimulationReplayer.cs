using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Drakken.Client;
using Drakken.Client.World;
using UnityEngine;

namespace Drakken.Domain.Dice
{
    public class DiceSimulationReplayer
    {
        public Task<Dictionary<int, DiceView>> Play(
            AssetDatabase assets, DiceSimulationTraces trace, IGameStateProvider gameStateProvider, CancellationToken ct)
            => Play(assets, trace, existingViews: null, gameStateProvider, ct);

        public async Task<Dictionary<int, DiceView>> Play(
            AssetDatabase assets,
            DiceSimulationTraces trace,
            ScenePlayerObjects existingViews,
            IGameStateProvider gameStateProvider,
            CancellationToken ct)
        {
            var relevantTraces = trace.Dice.FindAll(diceTrace => diceTrace.PoseTraces.Count > 0);

            float maxTimeSeconds = 0f;
            foreach (var diceTrace in relevantTraces)
            {
                maxTimeSeconds = Mathf.Max(maxTimeSeconds, diceTrace.PoseTraces[^1].Tick * trace.FixedTimestep);
            }

            // Views currently on screen for this playback, keyed by dice instance id
            // Created and destroyed as playback crosses each dice's SpawnTick / RemoveTick
            Dictionary<int, DiceView> liveViews = new();
            Dictionary<int, DiceView> resultViews = new();

            // Scrub through the simulation with interpolated frames
            float elapsedSeconds = 0f;
            while (elapsedSeconds < maxTimeSeconds)
            {
                if (ct.IsCancellationRequested) break;

                elapsedSeconds += Time.deltaTime;
                ApplyAtTime(assets, relevantTraces, existingViews, liveViews, resultViews, gameStateProvider, trace.FixedTimestep, elapsedSeconds);

                await Task.Yield();
            }

            if (!ct.IsCancellationRequested)
                ApplyAtTime(assets, relevantTraces, existingViews, liveViews, resultViews, gameStateProvider, trace.FixedTimestep, maxTimeSeconds);

            return resultViews;
        }

        private void ApplyAtTime(
            AssetDatabase assets,
            List<DiceSessionTrace> traces,
            ScenePlayerObjects existingViews,
            Dictionary<int, DiceView> liveViews,
            Dictionary<int, DiceView> resultViews,
            IGameStateProvider gameStateProvider,
            float fixedTimestep,
            float elapsedSeconds)
        {
            float rawTick = elapsedSeconds / fixedTimestep;

            // For each dice that is being traced
            foreach (var diceTrace in traces)
            {
                int instanceId = diceTrace.Instance.InstanceId;

                bool shouldExist =
                    rawTick >= diceTrace.SpawnTick &&
                    (diceTrace.RemoveTick < 0 || rawTick < diceTrace.RemoveTick);

                liveViews.TryGetValue(instanceId, out var view);

                // we are not spawned, or removed, ensure we dont have a dice view
                if (!shouldExist)
                {
                    if (view != null)
                    {
                        Object.Destroy(view.gameObject);
                        liveViews.Remove(instanceId);
                    }
                    continue;
                }

                // Otherwise make sure that we have a live view assigned
                if (view == null)
                {
                    // Reuse existing view or create a new one
                    view = existingViews?.FindDiceView(instanceId);
                    if (view == null) view = DiceView.Create(assets, diceTrace.Instance, gameStateProvider);
                    liveViews[instanceId] = view;

                    // We can only guaranteed add this dice to the result if it is not removed
                    if (diceTrace.RemoveTick < 0)
                    {
                        resultViews[instanceId] = view;
                    }
                }

                // Now we have dice views sorted sample and interpolate traces
                var (lower, upper, t) = SampleAt(diceTrace.PoseTraces, rawTick);

                view.transform.SetPositionAndRotation(
                    Vector3.Lerp(lower.Position, upper.Position, t),
                    Quaternion.Slerp(lower.Rotation, upper.Rotation, t));
            }
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
