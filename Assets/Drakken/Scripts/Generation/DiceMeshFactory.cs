using System;
using System.Collections.Generic;
using Drakken.Domain;
using UnityEngine;

namespace Drakken.Generation
{
    public static class DiceMeshFactory
    {
        public static DiceMesh Create(DiceInstance diceInstance, Material material = null)
        {
            DiceMesh diceMesh = diceInstance.Sides switch
            {
                4 => CreateTetrahedron(),
                6 => CreateCube(),
                _ when diceInstance.Sides > 0 && diceInstance.Sides % 2 == 0 => CreateBipyramid(diceInstance.Sides),
                _ => throw new NotSupportedException($"Procedural dice mesh generation for a D{diceInstance.Sides} is not implemented yet.")
            };

            if (material != null)
            {
                diceMesh.GameObject.GetComponent<MeshRenderer>().sharedMaterial = material;
            }

            return diceMesh;
        }

        public static int GetUpFaceValue(Transform dieTransform, IReadOnlyList<DiceFacePose> faces)
        {
            int bestFaceIndex = 0;
            float bestDot = float.NegativeInfinity;

            for (int i = 0; i < faces.Count; i++)
            {
                float dot = Vector3.Dot(dieTransform.TransformDirection(faces[i].Direction), Vector3.up);
                if (dot > bestDot)
                {
                    bestDot = dot;
                    bestFaceIndex = i;
                }
            }

            return bestFaceIndex + 1;
        }

        private static Quaternion GetFaceLabelRotation(Vector3 direction)
        {
            Vector3 upHint = Mathf.Abs(Vector3.Dot(direction, Vector3.up)) > 0.99f ? Vector3.forward : Vector3.up;
            return Quaternion.LookRotation(-direction, upHint);
        }

        private static DiceMesh CreateCube()
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = "D6 (Generated)";

            // Opposite faces sum to 7, matching standard D6 convention.
            Vector3[] directions =
            {
                Vector3.up, // 1
                Vector3.right, // 2
                Vector3.forward, // 3
                Vector3.back, // 4
                Vector3.left, // 5
                Vector3.down // 6
            };

            // GameObject.CreatePrimitive(Cube) is a unit cube, so faces sit at half-extent 0.5.
            const float halfExtent = 0.5f;
            DiceFacePose[] faces = new DiceFacePose[directions.Length];
            for (int i = 0; i < directions.Length; i++)
            {
                faces[i] = new DiceFacePose(directions[i], directions[i] * halfExtent, GetFaceLabelRotation(directions[i]));
            }

            return new DiceMesh(go, faces);
        }

        private static DiceMesh CreateTetrahedron()
        {
            // Alternating corners of a cube form a regular tetrahedron centered on the origin.
            Vector3[] c =
            {
                new(0.5f, 0.5f, 0.5f),
                new(0.5f, -0.5f, -0.5f),
                new(-0.5f, 0.5f, -0.5f),
                new(-0.5f, -0.5f, 0.5f)
            };

            ConvexMeshBuilder builder = new();
            builder.AddTriangle(c[1], c[2], c[3]);
            builder.AddTriangle(c[0], c[3], c[2]);
            builder.AddTriangle(c[0], c[1], c[3]);
            builder.AddTriangle(c[0], c[2], c[1]);

            GameObject go = builder.Build("D4 (Generated)");
            return new DiceMesh(go, builder.Faces);
        }

        private static DiceMesh CreateBipyramid(int sides)
        {
            int baseSideCount = sides / 2;
            if (baseSideCount < 3)
            {
                throw new NotSupportedException($"Procedural dice mesh generation for a D{sides} is not implemented yet.");
            }

            const float baseRadius = 1f;
            const float apexHeight = 1f;

            Vector3[] baseVertices = new Vector3[baseSideCount];
            for (int i = 0; i < baseSideCount; i++)
            {
                float angle = 2f * Mathf.PI * i / baseSideCount;
                baseVertices[i] = new Vector3(Mathf.Cos(angle) * baseRadius, 0f, Mathf.Sin(angle) * baseRadius);
            }

            Vector3 topApex = new(0f, apexHeight, 0f);
            Vector3 bottomApex = new(0f, -apexHeight, 0f);

            ConvexMeshBuilder builder = new();
            for (int i = 0; i < baseSideCount; i++)
            {
                Vector3 current = baseVertices[i];
                Vector3 next = baseVertices[(i + 1) % baseSideCount];

                builder.AddTriangle(topApex, current, next);
                builder.AddTriangle(bottomApex, next, current);
            }

            GameObject go = builder.Build($"D{sides} (Generated)");
            return new DiceMesh(go, builder.Faces);
        }

        private class ConvexMeshBuilder
        {
            private readonly List<Vector3> vertices = new();
            private readonly List<int> triangles = new();
            private readonly List<DiceFacePose> faces = new();

            public IReadOnlyList<DiceFacePose> Faces => faces;

            public void AddTriangle(Vector3 a, Vector3 b, Vector3 c)
            {
                Vector3 centroid = (a + b + c) / 3f;
                Vector3 normal = Vector3.Cross(b - a, c - a).normalized;

                // Fix winding direction
                if (Vector3.Dot(normal, centroid) < 0f)
                {
                    (b, c) = (c, b);
                }

                int startIndex = vertices.Count;
                vertices.Add(a);
                vertices.Add(b);
                vertices.Add(c);

                triangles.Add(startIndex);
                triangles.Add(startIndex + 1);
                triangles.Add(startIndex + 2);

                Vector3 direction = centroid.normalized;
                faces.Add(new DiceFacePose(direction, centroid, GetFaceLabelRotation(direction)));
            }

            public GameObject Build(string name)
            {
                Mesh mesh = new() { name = name };
                mesh.SetVertices(vertices);
                mesh.SetTriangles(triangles, 0);
                mesh.RecalculateNormals();
                mesh.RecalculateBounds();

                GameObject go = new(name);
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                go.AddComponent<MeshRenderer>();

                MeshCollider collider = go.AddComponent<MeshCollider>();
                collider.sharedMesh = mesh;
                collider.convex = true;

                return go;
            }
        }

        public readonly struct DiceFacePose
        {
            public readonly Vector3 Direction;
            public readonly Vector3 Position;
            public readonly Quaternion LabelRotation;

            public DiceFacePose(Vector3 direction, Vector3 position, Quaternion labelRotation)
            {
                Direction = direction;
                Position = position;
                LabelRotation = labelRotation;
            }
        }

        public readonly struct DiceMesh
        {
            public readonly GameObject GameObject;
            public readonly IReadOnlyList<DiceFacePose> Faces; // Index i is the pose for face value i+1

            public DiceMesh(GameObject gameObject, IReadOnlyList<DiceFacePose> faces)
            {
                GameObject = gameObject;
                Faces = faces;
            }
        }
    }
}
