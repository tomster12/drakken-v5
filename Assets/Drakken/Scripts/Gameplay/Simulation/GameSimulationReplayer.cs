using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Drakken.Client;
using Drakken.Client.World;
using Drakken.Domain;
using UnityEngine;

namespace Drakken.Gameplay.Simulation
{
    public class GameSimulationReplayer
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

        public async Task Play(
            GameSimulationTrace trace,
            CancellationToken ct,
            ScenePlayerObjects existingViews = null)
        {
            var relevantTraces = trace.Dice.FindAll(diceTrace => diceTrace.PoseTraces.Count > 0);

            float maxTimeSeconds = 0f;
            foreach (var diceTrace in relevantTraces)
            {
                maxTimeSeconds = Mathf.Max(maxTimeSeconds, diceTrace.PoseTraces[^1].Tick * trace.FixedTimestep);
            }

            Dictionary<int, DiceView> liveViews = new();
            Dictionary<int, int> nextSettleIndices = new();
            int nextEventIndex = 0;
            List<Task> effectTasks = new();

            float elapsedSeconds = 0f;
            while (elapsedSeconds < maxTimeSeconds)
            {
                if (ct.IsCancellationRequested) break;

                elapsedSeconds += Time.deltaTime;
                float rawTick = elapsedSeconds / trace.FixedTimestep;

                ApplyAtTime(relevantTraces, existingViews, liveViews, nextSettleIndices, rawTick, ct);
                nextEventIndex = ProcessSimulationEvents(trace, existingViews, liveViews, nextEventIndex, rawTick, effectTasks, ct);

                await Task.Yield();
            }

            if (!ct.IsCancellationRequested)
            {
                float finalTick = maxTimeSeconds / trace.FixedTimestep;
                ApplyAtTime(relevantTraces, existingViews, liveViews, nextSettleIndices, finalTick, ct);
                ProcessSimulationEvents(trace, existingViews, liveViews, nextEventIndex, finalTick, effectTasks, ct);
            }

            await Task.WhenAll(effectTasks);
        }

        private void ApplyAtTime(
            List<DiceSessionTrace> traces,
            ScenePlayerObjects existingViews,
            Dictionary<int, DiceView> liveViews,
            Dictionary<int, int> nextSettleIndices,
            float rawTick,
            CancellationToken ct)
        {
            foreach (var diceTrace in traces)
            {
                var diceView = EnsureLiveDiceView(diceTrace, rawTick, existingViews, liveViews);
                if (diceView == null) continue;

                var (lower, upper, t) = SampleAt(diceTrace.PoseTraces, rawTick);

                diceView.transform.SetPositionAndRotation(
                    Vector3.Lerp(lower.Position, upper.Position, t),
                    Quaternion.Slerp(lower.Rotation, upper.Rotation, t));

                ApplySettleEvents(diceTrace, diceView, rawTick, nextSettleIndices, ct);
            }
        }

        private static void ApplySettleEvents(
            DiceSessionTrace diceTrace,
            DiceView diceView,
            float rawTick,
            Dictionary<int, int> nextSettleIndices,
            CancellationToken ct)
        {
            int instanceId = diceTrace.Instance.InstanceId;
            nextSettleIndices.TryGetValue(instanceId, out int nextIndex);

            while (nextIndex < diceTrace.SettleEvents.Count && rawTick >= diceTrace.SettleEvents[nextIndex].Tick)
            {
                var settleEvent = diceTrace.SettleEvents[nextIndex];

                diceView.RefreshEffects(diceTrace.Instance);
                _ = diceView.SetSettledFace(diceTrace.Instance, settleEvent.Side, ct);

                nextIndex++;
            }

            nextSettleIndices[instanceId] = nextIndex;
        }

        private int ProcessSimulationEvents(
            GameSimulationTrace trace,
            ScenePlayerObjects existingViews,
            Dictionary<int, DiceView> liveViews,
            int nextEventIndex,
            float rawTick,
            List<Task> effectTasks,
            CancellationToken ct)
        {
            var animateCtx = new EventAnimateContext(gameClient, existingViews, liveViews);

            while (nextEventIndex < trace.Events.Count && rawTick >= trace.Events[nextEventIndex].Tick)
            {
                var evt = trace.Events[nextEventIndex];
                var logic = EventRegistry.Get(evt.EventId, evt.Kind);

                if (logic != null)
                {
                    effectTasks.Add(logic.AnimateEvent(animateCtx, evt.Resolution, evt.SourceInstanceId, ct));
                }

                nextEventIndex++;
            }

            return nextEventIndex;
        }

        private DiceView EnsureLiveDiceView(
            DiceSessionTrace diceTrace,
            float rawTick,
            ScenePlayerObjects existingViews,
            Dictionary<int, DiceView> liveViews)
        {
            bool shouldExist =
                rawTick >= diceTrace.SpawnTick &&
                (diceTrace.RemoveTick < 0 || rawTick < diceTrace.RemoveTick);

            int instanceId = diceTrace.Instance.InstanceId;

            if (!liveViews.TryGetValue(instanceId, out var diceView))
            {
                diceView = existingViews?.FindDiceView(instanceId);
            }

            if (!shouldExist)
            {
                if (diceView != null)
                {
                    GameObject.Destroy(diceView.gameObject);
                    liveViews.Remove(instanceId);
                    existingViews?.DiceViews.Remove(instanceId);
                }
                return null;
            }

            if (!liveViews.ContainsKey(instanceId))
            {
                if (diceView == null) diceView = DiceView.Create(assets, diceTrace.Instance, gameClient);
                liveViews[instanceId] = diceView;

                diceView.UnsetSettledFace();
                diceView.RefreshEffects(diceTrace.Instance);

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
