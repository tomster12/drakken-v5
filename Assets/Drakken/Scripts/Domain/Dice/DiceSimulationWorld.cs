using System;
using System.Collections.Generic;
using System.Linq;
using Drakken.Client.World;
using Drakken.Common.Utility;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Drakken.Domain.Dice
{
    public class DiceSimulationWorld
    {
        private const float fixedTimestep = 1f / 30f;
        private const int tickTimeout = 2000;
        private const float settledLinearVelocity = 0.001f;
        private const float settledAngularVelocity = 0.001f;
        private const float settledDuration = 0.5f;

        private readonly Scene scene;
        private readonly PhysicsScene physicsScene;
        private readonly Dictionary<int, DiceBody> diceBodiesByInstanceId = new();
        private readonly List<DiceSessionTrace> sessionRemovedTraces = new();
        private int currentTick;
        private int sessionStartTick;

        public bool AllDynamicSettled =>
            diceBodiesByInstanceId.Values.All(body => body.Rigidbody.isKinematic || body.IsSettled);

        public DiceSimulationWorld(string name, DiceTray trayTemplate)
        {
            scene = SceneManager.CreateScene(name, new CreateSceneParameters(LocalPhysicsMode.Physics3D));
            physicsScene = scene.GetPhysicsScene();

            CreateTray(trayTemplate);
        }

        private void CreateTray(DiceTray trayTemplate)
        {
            GameObject trayGO = GameObject.Instantiate(trayTemplate.gameObject);
            SceneManager.MoveGameObjectToScene(trayGO, scene);

            trayGO.SetActive(true);
            foreach (Transform child in trayGO.GetComponentsInChildren<Transform>(true))
            {
                child.gameObject.SetActive(true);
            }

            foreach (var renderer in trayGO.GetComponentsInChildren<Renderer>())
            {
                renderer.enabled = false;
            }
        }

        public async void Dispose()
        {
            foreach (var body in diceBodiesByInstanceId.Values)
            {
                GameObject.Destroy(body.Rigidbody.gameObject);
            }

            diceBodiesByInstanceId.Clear();

            await SceneManager.UnloadSceneAsync(scene);
        }

        // ------------------------------ Dice

        public int SpawnDice(
            DiceInstance instance,
            Vector3 position,
            Quaternion rotation,
            Vector3 linearImpulse,
            Vector3 angularImpulse)
        {
            var diceMesh = DiceMeshFactory.Create(instance);
            SceneManager.MoveGameObjectToScene(diceMesh.GameObject, scene);

            diceMesh.GameObject.transform.SetPositionAndRotation(position, rotation);
            diceMesh.Renderer.enabled = false;

            var diceRB = diceMesh.GameObject.AddComponent<Rigidbody>();
            diceRB.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            diceRB.AddForce(linearImpulse, ForceMode.VelocityChange);
            diceRB.AddTorque(angularImpulse, ForceMode.VelocityChange);

            var diceBody = new DiceBody
            {
                Instance = instance,
                Rigidbody = diceRB,
                MeshFaces = diceMesh.Faces,
                SpawnTick = currentTick,
            };

            diceBody.RecordPose(currentTick);

            diceBodiesByInstanceId[instance.InstanceId] = diceBody;
            return instance.InstanceId;
        }

        public void WakeDice(int diceInstanceId, Vector3 linearImpulse, Vector3 angularImpulse)
        {
            var diceBody = diceBodiesByInstanceId[diceInstanceId];

            // Record resting pose as the start of this session
            diceBody.RecordPose(currentTick);

            diceBody.Rigidbody.isKinematic = false;
            diceBody.Rigidbody.AddForce(linearImpulse, ForceMode.VelocityChange);
            diceBody.Rigidbody.AddTorque(angularImpulse, ForceMode.VelocityChange);
            diceBody.IsSettled = false;
            diceBody.SettledTimer = 0f;
        }

        public void RemoveDice(int diceInstanceId)
        {
            var diceBody = diceBodiesByInstanceId[diceInstanceId];
            diceBodiesByInstanceId.Remove(diceInstanceId);

            // Build the trace for the dice before removing
            sessionRemovedTraces.Add(BuildSessionTrace(diceBody, removeTick: currentTick));

            GameObject.Destroy(diceBody.Rigidbody.gameObject);
        }

        public void FreezeAllDice()
        {
            // Officially lock all dices in place as kinematic and record value
            foreach (var body in diceBodiesByInstanceId.Values)
            {
                if (body.Rigidbody.isKinematic) continue;

                body.Instance.Value = DiceMeshFactory.GetUpFaceValue(body.Rigidbody.transform, body.MeshFaces);
                body.RecordPose(currentTick);
                body.Rigidbody.isKinematic = true;
            }
        }

        public void SimulateUntilAllSettled(bool freezeOnSettled = true)
        {
            // Run until all dice are settled
            while (!AllDynamicSettled)
            {
                var settled = SimulateUntilAnySettled();

                // If we do not get the list of settled dice then we hit the tick limit
                if (settled == null)
                {
                    throw new InvalidOperationException("Could not finish simulation");
                }
            }

            if (freezeOnSettled)
            {
                FreezeAllDice();
            }
        }

        public List<int> SimulateUntilAnySettled()
        {
            float settleLinSqr = settledLinearVelocity * settledLinearVelocity;
            float settleAngSqr = settledAngularVelocity * settledAngularVelocity;

            // Track dice that have transitioned (dynamic -> settled)
            List<int> transitioned = new();

            for (int i = 0; i < tickTimeout; i++)
            {
                // Step physics
                physicsScene.Simulate(fixedTimestep);
                currentTick++;

                // For each dynamic dice body
                foreach (var (instanceId, body) in diceBodiesByInstanceId)
                {
                    if (body.Rigidbody.isKinematic) continue;

                    // Track current position
                    body.RecordPose(currentTick);

                    // If we are sufficiently not moving then update settled values
                    bool isBelowThreshold =
                        body.Rigidbody.linearVelocity.sqrMagnitude < settleLinSqr &&
                        body.Rigidbody.angularVelocity.sqrMagnitude < settleAngSqr;

                    if (isBelowThreshold)
                    {
                        body.SettledTimer += fixedTimestep;
                        if (!body.IsSettled && body.SettledTimer >= settledDuration)
                        {
                            body.IsSettled = true;
                            transitioned.Add(instanceId);
                        }
                    }
                    else
                    {
                        body.SettledTimer = 0f;
                        body.IsSettled = false;
                    }
                }

                // If any dice settled this tick, or all dice settled, then return
                if (transitioned.Count > 0) return transitioned;
                if (AllDynamicSettled) return transitioned;
            }

            return null;
        }

        public DiceSimulationTraces ExtractSessionTraces()
        {
            DiceSimulationTraces traces = new() { FixedTimestep = fixedTimestep };

            // Get a trace of each dice for this session and clear out traces
            foreach (var body in diceBodiesByInstanceId.Values)
            {
                if (body.SessionPoses.Count == 0) continue;

                traces.Dice.Add(BuildSessionTrace(body, removeTick: -1));
                body.SessionPoses.Clear();
            }

            // Make sure to also include any dice that was removed this session
            foreach (var trace in sessionRemovedTraces)
            {
                traces.Dice.Add(trace);
            }
            sessionRemovedTraces.Clear();

            sessionStartTick = currentTick;
            return traces;
        }

        private DiceSessionTrace BuildSessionTrace(DiceBody body, int removeTick)
        {
            // Build a trace of a dice over this session, rebased to 0 at session start
            List<DicePoseTrace> poses = new(body.SessionPoses.Count);

            foreach (var pose in body.SessionPoses)
            {
                poses.Add(new DicePoseTrace
                {
                    Tick = pose.Tick - sessionStartTick,
                    Position = pose.Position,
                    Rotation = pose.Rotation,
                });
            }

            return new DiceSessionTrace
            {
                Instance = body.Instance.Clone(),
                SpawnTick = Mathf.Max(0, body.SpawnTick - sessionStartTick),
                PoseTraces = poses,
                RemoveTick = removeTick < 0 ? -1 : removeTick - sessionStartTick,
            };
        }

        private class DiceBody
        {
            public DiceInstance Instance;
            public IReadOnlyList<DiceMeshFactory.DiceFacePose> MeshFaces;
            public Rigidbody Rigidbody;
            public int SpawnTick;
            public bool IsSettled;
            public float SettledTimer;
            public readonly List<DicePoseTrace> SessionPoses = new();

            public void RecordPose(int tick)
            {
                SessionPoses.Add(new DicePoseTrace
                {
                    Tick = tick,
                    Position = Rigidbody.position,
                    Rotation = Rigidbody.rotation,
                });
            }
        }
    }
}
