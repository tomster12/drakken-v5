using UnityEngine;

namespace Drakken.Testing
{
    public class DicePhysicsTestRunner : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private DicePhysicsRecorder recorder;
        [SerializeField] private DicePhysicsReplayer replayer;

        [Header("Config")]
        [SerializeField] private int[] sideCountsToSpawn = { 6, 6, 4, 8 };
        [SerializeField] private Vector3 spawnOrigin = new(-5f, 1f, 0f);
        [SerializeField] private Vector3 throwTarget = Vector3.zero;

        private async void Awake()
        {
            var trace = recorder.SimulateRoll(sideCountsToSpawn, spawnOrigin, throwTarget);

            replayer.Replay(trace);
        }
    }
}
