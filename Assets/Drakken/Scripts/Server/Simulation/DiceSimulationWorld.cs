using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Drakken.Client.World;
using Drakken.Common.Utility;
using Drakken.Domain;
using Drakken.Domain.Static;
using Drakken.Generation;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Drakken.Server.Simulation
{
    public class DiceSimulationWorld
    {
        private const float dicePhysicsFixedTimestep = 1f / 30f;
        private const int dicePhysicsMaxTicksPerStep = 2000;
        private const float diceSettleLinearVelocityThreshold = 0.001f;
        private const float diceSettleAngularVelocityThreshold = 0.001f;
        private const float diceRequiredSettleDuration = 0.5f;

        private readonly Scene scene;
        private readonly PhysicsScene physicsScene;
        private readonly float fixedTimestep;
        private readonly Dictionary<int, DiceBody> bodiesByInstanceId = new();
        private readonly List<DiceLifetimeTrace> removedSinceLastExtract = new();
        private int currentTick;
        private int lastExtractTick;

        // ------------------------------ Setup

        public DiceSimulationWorld(string name, DiceTray trayTemplate)
        {
            scene = SceneManager.CreateScene(name, new CreateSceneParameters(LocalPhysicsMode.Physics3D));
            physicsScene = scene.GetPhysicsScene();
            fixedTimestep = dicePhysicsFixedTimestep;

            CreateTray(trayTemplate);
        }

        private void CreateTray(DiceTray trayTemplate)
        {
            GameObject instance = GameObject.Instantiate(trayTemplate.gameObject);
            SceneManager.MoveGameObjectToScene(instance, scene);

            instance.SetActive(true);
            foreach (Transform child in instance.GetComponentsInChildren<Transform>(true))
            {
                child.gameObject.SetActive(true);
            }

            foreach (var renderer in instance.GetComponentsInChildren<Renderer>())
            {
                renderer.enabled = false;
            }
        }

        public async void Dispose()
        {
            foreach (var body in bodiesByInstanceId.Values)
            {
                GameObject.Destroy(body.Rigidbody.gameObject);
            }
            bodiesByInstanceId.Clear();

            await SceneManager.UnloadSceneAsync(scene);
        }

        // ------------------------------ Dice

        public int Spawn(
            DiceInstance instance,
            Vector3 position,
            Quaternion rotation,
            Vector3 linearImpulse,
            Vector3 angularImpulse)
        {
            var mesh = DiceMeshFactory.Create(instance);
            mesh.GameObject.transform.SetPositionAndRotation(position, rotation);
            mesh.Renderer.enabled = false;

            // Move into this world's local physics scene before adding the Rigidbody / applying the
            // impulse - moving a GameObject into a scene with a different PhysicsScene recreates its
            // native rigidbody actor, which silently zeroes out any velocity set beforehand.
            SceneManager.MoveGameObjectToScene(mesh.GameObject, scene);

            var rb = mesh.GameObject.AddComponent<Rigidbody>();
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            rb.AddForce(linearImpulse, ForceMode.VelocityChange);
            rb.AddTorque(angularImpulse, ForceMode.VelocityChange);

            var body = new DiceBody
            {
                Instance = instance,
                Rigidbody = rb,
                MeshFaces = mesh.Faces,
                SpawnTick = currentTick,
            };

            body.RecordPose(currentTick);

            bodiesByInstanceId[instance.InstanceId] = body;
            return instance.InstanceId;
        }

        public void Wake(int diceInstanceId, Vector3 linearImpulse, Vector3 angularImpulse)
        {
            var body = bodiesByInstanceId[diceInstanceId];

            // resting pose, before the impulse, as this window's start
            body.RecordPose(currentTick);

            body.Rigidbody.isKinematic = false;
            body.Rigidbody.AddForce(linearImpulse, ForceMode.VelocityChange);
            body.Rigidbody.AddTorque(angularImpulse, ForceMode.VelocityChange);
            body.IsSettled = false;
            body.SettledTimer = 0f;
        }

        public void Remove(int diceInstanceId)
        {
            var body = bodiesByInstanceId[diceInstanceId];
            bodiesByInstanceId.Remove(diceInstanceId);

            removedSinceLastExtract.Add(BuildRecord(body, removeTick: currentTick));

            GameObject.Destroy(body.Rigidbody.gameObject);
        }

        public bool AllDynamicSettled =>
            bodiesByInstanceId.Values.All(body => body.Rigidbody.isKinematic || body.IsSettled);

        public void SimulateUntilAllSettled()
        {
            // Runs StepUntilSettleTransition until every dynamic die is settled, ignoring transitions -
            // for the common case (drafting, a plain reroll) where nothing needs to react per-die.

            while (!AllDynamicSettled)
            {
                var settled = StepUntilAnySettled();
                if (settled == null)
                {
                    Log.Error("DiceSimulationWorld", "Could not finish simulation");
                    break;
                }
            }
        }

        public List<int> StepUntilAnySettled(int maxTicks = -1)
        {
            // Advances ticks until at least one currently-dynamic die's settled state flips false -> true
            // this call, or nothing is left dynamic. Returns the instance ids that just transitioned -
            // may be empty if the loop bottoms out on AllDynamicSettled without a fresh transition.

            if (maxTicks < 0) maxTicks = dicePhysicsMaxTicksPerStep;

            float settleLinSqr = diceSettleLinearVelocityThreshold * diceSettleLinearVelocityThreshold;
            float settleAngSqr = diceSettleAngularVelocityThreshold * diceSettleAngularVelocityThreshold;

            List<int> transitioned = new();

            for (int i = 0; i < maxTicks; i++)
            {
                physicsScene.Simulate(fixedTimestep);
                currentTick++;

                foreach (var (instanceId, body) in bodiesByInstanceId)
                {
                    if (body.Rigidbody.isKinematic) continue;

                    body.RecordPose(currentTick);

                    bool isBelowThreshold =
                        body.Rigidbody.linearVelocity.sqrMagnitude < settleLinSqr &&
                        body.Rigidbody.angularVelocity.sqrMagnitude < settleAngSqr;

                    if (isBelowThreshold)
                    {
                        body.SettledTimer += fixedTimestep;
                        if (!body.IsSettled && body.SettledTimer >= diceRequiredSettleDuration)
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

                if (transitioned.Count > 0) return transitioned;
                if (AllDynamicSettled) return transitioned;
            }

            return null;
        }

        public void FreezeAll()
        {
            // The one commit point: every currently-dynamic die's up-face becomes final and its rigidbody
            // goes kinematic, so later actions (even ones that physically bump it) can never change it.

            foreach (var body in bodiesByInstanceId.Values)
            {
                if (body.Rigidbody.isKinematic) continue;

                body.Instance.Value = DiceMeshFactory.GetUpFaceValue(body.Rigidbody.transform, body.MeshFaces);
                body.RecordPose(currentTick);
                body.Rigidbody.isKinematic = true;
            }
        }

        // ------------------------------ Data

        public DiceSimulationTraces ExtractTraceSinceLastExtract()
        {
            // Extracts everything that happened since the last extraction as one self-contained trace
            // (ticks rebased to start at 0), for shipping to clients. Dice untouched this window (already
            // resting from a previous action) are simply omitted.

            DiceSimulationTraces trace = new() { FixedTimestep = fixedTimestep };

            foreach (var body in bodiesByInstanceId.Values)
            {
                if (body.PosesSinceExtract.Count == 0) continue;

                trace.Dice.Add(BuildRecord(body, removeTick: -1));
                body.PosesSinceExtract.Clear();
            }

            foreach (var record in removedSinceLastExtract)
            {
                trace.Dice.Add(record);
            }
            removedSinceLastExtract.Clear();

            lastExtractTick = currentTick;
            return trace;
        }

        private DiceLifetimeTrace BuildRecord(DiceBody body, int removeTick)
        {
            List<DicePoseTrace> relativePoses = new(body.PosesSinceExtract.Count);
            foreach (var pose in body.PosesSinceExtract)
            {
                relativePoses.Add(new DicePoseTrace
                {
                    Tick = pose.Tick - lastExtractTick,
                    Position = pose.Position,
                    Rotation = pose.Rotation,
                });
            }

            return new DiceLifetimeTrace
            {
                Instance = body.Instance.Clone(),
                SpawnTick = Mathf.Max(0, body.SpawnTick - lastExtractTick),
                PoseTraces = relativePoses,
                RemoveTick = removeTick < 0 ? -1 : removeTick - lastExtractTick,
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
            public readonly List<DicePoseTrace> PosesSinceExtract = new();

            public void RecordPose(int tick)
            {
                PosesSinceExtract.Add(new DicePoseTrace
                {
                    Tick = tick,
                    Position = Rigidbody.position,
                    Rotation = Rigidbody.rotation,
                });
            }
        }
    }
}
