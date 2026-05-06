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
        "_BaseMap",
        "_MainTex",
        "_ColorMap",
        "_LayerColorMap",
        "_AlbedoMap",
        "_Albedo",
        "_DiffuseMap",
        "_Diffuse",
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
    [SerializeField] private bool disableRealtimeShadows = true;

    private readonly List<Material> _materialInstances = new();

    private enum MaterialRole
    {
        Default,
        Foliage,
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
        return material != null
            && material.shader != null
            && material.shader.name.StartsWith("TriForge/", StringComparison.Ordinal);
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

        BattlePresentationMaterialFactory.ApplyColor(material, ResolveFallbackColor(source, role));
        ConfigureLitFallback(material, role);
        return material;
    }

    private static Material CreateMaterial(MaterialRole role)
    {
        return new Material(ResolveShader());
    }

    private void ConfigureLitFallback(Material material, MaterialRole role)
    {
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
        SetKeyword(material, "DR_RIM_ON", false);
        SetKeyword(material, "DR_SPECULAR_ON", false);
        SetKeyword(material, "DR_LIGHT_ATTENUATION", false);
        SetKeyword(material, "DR_GRADIENT_ON", false);
        SetFloat(material, "_TextureImpact", profile.TextureImpact);
        SetFloat(material, "_LightContribution", profile.LightContribution);
        SetFloat(material, "_SelfShadingSize", profile.SelfShadingSize);
        SetFloat(material, "_ReceiveShadows", 0f);
        SetFloat(material, "_SpecularEnabled", 0f);
        SetFloat(material, "_RimEnabled", profile.RimEnabled ? 1f : 0f);
        SetFloat(material, "_FlatRimSize", profile.RimSize);
        SetFloat(material, "_FlatRimEdgeSmoothness", 0.55f);
        SetFloat(material, "_OverrideLightAttenuation", 0f);
        SetColor(material, "_ShadowColor", profile.ShadowColor);
        SetColor(material, "_FlatRimColor", profile.RimColor);

        if (forceDoubleSidedFallbacks)
        {
            SetFloat(material, "_Cull", role == MaterialRole.Foliage ? (float)CullMode.Off : (float)CullMode.Back);
        }
    }

    private static MaterialRole ResolveRole(Material source)
    {
        var key = $"{source.name}|{source.shader.name}".ToLowerInvariant();
        if (ContainsAny(key, "leaf", "leaves", "grass", "fern", "foliage", "plant", "bush", "flower", "blossom"))
        {
            return MaterialRole.Foliage;
        }

        if (ContainsAny(key, "moss"))
        {
            return MaterialRole.Moss;
        }

        if (ContainsAny(key, "bark", "trunk", "tree_b", "wood", "log", "branch", "debris", "sign"))
        {
            return MaterialRole.Bark;
        }

        if (ContainsAny(key, "rock", "stone", "ruin", "rubble", "arch", "wall", "column"))
        {
            return MaterialRole.Rock;
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
            MaterialRole.Foliage => new Color(1.10f, 1.18f, 0.88f, 1f),
            MaterialRole.Bark => new Color(1.02f, 0.86f, 0.62f, 1f),
            MaterialRole.Moss => new Color(0.90f, 1.05f, 0.72f, 1f),
            MaterialRole.Rock => new Color(0.98f, 0.92f, 0.78f, 1f),
            MaterialRole.Soil => new Color(1.02f, 0.78f, 0.52f, 1f),
            _ => new Color(1.00f, 0.94f, 0.80f, 1f)
        };
    }

    private static QuibliProfile ResolveProfile(MaterialRole role)
    {
        return role switch
        {
            MaterialRole.Foliage => new QuibliProfile(
                textureImpact: 0.76f,
                lightContribution: 0.82f,
                selfShadingSize: 0.08f,
                shadowColor: new Color(0.22f, 0.36f, 0.14f, 1f),
                rimColor: Color.clear,
                rimSize: 0f,
                rimEnabled: false),
            MaterialRole.Bark => new QuibliProfile(
                textureImpact: 0.72f,
                lightContribution: 0.74f,
                selfShadingSize: 0.14f,
                shadowColor: new Color(0.28f, 0.20f, 0.12f, 1f),
                rimColor: Color.clear,
                rimSize: 0f,
                rimEnabled: false),
            MaterialRole.Moss => new QuibliProfile(
                textureImpact: 0.74f,
                lightContribution: 0.76f,
                selfShadingSize: 0.12f,
                shadowColor: new Color(0.20f, 0.34f, 0.14f, 1f),
                rimColor: Color.clear,
                rimSize: 0f,
                rimEnabled: false),
            MaterialRole.Rock => new QuibliProfile(
                textureImpact: 0.70f,
                lightContribution: 0.70f,
                selfShadingSize: 0.14f,
                shadowColor: new Color(0.34f, 0.32f, 0.26f, 1f),
                rimColor: Color.clear,
                rimSize: 0f,
                rimEnabled: false),
            MaterialRole.Soil => new QuibliProfile(
                textureImpact: 0.70f,
                lightContribution: 0.72f,
                selfShadingSize: 0.14f,
                shadowColor: new Color(0.34f, 0.22f, 0.12f, 1f),
                rimColor: Color.clear,
                rimSize: 0f,
                rimEnabled: false),
            _ => new QuibliProfile(
                textureImpact: 0.72f,
                lightContribution: 0.72f,
                selfShadingSize: 0.14f,
                shadowColor: new Color(0.30f, 0.23f, 0.15f, 1f),
                rimColor: Color.clear,
                rimSize: 0f,
                rimEnabled: false)
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
        foreach (var propertyName in TexturePropertySearchOrder)
        {
            if (!source.HasProperty(propertyName))
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
    }

    private static void SetColor(Material material, string propertyName, Color value)
    {
        if (material.HasProperty(propertyName))
        {
            material.SetColor(propertyName, value);
        }
    }

    private static Shader ResolveShader()
    {
        return Shader.Find("Universal Render Pipeline/Lit")
            ?? Shader.Find("Universal Render Pipeline/Unlit")
            ?? Shader.Find("Quibli/Stylized Lit")
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

    private static void SetKeyword(Material material, string keyword, bool enabled)
    {
        if (enabled)
        {
            material.EnableKeyword(keyword);
            return;
        }

        material.DisableKeyword(keyword);
    }

    private readonly struct QuibliProfile
    {
        public QuibliProfile(
            float textureImpact,
            float lightContribution,
            float selfShadingSize,
            Color shadowColor,
            Color rimColor,
            float rimSize,
            bool rimEnabled)
        {
            TextureImpact = textureImpact;
            LightContribution = lightContribution;
            SelfShadingSize = selfShadingSize;
            ShadowColor = shadowColor;
            RimColor = rimColor;
            RimSize = rimSize;
            RimEnabled = rimEnabled;
        }

        public float TextureImpact { get; }
        public float LightContribution { get; }
        public float SelfShadingSize { get; }
        public Color ShadowColor { get; }
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
