using System.Collections.Generic;
using Drakken.Client;
using Drakken.Common.Utility;
using Drakken.Domain;
using Drakken.Generation;
using UnityEngine;

namespace Drakken.Testing
{
    public class DicePhysicsRecorder : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private AssetDatabase assetDatabase;

        [Header("Config")]
        [SerializeField] private float fixedTimestep = 1f / 30f;
        [SerializeField] private float throwSpeed = 5f;
        [SerializeField] private float settleLinearVelocityThreshold = 0.01f;
        [SerializeField] private float settleAngularVelocityThreshold = 0.01f;
        [SerializeField] private float requiredSettleDuration = 0.5f;
        [SerializeField] private int timeoutTicks = 900;

        private List<PhysicsDice> physicsDice;
        private List<DiceInstance> diceInstances;
        private List<DicePhysicsTickSnapshot> tickSnapshots;
        private Dictionary<int, float> settledTimers;
        private int currentTick;

        public IReadOnlyList<DiceInstance> DiceInstances => diceInstances;

        public DicePhysicsReplayTrace SimulateRoll(IReadOnlyList<int> diceToSpawn, Vector3 spawnOrigin, Vector3 throwTarget)
        {
            SpawnSimulationDice(diceToSpawn, spawnOrigin, throwTarget);

            var trace = RunSimulationLoop();

            DestroySimulationDice();

            return trace;
        }

        private void SpawnSimulationDice(IReadOnlyList<int> diceToSpawn, Vector3 spawnOrigin, Vector3 throwTarget)
        {
            physicsDice = new List<PhysicsDice>();
            diceInstances = new List<DiceInstance>();
            settledTimers = new Dictionary<int, float>();
            tickSnapshots = new List<DicePhysicsTickSnapshot>();
            currentTick = 0;

            for (int i = 0; i < diceToSpawn.Count; i++)
            {
                int sideCount = diceToSpawn[i];
                var diceInstance = DiceInstance.Create(sideCount);
                diceInstances.Add(diceInstance);

                var diceMesh = DiceMeshFactory.Create(diceInstance, assetDatabase.DiceMeshMaterial);
                var diceGO = diceMesh.GameObject;
                var diceRB = diceGO.AddComponent<Rigidbody>();

                Vector3 spawnPosition = spawnOrigin + new Vector3(i * 1.5f, 0f, 0f);
                Vector3 throwVelocity = (throwTarget - spawnPosition).normalized * throwSpeed;

                diceGO.transform.SetPositionAndRotation(spawnPosition, Quaternion.identity);
                diceRB.AddForce(throwVelocity, ForceMode.VelocityChange);
                diceRB.AddTorque(UnityEngine.Random.insideUnitSphere * 5f, ForceMode.VelocityChange);

                physicsDice.Add(new PhysicsDice
                {
                    Instance = diceInstance,
                    spawnPosition = spawnPosition,
                    rigidbody = diceRB,
                    faces = diceMesh.Faces
                });

                settledTimers[diceInstance.InstanceId] = 0f;
            }
        }

        private DicePhysicsReplayTrace RunSimulationLoop()
        {
            Physics.simulationMode = SimulationMode.Script;

            var startTime = Time.realtimeSinceStartup;

            while (currentTick < timeoutTicks)
            {
                Physics.Simulate(fixedTimestep);

                RecordTickSnapshot();

                UpdateSettleTimers();
                if (CheckAllDiceSettled()) break;

                currentTick++;
            }

            var endTime = Time.realtimeSinceStartup;
            Log.Info("DicePhysicsRecorder",
                $"Simulation completed in {endTime - startTime} seconds over {currentTick} ticks.");

            DetermineFinalValues();
            var trace = BuildTrace();
            return trace;
        }

        private void DestroySimulationDice()
        {
            foreach (var dice in physicsDice)
            {
                Destroy(dice.rigidbody.gameObject);
            }

            physicsDice.Clear();
            diceInstances.Clear();
            tickSnapshots.Clear();
            settledTimers.Clear();
        }

        private void RecordTickSnapshot()
        {
            List<DicePhysicsInstanceSnapshot> snapshots = new(physicsDice.Count);

            foreach (PhysicsDice dice in physicsDice)
            {
                snapshots.Add(new DicePhysicsInstanceSnapshot
                {
                    DiceInstanceId = dice.Instance.InstanceId,
                    Position = dice.rigidbody.position,
                    Rotation = dice.rigidbody.rotation
                });
            }

            tickSnapshots.Add(new DicePhysicsTickSnapshot
            {
                tick = currentTick,
                InstanceSnapshots = snapshots
            });
        }

        private void UpdateSettleTimers()
        {
            foreach (PhysicsDice dice in physicsDice)
            {
                bool isBelowSettleThreshold =
                    dice.rigidbody.linearVelocity.sqrMagnitude < settleLinearVelocityThreshold * settleLinearVelocityThreshold &&
                    dice.rigidbody.angularVelocity.sqrMagnitude < settleAngularVelocityThreshold * settleAngularVelocityThreshold;

                if (isBelowSettleThreshold)
                {
                    settledTimers[dice.Instance.InstanceId] += fixedTimestep;
                }
                else
                {
                    settledTimers[dice.Instance.InstanceId] = 0f;
                }
            }
        }

        private bool CheckAllDiceSettled()
        {
            foreach (PhysicsDice dice in physicsDice)
            {
                if (settledTimers[dice.Instance.InstanceId] < requiredSettleDuration)
                {
                    return false;
                }
            }

            return true;
        }

        private void DetermineFinalValues()
        {
            foreach (PhysicsDice dice in physicsDice)
            {
                dice.Instance.Value = DiceMeshFactory.GetUpFaceValue(dice.rigidbody.transform, dice.faces);
            }
        }

        private DicePhysicsReplayTrace BuildTrace()
        {
            List<InitialDiceRecord> initialDice = new(physicsDice.Count);

            foreach (PhysicsDice dice in physicsDice)
            {
                initialDice.Add(new InitialDiceRecord
                {
                    Instance = dice.Instance,
                    spawnPosition = dice.spawnPosition
                });
            }

            return new DicePhysicsReplayTrace
            {
                initialDice = initialDice,
                tickSnapshots = new(tickSnapshots),
                FixedTimestep = fixedTimestep
            };
        }

        private struct PhysicsDice
        {
            public DiceInstance Instance;
            public Vector3 spawnPosition;
            public Rigidbody rigidbody;
            public IReadOnlyList<DiceMeshFactory.DiceFacePose> faces;
        }
    }
}
