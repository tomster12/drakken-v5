using System.Collections.Generic;
using System.Linq;
using Drakken.Domain;
using Drakken.Domain.Static;
using Drakken.Generation;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Drakken.Server.Dice
{
    // A persistent, per-player physics table. Lives for the whole match - dice thrown into it sit
    // here (kinematic, at rest) between actions, so a later action (a round-start reroll, a token's
    // dice effect) resumes against exactly where they already are rather than needing to reseed state.
    //
    // "Settled" and "final" are different things: StepUntilSettleTransition only reports that a die
    // has stopped moving for long enough to look natural - it can still get knocked and unsettle again.
    // FreezeAll is the one deliberate commit point where every currently-dynamic die's value becomes
    // permanent (its rigidbody goes kinematic, so nothing can disturb it further).
    public class DicePhysicsWorld
    {
        private readonly Scene scene;
        private readonly PhysicsScene physicsScene;
        private readonly float fixedTimestep;
        private readonly Dictionary<int, DiceBody> bodiesByInstanceId = new();
        private readonly List<DiceTraceRecord> removedSinceLastExtract = new();
        private int currentTick;
        private int lastExtractTick;

        public DicePhysicsWorld(string name, Vector3 trayCenter, Vector3 traySize, float wallHeight)
        {
            scene = SceneManager.CreateScene(name, new CreateSceneParameters(LocalPhysicsMode.Physics3D));
            physicsScene = scene.GetPhysicsScene();
            fixedTimestep = GameConstants.DicePhysicsFixedTimestep;

            CreateTray(trayCenter, traySize, wallHeight);
        }

        private void CreateTray(Vector3 center, Vector3 size, float wallHeight)
        {
            const float wallThickness = 0.5f;

            CreateStaticBox("Tray Floor", center, new Vector3(size.x, wallThickness, size.z));
            CreateStaticBox("Tray Wall +X", center + new Vector3(size.x / 2f, wallHeight / 2f, 0f), new Vector3(wallThickness, wallHeight, size.z));
            CreateStaticBox("Tray Wall -X", center + new Vector3(-size.x / 2f, wallHeight / 2f, 0f), new Vector3(wallThickness, wallHeight, size.z));
            CreateStaticBox("Tray Wall +Z", center + new Vector3(0f, wallHeight / 2f, size.z / 2f), new Vector3(size.x, wallHeight, wallThickness));
            CreateStaticBox("Tray Wall -Z", center + new Vector3(0f, wallHeight / 2f, -size.z / 2f), new Vector3(size.x, wallHeight, wallThickness));
        }

        private void CreateStaticBox(string name, Vector3 position, Vector3 size)
        {
            GameObject go = new(name);
            SceneManager.MoveGameObjectToScene(go, scene);
            go.transform.position = position;

            BoxCollider collider = go.AddComponent<BoxCollider>();
            collider.size = size;
        }

        // ------------------------------ Actions

        public int Spawn(DiceInstance instance, Vector3 position, Quaternion rotation,
            Vector3 linearImpulse, Vector3 angularImpulse, int parentInstanceId = 0)
        {
            var mesh = DiceMeshFactory.Create(instance);
            SceneManager.MoveGameObjectToScene(mesh.GameObject, scene);
            mesh.GameObject.transform.SetPositionAndRotation(position, rotation);

            var rigidbody = mesh.GameObject.AddComponent<Rigidbody>();
            rigidbody.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            rigidbody.AddForce(linearImpulse, ForceMode.VelocityChange);
            rigidbody.AddTorque(angularImpulse, ForceMode.VelocityChange);

            var body = new DiceBody
            {
                Instance = instance,
                Rigidbody = rigidbody,
                Faces = mesh.Faces,
                ParentInstanceId = parentInstanceId,
                SpawnTick = currentTick,
            };
            body.RecordPose(currentTick);

            bodiesByInstanceId[instance.InstanceId] = body;
            return instance.InstanceId;
        }

        public void Wake(int diceInstanceId, Vector3 linearImpulse, Vector3 angularImpulse)
        {
            var body = bodiesByInstanceId[diceInstanceId];

            body.RecordPose(currentTick); // resting pose, before the impulse, as this window's start
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

        // Advances ticks until at least one currently-dynamic die's settled state flips false -> true
        // this call, or nothing is left dynamic. Returns the instance ids that just transitioned -
        // may be empty if the loop bottoms out on AllDynamicSettled without a fresh transition.
        public List<int> StepUntilSettleTransition(int maxTicks = -1)
        {
            if (maxTicks < 0) maxTicks = GameConstants.DicePhysicsMaxTicksPerStep;

            float settleLinSqr = GameConstants.DiceSettleLinearVelocityThreshold * GameConstants.DiceSettleLinearVelocityThreshold;
            float settleAngSqr = GameConstants.DiceSettleAngularVelocityThreshold * GameConstants.DiceSettleAngularVelocityThreshold;

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
                        if (!body.IsSettled && body.SettledTimer >= GameConstants.DiceRequiredSettleDuration)
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

            return transitioned;
        }

        public bool AllDynamicSettled =>
            bodiesByInstanceId.Values.All(body => body.Rigidbody.isKinematic || body.IsSettled);

        // Runs StepUntilSettleTransition until every dynamic die is settled, ignoring transitions -
        // for the common case (drafting, a plain reroll) where nothing needs to react per-die.
        public void SettleAll()
        {
            while (!AllDynamicSettled)
            {
                StepUntilSettleTransition();
            }
        }

        // The one commit point: every currently-dynamic die's up-face becomes final and its rigidbody
        // goes kinematic, so later actions (even ones that physically bump it) can never change it.
        public void FreezeAll()
        {
            foreach (var body in bodiesByInstanceId.Values)
            {
                if (body.Rigidbody.isKinematic) continue;

                body.Instance.Value = DiceMeshFactory.GetUpFaceValue(body.Rigidbody.transform, body.Faces);
                body.RecordPose(currentTick);
                body.Rigidbody.isKinematic = true;
            }
        }

        // Extracts everything that happened since the last extraction as one self-contained trace
        // (ticks rebased to start at 0), for shipping to clients. Dice untouched this window (already
        // resting from a previous action) are simply omitted.
        public DicePhysicsTrace ExtractTraceSinceLastExtract()
        {
            DicePhysicsTrace trace = new() { FixedTimestep = fixedTimestep };

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

        private DiceTraceRecord BuildRecord(DiceBody body, int removeTick)
        {
            List<DicePoseSample> relativePoses = new(body.PosesSinceExtract.Count);
            foreach (var pose in body.PosesSinceExtract)
            {
                relativePoses.Add(new DicePoseSample
                {
                    Tick = pose.Tick - lastExtractTick,
                    Position = pose.Position,
                    Rotation = pose.Rotation,
                });
            }

            return new DiceTraceRecord
            {
                Instance = body.Instance.Clone(),
                ParentInstanceId = body.ParentInstanceId,
                SpawnTick = Mathf.Max(0, body.SpawnTick - lastExtractTick),
                Poses = relativePoses,
                RemoveTick = removeTick < 0 ? -1 : removeTick - lastExtractTick,
            };
        }

        public void Dispose()
        {
            foreach (var body in bodiesByInstanceId.Values)
            {
                GameObject.Destroy(body.Rigidbody.gameObject);
            }
            bodiesByInstanceId.Clear();

            SceneManager.UnloadSceneAsync(scene);
        }

        private class DiceBody
        {
            public DiceInstance Instance;
            public Rigidbody Rigidbody;
            public IReadOnlyList<DiceMeshFactory.DiceFacePose> Faces;
            public int ParentInstanceId;
            public int SpawnTick;
            public bool IsSettled;
            public float SettledTimer;
            public readonly List<DicePoseSample> PosesSinceExtract = new();

            public void RecordPose(int tick)
            {
                PosesSinceExtract.Add(new DicePoseSample
                {
                    Tick = tick,
                    Position = Rigidbody.position,
                    Rotation = Rigidbody.rotation,
                });
            }
        }
    }
}
