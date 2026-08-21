using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Drakken.Client;
using Drakken.Client.World;
using Drakken.Domain;
using Drakken.Utility;
using UnityEngine;

namespace Drakken.Gameplay.Simulation
{
    public class GameSimulationReplayer
    {
        private AssetDatabase assets;
        private GameClient gameClient;
        private int clientIndex;

        public void Init(AssetDatabase assets, GameClient gameClient, int clientIndex)
        {
            this.assets = assets;
            this.gameClient = gameClient;
            this.clientIndex = clientIndex;
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

            // While not finished tick forward
            float elapsedSeconds = 0f;
            while (elapsedSeconds < maxTimeSeconds)
            {
                if (ct.IsCancellationRequested) break;

                elapsedSeconds += Time.deltaTime;
                float rawTick = elapsedSeconds / trace.FixedTimestep;

                // Apply the trace at the current and process events
                ApplyAtTime(relevantTraces, existingViews, liveViews, nextSettleIndices, rawTick, ct);
                nextEventIndex = ProcessSimulationEvents(trace, existingViews, liveViews, nextEventIndex, rawTick, effectTasks, ct);

                await Task.Yield();
            }

            // Apply the trace at the final tick and process any remaining events            
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
                // Spawn / remove dice view if need be
                var diceView = EnsureLiveDiceView(diceTrace, rawTick, existingViews, liveViews);
                if (diceView == null) continue;

                // Apply position and rotation for this dice interpolated
                var (lower, upper, t) = SampleAt(diceTrace.PoseTraces, rawTick);

                diceView.transform.SetPositionAndRotation(
                    Vector3.Lerp(lower.Position, upper.Position, t),
                    Quaternion.Slerp(lower.Rotation, upper.Rotation, t));

                // And now handle settle events    
                int instanceId = diceTrace.Instance.InstanceId;
                nextSettleIndices.TryGetValue(diceTrace.Instance.InstanceId, out int nextEventIndex);
                nextSettleIndices[instanceId] = ProcessSettleEvents(diceTrace, diceView, rawTick, nextEventIndex, ct);
            }
        }

        private int ProcessSettleEvents(
            DiceSessionTrace diceTrace,
            DiceView diceView,
            float rawTick,
            int nextEventIndex,
            CancellationToken ct)
        {
            int instanceId = diceTrace.Instance.InstanceId;

            while (nextEventIndex < diceTrace.SettleEvents.Count && rawTick >= diceTrace.SettleEvents[nextEventIndex].Tick)
            {
                var settleEvent = diceTrace.SettleEvents[nextEventIndex];
                var dice = gameClient.GameState.Clients[clientIndex].Dice.Find(d => d.InstanceId == instanceId);
                Assert.NotNull(dice);

                // Update dice instance and view
                dice.CurrentSide = settleEvent.Side;
                _ = diceView.SetSettledFace(diceTrace.Instance, settleEvent.Side, ct);

                nextEventIndex++;
            }

            return nextEventIndex;
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
                    // Apply effect logic and ensure views are updated
                    logic.ApplyEvent(gameClient.GameState, evt.Resolution, clientIndex);
                    RefreshLiveDiceEffects(existingViews, liveViews);

                    effectTasks.Add(logic.AnimateEvent(animateCtx, evt.Resolution, ct));
                }

                nextEventIndex++;
            }

            return nextEventIndex;
        }

        private void RefreshLiveDiceEffects(
            ScenePlayerObjects existingViews,
            Dictionary<int, DiceView> liveViews)
        {
            foreach (var dice in gameClient.GameState.Clients[clientIndex].Dice)
            {
                var diceView = liveViews.TryGetValue(dice.InstanceId, out var view)
                    ? view
                    : existingViews?.FindDiceView(dice.InstanceId);

                diceView?.RefreshEffects(dice);
            }
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

                // Only clear the settled-face highlight if this dice is actually going to resettle
                // during this trace - a dice that was merely woken to test whether it needed to
                // move (see GameSimulationWorld.WakeOtherSettledDice) but didn't should keep
                // showing whatever it last settled on
                if (diceTrace.SettleEvents.Count > 0) diceView.UnsetSettledFace();

                var currentDice = gameClient.GameState.Clients[clientIndex].Dice.Find(d => d.InstanceId == instanceId);
                if (currentDice != null) diceView.RefreshEffects(currentDice);

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
