//
//  Outline.cs
//  QuickOutline
//
//  Created by Chris Nolet on 3/30/18.
//  Copyright © 2018 Chris Nolet. All rights reserved.
//
//  Modified for this project.
//

using System.Collections.Generic;
using System.Linq;
using UnityEngine;

[DisallowMultipleComponent]
public class Outline : MonoBehaviour
{
    private static readonly Dictionary<Mesh, List<Vector3>> smoothNormalsBySourceMesh = new();

    public enum Mode
    {
        OutlineAll,
        OutlineVisible,
        OutlineHidden,
        OutlineAndSilhouette,
        SilhouetteOnly
    }

    public Mode OutlineMode
    {
        get { return outlineMode; }
        set
        {
            outlineMode = value;
            UpdateMaterialProperties();
        }
    }

    public Color OutlineColor
    {
        get { return outlineColor; }
        set
        {
            outlineColor = value;
            UpdateMaterialProperties();
        }
    }

    public float OutlineWidth
    {
        get { return outlineWidth; }
        set
        {
            outlineWidth = value;
            UpdateMaterialProperties();
        }
    }

    public bool IsSetUp => outlineMaskMaterial != null;
    public bool IsEnabled { get; private set; }

    [SerializeField] private Mode outlineMode;
    [SerializeField] private Color outlineColor = Color.white;
    [SerializeField, Range(0f, 20f)] private float outlineWidth = 2f;

    private Material outlineMaskMaterial;
    private Material outlineFillMaterial;

    [ContextMenu("Setup")]
    public void Setup()
    {
        if (IsSetUp) Teardown();

        outlineMaskMaterial = Instantiate(Resources.Load<Material>(@"Materials/OutlineMask"));
        outlineFillMaterial = Instantiate(Resources.Load<Material>(@"Materials/OutlineFill"));

        outlineMaskMaterial.name = "OutlineMask (Instance)";
        outlineFillMaterial.name = "OutlineFill (Instance)";

        LoadNormalsIntoMeshes();
        UpdateMaterialProperties();
    }

    [ContextMenu("Teardown")]
    public void Teardown()
    {
        Disable();

        DestroySafe(outlineMaskMaterial);
        DestroySafe(outlineFillMaterial);

        outlineMaskMaterial = null;
        outlineFillMaterial = null;
    }

    [ContextMenu("Enable")]
    public void Enable()
    {
        if (!IsSetUp) return;
        if (IsEnabled) return;

        foreach (var renderer in GetComponentsInChildren<Renderer>(true))
        {
            if (IsTextMeshProRenderer(renderer)) continue;

            var materials = renderer.sharedMaterials.ToList();

            materials.Add(outlineMaskMaterial);
            materials.Add(outlineFillMaterial);

            renderer.materials = materials.ToArray();
        }

        IsEnabled = true;
    }

    [ContextMenu("Disable")]
    public void Disable()
    {
        if (!IsSetUp) return;
        if (!IsEnabled) return;

        foreach (var renderer in GetComponentsInChildren<Renderer>(true))
        {
            if (IsTextMeshProRenderer(renderer)) continue;

            var materials = renderer.sharedMaterials.ToList();

            materials.Remove(outlineMaskMaterial);
            materials.Remove(outlineFillMaterial);

            renderer.materials = materials.ToArray();
        }

        IsEnabled = false;
    }

    public void SetEnabled(bool enabled)
    {
        if (enabled) Enable();
        else Disable();
    }

    private void OnDestroy()
    {
        if (IsSetUp) Teardown();
    }

    private void UpdateMaterialProperties()
    {
        if (!IsSetUp) return;

        outlineFillMaterial.SetColor("_OutlineColor", outlineColor);

        switch (outlineMode)
        {
            case Mode.OutlineAll:
                outlineMaskMaterial.SetFloat("_ZTest", (float)UnityEngine.Rendering.CompareFunction.Always);
                outlineFillMaterial.SetFloat("_ZTest", (float)UnityEngine.Rendering.CompareFunction.Always);
                outlineFillMaterial.SetFloat("_OutlineWidth", outlineWidth);
                break;

            case Mode.OutlineVisible:
                outlineMaskMaterial.SetFloat("_ZTest", (float)UnityEngine.Rendering.CompareFunction.Always);
                outlineFillMaterial.SetFloat("_ZTest", (float)UnityEngine.Rendering.CompareFunction.LessEqual);
                outlineFillMaterial.SetFloat("_OutlineWidth", outlineWidth);
                break;

            case Mode.OutlineHidden:
                outlineMaskMaterial.SetFloat("_ZTest", (float)UnityEngine.Rendering.CompareFunction.Always);
                outlineFillMaterial.SetFloat("_ZTest", (float)UnityEngine.Rendering.CompareFunction.Greater);
                outlineFillMaterial.SetFloat("_OutlineWidth", outlineWidth);
                break;

            case Mode.OutlineAndSilhouette:
                outlineMaskMaterial.SetFloat("_ZTest", (float)UnityEngine.Rendering.CompareFunction.LessEqual);
                outlineFillMaterial.SetFloat("_ZTest", (float)UnityEngine.Rendering.CompareFunction.Always);
                outlineFillMaterial.SetFloat("_OutlineWidth", outlineWidth);
                break;

            case Mode.SilhouetteOnly:
                outlineMaskMaterial.SetFloat("_ZTest", (float)UnityEngine.Rendering.CompareFunction.LessEqual);
                outlineFillMaterial.SetFloat("_ZTest", (float)UnityEngine.Rendering.CompareFunction.Greater);
                outlineFillMaterial.SetFloat("_OutlineWidth", 0f);
                break;
        }
    }

    private void DestroySafe(Material material)
    {
        if (material == null) return;
        if (Application.isPlaying) Destroy(material);
        else DestroyImmediate(material);
    }

    private static bool IsTextMeshProRenderer(Renderer renderer)
    {
        return renderer.TryGetComponent<TMPro.TMP_Text>(out _);
    }

    private void LoadNormalsIntoMeshes()
    {
        foreach (var meshFilter in GetComponentsInChildren<MeshFilter>(true))
        {
            if (!meshFilter.TryGetComponent<Renderer>(out var renderer)) continue;
            if (IsTextMeshProRenderer(renderer)) continue;

            var sourceMesh = meshFilter.sharedMesh;

            if (!smoothNormalsBySourceMesh.TryGetValue(sourceMesh, out var smoothNormals))
            {
                smoothNormals = CalculateSmoothNormals(sourceMesh);
                smoothNormalsBySourceMesh[sourceMesh] = smoothNormals;
            }

            var instancedMesh = meshFilter.mesh;
            instancedMesh.SetUVs(3, smoothNormals);
            CombineSubmeshes(instancedMesh, renderer.sharedMaterials);
        }
    }

    private List<Vector3> CalculateSmoothNormals(Mesh mesh)
    {
        var groups = mesh.vertices.Select((vertex, index) => new KeyValuePair<Vector3, int>(vertex, index)).GroupBy(pair => pair.Key);

        var smoothNormals = new List<Vector3>(mesh.normals);

        foreach (var group in groups)
        {
            if (group.Count() == 1) continue;

            var smoothNormal = Vector3.zero;

            foreach (var pair in group)
            {
                smoothNormal += smoothNormals[pair.Value];
            }

            smoothNormal.Normalize();

            foreach (var pair in group)
            {
                smoothNormals[pair.Value] = smoothNormal;
            }
        }

        return smoothNormals;
    }

    private void CombineSubmeshes(Mesh mesh, Material[] materials)
    {
        if (mesh.subMeshCount == 1) return;
        if (mesh.subMeshCount > materials.Length) return;

        mesh.subMeshCount++;
        mesh.SetTriangles(mesh.triangles, mesh.subMeshCount - 1);
    }
}
