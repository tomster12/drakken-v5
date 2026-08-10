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
        public Task<Dictionary<int, DiceView>> Play(AssetDatabase assets, DiceSimulationTraces trace, CancellationToken ct)
            => Play(assets, trace, existingViews: null, ct);

        // Reuses whatever view already exists (looked up by dice instance id) for any dice already on
        // screen for this player - e.g. one the session's physics disturbed without replacing it - rather
        // than spawning a duplicate. Never writes back to existingViews; placing a freshly-created view
        // into a specific slot is the caller's responsibility.
        public async Task<Dictionary<int, DiceView>> Play(
            AssetDatabase assets,
            DiceSimulationTraces trace,
            ScenePlayerObjects existingViews,
            CancellationToken ct)
        {
            List<(DiceSessionTrace Traces, DiceView View)> pairs = new();
            Dictionary<int, DiceView> viewsByInstanceId = new();

            // For each dice in the simulation trace
            foreach (var diceTrace in trace.Dice)
            {
                if (diceTrace.PoseTraces.Count == 0) continue;

                // If we have not been given a relevant diceView then make a new one
                var view = existingViews?.FindDiceView(diceTrace.Instance.InstanceId);
                if (view == null)
                {
                    view = DiceView.Create(assets, diceTrace.Instance);
                    view.transform.SetPositionAndRotation(diceTrace.PoseTraces[0].Position, diceTrace.PoseTraces[0].Rotation);
                }

                // Now track the trace / view for re-simulating
                pairs.Add((diceTrace, view));
                viewsByInstanceId[diceTrace.Instance.InstanceId] = view;
            }

            float maxTimeSeconds = 0f;
            foreach (var (Traces, _) in pairs)
            {
                maxTimeSeconds = Mathf.Max(maxTimeSeconds, Traces.PoseTraces[^1].Tick * trace.FixedTimestep);
            }

            // Scrub through the simulating with interpolated frames
            float elapsedSeconds = 0f;
            while (elapsedSeconds < maxTimeSeconds)
            {
                if (ct.IsCancellationRequested) break;

                elapsedSeconds += Time.deltaTime;
                ApplyAtTime(pairs, trace.FixedTimestep, elapsedSeconds);

                await Task.Yield();
            }

            if (!ct.IsCancellationRequested)
                ApplyAtTime(pairs, trace.FixedTimestep, maxTimeSeconds);

            return viewsByInstanceId;
        }

        private void ApplyAtTime(List<(DiceSessionTrace Traces, DiceView View)> pairs, float fixedTimestep, float elapsedSeconds)
        {
            float rawTick = elapsedSeconds / fixedTimestep;

            foreach (var (Traces, view) in pairs)
            {
                var (lower, upper, t) = SampleAt(Traces.PoseTraces, rawTick);

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
