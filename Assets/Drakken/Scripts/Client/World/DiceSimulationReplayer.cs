using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Drakken.Client;
using Drakken.Domain;
using UnityEngine;

namespace Drakken.Client.World
{
    public class DiceSimulationReplayer
    {
        public async Task<DiceView[]> Play(AssetDatabase assets, DiceSimulationTraces trace, CancellationToken ct)
        {
            // Plays the physics trace and returns the DiceViews it created, left at their final settled pose -
            // these become the permanent, interactable dice for the rest of the game (no separate "row" spawn).

            List<(DiceLifetimeTrace Record, DiceView View)> pairs = new();

            float maxTimeSeconds = 0f;
            foreach (var diceRecord in trace.Dice)
            {
                if (diceRecord.PoseTraces.Count == 0) continue;

                var view = DiceView.Create(assets, diceRecord.Instance);
                view.transform.SetPositionAndRotation(diceRecord.PoseTraces[0].Position, diceRecord.PoseTraces[0].Rotation);
                pairs.Add((diceRecord, view));

                maxTimeSeconds = Mathf.Max(maxTimeSeconds, diceRecord.PoseTraces[^1].Tick * trace.FixedTimestep);
            }

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

            return pairs.Select(pair => pair.View).ToArray();
        }

        private void ApplyAtTime(List<(DiceLifetimeTrace Record, DiceView View)> pairs, float fixedTimestep, float elapsedSeconds)
        {
            float rawTick = elapsedSeconds / fixedTimestep;

            foreach (var (record, view) in pairs)
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
    }
}
