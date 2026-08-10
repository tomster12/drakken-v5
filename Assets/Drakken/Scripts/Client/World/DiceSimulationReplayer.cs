using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Drakken.Client;
using Drakken.Domain;
using UnityEngine;

namespace Drakken.Client.World
{
    public class DiceSimulationReplayer
    {
        private readonly List<(DiceLifetimeTrace Record, DiceView View)> activePairs = new();

        public async Task Play(AssetDatabase assets, DiceSimulationTraces trace, CancellationToken ct)
        {
            ClearAll();

            float maxTimeSeconds = 0f;
            foreach (var record in trace.Dice)
            {
                if (record.PoseTraces.Count == 0) continue;

                var view = DiceView.CreateProcedural(assets, record.Instance);
                view.transform.SetPositionAndRotation(record.PoseTraces[0].Position, record.PoseTraces[0].Rotation);
                activePairs.Add((record, view));

                maxTimeSeconds = Mathf.Max(maxTimeSeconds, record.PoseTraces[^1].Tick * trace.FixedTimestep);
            }

            float elapsedSeconds = 0f;
            while (elapsedSeconds < maxTimeSeconds)
            {
                if (ct.IsCancellationRequested) return;

                elapsedSeconds += Time.deltaTime;
                ApplyAtTime(trace.FixedTimestep, elapsedSeconds);

                await Task.Yield();
            }

            if (ct.IsCancellationRequested) return;

            ApplyAtTime(trace.FixedTimestep, maxTimeSeconds);
        }

        private void ApplyAtTime(float fixedTimestep, float elapsedSeconds)
        {
            float rawTick = elapsedSeconds / fixedTimestep;

            foreach (var (record, view) in activePairs)
            {
                var (lower, upper, t) = SampleAt(record.PoseTraces, rawTick);

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

        public void ClearAll()
        {
            foreach (var (_, view) in activePairs)
            {
                GameObject.Destroy(view.gameObject);
            }
            activePairs.Clear();
        }
    }
}
