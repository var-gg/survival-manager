using System;
using System.Collections.Generic;
using System.Linq;
using SM.Atlas.Model;
using UnityEngine;

namespace SM.Unity.Atlas;

[DisallowMultipleComponent]
public sealed class AtlasCharacterStandeePresenter : MonoBehaviour
{
    public readonly struct StandeeEntry
    {
        public StandeeEntry(string characterId, string nodeId, Vector3 worldPosition, float scale, Color accentColor)
        {
            CharacterId = characterId;
            NodeId = nodeId;
            WorldPosition = worldPosition;
            Scale = scale;
            AccentColor = accentColor;
        }

        public string CharacterId { get; }
        public string NodeId { get; }
        public Vector3 WorldPosition { get; }
        public float Scale { get; }
        public Color AccentColor { get; }
    }

    private static readonly (string CharacterId, string NodeId, Color Accent)[] DefaultPlacements =
    {
        ("hero_dawn_priest", "hex_m3_1", new Color(0.95f, 0.78f, 0.38f, 1f)),
        ("hero_pack_raider", "hex_m2_2", new Color(0.82f, 0.36f, 0.23f, 1f)),
        ("hero_echo_savant", "hex_m1_0", new Color(0.58f, 0.50f, 0.95f, 1f)),
        ("hero_grave_hexer", "hex_0_0", new Color(0.62f, 0.76f, 0.88f, 1f)),
    };

    [SerializeField] private Transform standeeRoot = null!;

    public static IReadOnlyList<StandeeEntry> BuildPlan(AtlasRegionDefinition region)
    {
        if (region == null)
        {
            throw new ArgumentNullException(nameof(region));
        }

        var nodes = region.Nodes.ToDictionary(node => node.NodeId, StringComparer.Ordinal);
        return DefaultPlacements
            .Where(placement => nodes.ContainsKey(placement.NodeId))
            .Select(placement =>
            {
                var node = nodes[placement.NodeId];
                var position = AtlasHexWorldMapper.ToWorld(node.Hex) + new Vector3(0f, 0.42f, -0.08f);
                return new StandeeEntry(placement.CharacterId, placement.NodeId, position, 0.78f, placement.Accent);
            })
            .ToArray();
    }

    public void Rebuild(
        AtlasRegionDefinition region,
        Func<StandeeEntry, Material>? characterMaterialResolver = null,
        Material? baseMaterial = null)
    {
        EnsureRoot();
        ClearChildren(standeeRoot);
        foreach (var entry in BuildPlan(region))
        {
            CreateStandee(entry, characterMaterialResolver?.Invoke(entry), baseMaterial);
        }
    }

    private void EnsureRoot()
    {
        if (standeeRoot != null)
        {
            return;
        }

        var go = new GameObject("AtlasStandeeRuntimeRoot");
        go.transform.SetParent(transform, false);
        standeeRoot = go.transform;
    }

    private void CreateStandee(StandeeEntry entry, Material? characterMaterial, Material? baseMaterial)
    {
        var root = new GameObject($"P09Standee_{entry.CharacterId}");
        root.transform.SetParent(standeeRoot, false);
        root.transform.position = entry.WorldPosition;
        root.transform.localScale = Vector3.one * entry.Scale;
        root.transform.rotation = Quaternion.Euler(0f, 145f, 0f);

        var material = characterMaterial ?? CreateMaterial(entry.AccentColor);
        CreatePrimitive(root.transform, PrimitiveType.Capsule, "P09Body", new Vector3(0f, 0.58f, 0f), new Vector3(0.24f, 0.42f, 0.24f), material);
        CreatePrimitive(root.transform, PrimitiveType.Sphere, "P09Head", new Vector3(0f, 1.10f, 0f), new Vector3(0.28f, 0.28f, 0.28f), material);
        CreatePrimitive(
            root.transform,
            PrimitiveType.Cylinder,
            "P09StandeeBase",
            new Vector3(0f, 0.04f, 0f),
            new Vector3(0.34f, 0.028f, 0.34f),
            baseMaterial ?? CreateMaterial(new Color(0.08f, 0.07f, 0.06f, 0.72f)));
    }

    private static void CreatePrimitive(Transform parent, PrimitiveType type, string name, Vector3 localPosition, Vector3 localScale, Material material)
    {
        var go = GameObject.CreatePrimitive(type);
        go.name = name;
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPosition;
        go.transform.localScale = localScale;
        var renderer = go.GetComponent<Renderer>();
        renderer.sharedMaterial = material;
        var collider = go.GetComponent<Collider>();
        if (collider != null)
        {
            if (Application.isPlaying)
            {
                Destroy(collider);
            }
            else
            {
                DestroyImmediate(collider);
            }
        }
    }

    private static Material CreateMaterial(Color color)
    {
        var shader = Shader.Find("lilToon") ?? Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
        var material = new Material(shader)
        {
            color = color,
        };
        if (material.HasProperty("_BaseColor"))
        {
            material.SetColor("_BaseColor", color);
        }

        if (material.HasProperty("_EmissionColor"))
        {
            material.SetColor("_EmissionColor", color * 0.45f);
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
