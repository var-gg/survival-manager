using System.Collections.Generic;
using SM.Combat.Model;
using UnityEngine;

namespace SM.Unity;

public sealed class BattleP09ActorVisualAdapter : BattleActorVisualAdapter
{
    private const float GroundShadowWorldY = -0.965f;
    private static readonly Vector3 GroundShadowWorldScale = new(0.58f, 0.006f, 0.58f);

    [SerializeField] private Transform visualRoot = null!;
    [SerializeField] private Transform vendorVisualSlot = null!;
    [SerializeField] private Transform p09ModelRoot = null!;
    [SerializeField] private GameObject p09VisualPrefab = null!;
    [SerializeField] private Renderer pulseProxyRenderer = null!;
    [SerializeField] private Renderer shadowRenderer = null!;
    [SerializeField] private Vector3 modelLocalPosition = Vector3.zero;
    [SerializeField] private Vector3 modelLocalEulerAngles = Vector3.zero;
    [SerializeField] private Vector3 modelLocalScale = Vector3.one;
    [SerializeField] private BattleP09AppearancePreset appearancePreset = null!;

    private readonly List<Material> _materialInstances = new();

    public override Transform? VisualRoot => visualRoot;
    public override Renderer? PrimaryRenderer => pulseProxyRenderer;
    public override Renderer? ShadowRenderer => shadowRenderer;

    public void ConfigureAuthoring(
        Transform visualRootTransform,
        Transform vendorSlot,
        Transform modelRoot,
        GameObject? visualPrefab,
        Renderer pulseProxy,
        Renderer shadow,
        Vector3 localPosition,
        Vector3 localEulerAngles,
        Vector3 localScale,
        BattleP09AppearancePreset? configuredAppearancePreset = null)
    {
        visualRoot = visualRootTransform;
        vendorVisualSlot = vendorSlot;
        p09ModelRoot = modelRoot;
        p09VisualPrefab = visualPrefab!;
        pulseProxyRenderer = pulseProxy;
        shadowRenderer = shadow;
        modelLocalPosition = localPosition;
        modelLocalEulerAngles = localEulerAngles;
        modelLocalScale = localScale;
        appearancePreset = configuredAppearancePreset!;
        ApplyModelTransform();
    }

    public override void Initialize(BattleActorWrapper wrapper, BattleUnitReadModel actor)
    {
        if (visualRoot == null)
        {
            visualRoot = wrapper.VisualRoot;
        }

        if (vendorVisualSlot == null)
        {
            vendorVisualSlot = wrapper.VendorVisualSlot;
        }

        if (p09ModelRoot == null)
        {
            p09ModelRoot = ResolveExistingModelRoot();
        }

        if (p09ModelRoot == null && p09VisualPrefab != null && vendorVisualSlot != null)
        {
            var instance = Instantiate(p09VisualPrefab, vendorVisualSlot, false);
            instance.name = p09VisualPrefab.name;
            p09ModelRoot = instance.transform;
        }

        ApplyModelTransform();
        ApplyAppearancePreset();
        EnsureReadableP09Materials();
        EnsureProxyRenderers();
        EnsureMaterial(pulseProxyRenderer, Color.clear);
        EnsureMaterial(shadowRenderer, new Color(0f, 0f, 0f, 0.28f));
    }

    public override void ApplyState(in BattleActorVisualState state)
    {
        if (visualRoot != null)
        {
            visualRoot.localPosition = state.LocalPosition;
            visualRoot.localScale = state.LocalScale;
            visualRoot.localRotation = state.LocalRotation;
        }

        if (pulseProxyRenderer != null)
        {
            BattlePresentationMaterialFactory.ApplyColor(pulseProxyRenderer.sharedMaterial, Color.clear);
        }

        if (shadowRenderer != null)
        {
            ApplyGroundShadow(state.ShadowColor);
        }
    }

    private void ApplyGroundShadow(Color shadowColor)
    {
        if (shadowRenderer == null)
        {
            return;
        }

        BattlePresentationMaterialFactory.ApplyColor(shadowRenderer.sharedMaterial, shadowColor);
        var shadow = shadowRenderer.transform;
        shadow.SetPositionAndRotation(new Vector3(transform.position.x, GroundShadowWorldY, transform.position.z), Quaternion.identity);
        shadow.localScale = DivideByParentScale(shadow, GroundShadowWorldScale);
    }

    private void OnDestroy()
    {
        foreach (var material in _materialInstances)
        {
            if (material != null)
            {
                DestroyPresentationObject(material);
            }
        }

        _materialInstances.Clear();
    }

    private Transform? ResolveExistingModelRoot()
    {
        if (vendorVisualSlot == null)
        {
            return null;
        }

        for (var i = 0; i < vendorVisualSlot.childCount; i++)
        {
            var child = vendorVisualSlot.GetChild(i);
            if (child.gameObject.activeSelf)
            {
                return child;
            }
        }

        return vendorVisualSlot.childCount > 0 ? vendorVisualSlot.GetChild(0) : null;
    }

    private void ApplyModelTransform()
    {
        if (p09ModelRoot == null)
        {
            return;
        }

        p09ModelRoot.localPosition = modelLocalPosition;
        p09ModelRoot.localRotation = Quaternion.Euler(modelLocalEulerAngles);
        p09ModelRoot.localScale = modelLocalScale;
    }

    private void ApplyAppearancePreset()
    {
        if (p09ModelRoot == null || appearancePreset == null)
        {
            return;
        }

        appearancePreset.ApplyTo(p09ModelRoot, _materialInstances);
    }

    private void EnsureReadableP09Materials()
    {
        if (p09ModelRoot == null)
        {
            return;
        }

        var materialCache = new Dictionary<Material, Material>();
        foreach (var renderer in p09ModelRoot.GetComponentsInChildren<Renderer>(true))
        {
            ConfigureRendererForBattlePresentation(renderer);

            var materials = renderer.sharedMaterials;
            var changed = false;
            for (var i = 0; i < materials.Length; i++)
            {
                var source = materials[i];
                if (!ShouldTuneToonMaterial(source))
                {
                    continue;
                }

                if (!materialCache.TryGetValue(source, out var clone))
                {
                    clone = new Material(source)
                    {
                        hideFlags = HideFlags.DontSave
                    };
                    TuneToonMaterialForBattle(clone);
                    materialCache[source] = clone;
                    _materialInstances.Add(clone);
                }

                materials[i] = clone;
                changed = true;
            }

            if (changed)
            {
                renderer.sharedMaterials = materials;
            }
        }
    }

    private static void ConfigureRendererForBattlePresentation(Renderer renderer)
    {
        // GPT-Pro 가이드: 캐릭터는 환경과 같은 key sun 그림자를 받아야 함.
        // 이전엔 Off로 lilToon outline 충돌 회피했으나 그림자가 안 보이는 부작용 큼.
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
        renderer.receiveShadows = true;
        renderer.allowOcclusionWhenDynamic = false;
    }

    private static bool ShouldTuneToonMaterial(Material? material)
    {
        return material != null
               && material.shader != null
               && material.shader.name.Contains("lilToon", System.StringComparison.Ordinal);
    }

    private static void TuneToonMaterialForBattle(Material material)
    {
        SetFloatIfPresent(material, "_UseOutline", 0f);
        SetFloatIfPresent(material, "_OutlineWidth", 0f);
        SetFloatIfPresent(material, "_OutlineFixWidth", 0f);
        SetFloatIfPresent(material, "_UseShadow", 0f);

        if (material.HasProperty("_AsUnlit"))
        {
            material.SetFloat("_AsUnlit", Mathf.Max(material.GetFloat("_AsUnlit"), 0.92f));
        }

        if (material.HasProperty("_LightMinLimit"))
        {
            material.SetFloat("_LightMinLimit", Mathf.Max(material.GetFloat("_LightMinLimit"), 0.86f));
        }

        if (material.HasProperty("_lilDirectionalLightStrength"))
        {
            material.SetFloat("_lilDirectionalLightStrength", Mathf.Min(material.GetFloat("_lilDirectionalLightStrength"), 0.28f));
        }

        SetFloatIfPresent(material, "_ShadowReceive", 0f);
        SetFloatIfPresent(material, "_Shadow2ndReceive", 0f);
        SetFloatIfPresent(material, "_Shadow3rdReceive", 0f);
        if (material.HasProperty("_ShadowStrength"))
        {
            material.SetFloat("_ShadowStrength", Mathf.Min(material.GetFloat("_ShadowStrength"), 0.35f));
        }
    }

    private static void SetFloatIfPresent(Material material, string propertyName, float value)
    {
        if (material.HasProperty(propertyName))
        {
            material.SetFloat(propertyName, value);
        }
    }

    private void EnsureProxyRenderers()
    {
        if (visualRoot == null)
        {
            return;
        }

        if (pulseProxyRenderer == null)
        {
            pulseProxyRenderer = CreatePrimitiveRenderer(
                visualRoot,
                PrimitiveType.Cube,
                "PulseProxy",
                Vector3.zero,
                Vector3.one * 0.01f,
                Color.clear,
                isVisible: false);
        }

        if (shadowRenderer == null)
        {
            shadowRenderer = CreatePrimitiveRenderer(
                visualRoot,
                PrimitiveType.Cylinder,
                "GroundShadow",
                Vector3.zero,
                GroundShadowWorldScale,
                new Color(0f, 0f, 0f, 0.28f),
                isVisible: true);
        }
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

    private static void EnsureMaterial(Renderer? renderer, Color color)
    {
        if (renderer == null || renderer.sharedMaterial != null)
        {
            return;
        }

        renderer.sharedMaterial = BattlePresentationMaterialFactory.Create(color);
    }

    private static Vector3 DivideByParentScale(Transform child, Vector3 worldScale)
    {
        var parentScale = child.parent != null ? child.parent.lossyScale : Vector3.one;
        return new Vector3(
            worldScale.x / SafeScale(parentScale.x),
            worldScale.y / SafeScale(parentScale.y),
            worldScale.z / SafeScale(parentScale.z));
    }

    private static float SafeScale(float scale)
    {
        var abs = Mathf.Abs(scale);
        return abs <= 0.0001f ? 1f : abs;
    }

    private static void DestroyPresentationObject(Object target)
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
