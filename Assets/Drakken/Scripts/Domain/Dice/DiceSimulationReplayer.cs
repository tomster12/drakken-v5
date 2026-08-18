using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Drakken.Client;
using Drakken.Client.World;
using Drakken.Common.Utility;
using Drakken.Domain.Tokens.Logic;
using UnityEngine;

namespace Drakken.Domain.Dice
{
    public class DiceSimulationReplayer
    {
        private static readonly Color ValueChangeColor = Colors.Hex("#9cec92");
        private const float ValueChangeHighlightDuration = 0.9f;
        private const float ValueChangeLabelRiseHeight = 0.6f;

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

        // valueChanges is a resolution's generic side effect list (e.g. Bolster's +1s) - any entry
        // whose SourceInstanceId settles during this replay gets a default flash+label, synced to
        // the moment it actually lands rather than as a fixed beat tacked onto the end
        public async Task Play(
            DiceSimulationTraces trace,
            CancellationToken ct,
            ScenePlayerObjects existingViews = null,
            List<DiceValueChange> valueChanges = null)
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

            // Track unprocessed SettleEvents indices per dice instance so each is only applied once
            Dictionary<int, int> nextSettleIndices = new();

            // Any flash/label tasks triggered by value changes along the way, awaited at the end
            List<Task> effectTasks = new();

            // Scrub through the simulation with interpolated frames
            float elapsedSeconds = 0f;
            while (elapsedSeconds < maxTimeSeconds)
            {
                if (ct.IsCancellationRequested) break;

                elapsedSeconds += Time.deltaTime;

                ApplyAtTime(relevantTraces, existingViews, liveViews, nextSettleIndices, valueChanges, effectTasks, trace.FixedTimestep, elapsedSeconds, ct);

                await Task.Yield();
            }

            // Apply with maxTimeSeconds at the end to catch up any remaining settle events
            if (!ct.IsCancellationRequested)
            {
                ApplyAtTime(relevantTraces, existingViews, liveViews, nextSettleIndices, valueChanges, effectTasks, trace.FixedTimestep, maxTimeSeconds, ct);
            }

            await Task.WhenAll(effectTasks);
        }

        private void ApplyAtTime(
            List<DiceSessionTrace> traces,
            ScenePlayerObjects existingViews,
            Dictionary<int, DiceView> liveViews,
            Dictionary<int, int> nextSettleIndices,
            List<DiceValueChange> valueChanges,
            List<Task> effectTasks,
            float fixedTimestep,
            float elapsedSeconds,
            CancellationToken ct)
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

                ApplySettleEvents(diceTrace, diceView, rawTick, nextSettleIndices, existingViews, liveViews, valueChanges, effectTasks, ct);
            }
        }

        private void ApplySettleEvents(
            DiceSessionTrace diceTrace,
            DiceView diceView,
            float rawTick,
            Dictionary<int, int> nextSettleIndices,
            ScenePlayerObjects existingViews,
            Dictionary<int, DiceView> liveViews,
            List<DiceValueChange> valueChanges,
            List<Task> effectTasks,
            CancellationToken ct)
        {
            int instanceId = diceTrace.Instance.InstanceId;
            nextSettleIndices.TryGetValue(instanceId, out int nextIndex);

            // Fire every settle event this dice has crossed since the last processed frame
            while (nextIndex < diceTrace.SettleEvents.Count && rawTick >= diceTrace.SettleEvents[nextIndex].Tick)
            {
                var settleEvent = diceTrace.SettleEvents[nextIndex];

                diceView.RefreshEffects(diceTrace.Instance);
                _ = diceView.SetSettledFace(diceTrace.Instance, settleEvent.Side, ct);

                PlayValueChangeEffects(instanceId, existingViews, liveViews, valueChanges, effectTasks, ct);

                nextIndex++;
            }

            nextSettleIndices[instanceId] = nextIndex;
        }

        private void PlayValueChangeEffects(
            int sourceInstanceId,
            ScenePlayerObjects existingViews,
            Dictionary<int, DiceView> liveViews,
            List<DiceValueChange> valueChanges,
            List<Task> effectTasks,
            CancellationToken ct)
        {
            if (valueChanges == null) return;

            foreach (var change in valueChanges)
            {
                if (change.SourceInstanceId != sourceInstanceId) continue;

                if (!liveViews.TryGetValue(change.InstanceId, out var diceView))
                    diceView = existingViews?.FindDiceView(change.InstanceId);

                if (diceView == null) continue;

                diceView.RefreshLabels();

                effectTasks.Add(diceView.FlashHighlight(ValueChangeColor, ValueChangeHighlightDuration, ct));
                effectTasks.Add(gameClient.Vfx.SpawnFloatingLabel(
                    "+1",
                    ValueChangeColor,
                    diceView.transform.position + Vector3.up * ValueChangeLabelRiseHeight,
                    Quaternion.Euler(45f, gameClient.Match.ClientIndex == 1 ? 180f : 0f, 0f),
                    ct));

                _ = diceView.SetSettledFace(diceView.DiceInstance, diceView.DiceInstance.CurrentSide, ct);
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
