using System;
using System.Collections.Generic;
using System.Linq;
using SM.Atlas.Model;
using SM.Unity.UI.Atlas;
using UnityEngine;

namespace SM.Unity.Atlas;

[DisallowMultipleComponent]
public sealed class AtlasSigilAuraVFXController : MonoBehaviour
{
    public readonly struct AuraEntry
    {
        public AuraEntry(string nodeId, AtlasHexCoordinate hex, Vector3 worldPosition, IReadOnlyList<AtlasModifierCategory> categories)
        {
            NodeId = nodeId;
            Hex = hex;
            WorldPosition = worldPosition;
            Categories = categories;
            IsOverlap = categories.Count > 1;
        }

        public string NodeId { get; }
        public AtlasHexCoordinate Hex { get; }
        public Vector3 WorldPosition { get; }
        public IReadOnlyList<AtlasModifierCategory> Categories { get; }
        public bool IsOverlap { get; }
    }

    [SerializeField] private Transform auraRoot = null!;

    public static IReadOnlyList<AuraEntry> BuildAuraPlan(AtlasScreenViewState state)
    {
        if (state == null)
        {
            throw new ArgumentNullException(nameof(state));
        }

        return state.Tiles
            .Where(tile => tile.AuraCategories.Count > 0)
            .OrderBy(tile => tile.Hex.R)
            .ThenBy(tile => tile.Hex.Q)
            .Select(tile => new AuraEntry(tile.NodeId, tile.Hex, AtlasHexWorldMapper.ToWorld(tile.Hex), tile.AuraCategories))
            .ToArray();
    }

    public static Color ResolveCategoryColor(AtlasModifierCategory category)
    {
        return category switch
        {
            AtlasModifierCategory.RewardBias => new Color(1.00f, 0.72f, 0.22f, 0.34f),
            AtlasModifierCategory.ThreatPressure => new Color(1.00f, 0.28f, 0.36f, 0.30f),
            AtlasModifierCategory.AffinityBoost => new Color(0.22f, 0.90f, 0.78f, 0.28f),
            _ => new Color(1f, 1f, 1f, 0.20f),
        };
    }

    public static Color ResolveOverlapColor()
    {
        return new Color(1.00f, 0.52f, 0.32f, 0.42f);
    }

    public void Render(
        AtlasScreenViewState state,
        Mesh? discMesh = null,
        Mesh? ringMesh = null,
        Func<AtlasModifierCategory, Material>? categoryMaterialResolver = null,
        Material? overlapMaterial = null)
    {
        EnsureRoot();
        ClearChildren(auraRoot);

        var shader = Shader.Find("SM/Atlas/SigilAuraRing") ?? Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Standard");
        foreach (var entry in BuildAuraPlan(state))
        {
            CreateAura(entry, shader, discMesh, ringMesh, categoryMaterialResolver, overlapMaterial);
        }
    }

    private void EnsureRoot()
    {
        if (auraRoot != null)
        {
            return;
        }

        var existing = transform.Find("AtlasSigilAuraRuntimeRoot");
        if (existing != null)
        {
            auraRoot = existing;
            return;
        }

        var go = new GameObject("AtlasSigilAuraRuntimeRoot");
        go.transform.SetParent(transform, false);
        auraRoot = go.transform;
    }

    private void CreateAura(
        AuraEntry entry,
        Shader shader,
        Mesh? discMesh,
        Mesh? ringMesh,
        Func<AtlasModifierCategory, Material>? categoryMaterialResolver,
        Material? overlapMaterial)
    {
        for (var i = 0; i < entry.Categories.Count; i++)
        {
            var go = new GameObject($"Aura_{entry.NodeId}_{entry.Categories[i]}");
            go.transform.SetParent(auraRoot, false);
            go.transform.position = entry.WorldPosition + Vector3.up * (0.055f + i * 0.006f);
            go.transform.localScale = Vector3.one * (1f + i * 0.035f);
            AssignMesh(go, ringMesh ?? AtlasHexWorldMapper.CreateHexRingMesh(AtlasHexWorldMapper.HexRadius * 1.06f, 0.050f));
            AssignMaterial(
                go,
                categoryMaterialResolver?.Invoke(entry.Categories[i]) ?? CreateAuraMaterial(shader, ResolveCategoryColor(entry.Categories[i]), 0.9f + i * 0.2f));
        }

        if (!entry.IsOverlap)
        {
            return;
        }

        var overlap = new GameObject($"Aura_{entry.NodeId}_OverlapAmberCoral");
        overlap.transform.SetParent(auraRoot, false);
        overlap.transform.position = entry.WorldPosition + Vector3.up * 0.085f;
        overlap.transform.localScale = Vector3.one * 1.10f;
        AssignMesh(overlap, ringMesh ?? AtlasHexWorldMapper.CreateHexRingMesh(AtlasHexWorldMapper.HexRadius * 1.10f, 0.045f));
        AssignMaterial(overlap, overlapMaterial ?? CreateAuraMaterial(shader, ResolveOverlapColor(), 1.35f));
    }

    private static void AssignMesh(GameObject go, Mesh mesh)
    {
        var filter = go.AddComponent<MeshFilter>();
        filter.sharedMesh = mesh;
        go.AddComponent<MeshRenderer>();
    }

    private static void AssignMaterial(GameObject go, Material material)
    {
        var renderer = go.GetComponent<MeshRenderer>();
        renderer.sharedMaterial = material;
    }

    private static Material CreateAuraMaterial(Shader shader, Color color, float pulseOffset)
    {
        var material = new Material(shader)
        {
            name = $"M_Atlas_Aura_{color.r:0.00}_{color.g:0.00}_{color.b:0.00}",
            color = color,
        };
        if (material.HasProperty("_BaseColor"))
        {
            material.SetColor("_BaseColor", color);
        }

        if (material.HasProperty("_EmissionColor"))
        {
            material.SetColor("_EmissionColor", color * 1.6f);
        }

        if (material.HasProperty("_RingColor"))
        {
            material.SetColor("_RingColor", color);
        }

        if (material.HasProperty("_PulseOffset"))
        {
            material.SetFloat("_PulseOffset", pulseOffset);
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
