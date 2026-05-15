#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using SM.Combat.Model;
using UnityEditor;
using UnityEngine;

namespace SM.Unity;

public sealed partial class BattleActorPresentationCatalog
{
    private const float EditorP09TargetModelHeight = 2.05f;
    private const int EditorP09RoughHairVariantCount = 10;
    private const string EditorP09VisualPrefabPath = "Assets/P09_Modular_Humanoid/Scenes/DemoScene_Data/Demo_Prefab/P09_Human_Combat_Demo Variant.prefab";

    private static readonly string[] EditorP09AbstractCharacterIds =
    {
        "chr_0001",
        "chr_0002",
        "chr_0003",
        "chr_0004",
        "chr_0005",
        "chr_0006",
        "chr_0007",
        "chr_0008",
    };

    private static readonly string[] EditorP09HeroSmokeIds =
    {
        "hero-1",
        "hero-3",
        "hero-5",
        "hero-7",
    };

    private static readonly string[] EditorP09HairColors =
    {
        "Red",
        "Blue",
        "Gold",
        "Green",
        "Purple",
        "Pink",
        "Ivory",
        "Brown",
    };

    private static readonly string[] EditorP09EyeColors =
    {
        "Bright",
        "Cyan",
        "Yellow",
        "Purple",
        "Red",
        "Cyan",
        "Bright",
        "Yellow",
    };

    private static BattleActorPresentationCatalog? TryCreateEditorP09FallbackCatalog()
    {
        var visualPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(EditorP09VisualPrefabPath);
        if (visualPrefab == null)
        {
            return null;
        }

        var catalog = CreateInstance<BattleActorPresentationCatalog>();
        catalog.hideFlags = HideFlags.DontSave;

        var primitive = CreateRuntimePrimitiveWrapperTemplate();
        catalog.SetDefaultWrapper(primitive);
        catalog.SetTeamDefaultWrapper(TeamSide.Ally, primitive);
        catalog.SetTeamDefaultWrapper(TeamSide.Enemy, primitive);

        var appearancePresets = Resources
            .LoadAll<BattleP09AppearancePreset>(BattleP09AppearancePreset.ResourcesFolder)
            .Where(preset => preset != null && !string.IsNullOrWhiteSpace(preset.CharacterId))
            .ToDictionary(preset => preset.CharacterId, StringComparer.Ordinal);
        var wrappers = new Dictionary<string, BattleActorWrapper>(StringComparer.Ordinal);

        BattleActorWrapper ResolveWrapper(string characterId, int slotIndex)
        {
            if (wrappers.TryGetValue(characterId, out var existing))
            {
                return existing;
            }

            appearancePresets.TryGetValue(characterId, out var preset);
            var wrapper = CreateEditorP09WrapperTemplate(characterId, slotIndex, visualPrefab, preset);
            wrappers[characterId] = wrapper;
            return wrapper;
        }

        for (var i = 0; i < EditorP09AbstractCharacterIds.Length; i++)
        {
            var characterId = EditorP09AbstractCharacterIds[i];
            var wrapper = ResolveWrapper(characterId, i);
            catalog.SetCharacterOverride(characterId, wrapper);
            catalog.SetArchetypeOverride(characterId, wrapper);
        }

        for (var i = 0; i < BattleP09AppearanceRoster.CanonicalCharacterIds.Count; i++)
        {
            var characterId = BattleP09AppearanceRoster.CanonicalCharacterIds[i];
            var wrapper = ResolveWrapper(characterId, i);
            catalog.SetCharacterOverride(characterId, wrapper);
            catalog.SetArchetypeOverride(characterId, wrapper);
        }

        for (var i = 0; i < EditorP09HeroSmokeIds.Length; i++)
        {
            var characterId = EditorP09HeroSmokeIds[i];
            catalog.SetCharacterOverride(characterId, ResolveWrapper(characterId, i * 2));
        }

        return catalog;
    }

    private static BattleActorWrapper CreateEditorP09WrapperTemplate(
        string characterId,
        int slotIndex,
        GameObject visualPrefab,
        BattleP09AppearancePreset? appearancePreset)
    {
        var root = new GameObject($"BattleActor_P09_{characterId}_RuntimeTemplate");
        root.hideFlags = HideFlags.DontSave;

        var wrapper = root.AddComponent<BattleActorWrapper>();
        root.AddComponent<BattleActorView>();
        var adapter = root.AddComponent<BattleP09ActorVisualAdapter>();
        root.AddComponent<BattleAnimationEventBridge>();
        root.AddComponent<BattleHumanoidAnimationDriver>();
        root.AddComponent<BattleActorVfxSurface>();
        root.AddComponent<BattleActorAudioSurface>();

        var socketRig = CreateChild(root.transform, "SocketRig");
        var center = CreateChild(socketRig, "Center");
        center.localPosition = new Vector3(0f, 0.10f, 0f);
        var head = CreateChild(socketRig, "Head");
        head.localPosition = new Vector3(0f, 2.0f, 0f);
        var hud = CreateChild(socketRig, "Hud");
        hud.localPosition = new Vector3(0f, 2.25f, 0f);
        var hit = CreateChild(socketRig, "Hit");
        hit.localPosition = new Vector3(0f, 1.05f, 0.28f);
        var feet = CreateChild(socketRig, "Feet");
        feet.localPosition = new Vector3(0f, GroundPlaneY, 0f);
        var telegraph = CreateChild(socketRig, "Telegraph");
        telegraph.localPosition = feet.localPosition;
        var cameraFocus = CreateChild(socketRig, "CameraFocus");
        cameraFocus.localPosition = new Vector3(0f, 1.2f, 0f);

        var visualRoot = CreateChild(root.transform, "VisualRoot");
        var vendorVisualSlot = CreateChild(visualRoot, "VendorVisualSlot");
        var cast = CreateChild(visualRoot, "Cast");
        cast.localPosition = new Vector3(0f, 0.22f, 0.72f);
        var projectileOrigin = CreateChild(visualRoot, "ProjectileOrigin");
        projectileOrigin.localPosition = cast.localPosition;

        var shadowRenderer = CreatePrimitiveRenderer(
            visualRoot,
            PrimitiveType.Cylinder,
            "GroundShadow",
            new Vector3(0f, -1.02f, 0f),
            new Vector3(0.58f, 0.03f, 0.58f),
            new Color(0f, 0f, 0f, 0.28f),
            isVisible: true);
        var pulseProxyRenderer = CreatePrimitiveRenderer(
            visualRoot,
            PrimitiveType.Cube,
            "PulseProxy",
            Vector3.zero,
            Vector3.one * 0.01f,
            Color.clear,
            isVisible: false);

        var model = Instantiate(visualPrefab, vendorVisualSlot, false);
        model.name = $"P09Model_{characterId}";
        ApplyHideFlagsToHierarchy(model.transform, HideFlags.DontSave);
        if (appearancePreset != null)
        {
            var generatedMaterials = new List<Material>();
            appearancePreset.ApplyTo(model.transform, generatedMaterials);
        }
        else
        {
            ApplyEditorP09RoughVariant(model, slotIndex);
        }

        var modelScale = Vector3.one;
        var modelPosition = Vector3.zero;
        if (TryCalculateActiveRendererBounds(model, out var bounds) && bounds.size.y > 0.01f)
        {
            var uniformScale = Mathf.Clamp(EditorP09TargetModelHeight / bounds.size.y, 0.25f, 2.5f);
            modelScale = Vector3.one * uniformScale;
            modelPosition = new Vector3(0f, GroundPlaneY - bounds.min.y * uniformScale, 0f);
        }

        model.transform.localPosition = modelPosition;
        model.transform.localRotation = Quaternion.identity;
        model.transform.localScale = modelScale;

        wrapper.ConfigureAuthoring(
            visualRoot,
            vendorVisualSlot,
            center,
            head,
            hud,
            hit,
            feet,
            telegraph,
            cast,
            projectileOrigin,
            cameraFocus);
        adapter.ConfigureAuthoring(
            visualRoot,
            vendorVisualSlot,
            model.transform,
            visualPrefab,
            pulseProxyRenderer,
            shadowRenderer,
            modelPosition,
            Vector3.zero,
            modelScale,
            appearancePreset);

        root.SetActive(false);
        return wrapper;
    }

    private static void ApplyEditorP09RoughVariant(GameObject model, int slotIndex)
    {
        var normalizedSlotIndex = Math.Max(0, slotIndex);
        var hairName = ResolveEditorP09RoughHairName(normalizedSlotIndex);
        var hairMaterial = LoadEditorP09HairMaterial(hairName, EditorP09HairColors[normalizedSlotIndex % EditorP09HairColors.Length]);
        var eyeMaterial = AssetDatabase.LoadAssetAtPath<Material>(
            $"Assets/P09_Modular_Humanoid/Model_DATA/Materials/Common/Eye/P09_Eye_{EditorP09EyeColors[normalizedSlotIndex % EditorP09EyeColors.Length]}.mat");

        foreach (var transform in model.GetComponentsInChildren<Transform>(true))
        {
            if (IsEditorP09HairGroupName(transform.name))
            {
                transform.gameObject.SetActive(string.Equals(transform.name, hairName, StringComparison.Ordinal));
            }
        }

        if (hairMaterial != null)
        {
            ApplyEditorP09MaterialByPrefix(model, "P09_Hair", hairMaterial);
        }

        if (eyeMaterial != null)
        {
            ApplyEditorP09MaterialByPrefix(model, "P09_Eye", eyeMaterial);
        }
    }

    internal static string ResolveEditorP09RoughHairName(int slotIndex)
    {
        var normalizedSlotIndex = Math.Max(0, slotIndex);
        var hairIndex = normalizedSlotIndex % EditorP09RoughHairVariantCount + 1;
        return $"Hair_{hairIndex:00}";
    }

    private static bool IsEditorP09HairGroupName(string name)
    {
        if (!name.StartsWith("Hair_", StringComparison.Ordinal) || name.Length != 7)
        {
            return false;
        }

        return char.IsDigit(name[5]) && char.IsDigit(name[6]);
    }

    private static Material? LoadEditorP09HairMaterial(string hairName, string colorName)
    {
        var folder = string.Equals(hairName, "Hair_10", StringComparison.Ordinal) ? "Hair_10" : "Hair";
        var prefix = string.Equals(hairName, "Hair_10", StringComparison.Ordinal) ? "P09_Hair_10" : "P09_Hair";
        return AssetDatabase.LoadAssetAtPath<Material>(
            $"Assets/P09_Modular_Humanoid/Model_DATA/Materials/Common/{folder}/{prefix}_{colorName}.mat");
    }

    private static void ApplyEditorP09MaterialByPrefix(GameObject model, string materialPrefix, Material replacement)
    {
        foreach (var renderer in model.GetComponentsInChildren<Renderer>(true))
        {
            var materials = renderer.sharedMaterials;
            var changed = false;
            for (var i = 0; i < materials.Length; i++)
            {
                var material = materials[i];
                if (material != null && material.name.StartsWith(materialPrefix, StringComparison.Ordinal))
                {
                    materials[i] = replacement;
                    changed = true;
                }
            }

            if (changed)
            {
                renderer.sharedMaterials = materials;
            }
        }
    }

    private static bool TryCalculateActiveRendererBounds(GameObject root, out Bounds bounds)
    {
        return BattleP09VisualBounds.TryCalculateStableHumanoidBounds(root.transform, out bounds);
    }

    private static Renderer CreatePrimitiveRenderer(
        Transform parent,
        PrimitiveType primitiveType,
        string name,
        Vector3 localPosition,
        Vector3 localScale,
        Color color,
        bool isVisible)
    {
        var go = GameObject.CreatePrimitive(primitiveType);
        go.name = name;
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPosition;
        go.transform.localScale = localScale;
        go.hideFlags = HideFlags.DontSave;

        var collider = go.GetComponent<Collider>();
        if (collider != null)
        {
            DestroyPresentationObject(collider);
        }

        var renderer = go.GetComponent<Renderer>();
        renderer.sharedMaterial = BattlePresentationMaterialFactory.Create(color);
        renderer.enabled = isVisible;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        return renderer;
    }

    private static void ApplyHideFlagsToHierarchy(Transform root, HideFlags hideFlags)
    {
        foreach (var transform in root.GetComponentsInChildren<Transform>(true))
        {
            transform.gameObject.hideFlags = hideFlags;
        }
    }

    private static void DestroyPresentationObject(UnityEngine.Object target)
    {
        if (Application.isPlaying)
        {
            Destroy(target);
        }
        else
        {
            DestroyImmediate(target);
        }
    }
}
#endif
