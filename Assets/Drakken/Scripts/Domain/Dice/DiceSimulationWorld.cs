using System;
using System.Collections.Generic;
using System.Linq;
using Drakken.Client.World;
using Drakken.Common.Utility;
using Drakken.Domain.Dice.Effects;
using Drakken.Domain.Tokens.Logic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Drakken.Domain.Dice
{
    // Dispatches dice/face effects (see Drakken.Domain.Dice.Effects) as part of its own physics
    // step - this is what makes permanent effects (e.g. Bolster) fire no matter which token's
    // session disturbs the dice, rather than only when their own token happens to be running.
    public class DiceSimulationWorld
    {
        private const float fixedTimestep = 1f / 30f;
        private const int tickTimeout = 2000;
        private const float settledLinearVelocity = 0.001f;
        private const float settledAngularVelocity = 0.001f;
        private const float settledDuration = 0.25f;

        private readonly Scene scene;
        private readonly PhysicsScene physicsScene;
        private readonly Dictionary<int, DiceBody> diceBodiesByInstanceId = new();
        private readonly List<DiceSessionTrace> sessionRemovedTraces = new();
        private readonly List<KinematicDrive> activeDrives = new();
        private GameObject trayGO;
        private int currentTick;
        private int sessionStartTick;
        private int nextDriveId;
        private bool isInSession;
        private TokenResolution sessionResolution;

        public bool AllDynamicSettled =>
            activeDrives.Count == 0 &&
            diceBodiesByInstanceId.Values.All(body => body.Rigidbody.isKinematic || body.IsSettled);

        public Transform Tray => trayGO.transform;

        public DiceSimulationWorld(string name, DiceTray trayTemplate)
        {
            scene = SceneManager.CreateScene(name, new CreateSceneParameters(LocalPhysicsMode.Physics3D));
            physicsScene = scene.GetPhysicsScene();

            CreateTray(trayTemplate);
        }

        private void CreateTray(DiceTray trayTemplate)
        {
            trayGO = GameObject.Instantiate(trayTemplate.gameObject);
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

        // ------------------------------ Session

        // resolution is null for sessions not driven by a token (e.g. a plain reroll) - any
        // settle-triggered effect that fires still mutates dice directly, it just has nothing to
        // record outcomes into for a later Apply/animate step
        public void BeginSession(TokenResolution resolution, IEnumerable<DiceInstance> currentDiceInstances)
        {
            Assert.True(!isInSession, "Cannot begin a session while one is already in progress");

            // Execute() on tokens runs on a fresh GameState.Clone()
            // This means the persistent DiceBody points at an old DiceInstance
            // This is the tradeoff for leaky simulation, but the execute on a cloned gamestate
            // Vaguely mirrors TokenViews usage of the real GameState dice instance
            foreach (var instance in currentDiceInstances)
            {
                if (diceBodiesByInstanceId.TryGetValue(instance.InstanceId, out var body))
                {
                    body.Instance = instance;
                }
            }

            isInSession = true;
            sessionStartTick = currentTick;
            sessionResolution = resolution;
        }

        public DiceSimulationTraces EndSession()
        {
            Assert.True(isInSession, "Cannot end a session that has not begun");

            DiceSimulationTraces traces = new() { FixedTimestep = fixedTimestep };

            // Get a trace of each dice for this session and clear out traces
            foreach (var body in diceBodiesByInstanceId.Values)
            {
                if (body.SessionPoseTraces.Count == 0) continue;

                traces.Dice.Add(BuildSessionTrace(body, removeTick: -1));
                body.SessionPoseTraces.Clear();
                body.SessionSettleEvents.Clear();
            }

            // Make sure to also include any dice that was removed this session
            foreach (var trace in sessionRemovedTraces)
            {
                traces.Dice.Add(trace);
            }
            sessionRemovedTraces.Clear();

            isInSession = false;
            sessionResolution = null;
            return traces;
        }

        private DiceSessionTrace BuildSessionTrace(DiceBody body, int removeTick)
        {
            // Build a trace of a dice over this session, rebased to 0 at session start
            List<DicePoseTrace> poseTraces = new(body.SessionPoseTraces.Count);

            foreach (var pose in body.SessionPoseTraces)
            {
                poseTraces.Add(new DicePoseTrace
                {
                    Tick = pose.Tick - sessionStartTick,
                    Position = pose.Position,
                    Rotation = pose.Rotation,
                });
            }

            List<DiceSettleEvent> settleEvents = new(body.SessionSettleEvents.Count);

            foreach (var settleEvent in body.SessionSettleEvents)
            {
                settleEvents.Add(new DiceSettleEvent
                {
                    Tick = settleEvent.Tick - sessionStartTick,
                    Side = settleEvent.Side,
                });
            }

            return new DiceSessionTrace
            {
                Instance = body.Instance.Clone(),
                SpawnTick = Mathf.Max(0, body.SpawnTick - sessionStartTick),
                PoseTraces = poseTraces,
                SettleEvents = settleEvents,
                RemoveTick = removeTick < 0 ? -1 : removeTick - sessionStartTick,
            };
        }

        // ------------------------------ Dice

        public int SpawnDice(
            DiceInstance instance,
            Vector3 position,
            Quaternion rotation,
            Vector3? linearImpulse = null,
            Vector3? angularImpulse = null)
        {
            Assert.True(isInSession, "Cannot spawn dice outside of a session");

            var diceMesh = DiceMeshFactory.Create(instance);
            SceneManager.MoveGameObjectToScene(diceMesh.GameObject, scene);

            diceMesh.GameObject.transform.SetPositionAndRotation(position, rotation);
            diceMesh.Renderer.enabled = false;

            var diceRB = diceMesh.GameObject.AddComponent<Rigidbody>();
            diceRB.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

            if (linearImpulse.HasValue) diceRB.AddForce(linearImpulse.Value, ForceMode.VelocityChange);
            if (angularImpulse.HasValue) diceRB.AddTorque(angularImpulse.Value, ForceMode.VelocityChange);

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

        public void WakeDice(
            int diceInstanceId,
            Vector3? linearImpulse = null,
            Vector3? angularImpulse = null)
        {
            Assert.True(isInSession, "Cannot wake dice outside of a session");

            var diceBody = diceBodiesByInstanceId[diceInstanceId];

            // Record resting pose as the start of this session
            diceBody.RecordPose(currentTick);

            diceBody.Rigidbody.isKinematic = false;
            if (linearImpulse.HasValue) diceBody.Rigidbody.AddForce(linearImpulse.Value, ForceMode.VelocityChange);
            if (angularImpulse.HasValue) diceBody.Rigidbody.AddTorque(angularImpulse.Value, ForceMode.VelocityChange);
            diceBody.IsSettled = false;
            diceBody.SettledTimer = 0f;
        }

        public void RemoveDice(int diceInstanceId)
        {
            Assert.True(isInSession, "Cannot remove dice outside of a session");

            // Remove body from simulation
            var diceBody = diceBodiesByInstanceId[diceInstanceId];
            diceBodiesByInstanceId.Remove(diceInstanceId);

            // Add a "removed trace" for this dice with correct removeTick
            sessionRemovedTraces.Add(BuildSessionTrace(diceBody, removeTick: currentTick));

            // Disable colliders as well because Destroy() is deferred until end of tick
            GameObject.Destroy(diceBody.Rigidbody.gameObject);

            foreach (var diceCollider in diceBody.Rigidbody.GetComponentsInChildren<Collider>())
                diceCollider.enabled = false;
        }

        public void FreezeAllDice()
        {
            Assert.True(isInSession, "Cannot freeze dice outside of a session");

            // Officially lock all dices in place as kinematic and record value
            foreach (var body in diceBodiesByInstanceId.Values)
            {
                if (body.Rigidbody.isKinematic) continue;

                body.Instance.CurrentSide = PeekDiceSide(body.Instance.InstanceId);
                body.RecordPose(currentTick);
                body.Rigidbody.isKinematic = true;
            }
        }

        public string DriveDice(
            int diceInstanceId,
            float durationSeconds,
            Func<float, Vector3> positionAtTime,
            Func<float, Quaternion> rotationAtTime,
            Action onComplete = null)
        {
            Assert.True(isInSession, "Cannot drive dice outside of a session");

            // Script a dice to move along a path, returns an identifying drive ID
            var body = diceBodiesByInstanceId[diceInstanceId];
            body.Rigidbody.isKinematic = true;

            var drive = new KinematicDrive
            {
                Id = "drive_" + (++nextDriveId),
                InstanceId = diceInstanceId,
                StartTick = currentTick,
                DurationTicks = Mathf.Max(1, Mathf.RoundToInt(durationSeconds / fixedTimestep)),
                PositionAtTime = positionAtTime,
                RotationAtTime = rotationAtTime,
                OnComplete = onComplete,
            };
            activeDrives.Add(drive);
            return drive.Id;
        }

        public SimulationResult Simulate(
            bool untilAllSettled = false,
            bool untilAnySettled = false,
            float? forSeconds = null,
            IEnumerable<string> untilDrivesComplete = null)
        {
            Assert.True(isInSession, "Cannot simulate outside of a session");

            Assert.True(untilAllSettled || untilAnySettled || forSeconds.HasValue || untilDrivesComplete != null,
                "Simulate requires at least one stop condition");

            var driveIdsToWaitFor = untilDrivesComplete != null
                ? new HashSet<string>(untilDrivesComplete)
                : null;

            int? stopTick = forSeconds.HasValue
                ? currentTick + Mathf.Max(1, Mathf.RoundToInt(forSeconds.Value / fixedTimestep))
                : null;

            List<int> settledThisCall = new();
            List<string> completedDrivesThisCall = new();

            // Step tick by tick until we hit a stopping condition
            for (int i = 0; i < tickTimeout; i++)
            {
                var stepResult = Step();

                settledThisCall.AddRange(stepResult.SettledInstanceIds);
                completedDrivesThisCall.AddRange(stepResult.CompletedDriveIds);

                if (untilAllSettled && AllDynamicSettled)
                    return new SimulationResult(settledThisCall, completedDrivesThisCall, timedOut: false);

                if (untilAnySettled && stepResult.SettledInstanceIds.Count > 0)
                    return new SimulationResult(settledThisCall, completedDrivesThisCall, timedOut: false);

                if (stopTick.HasValue && currentTick >= stopTick.Value)
                    return new SimulationResult(settledThisCall, completedDrivesThisCall, timedOut: false);

                if (driveIdsToWaitFor != null && stepResult.CompletedDriveIds.Any(driveIdsToWaitFor.Contains))
                    return new SimulationResult(settledThisCall, completedDrivesThisCall, timedOut: false);
            }

            // We have hit tick limit so log and carry on
            Log.Error("DiceSimulationWorld", "Could not finish simulation");
            return new SimulationResult(settledThisCall, completedDrivesThisCall, timedOut: true);
        }

        private StepResult Step()
        {
            List<string> completedDrives = new();
            HashSet<int> drivenInstanceIds = null;

            // Advance any active kinematic drives towards their target pose for this tick
            if (activeDrives.Count > 0)
            {
                drivenInstanceIds = new HashSet<int>();

                for (int i = activeDrives.Count - 1; i >= 0; i--)
                {
                    var drive = activeDrives[i];
                    var body = diceBodiesByInstanceId[drive.InstanceId];

                    float t = Mathf.Clamp01((float)(currentTick - drive.StartTick + 1) / drive.DurationTicks);
                    body.Rigidbody.MovePosition(drive.PositionAtTime(t));
                    body.Rigidbody.MoveRotation(drive.RotationAtTime(t));
                    drivenInstanceIds.Add(drive.InstanceId);

                    if (t >= 1f)
                    {
                        completedDrives.Add(drive.Id);
                        activeDrives.RemoveAt(i);
                        drive.OnComplete?.Invoke();
                    }
                }
            }

            // Drive the physics forward
            physicsScene.Simulate(fixedTimestep);
            currentTick++;

            // Perform check for dice settled and recording pose
            float settleLinSqr = settledLinearVelocity * settledLinearVelocity;
            float settleAngSqr = settledAngularVelocity * settledAngularVelocity;
            List<int> settled = new();

            foreach (var (instanceId, body) in diceBodiesByInstanceId)
            {
                bool isDriven = drivenInstanceIds != null && drivenInstanceIds.Contains(instanceId);

                if (body.Rigidbody.isKinematic)
                {
                    // Only record pose of driven kinematic bodies
                    if (isDriven) body.RecordPose(currentTick);
                    continue;
                }

                // Record body for instanceId for this tick
                body.RecordPose(currentTick);

                // Check if this body is settled
                bool isBelowThreshold =
                    body.Rigidbody.linearVelocity.sqrMagnitude < settleLinSqr &&
                    body.Rigidbody.angularVelocity.sqrMagnitude < settleAngSqr;

                if (isBelowThreshold)
                {
                    body.SettledTimer += fixedTimestep;
                    if (!body.IsSettled && body.SettledTimer >= settledDuration)
                    {
                        body.IsSettled = true;
                        settled.Add(instanceId);
                        body.SessionSettleEvents.Add(new DiceSettleEvent
                        {
                            Tick = currentTick,
                            Side = DiceMeshFactory.GetFaceUpSide(body.Rigidbody.transform, body.MeshFaces),
                        });
                    }
                }
                else
                {
                    body.SettledTimer = 0f;
                    body.IsSettled = false;
                }
            }

            // Dispatch after the structural iteration above so effects are free to spawn/remove
            // dice (e.g. Mitosis splitting) without invalidating it
            foreach (var instanceId in settled)
            {
                if (diceBodiesByInstanceId.TryGetValue(instanceId, out var settledBody))
                    DispatchSettle(settledBody);
            }

            return new(settled, completedDrives);
        }

        // Fires any registered dice/face effect on a settled dice, regardless of which token's
        // session (if any) is driving this simulation - this is what makes permanent effects
        // like Bolster work no matter what caused the dice to settle
        private void DispatchSettle(DiceBody body)
        {
            var dice = body.Instance;
            dice.CurrentSide = DiceMeshFactory.GetFaceUpSide(body.Rigidbody.transform, body.MeshFaces);

            var candidatePool = diceBodiesByInstanceId.Values.Select(b => b.Instance).ToList();
            var ctx = new DiceEffectSettleContext(dice, candidatePool, this, sessionResolution);

            foreach (var effectId in dice.DiceEffects.ToList())
                DiceEffectRegistry.Get(effectId)?.OnSettled(ctx);

            foreach (var effectId in dice.Faces[dice.CurrentSide].FaceEffects.ToList())
                FaceEffectRegistry.Get(effectId)?.OnSettled(ctx);
        }

        public int PeekDiceSide(int diceInstanceId)
        {
            var diceBody = diceBodiesByInstanceId[diceInstanceId];
            return DiceMeshFactory.GetFaceUpSide(diceBody.Rigidbody.transform, diceBody.MeshFaces);
        }

        public (Vector3 Position, Quaternion Rotation) GetDicePose(int diceInstanceId)
        {
            var diceBody = diceBodiesByInstanceId[diceInstanceId];
            return (diceBody.Rigidbody.position, diceBody.Rigidbody.rotation);
        }

        // ------------------------------ Data

        public readonly struct SimulationResult
        {
            public readonly List<int> SettledInstanceIds;
            public readonly List<string> CompletedDriveIds;
            public readonly bool TimedOut;

            public SimulationResult(List<int> settledInstanceIds, List<string> completedDriveIds, bool timedOut)
            {
                SettledInstanceIds = settledInstanceIds;
                CompletedDriveIds = completedDriveIds;
                TimedOut = timedOut;
            }
        }

        public readonly struct StepResult
        {
            public readonly List<int> SettledInstanceIds;
            public readonly List<string> CompletedDriveIds;

            public StepResult(List<int> settledInstanceIds, List<string> completedDriveIds)
            {
                SettledInstanceIds = settledInstanceIds;
                CompletedDriveIds = completedDriveIds;
            }
        }

        private class KinematicDrive
        {
            public string Id;
            public int InstanceId;
            public int StartTick;
            public int DurationTicks;
            public Func<float, Vector3> PositionAtTime;
            public Func<float, Quaternion> RotationAtTime;
            public Action OnComplete;
        }

        private class DiceBody
        {
            public DiceInstance Instance;
            public IReadOnlyList<DiceMeshFactory.DiceFacePose> MeshFaces;
            public Rigidbody Rigidbody;
            public int SpawnTick;
            public bool IsSettled;
            public float SettledTimer;
            public readonly List<DicePoseTrace> SessionPoseTraces = new();
            public readonly List<DiceSettleEvent> SessionSettleEvents = new();

            public void RecordPose(int tick)
            {
                SessionPoseTraces.Add(new DicePoseTrace
                {
                    Tick = tick,
                    Position = Rigidbody.position,
                    Rotation = Rigidbody.rotation,
                });
            }
        }
    }
}
