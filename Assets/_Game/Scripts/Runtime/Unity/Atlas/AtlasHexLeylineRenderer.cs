using System;
using System.Collections.Generic;
using System.Linq;
using SM.Atlas.Model;
using SM.Atlas.Services;
using UnityEngine;

namespace SM.Unity.Atlas;

[DisallowMultipleComponent]
public sealed class AtlasHexLeylineRenderer : MonoBehaviour
{
    public readonly struct LeylineEntry
    {
        public LeylineEntry(string nodeId, AtlasHexCoordinate hex, Vector3 worldPosition, AtlasNodeKind kind)
        {
            NodeId = nodeId;
            Hex = hex;
            WorldPosition = worldPosition;
            Kind = kind;
        }

        public string NodeId { get; }
        public AtlasHexCoordinate Hex { get; }
        public Vector3 WorldPosition { get; }
        public AtlasNodeKind Kind { get; }
    }

    [SerializeField] private Transform leylineRoot = null!;

    public static IReadOnlyList<LeylineEntry> BuildPlan(AtlasRegionDefinition region)
    {
        if (region == null)
        {
            throw new ArgumentNullException(nameof(region));
        }

        return region.Nodes
            .OrderBy(node => node.Hex.R)
            .ThenBy(node => node.Hex.Q)
            .Select(node => new LeylineEntry(node.NodeId, node.Hex, AtlasHexWorldMapper.ToWorld(node.Hex), node.Kind))
            .ToArray();
    }

    public void RebuildDefault()
    {
        Rebuild(AtlasGrayboxDataFactory.CreateRegion());
    }

    public void Rebuild(
        AtlasRegionDefinition region,
        Mesh? ringMesh = null,
        Func<AtlasNodeKind, Material>? materialResolver = null)
    {
        EnsureRoot();
        ClearChildren(leylineRoot);

        var shader = Shader.Find("SM/Atlas/HexLeyLine") ?? Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Standard");
        var mesh = ringMesh ?? AtlasHexWorldMapper.CreateHexRingMesh(AtlasHexWorldMapper.HexRadius, 0.028f, 0.012f);
        foreach (var entry in BuildPlan(region))
        {
            var go = new GameObject($"LeyLine_{entry.NodeId}");
            go.transform.SetParent(leylineRoot, false);
            go.transform.position = entry.WorldPosition + Vector3.up * 0.03f;

            var filter = go.AddComponent<MeshFilter>();
            filter.sharedMesh = mesh;

            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = materialResolver?.Invoke(entry.Kind) ?? CreateLeylineMaterial(shader, entry.Kind);
        }
    }

    private void EnsureRoot()
    {
        if (leylineRoot != null)
        {
            return;
        }

        var go = new GameObject("AtlasLeylineRuntimeRoot");
        go.transform.SetParent(transform, false);
        leylineRoot = go.transform;
    }

    private static Material CreateLeylineMaterial(Shader shader, AtlasNodeKind kind)
    {
        var color = kind switch
        {
            AtlasNodeKind.Boss => new Color(1.00f, 0.38f, 0.32f, 0.70f),
            AtlasNodeKind.Elite => new Color(0.94f, 0.58f, 0.38f, 0.66f),
            AtlasNodeKind.SigilAnchor => new Color(0.50f, 0.90f, 0.96f, 0.68f),
            AtlasNodeKind.Reward => new Color(1.00f, 0.76f, 0.28f, 0.64f),
            AtlasNodeKind.Event => new Color(0.56f, 0.70f, 1.00f, 0.58f),
            _ => new Color(1.00f, 0.88f, 0.54f, 0.48f),
        };
        var material = new Material(shader)
        {
            name = $"M_Atlas_LeyLine_{kind}",
            color = color,
        };
        if (material.HasProperty("_BaseColor"))
        {
            material.SetColor("_BaseColor", color);
        }

        if (material.HasProperty("_EmissionColor"))
        {
            material.SetColor("_EmissionColor", color * 1.45f);
        }

        if (material.HasProperty("_Tint"))
        {
            material.SetColor("_Tint", color);
        }

        return material;
    }

    private static void ClearChildren(Transform root)
    {
        for (var i = root.childCount - 1; i >= 0; i--)
        {
            var child = root.GetChild(i).gameObject;
            if (Application.isPlaying)
            {
                Destroy(child);
            }
            else
            {
                DestroyImmediate(child);
            }
        }
    }
}
