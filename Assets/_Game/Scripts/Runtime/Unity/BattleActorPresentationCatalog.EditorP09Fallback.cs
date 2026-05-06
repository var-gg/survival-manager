#if UNITY_EDITOR
using System;
using SM.Combat.Model;
using UnityEditor;
using UnityEngine;

namespace SM.Unity;

public sealed partial class BattleActorPresentationCatalog
{
    private const float EditorP09TargetModelHeight = 2.05f;
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

    private static readonly string[] EditorP09CanonicalCombatIds =
    {
        "warden",
        "guardian",
        "slayer",
        "raider",
        "hunter",
        "priest",
        "hexer",
        "scout",
        "bulwark",
        "reaver",
        "marksman",
        "shaman",
        "bastion_penitent",
        "mirror_cantor",
        "pale_executor",
        "rift_stalker",
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

        var wrappers = new BattleActorWrapper[EditorP09AbstractCharacterIds.Length];
        for (var i = 0; i < wrappers.Length; i++)
        {
            wrappers[i] = CreateEditorP09WrapperTemplate(i, visualPrefab);
            catalog.SetCharacterOverride(EditorP09AbstractCharacterIds[i], wrappers[i]);
            catalog.SetArchetypeOverride(EditorP09AbstractCharacterIds[i], wrappers[i]);
        }

        for (var i = 0; i < EditorP09CanonicalCombatIds.Length; i++)
        {
            var wrapper = wrappers[i % wrappers.Length];
            catalog.SetCharacterOverride(EditorP09CanonicalCombatIds[i], wrapper);
            catalog.SetArchetypeOverride(EditorP09CanonicalCombatIds[i], wrapper);
        }

        for (var i = 0; i < EditorP09HeroSmokeIds.Length; i++)
        {
            catalog.SetCharacterOverride(EditorP09HeroSmokeIds[i], wrappers[(i * 2) % wrappers.Length]);
        }

        return catalog;
    }

    private static BattleActorWrapper CreateEditorP09WrapperTemplate(int slotIndex, GameObject visualPrefab)
    {
        var root = new GameObject($"BattleActor_P09_{EditorP09AbstractCharacterIds[slotIndex]}_RuntimeTemplate");
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
        model.name = $"P09Model_{EditorP09AbstractCharacterIds[slotIndex]}";
        ApplyHideFlagsToHierarchy(model.transform, HideFlags.DontSave);
        ApplyEditorP09RoughVariant(model, slotIndex);

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
            modelScale);

        root.SetActive(false);
        return wrapper;
    }

    private static void ApplyEditorP09RoughVariant(GameObject model, int slotIndex)
    {
        var hairName = $"Hair_{slotIndex + 1:00}";
        var hairMaterial = LoadEditorP09HairMaterial(hairName, EditorP09HairColors[slotIndex % EditorP09HairColors.Length]);
        var eyeMaterial = AssetDatabase.LoadAssetAtPath<Material>(
            $"Assets/P09_Modular_Humanoid/Model_DATA/Materials/Common/Eye/P09_Eye_{EditorP09EyeColors[slotIndex % EditorP09EyeColors.Length]}.mat");

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
        bounds = default;
        var hasBounds = false;
        foreach (var renderer in root.GetComponentsInChildren<Renderer>(true))
        {
            if (!renderer.enabled || !renderer.gameObject.activeInHierarchy)
            {
                continue;
            }

            if (!hasBounds)
            {
                bounds = renderer.bounds;
                hasBounds = true;
            }
            else
            {
                bounds.Encapsulate(renderer.bounds);
            }
        }

        return hasBounds;
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
