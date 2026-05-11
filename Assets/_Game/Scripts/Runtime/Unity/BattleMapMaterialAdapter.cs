using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace SM.Unity;

[DisallowMultipleComponent]
public sealed class BattleMapMaterialAdapter : MonoBehaviour
{
    private static readonly string[] TexturePropertySearchOrder =
    {
        "_ColorMap",
        "_BaseColor",
        "_LayerColorMap",
        "_AlbedoMap",
        "_Albedo",
        "_DiffuseMap",
        "_Diffuse",
        "_BaseMap",
        "_MainTex",
        "_LayerAlbedo",
        "_Layer0_BaseMap",
        "_Layer0BaseMap",
        "_Layer0Tex",
        "_Layer1_BaseMap",
        "_Layer1BaseMap",
        "_Layer1Tex",
    };

    [SerializeField] private bool remapTriForgeMaterials = true;
    [SerializeField] private bool forceDoubleSidedFallbacks = true;
    [SerializeField] private bool disableDynamicOcclusion = true;
    [SerializeField] private bool disableRealtimeShadows = false;

    private readonly List<Material> _materialInstances = new();

    private enum MaterialRole
    {
        Default,
        Foliage,
        Grass,
        Bark,
        Moss,
        Rock,
        Soil
    }

    private void Awake()
    {
        Apply();
    }

    public void Apply()
    {
        if (!remapTriForgeMaterials)
        {
            return;
        }

        var cache = new Dictionary<Material, Material>();
        foreach (var targetRenderer in GetComponentsInChildren<Renderer>(true))
        {
            if (disableDynamicOcclusion)
            {
                targetRenderer.allowOcclusionWhenDynamic = false;
            }

            if (disableRealtimeShadows)
            {
                targetRenderer.shadowCastingMode = ShadowCastingMode.Off;
                targetRenderer.receiveShadows = false;
            }
            else
            {
                targetRenderer.shadowCastingMode = ShadowCastingMode.On;
                targetRenderer.receiveShadows = true;
            }

            var materials = targetRenderer.sharedMaterials;
            var changed = false;
            for (var i = 0; i < materials.Length; i++)
            {
                var source = materials[i];
                if (!ShouldRemap(source))
                {
                    continue;
                }

                if (!cache.TryGetValue(source, out var replacement))
                {
                    replacement = CreateReplacement(source);
                    cache[source] = replacement;
                    _materialInstances.Add(replacement);
                }

                materials[i] = replacement;
                changed = true;
            }

            if (changed)
            {
                targetRenderer.sharedMaterials = materials;
            }
        }
    }

    private static bool ShouldRemap(Material? material)
    {
        if (material == null || material.shader == null)
        {
            return false;
        }

        if (material.shader.name.StartsWith("Quibli/", StringComparison.Ordinal))
        {
            return false;
        }

        if (material.shader.name.StartsWith("TriForge/", StringComparison.Ordinal))
        {
            return true;
        }

        var materialName = material.name.ToLowerInvariant();
        if (materialName.StartsWith("m_battlemap_wp_", StringComparison.Ordinal))
        {
            return false;
        }

        return ContainsAny(
            materialName,
            "fwof",
            "fw01",
            "forest",
            "rock",
            "stone",
            "tree",
            "bush",
            "grass",
            "fern",
            "moss",
            "mushroom",
            "ruin",
            "log",
            "branch");
    }

    private Material CreateReplacement(Material source)
    {
        var role = ResolveRole(source);
        var material = CreateMaterial(role);
        material.name = $"BattleMap_{source.name}";

        if (TryFindTexture(source, out var texture))
        {
            ApplyTexture(material, texture);
        }

        ApplyRoleColor(material, ResolveFallbackColor(source, role), role);
        ConfigureLitFallback(material, role);
        return material;
    }

    private static Material CreateMaterial(MaterialRole role)
    {
        return new Material(ResolveShader(role));
    }

    private void ConfigureLitFallback(Material material, MaterialRole role)
    {
        if (material.shader != null && material.shader.name == "Quibli/Grass")
        {
            ConfigureQuibliGrassFallback(material);
            return;
        }

        if (material.shader != null && material.shader.name == "Quibli/Stylized Lit")
        {
            ConfigureQuibliFallback(material, role);
            return;
        }

        if (forceDoubleSidedFallbacks && material.HasProperty("_Cull"))
        {
            material.SetFloat("_Cull", role == MaterialRole.Foliage ? (float)CullMode.Off : (float)CullMode.Back);
        }

        if (material.HasProperty("_Metallic"))
        {
            material.SetFloat("_Metallic", 0f);
        }

        if (material.HasProperty("_Smoothness"))
        {
            material.SetFloat("_Smoothness", 0.22f);
        }

        if (role == MaterialRole.Foliage)
        {
            SetFloat(material, "_AlphaClip", 1f);
            SetFloat(material, "_Cutoff", 0.35f);
            material.EnableKeyword("_ALPHATEST_ON");
        }
    }

    private void ConfigureQuibliFallback(Material material, MaterialRole role)
    {
        var profile = ResolveProfile(role);
        SetKeyword(material, "DR_RIM_ON", profile.RimEnabled);
        SetKeyword(material, "DR_SPECULAR_ON", false);
        SetKeyword(material, "DR_LIGHT_ATTENUATION", true);
        SetKeyword(material, "DR_GRADIENT_ON", false);
        SetFloat(material, "_GradientEnabled", 0f);
        SetFloat(material, "_TextureImpact", profile.TextureImpact);
        SetFloat(material, "_LightContribution", profile.LightContribution);
        SetFloat(material, "_SelfShadingSize", profile.SelfShadingSize);
        SetFloat(material, "_ReceiveShadows", 1f);

        var enableSpecular = role is MaterialRole.Rock or MaterialRole.Bark;
        SetFloat(material, "_SpecularEnabled", enableSpecular ? 1f : 0f);
        if (enableSpecular)
        {
            SetKeyword(material, "DR_SPECULAR_ON", true);
            SetFloat(material, "_FlatSpecularSize", role == MaterialRole.Rock ? 0.14f : 0.10f);
            SetFloat(material, "_FlatSpecularEdgeSmoothness", 0.20f);
            SetColor(material, "_FlatSpecularColor", new Color(1f, 0.92f, 0.78f, 1f));
            SetFloat(material, "_Glossiness", 0.20f);
        }
        SetFloat(material, "_RimEnabled", profile.RimEnabled ? 1f : 0f);
        SetFloat(material, "_FlatRimSize", profile.RimSize);
        SetFloat(material, "_FlatRimEdgeSmoothness", 0.22f);
        SetFloat(material, "_FlatRimLightAlign", 1f);
        SetFloat(material, "_OverrideLightAttenuation", 1f);
        SetVector(material, "_LightAttenuation", new Vector4(0.08f, 0.92f, 0f, 0f));
        SetColor(material, "_ColorGradient", Color.Lerp(profile.ShadowColor, ReadBaseColor(material), 0.45f));
        SetColor(material, "_ShadowColor", profile.ShadowColor);
        SetColor(material, "_FlatRimColor", profile.RimColor);
        ApplyQuibliUnityShadows(material, profile);

        if (role is MaterialRole.Foliage or MaterialRole.Grass)
        {
            var cutoff = role == MaterialRole.Foliage ? 0.48f : 0.42f;
            SetFloat(material, "_Surface", 0f);
            SetFloat(material, "_AlphaClip", 1f);
            SetFloat(material, "_AlphaCutoff", cutoff);
            SetFloat(material, "_Cutoff", cutoff);
            material.SetOverrideTag("RenderType", "TransparentCutout");
            material.renderQueue = (int)RenderQueue.AlphaTest;
            material.EnableKeyword("_ALPHATEST_ON");
        }
        else
        {
            SetFloat(material, "_AlphaClip", 0f);
            material.DisableKeyword("_ALPHATEST_ON");
        }

        if (forceDoubleSidedFallbacks)
        {
            SetFloat(material, "_Cull", role == MaterialRole.Foliage ? (float)CullMode.Off : (float)CullMode.Back);
        }
    }

    private static void ConfigureQuibliGrassFallback(Material material)
    {
        material.SetOverrideTag("RenderType", "TransparentCutout");
        material.renderQueue = (int)RenderQueue.AlphaTest;
        material.EnableKeyword("_ALPHATEST_ON");

        SetFloat(material, "_AlphaClipThreshold", 0.50f);
        SetFloat(material, "Alpha_Clip_Threshold", 0.38f);
        SetFloat(material, "_ShadowStrength", 0.16f);
        SetFloat(material, "_UnityShadowMode", 1f);
        SetFloat(material, "_UnityShadowPower", 0.42f);
        SetFloat(material, "_UnityShadowSharpness", 7f);
        SetFloat(material, "_WindEnabled", 0f);
        SetFloat(material, "_GustEnabled", 0f);
        SetColor(material, "_ColorBottom", new Color(0.08f, 0.24f, 0.055f, 1f));
        SetColor(material, "_ColorTop", new Color(0.55f, 0.70f, 0.18f, 1f));
        SetColor(material, "_SecondaryColor", new Color(0.88f, 0.56f, 0.13f, 1f));
        SetColor(material, "_PatchesColor", new Color(0.84f, 0.68f, 0.16f, 1f));
    }

    private static MaterialRole ResolveRole(Material source)
    {
        var key = $"{source.name}|{source.shader.name}".ToLowerInvariant();
        if (ContainsAny(key, "grass", "fern", "plant", "flower", "clover"))
        {
            return MaterialRole.Grass;
        }

        if (ContainsAny(key, "leaf", "leaves", "foliage", "bush", "blossom"))
        {
            return MaterialRole.Foliage;
        }

        if (ContainsAny(key, "bark", "trunk", "tree_b", "wood", "log", "branch", "debris", "sign"))
        {
            return MaterialRole.Bark;
        }

        if (ContainsAny(key, "rock", "stone", "ruin", "rubble", "arch", "wall", "column"))
        {
            return MaterialRole.Rock;
        }

        if (ContainsAny(key, "moss"))
        {
            return MaterialRole.Moss;
        }

        if (ContainsAny(key, "dust", "dirt", "soil", "ground", "terrain", "road"))
        {
            return MaterialRole.Soil;
        }

        return MaterialRole.Default;
    }

    private static Color ResolveFallbackColor(Material source, MaterialRole role)
    {
        var key = $"{source.name}|{source.shader.name}".ToLowerInvariant();
        if (ContainsAny(key, "flower", "blossom"))
        {
            return new Color(0.70f, 0.42f, 0.62f, 1f);
        }

        return role switch
        {
            MaterialRole.Foliage => new Color(0.34f, 0.52f, 0.12f, 1f),
            MaterialRole.Grass => new Color(0.46f, 0.64f, 0.14f, 1f),
            MaterialRole.Bark => new Color(0.56f, 0.30f, 0.11f, 1f),
            MaterialRole.Moss => new Color(0.34f, 0.48f, 0.14f, 1f),
            MaterialRole.Rock => new Color(0.38f, 0.32f, 0.23f, 1f),
            MaterialRole.Soil => new Color(0.70f, 0.43f, 0.18f, 1f),
            _ => new Color(0.48f, 0.36f, 0.20f, 1f)
        };
    }

    private static QuibliProfile ResolveProfile(MaterialRole role)
    {
        return role switch
        {
            MaterialRole.Foliage => new QuibliProfile(
                textureImpact: 0.42f,
                lightContribution: 0.72f,
                selfShadingSize: 0.24f,
                shadowColor: new Color(0.07f, 0.16f, 0.045f, 1f),
                unityShadowPower: 0.50f,
                unityShadowSharpness: 6f,
                shadowEdgeSize: 0.55f,
                rimColor: new Color(1.00f, 0.80f, 0.42f, 1f),
                rimSize: 0.22f,
                rimEnabled: true),
            MaterialRole.Grass => new QuibliProfile(
                textureImpact: 0.40f,
                lightContribution: 0.75f,
                selfShadingSize: 0.20f,
                shadowColor: new Color(0.06f, 0.14f, 0.035f, 1f),
                unityShadowPower: 0.48f,
                unityShadowSharpness: 6f,
                shadowEdgeSize: 0.58f,
                rimColor: new Color(1.00f, 0.84f, 0.46f, 1f),
                rimSize: 0.20f,
                rimEnabled: true),
            MaterialRole.Bark => new QuibliProfile(
                textureImpact: 0.42f,
                lightContribution: 0.70f,
                selfShadingSize: 0.26f,
                shadowColor: new Color(0.15f, 0.075f, 0.025f, 1f),
                unityShadowPower: 0.58f,
                unityShadowSharpness: 7f,
                shadowEdgeSize: 0.48f,
                rimColor: new Color(1.00f, 0.74f, 0.38f, 1f),
                rimSize: 0.24f,
                rimEnabled: true),
            MaterialRole.Moss => new QuibliProfile(
                textureImpact: 0.42f,
                lightContribution: 0.70f,
                selfShadingSize: 0.24f,
                shadowColor: new Color(0.06f, 0.14f, 0.035f, 1f),
                unityShadowPower: 0.50f,
                unityShadowSharpness: 6f,
                shadowEdgeSize: 0.52f,
                rimColor: new Color(0.98f, 0.80f, 0.42f, 1f),
                rimSize: 0.20f,
                rimEnabled: true),
            MaterialRole.Rock => new QuibliProfile(
                textureImpact: 0.40f,
                lightContribution: 0.68f,
                selfShadingSize: 0.28f,
                shadowColor: new Color(0.14f, 0.115f, 0.085f, 1f),
                unityShadowPower: 0.60f,
                unityShadowSharpness: 7f,
                shadowEdgeSize: 0.46f,
                rimColor: new Color(1.00f, 0.78f, 0.42f, 1f),
                rimSize: 0.24f,
                rimEnabled: true),
            MaterialRole.Soil => new QuibliProfile(
                textureImpact: 0.38f,
                lightContribution: 0.78f,
                selfShadingSize: 0.20f,
                shadowColor: new Color(0.22f, 0.10f, 0.035f, 1f),
                unityShadowPower: 0.55f,
                unityShadowSharpness: 7f,
                shadowEdgeSize: 0.52f,
                rimColor: Color.clear,
                rimSize: 0f,
                rimEnabled: false),
            _ => new QuibliProfile(
                textureImpact: 0.40f,
                lightContribution: 0.72f,
                selfShadingSize: 0.24f,
                shadowColor: new Color(0.13f, 0.09f, 0.05f, 1f),
                unityShadowPower: 0.52f,
                unityShadowSharpness: 6f,
                shadowEdgeSize: 0.52f,
                rimColor: new Color(1.00f, 0.78f, 0.42f, 1f),
                rimSize: 0.22f,
                rimEnabled: true)
        };
    }

    private static bool ContainsAny(string value, params string[] fragments)
    {
        foreach (var fragment in fragments)
        {
            if (value.IndexOf(fragment, StringComparison.Ordinal) >= 0)
            {
                return true;
            }
        }

        return false;
    }

    private static bool TryFindTexture(Material source, out Texture texture)
    {
        var texturePropertyNames = source.GetTexturePropertyNames();
        foreach (var propertyName in TexturePropertySearchOrder)
        {
            if (System.Array.IndexOf(texturePropertyNames, propertyName) < 0)
            {
                continue;
            }

            var candidate = source.GetTexture(propertyName);
            if (candidate != null)
            {
                texture = candidate;
                return true;
            }
        }

        texture = null!;
        return false;
    }

    private static void ApplyTexture(Material material, Texture texture)
    {
        if (material.HasProperty("_BaseMap"))
        {
            material.SetTexture("_BaseMap", texture);
        }

        if (material.HasProperty("_MainTex"))
        {
            material.SetTexture("_MainTex", texture);
        }

        if (material.HasProperty("Shape_Texture"))
        {
            material.SetTexture("Shape_Texture", texture);
        }

        if (material.HasProperty("Fill_Texture"))
        {
            material.SetTexture("Fill_Texture", texture);
        }
    }

    private static void ApplyRoleColor(Material material, Color color, MaterialRole role)
    {
        if (material.shader != null && material.shader.name == "Quibli/Grass")
        {
            SetColor(material, "_ColorTop", new Color(0.56f, 0.72f, 0.22f, 1f));
            SetColor(material, "_ColorBottom", new Color(0.08f, 0.24f, 0.06f, 1f));
            return;
        }

        if (material.shader != null && material.shader.name == "Quibli/Stylized Lit")
        {
            SetColor(material, "_BaseColor", color);
            SetColor(material, "_Color", color);
            return;
        }

        SetColor(material, "_BaseColor", color);
        SetColor(material, "_Color", color);
    }

    private static void SetColor(Material material, string propertyName, Color value)
    {
        if (material.HasProperty(propertyName))
        {
            material.SetColor(propertyName, value);
        }
    }

    private static Color ReadBaseColor(Material material)
    {
        return material.HasProperty("_BaseColor") ? material.GetColor("_BaseColor") : Color.white;
    }

    private static Shader ResolveShader(MaterialRole role)
    {
        if (role == MaterialRole.Grass)
        {
            return Shader.Find("Quibli/Grass")
                ?? Shader.Find("Quibli/Stylized Lit")
                ?? ResolveFallbackShader();
        }

        return Shader.Find("Quibli/Stylized Lit")
            ?? ResolveFallbackShader();
    }

    private static Shader ResolveFallbackShader()
    {
        return Shader.Find("Universal Render Pipeline/Lit")
            ?? Shader.Find("Universal Render Pipeline/Unlit")
            ?? Shader.Find("Standard")
            ?? Shader.Find("Unlit/Color")
            ?? Shader.Find("Sprites/Default")
            ?? throw new InvalidOperationException("No runtime shader could be resolved for battle map material fallback.");
    }

    private static void SetFloat(Material material, string propertyName, float value)
    {
        if (material.HasProperty(propertyName))
        {
            material.SetFloat(propertyName, value);
        }
    }

    private static void SetVector(Material material, string propertyName, Vector4 value)
    {
        if (material.HasProperty(propertyName))
        {
            material.SetVector(propertyName, value);
        }
    }

    private static void SetKeyword(Material material, string keyword, bool enabled)
    {
        if (enabled)
        {
            material.EnableKeyword(keyword);
            return;
        }

        material.DisableKeyword(keyword);
    }

    private static void ApplyQuibliUnityShadows(Material material, QuibliProfile profile)
    {
        SetFloat(material, "_UnityShadowMode", 1f);
        SetFloat(material, "_UnityShadowOcclusion", 0f);
        SetFloat(material, "_UnityShadowPower", profile.UnityShadowPower);
        SetFloat(material, "_UnityShadowSharpness", profile.UnityShadowSharpness);
        SetFloat(material, "_ShadowEdgeSize", profile.ShadowEdgeSize);
        SetFloat(material, "_ShadowEdgeSizeExtra", 0.045f);
    }

    private readonly struct QuibliProfile
    {
        public QuibliProfile(
            float textureImpact,
            float lightContribution,
            float selfShadingSize,
            Color shadowColor,
            float unityShadowPower,
            float unityShadowSharpness,
            float shadowEdgeSize,
            Color rimColor,
            float rimSize,
            bool rimEnabled)
        {
            TextureImpact = textureImpact;
            LightContribution = lightContribution;
            SelfShadingSize = selfShadingSize;
            ShadowColor = shadowColor;
            UnityShadowPower = unityShadowPower;
            UnityShadowSharpness = unityShadowSharpness;
            ShadowEdgeSize = shadowEdgeSize;
            RimColor = rimColor;
            RimSize = rimSize;
            RimEnabled = rimEnabled;
        }

        public float TextureImpact { get; }
        public float LightContribution { get; }
        public float SelfShadingSize { get; }
        public Color ShadowColor { get; }
        public float UnityShadowPower { get; }
        public float UnityShadowSharpness { get; }
        public float ShadowEdgeSize { get; }
        public Color RimColor { get; }
        public float RimSize { get; }
        public bool RimEnabled { get; }
    }

    private void OnDestroy()
    {
        foreach (var material in _materialInstances)
        {
            if (material == null)
            {
                continue;
            }

            if (Application.isPlaying)
            {
                Destroy(material);
            }
            else
            {
                DestroyImmediate(material);
            }
        }

        _materialInstances.Clear();
    }
}
