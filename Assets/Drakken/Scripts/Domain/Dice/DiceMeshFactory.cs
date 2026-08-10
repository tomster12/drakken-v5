using System;
using System.Collections.Generic;
using Drakken.Domain;
using UnityEngine;

namespace Drakken.Domain.Dice
{
    public static class DiceMeshFactory
    {
        public const float BaseScale = 0.3f;

        public static DiceMesh Create(DiceInstance diceInstance, Material material = null, float scale = 1f)
        {
            float effectiveScale = scale * BaseScale;

            DiceMesh diceMesh = diceInstance.Sides switch
            {
                4 => CreateTetrahedron(effectiveScale),
                6 => CreateCube(effectiveScale),
                8 => CreateOctohedron(effectiveScale),
                12 => CreateDodecahedron(effectiveScale),
                20 => CreateIcosahedron(effectiveScale),
                _ when diceInstance.Sides > 2 && diceInstance.Sides % 2 == 0 => CreateBipyramid(diceInstance.Sides, effectiveScale),
                _ => throw new NotSupportedException($"Procedural dice mesh generation for a D{diceInstance.Sides} is not implemented yet.")
            };

            if (material != null)
            {
                diceMesh.Renderer.sharedMaterial = material;
            }

            return diceMesh;
        }

        public static int GetUpFaceValue(Transform dieTransform, IReadOnlyList<DiceFacePose> faces)
        {
            int bestFaceIndex = 0;
            float bestDot = float.NegativeInfinity;

            for (int i = 0; i < faces.Count; i++)
            {
                float dot = Vector3.Dot(dieTransform.TransformDirection(faces[i].UpDetectionDirection), Vector3.up);
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

        private static DiceMesh CreateCube(float scale)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = "D6 (Generated)";

            // GameObject.CreatePrimitive(Cube) is a unit cube (half-extent 0.5);
            // scale it up to reach the requested half-extent.
            go.transform.localScale = Vector3.one * scale;

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

            float halfExtent = 0.5f * scale;
            DiceFacePose[] faces = new DiceFacePose[directions.Length];
            for (int i = 0; i < directions.Length; i++)
            {
                faces[i] = new DiceFacePose(directions[i], directions[i] * halfExtent, GetFaceLabelRotation(directions[i]));
            }

            return new DiceMesh(go, faces);
        }

        private static DiceMesh CreateTetrahedron(float scale)
        {
            // Alternating corners of a cube form a regular tetrahedron centered on the origin.
            float h = 0.5f * scale;
            Vector3[] c =
            {
                new(h, h, h),
                new(h, -h, -h),
                new(-h, h, -h),
                new(-h, -h, h)
            };

            // A regular tetrahedron rests on one face with the opposite vertex pointing up, so it's read
            // from that vertex rather than any face normal (which are all symmetric around that vertex
            // and would otherwise tie). Each triangle below is opposite vertex c[i], in that order.
            ConvexMeshBuilder builder = new();
            builder.AddTriangle(c[1], c[2], c[3], upDetectionDirection: c[0]);
            builder.AddTriangle(c[0], c[3], c[2], upDetectionDirection: c[1]);
            builder.AddTriangle(c[0], c[1], c[3], upDetectionDirection: c[2]);
            builder.AddTriangle(c[0], c[2], c[1], upDetectionDirection: c[3]);

            GameObject go = builder.Build("D4 (Generated)");
            return new DiceMesh(go, builder.Faces);
        }

        private static DiceMesh CreateOctohedron(float scale)
        {
            Vector3 posX = Vector3.right * scale;
            Vector3 negX = Vector3.left * scale;
            Vector3 posY = Vector3.up * scale;
            Vector3 negY = Vector3.down * scale;
            Vector3 posZ = Vector3.forward * scale;
            Vector3 negZ = Vector3.back * scale;

            // Opposite faces sum to 9, matching standard D8 convention.
            (Vector3 a, Vector3 b, Vector3 c)[] faceVerts =
            {
                (posX, posY, posZ), // 1
                (posX, posY, negZ), // 2
                (posX, negY, posZ), // 3
                (negX, posY, posZ), // 4
                (posX, negY, negZ), // 5
                (negX, posY, negZ), // 6
                (negX, negY, posZ), // 7
                (negX, negY, negZ) // 8
            };

            ConvexMeshBuilder builder = new();
            foreach ((Vector3 a, Vector3 b, Vector3 c) in faceVerts)
            {
                builder.AddTriangle(a, b, c);
            }

            GameObject go = builder.Build("D8 (Generated)");
            return new DiceMesh(go, builder.Faces);
        }

        private static DiceMesh CreateDodecahedron(float scale)
        {
            float phi = (1f + Mathf.Sqrt(5f)) / 2f;
            float inv = 1f / phi;

            // All 20 vertices of a regular dodecahedron sit at the same distance from the
            // origin; this normalizes that circumradius to 1 before applying the requested scale.
            float unitScale = 1f / Mathf.Sqrt(3f) * scale;

            Vector3[] v =
            {
                new Vector3(1, 1, 1) * unitScale, // 0
                new Vector3(1, 1, -1) * unitScale, // 1
                new Vector3(1, -1, 1) * unitScale, // 2
                new Vector3(1, -1, -1) * unitScale, // 3
                new Vector3(-1, 1, 1) * unitScale, // 4
                new Vector3(-1, 1, -1) * unitScale, // 5
                new Vector3(-1, -1, 1) * unitScale, // 6
                new Vector3(-1, -1, -1) * unitScale, // 7
                new Vector3(0, inv, phi) * unitScale, // 8
                new Vector3(inv, phi, 0) * unitScale, // 9
                new Vector3(phi, 0, inv) * unitScale, // 10
                new Vector3(0, inv, -phi) * unitScale, // 11
                new Vector3(inv, -phi, 0) * unitScale, // 12
                new Vector3(-phi, 0, inv) * unitScale, // 13
                new Vector3(0, -inv, phi) * unitScale, // 14
                new Vector3(-inv, phi, 0) * unitScale, // 15
                new Vector3(phi, 0, -inv) * unitScale, // 16
                new Vector3(0, -inv, -phi) * unitScale, // 17
                new Vector3(-inv, -phi, 0) * unitScale, // 18
                new Vector3(-phi, 0, -inv) * unitScale // 19
            };

            // Opposite faces sum to 13, matching standard D12 convention. Each entry lists a
            // pentagon's vertices in perimeter order.
            int[][] faceIndices =
            {
                new[] { 11, 17, 7, 19, 5 }, // 1
                new[] { 9, 1, 11, 5, 15 }, // 2
                new[] { 4, 15, 5, 19, 13 }, // 3
                new[] { 14, 8, 4, 13, 6 }, // 4
                new[] { 7, 18, 6, 13, 19 }, // 5
                new[] { 3, 12, 18, 7, 17 }, // 6
                new[] { 0, 9, 15, 4, 8 }, // 7
                new[] { 10, 16, 1, 9, 0 }, // 8
                new[] { 1, 16, 3, 17, 11 }, // 9
                new[] { 16, 10, 2, 12, 3 }, // 10
                new[] { 12, 2, 14, 6, 18 }, // 11
                new[] { 2, 10, 0, 8, 14 } // 12
            };

            ConvexMeshBuilder builder = new();
            foreach (int[] face in faceIndices)
            {
                builder.AddFace(new[] { v[face[0]], v[face[1]], v[face[2]], v[face[3]], v[face[4]] });
            }

            GameObject go = builder.Build("D12 (Generated)");
            return new DiceMesh(go, builder.Faces);
        }

        private static DiceMesh CreateIcosahedron(float scale)
        {
            float phi = (1f + Mathf.Sqrt(5f)) / 2f;

            // All 12 vertices of a regular icosahedron sit at the same distance from the
            // origin; this normalizes that circumradius to 1 before applying the requested scale.
            float unitScale = 1f / Mathf.Sqrt(phi + 2f) * scale;

            Vector3[] v =
            {
                new Vector3(0, 1, phi) * unitScale, // 0
                new Vector3(1, phi, 0) * unitScale, // 1
                new Vector3(phi, 0, 1) * unitScale, // 2
                new Vector3(0, 1, -phi) * unitScale, // 3
                new Vector3(1, -phi, 0) * unitScale, // 4
                new Vector3(-phi, 0, 1) * unitScale, // 5
                new Vector3(0, -1, phi) * unitScale, // 6
                new Vector3(-1, phi, 0) * unitScale, // 7
                new Vector3(phi, 0, -1) * unitScale, // 8
                new Vector3(0, -1, -phi) * unitScale, // 9
                new Vector3(-1, -phi, 0) * unitScale, // 10
                new Vector3(-phi, 0, -1) * unitScale // 11
            };

            // Opposite faces sum to 21, matching standard D20 convention.
            int[][] faceIndices =
            {
                new[] { 6, 4, 2 }, // 1
                new[] { 7, 1, 3 }, // 2
                new[] { 8, 1, 2 }, // 3
                new[] { 8, 4, 2 }, // 4
                new[] { 8, 1, 3 }, // 5
                new[] { 8, 9, 3 }, // 6
                new[] { 8, 9, 4 }, // 7
                new[] { 10, 9, 4 }, // 8
                new[] { 0, 1, 2 }, // 9
                new[] { 0, 6, 2 }, // 10
                new[] { 11, 9, 3 }, // 11
                new[] { 11, 10, 9 }, // 12
                new[] { 0, 7, 1 }, // 13
                new[] { 0, 7, 5 }, // 14
                new[] { 0, 6, 5 }, // 15
                new[] { 10, 6, 5 }, // 16
                new[] { 11, 7, 5 }, // 17
                new[] { 11, 10, 5 }, // 18
                new[] { 10, 6, 4 }, // 19
                new[] { 11, 7, 3 } // 20
            };

            ConvexMeshBuilder builder = new();
            foreach (int[] face in faceIndices)
            {
                builder.AddTriangle(v[face[0]], v[face[1]], v[face[2]]);
            }

            GameObject go = builder.Build("D20 (Generated)");
            return new DiceMesh(go, builder.Faces);
        }

        private static DiceMesh CreateBipyramid(int sides, float scale)
        {
            int baseSideCount = sides / 2;
            if (baseSideCount < 3)
            {
                throw new NotSupportedException($"Procedural dice mesh generation for a D{sides} is not implemented yet.");
            }

            float baseRadius = scale;
            float apexHeight = scale;

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

            // upDetectionDirection overrides what GetUpFaceValue checks against world-up for this face,
            // for shapes (e.g. tetrahedron) read from a vertex rather than the face's own normal.
            public void AddTriangle(Vector3 a, Vector3 b, Vector3 c, Vector3? upDetectionDirection = null)
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
                faces.Add(new DiceFacePose(direction, centroid, GetFaceLabelRotation(direction), upDetectionDirection));
            }

            // Adds a convex planar polygon (e.g. a pentagon) as a single dice face, fan-triangulated
            // from its centroid. verts must already be ordered around the polygon's perimeter.
            public void AddFace(Vector3[] verts, Vector3? upDetectionDirection = null)
            {
                Vector3 centroid = Vector3.zero;
                foreach (Vector3 vertex in verts)
                {
                    centroid += vertex;
                }
                centroid /= verts.Length;

                Vector3 normal = Vector3.Cross(verts[1] - verts[0], verts[2] - verts[0]).normalized;

                // Fix winding direction
                if (Vector3.Dot(normal, centroid) < 0f)
                {
                    Array.Reverse(verts);
                }

                int centerIndex = vertices.Count;
                vertices.Add(centroid);
                vertices.AddRange(verts);

                for (int i = 0; i < verts.Length; i++)
                {
                    triangles.Add(centerIndex);
                    triangles.Add(centerIndex + 1 + i);
                    triangles.Add(centerIndex + 1 + (i + 1) % verts.Length);
                }

                Vector3 direction = centroid.normalized;
                faces.Add(new DiceFacePose(direction, centroid, GetFaceLabelRotation(direction), upDetectionDirection));
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
            // The face's own outward normal direction; used to place/orient a label flush against it.
            public readonly Vector3 Direction;
            public readonly Vector3 Position;
            public readonly Quaternion LabelRotation;

            // Direction checked against world-up to decide whether this face's value is "the" reading
            // once the die settles. Usually the same as Direction, since the settled face really does
            // point up (cube, bipyramid). For shapes read from a vertex rather than a face (tetrahedron),
            // this instead points toward that vertex.
            public readonly Vector3 UpDetectionDirection;

            public DiceFacePose(Vector3 direction, Vector3 position, Quaternion labelRotation, Vector3? upDetectionDirection = null)
            {
                Direction = direction;
                Position = position;
                LabelRotation = labelRotation;
                UpDetectionDirection = (upDetectionDirection ?? direction).normalized;
            }
        }

        public readonly struct DiceMesh
        {
            public readonly GameObject GameObject;
            public readonly IReadOnlyList<DiceFacePose> Faces; // Index i is the pose for face value i+1
            public readonly MeshRenderer Renderer;

            public DiceMesh(GameObject gameObject, IReadOnlyList<DiceFacePose> faces)
            {
                GameObject = gameObject;
                Faces = faces;
                Renderer = gameObject.GetComponent<MeshRenderer>();
            }
        }
    }
}
