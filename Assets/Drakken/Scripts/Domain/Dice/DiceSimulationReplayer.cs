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
        public Task<Dictionary<int, DiceView>> Play(
            AssetDatabase assets, GameClient gameClient, DiceSimulationTraces trace, CancellationToken ct)
            => Play(assets, gameClient, trace, existingViews: null, ct);

        public async Task<Dictionary<int, DiceView>> Play(
            AssetDatabase assets,
            GameClient gameClient,
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

            // liveViews is views currently on screen for this playback, keyed by dice instance id
            // Created and destroyed as playback crosses each dice's SpawnTick / RemoveTick
            // resultViews is the final list of diceViews after all changrs
            Dictionary<int, DiceView> liveViews = new();
            Dictionary<int, DiceView> resultViews = new();

            // Scrub through the simulation with interpolated frames
            float elapsedSeconds = 0f;
            while (elapsedSeconds < maxTimeSeconds)
            {
                if (ct.IsCancellationRequested) break;

                elapsedSeconds += Time.deltaTime;
                ApplyAtTime(assets, gameClient, relevantTraces, existingViews, liveViews, resultViews, trace.FixedTimestep, elapsedSeconds);

                await Task.Yield();
            }

            // Apply with maxTimeSeconds at the end
            if (!ct.IsCancellationRequested)
            {
                ApplyAtTime(assets, gameClient, relevantTraces, existingViews, liveViews, resultViews, trace.FixedTimestep, maxTimeSeconds);
            }

            // Once simulation has come to rest update effects and settled face
            if (!ct.IsCancellationRequested)
            {
                foreach (var diceTrace in relevantTraces)
                {
                    if (resultViews.TryGetValue(diceTrace.Instance.InstanceId, out var view))
                    {
                        var settledFace = diceTrace.Instance.Faces[diceTrace.Instance.CurrentSide];
                        
                        // NOTE: Face effects are only refreshed on dice add, or on replay end
                        view.RefreshFaceEffects(diceTrace.Instance);

                        _ = view.SetSettledFace(diceTrace.Instance.CurrentSide, ct);
                    }
                }
            }

            return resultViews;
        }

        private void ApplyAtTime(
            AssetDatabase assets,
            GameClient gameClient,
            List<DiceSessionTrace> traces,
            ScenePlayerObjects existingViews,
            Dictionary<int, DiceView> liveViews,
            Dictionary<int, DiceView> resultViews,
            float fixedTimestep,
            float elapsedSeconds)
        {
            float rawTick = elapsedSeconds / fixedTimestep;

            // For each dice that is being traced
            foreach (var diceTrace in traces)
            {
                // Add / remove the dice view
                var diceView = EnsureLiveDiceView(
                    assets, gameClient,
                    diceTrace, rawTick,
                    existingViews, liveViews, resultViews);

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
            AssetDatabase assets,
            GameClient gameClient,
            DiceSessionTrace diceTrace,
            float rawTick,
            ScenePlayerObjects existingViews,
            Dictionary<int, DiceView> liveViews,
            Dictionary<int, DiceView> resultViews)
        {
            // Check if we should exist and if we already have a view
            // We shouldn't exist if we haven't spawned, or have been removed
            bool shouldExist =
                rawTick >= diceTrace.SpawnTick &&
                (diceTrace.RemoveTick < 0 || rawTick < diceTrace.RemoveTick);

            int instanceId = diceTrace.Instance.InstanceId;
            liveViews.TryGetValue(instanceId, out var diceView);

            // We shouldn't have a view so remove if needed
            if (!shouldExist)
            {
                if (diceView != null)
                {
                    GameObject.Destroy(diceView.gameObject);
                    liveViews.Remove(instanceId);
                }
                return null;
            }

            // Otherwise make sure this dice has a live view
            if (diceView == null)
            {
                // Reuse existing view or create a new one
                diceView = existingViews?.FindDiceView(instanceId);
                if (diceView == null) diceView = DiceView.Create(assets, diceTrace.Instance, gameClient);
                liveViews[instanceId] = diceView;

                // Set to not settled and update face effect
                diceView.UnsetSettledFace();
                diceView.RefreshFaceEffects(diceTrace.Instance);

                // If this isn't being removed then we can immediately add to resultViews
                if (diceTrace.RemoveTick < 0) resultViews[instanceId] = diceView;
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
