using System.Collections.Generic;
using SM.Combat.Model;
using UnityEngine;

namespace SM.Unity;

public sealed class BattleP09ActorVisualAdapter : BattleActorVisualAdapter
{
    private const float GroundShadowWorldY = -0.965f;
    private const float BattleCameraToonOutlineWidth = 0.12f;
    private const float BattleCameraToonOutlineFixWidth = 0.75f;
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
        // 원거리 유닛(활/지팡이)은 P09 기본 거치(등 홀스터)를 손 거치로 전환한다 — 전투 모션이
        // 빈손으로 재생되던 "활을 등에 멘 채 공격" 수술. 외형 프리셋이 무기 메시를 켠 뒤에 호출해야 한다.
        BattleP09WeaponStanceRig.ApplyRangedHandStance(p09ModelRoot, actor);
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
        // Keep actor-cast shadows, but avoid scene-received foliage shadows muddying small P09 silhouettes.
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
        renderer.receiveShadows = false;
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
        var materialName = material.name.ToLowerInvariant();
        var materialRole = ResolveToonMaterialRole(materialName);
        var isReflectiveRole = materialRole is ToonMaterialRole.ArmorOrMetal or ToonMaterialRole.Weapon;

        SetFloatIfPresent(material, "_UseOutline", 1f);
        SetFloatIfPresent(material, "_OutlineWidth", BattleCameraToonOutlineWidth);
        SetFloatIfPresent(material, "_OutlineFixWidth", BattleCameraToonOutlineFixWidth);
        SetFloatIfPresent(material, "_OutlineEnableLighting", 0f);
        SetColorIfPresent(material, "_OutlineColor", new Color(0.050f, 0.058f, 0.075f, 1f));

        SetFloatIfPresent(material, "_UseShadow", 1f);
        SetFloatIfPresent(material, "_AsUnlit", 1f);
        SetFloatIfPresent(material, "_LightMinLimit", 1f);
        SetFloatIfPresent(material, "_lilDirectionalLightStrength", 0f);

        SetFloatIfPresent(material, "_ShadowReceive", 0f);
        SetFloatIfPresent(material, "_Shadow2ndReceive", 0f);
        SetFloatIfPresent(material, "_Shadow3rdReceive", 0f);
        SetFloatIfPresent(material, "_ShadowStrength", 0f);
        SetColorIfPresent(material, "_ShadowColor", new Color(0.86f, 0.88f, 0.92f, 1f));
        SetFloatIfPresent(material, "_ShadowBorder", 0.42f);
        SetFloatIfPresent(material, "_ShadowBlur", 0.16f);

        SetFloatIfPresent(material, "_UseRim", 1f);
        SetFloatIfPresent(material, "_RimMainStrength", 0.22f);
        SetFloatIfPresent(material, "_RimShadowMask", 0.75f);
        SetFloatIfPresent(material, "_RimFresnelPower", 3.8f);
        SetFloatIfPresent(material, "_RimBorder", 0.16f);
        SetFloatIfPresent(material, "_RimBlur", 0.18f);
        SetColorIfPresent(material, "_RimColor", new Color(0.82f, 0.86f, 0.94f, 1f));
        SetColorIfPresent(material, "_RimIndirColor", new Color(0.36f, 0.44f, 0.58f, 1f));

        SetFloatIfPresent(material, "_UseBacklight", 0f);
        SetFloatIfPresent(material, "_BacklightMainStrength", 0f);
        SetColorIfPresent(material, "_BacklightColor", new Color(0.72f, 0.78f, 0.88f, 1f));
        SetFloatIfPresent(material, "_BacklightBorder", 0.30f);
        SetFloatIfPresent(material, "_BacklightBlur", 0.12f);
        SetFloatIfPresent(material, "_BacklightViewStrength", 0.25f);
        SetFloatIfPresent(material, "_BacklightDirectivity", 2.0f);
        SetFloatIfPresent(material, "_BacklightReceiveShadow", 0f);

        SetFloatIfPresent(material, "_UseMatCap", 1f);
        SetFloatIfPresent(material, "_MatCapMainStrength", isReflectiveRole ? 0.38f : 0.16f);
        SetFloatIfPresent(material, "_MatCapShadowMask", isReflectiveRole ? 0.02f : 0.04f);
        SetMainTextureHsvgBoostIfPresent(
            material,
            ResolveTextureSaturationBoost(materialRole),
            ResolveTextureValueBoost(materialRole));
        BoostReadablePaletteIfPresent(material, materialRole, materialName);
        ApplyBattleColorFillIfPresent(material, materialRole);
    }

    private enum ToonMaterialRole
    {
        Default,
        ArmorOrMetal,
        Hair,
        Skin,
        Weapon
    }

    private static ToonMaterialRole ResolveToonMaterialRole(string materialName)
    {
        if (ContainsAny(materialName, "skin", "body", "face"))
        {
            return ToonMaterialRole.Skin;
        }

        if (ContainsAny(materialName, "hair", "beard", "facial", "moustache", "mustache"))
        {
            return ToonMaterialRole.Hair;
        }

        if (ContainsAny(materialName, "weapon", "blade", "shield", "sword", "axe", "bow", "staff", "arrow", "dagger"))
        {
            return ToonMaterialRole.Weapon;
        }

        return ContainsAny(materialName, "armor", "metal", "helm", "helmet", "plate", "chain", "leather", "cloth", "robe")
            ? ToonMaterialRole.ArmorOrMetal
            : ToonMaterialRole.Default;
    }

    private static bool ContainsAny(string value, params string[] fragments)
    {
        foreach (var fragment in fragments)
        {
            if (value.IndexOf(fragment, System.StringComparison.Ordinal) >= 0)
            {
                return true;
            }
        }

        return false;
    }

    private static void SetFloatIfPresent(Material material, string propertyName, float value)
    {
        if (material.HasProperty(propertyName))
        {
            material.SetFloat(propertyName, value);
        }
    }

    private static void SetColorIfPresent(Material material, string propertyName, Color value)
    {
        if (material.HasProperty(propertyName))
        {
            material.SetColor(propertyName, value);
        }
    }

    private static void SetMainTextureHsvgBoostIfPresent(Material material, float minimumSaturation, float minimumValue)
    {
        if (!material.HasProperty("_MainTexHSVG"))
        {
            return;
        }

        var hsvg = material.GetVector("_MainTexHSVG");
        hsvg.y = Mathf.Max(hsvg.y, minimumSaturation);
        hsvg.z = Mathf.Max(hsvg.z, minimumValue);
        material.SetVector("_MainTexHSVG", hsvg);
    }

    private static float ResolveTextureSaturationBoost(ToonMaterialRole role)
    {
        return role switch
        {
            ToonMaterialRole.Hair => 1.62f,
            ToonMaterialRole.Skin => 1.12f,
            ToonMaterialRole.Weapon => 1.42f,
            ToonMaterialRole.ArmorOrMetal => 1.54f,
            _ => 1.48f
        };
    }

    private static float ResolveTextureValueBoost(ToonMaterialRole role)
    {
        return role switch
        {
            ToonMaterialRole.Hair => 1.55f,
            ToonMaterialRole.Skin => 1.26f,
            ToonMaterialRole.Weapon => 1.42f,
            ToonMaterialRole.ArmorOrMetal => 1.50f,
            _ => 1.44f
        };
    }

    private static void BoostReadablePaletteIfPresent(Material material, ToonMaterialRole role, string materialName)
    {
        var tint = ResolveReadabilityTint(materialName, role);
        var multiplier = ResolveColorMultiplier(role);
        var minimumSaturation = ResolveColorSaturationFloor(role);
        var minimumValue = ResolveColorValueFloor(role);

        BoostColorIfPresent(material, "_Color", multiplier, tint, minimumSaturation, minimumValue);
        BoostColorIfPresent(material, "_BaseColor", multiplier, tint, minimumSaturation, minimumValue);
        BoostColorIfPresent(material, "_Color2nd", multiplier, tint, minimumSaturation, minimumValue);
        BoostColorIfPresent(material, "_Color3rd", multiplier, tint, minimumSaturation, minimumValue);
    }

    private static float ResolveColorMultiplier(ToonMaterialRole role)
    {
        return role switch
        {
            ToonMaterialRole.Hair => 1.58f,
            ToonMaterialRole.Skin => 1.18f,
            ToonMaterialRole.Weapon => 1.46f,
            ToonMaterialRole.ArmorOrMetal => 1.52f,
            _ => 1.40f
        };
    }

    private static float ResolveColorSaturationFloor(ToonMaterialRole role)
    {
        return role switch
        {
            ToonMaterialRole.Skin => 0.18f,
            ToonMaterialRole.Weapon => 0.30f,
            ToonMaterialRole.Hair => 0.40f,
            ToonMaterialRole.ArmorOrMetal => 0.36f,
            _ => 0.34f
        };
    }

    private static float ResolveColorValueFloor(ToonMaterialRole role)
    {
        return role switch
        {
            ToonMaterialRole.Skin => 0.82f,
            ToonMaterialRole.Weapon => 0.80f,
            ToonMaterialRole.Hair => 0.72f,
            ToonMaterialRole.ArmorOrMetal => 0.78f,
            _ => 0.74f
        };
    }

    private static Color ResolveReadabilityTint(string materialName, ToonMaterialRole role)
    {
        if (ContainsAny(materialName, "blue", "cyan", "teal", "prism", "rift"))
        {
            return new Color(0.88f, 1.05f, 1.32f, 1f);
        }

        if (ContainsAny(materialName, "red", "rose", "ember", "rust"))
        {
            return new Color(1.34f, 0.92f, 0.84f, 1f);
        }

        if (ContainsAny(materialName, "gold", "yellow", "amber"))
        {
            return new Color(1.30f, 1.18f, 0.76f, 1f);
        }

        if (ContainsAny(materialName, "green", "jade", "moss", "forest"))
        {
            return new Color(0.86f, 1.28f, 0.88f, 1f);
        }

        if (ContainsAny(materialName, "purple", "violet", "magenta", "lavender"))
        {
            return new Color(1.18f, 0.92f, 1.34f, 1f);
        }

        if (ContainsAny(materialName, "pink"))
        {
            return new Color(1.34f, 0.96f, 1.16f, 1f);
        }

        if (ContainsAny(materialName, "orange", "brown", "tan", "leather"))
        {
            return new Color(1.28f, 1.04f, 0.82f, 1f);
        }

        if (ContainsAny(materialName, "ivory", "white", "bone", "pale"))
        {
            return new Color(1.18f, 1.14f, 1.02f, 1f);
        }

        return role switch
        {
            ToonMaterialRole.Skin => new Color(1.20f, 1.07f, 0.94f, 1f),
            ToonMaterialRole.Hair => new Color(1.18f, 1.07f, 0.94f, 1f),
            ToonMaterialRole.Weapon => new Color(1.12f, 1.10f, 1.04f, 1f),
            ToonMaterialRole.ArmorOrMetal => new Color(1.16f, 1.10f, 1.02f, 1f),
            _ => new Color(1.16f, 1.08f, 0.98f, 1f)
        };
    }

    private static void BoostColorIfPresent(
        Material material,
        string propertyName,
        float multiplier,
        Color tint,
        float minimumSaturation,
        float minimumValue)
    {
        if (!material.HasProperty(propertyName))
        {
            return;
        }

        var color = material.GetColor(propertyName);
        var boosted = new Color(
            Mathf.Min(color.r * multiplier * tint.r, 1.65f),
            Mathf.Min(color.g * multiplier * tint.g, 1.65f),
            Mathf.Min(color.b * multiplier * tint.b, 1.65f),
            color.a);
        boosted = EnsureReadableColor(boosted, tint, minimumSaturation, minimumValue);
        material.SetColor(
            propertyName,
            boosted);
    }

    private static void ApplyBattleColorFillIfPresent(Material material, ToonMaterialRole role)
    {
        if (!material.HasProperty("_UseEmission") || !material.HasProperty("_EmissionColor"))
        {
            return;
        }

        var fillColor = ResolveBattleColorFill(material, role);
        fillColor.a = 1f;
        material.SetFloat("_UseEmission", 1f);
        material.SetColor("_EmissionColor", fillColor);
        material.EnableKeyword("_EMISSION");
        SetFloatIfPresent(material, "_EmissionMainStrength", 0.65f);
        SetFloatIfPresent(material, "_EmissionBlend", ResolveEmissionBlend(role));
        SetFloatIfPresent(material, "_EmissionFluorescence", 0f);
    }

    private static Color ResolveBattleColorFill(Material material, ToonMaterialRole role)
    {
        var primary = ReadColorIfPresent(material, "_Color", Color.white);
        var secondary = ReadColorIfPresent(material, "_Color2nd", primary);
        var tertiary = ReadColorIfPresent(material, "_Color3rd", secondary);
        var mixed = Color.Lerp(primary, Color.Lerp(secondary, tertiary, 0.35f), 0.42f);
        return EnsureReadableColor(
            mixed,
            ResolveReadabilityTint(material.name.ToLowerInvariant(), role),
            ResolveColorSaturationFloor(role),
            ResolveColorValueFloor(role));
    }

    private static float ResolveEmissionBlend(ToonMaterialRole role)
    {
        return role switch
        {
            ToonMaterialRole.Skin => 0.10f,
            ToonMaterialRole.Hair => 0.24f,
            ToonMaterialRole.Weapon => 0.28f,
            ToonMaterialRole.ArmorOrMetal => 0.24f,
            _ => 0.22f
        };
    }

    private static Color ReadColorIfPresent(Material material, string propertyName, Color fallback)
    {
        return material.HasProperty(propertyName) ? material.GetColor(propertyName) : fallback;
    }

    private static Color EnsureReadableColor(Color color, Color fallbackTint, float minimumSaturation, float minimumValue)
    {
        Color.RGBToHSV(ClampToUnitColor(color), out var hue, out var saturation, out var value);
        if (saturation < minimumSaturation)
        {
            Color.RGBToHSV(ClampToUnitColor(fallbackTint), out var tintHue, out var tintSaturation, out _);
            if (tintSaturation > 0.04f)
            {
                hue = tintHue;
            }
        }

        var readable = Color.HSVToRGB(
            hue,
            Mathf.Max(saturation, minimumSaturation),
            Mathf.Max(value, minimumValue));
        return new Color(readable.r, readable.g, readable.b, color.a);
    }

    private static Color ClampToUnitColor(Color color)
    {
        return new Color(
            Mathf.Clamp01(color.r),
            Mathf.Clamp01(color.g),
            Mathf.Clamp01(color.b),
            color.a);
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
