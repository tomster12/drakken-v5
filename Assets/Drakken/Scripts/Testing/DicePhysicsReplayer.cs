using System.Collections.Generic;
using Drakken.Client;
using Drakken.Client.World;
using Drakken.Common.Utility;
using UnityEngine;

namespace Drakken.Testing
{
    public class DicePhysicsReplayer : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private AssetDatabase assetDatabase;

        private Dictionary<int, Transform> replayTransformsByInstanceId;
        private DicePhysicsReplayTrace activeTrace;
        private float replayElapsedTime;

        public void Replay(DicePhysicsReplayTrace trace)
        {
            Assert.Null(activeTrace);

            activeTrace = trace;
            replayElapsedTime = 0f;
            SpawnReplayDice();
        }

        private void SpawnReplayDice()
        {
            replayTransformsByInstanceId = new Dictionary<int, Transform>();

            foreach (DiceInstanceRecord record in activeTrace.dice)
            {
                var diceView = DiceView.CreateProcedural(assetDatabase, record.Instance);

                diceView.transform.SetPositionAndRotation(record.spawnPosition, Quaternion.identity);

                replayTransformsByInstanceId[record.Instance.InstanceId] = diceView.transform;
            }
        }

        private void Update()
        {
            if (activeTrace == null) return;

            replayElapsedTime += Time.deltaTime;
            ApplyInterpolatedTickSnapshots();
        }

        private void ApplyInterpolatedTickSnapshots()
        {
            float rawTickPosition = replayElapsedTime / activeTrace.FixedTimestep;
            int lowerTickIndex = Mathf.FloorToInt(rawTickPosition);
            int upperTickIndex = lowerTickIndex + 1;

            if (upperTickIndex >= activeTrace.tickSnapshots.Count)
            {
                ApplyTickSnapshot(activeTrace.tickSnapshots.Count - 1);
                activeTrace = null;
                return;
            }

            float interpolationFactor = rawTickPosition - lowerTickIndex;

            DicePhysicsTickSnapshot lowerTick = activeTrace.tickSnapshots[lowerTickIndex];
            DicePhysicsTickSnapshot upperTick = activeTrace.tickSnapshots[upperTickIndex];

            for (int i = 0; i < lowerTick.InstanceSnapshots.Count; i++)
            {
                DicePhysicsInstanceSnapshot lowerSnapshot = lowerTick.InstanceSnapshots[i];
                DicePhysicsInstanceSnapshot upperSnapshot = upperTick.InstanceSnapshots[i];

                if (!replayTransformsByInstanceId.TryGetValue(lowerSnapshot.DiceInstanceId, out Transform replayTransform))
                {
                    continue;
                }

                Vector3 interpolatedPosition = Vector3.Lerp(lowerSnapshot.Position, upperSnapshot.Position, interpolationFactor);
                Quaternion interpolatedRotation = Quaternion.Slerp(lowerSnapshot.Rotation, upperSnapshot.Rotation, interpolationFactor);

                replayTransform.SetPositionAndRotation(interpolatedPosition, interpolatedRotation);
            }
        }

        private void ApplyTickSnapshot(int tickIndex)
        {
            var tick = activeTrace.tickSnapshots[tickIndex];

            foreach (var snapshot in tick.InstanceSnapshots)
            {
                if (replayTransformsByInstanceId.TryGetValue(snapshot.DiceInstanceId, out Transform replayTransform))
                {
                    replayTransform.SetPositionAndRotation(snapshot.Position, snapshot.Rotation);
                }
            }
        }
    }
}
