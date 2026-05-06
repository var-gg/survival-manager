using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;

namespace SM.Unity
{

[CreateAssetMenu(menuName = "SM/Battle/P09 Appearance Preset", fileName = "p09_appearance_")]
public sealed class BattleP09AppearancePreset : ScriptableObject
{
    public const string ResourcesFolder = "_Game/Battle/Appearances/P09";
    public const string AssetFolder = "Assets/Resources/_Game/Battle/Appearances/P09";

    private const string SkinMaterialPattern = @"^P09_.*_Skin.*$";
    private const string EyeMaterialPattern = @"^P09_Eye.*$";

    [SerializeField] private string characterId = string.Empty;
    [SerializeField] private string displayName = string.Empty;
    [SerializeField] private BattleP09AppearanceCatalog catalog = null!;
    [SerializeField] private int weaponId;
    [SerializeField] private int shieldId;
    [SerializeField] private int headId;
    [SerializeField] private int chestId;
    [SerializeField] private int armId;
    [SerializeField] private int waistId;
    [SerializeField] private int legId;
    [SerializeField] private int sexId = 1;
    [SerializeField] private int faceTypeId = 1;
    [SerializeField] private int hairStyleId = 1;
    [SerializeField] private int hairColorId = 1;
    [SerializeField] private int skinId = 1;
    [SerializeField] private int eyeColorId = 1;
    [SerializeField] private int facialHairId;
    [SerializeField] private int bustSizeId = 2;
    [SerializeField] private List<BattleP09MaterialColorOverride> materialColorOverrides = new();
    [NonSerialized] private BattleP09AppearanceCatalog? fallbackCatalog;

    public string CharacterId => characterId;
    public string DisplayName => displayName;
    public BattleP09AppearanceCatalog Catalog => ResolveCatalog()!;
    public int SexId => sexId;

    public void ConfigureIdentity(string configuredCharacterId, string configuredDisplayName, BattleP09AppearanceCatalog configuredCatalog)
    {
        characterId = configuredCharacterId ?? string.Empty;
        displayName = configuredDisplayName ?? string.Empty;
        catalog = configuredCatalog;
    }

    public void SetContentId(BattleP09AppearancePartType type, int contentId)
    {
        switch (type)
        {
            case BattleP09AppearancePartType.Weapon:
                weaponId = contentId;
                break;
            case BattleP09AppearancePartType.Shield:
                shieldId = contentId;
                break;
            case BattleP09AppearancePartType.Head:
                headId = contentId;
                break;
            case BattleP09AppearancePartType.Chest:
                chestId = contentId;
                break;
            case BattleP09AppearancePartType.Arm:
                armId = contentId;
                break;
            case BattleP09AppearancePartType.Waist:
                waistId = contentId;
                break;
            case BattleP09AppearancePartType.Leg:
                legId = contentId;
                break;
            case BattleP09AppearancePartType.Sex:
                sexId = contentId <= 0 ? 1 : contentId;
                break;
            case BattleP09AppearancePartType.HairStyle:
                hairStyleId = contentId <= 0 ? 1 : contentId;
                break;
            case BattleP09AppearancePartType.HairColor:
                hairColorId = contentId <= 0 ? 1 : contentId;
                break;
            case BattleP09AppearancePartType.Skin:
                skinId = contentId <= 0 ? 1 : contentId;
                break;
            case BattleP09AppearancePartType.EyeColor:
                eyeColorId = contentId <= 0 ? 1 : contentId;
                break;
            case BattleP09AppearancePartType.FacialHair:
                facialHairId = contentId;
                break;
            case BattleP09AppearancePartType.BustSize:
                bustSizeId = contentId <= 0 ? 2 : contentId;
                break;
            case BattleP09AppearancePartType.FaceType:
                faceTypeId = contentId <= 0 ? 1 : contentId;
                break;
        }
    }

    public int GetContentId(BattleP09AppearancePartType type)
    {
        return type switch
        {
            BattleP09AppearancePartType.Weapon => weaponId,
            BattleP09AppearancePartType.Shield => shieldId,
            BattleP09AppearancePartType.Head => headId,
            BattleP09AppearancePartType.Chest => chestId,
            BattleP09AppearancePartType.Arm => armId,
            BattleP09AppearancePartType.Waist => waistId,
            BattleP09AppearancePartType.Leg => legId,
            BattleP09AppearancePartType.Sex => sexId,
            BattleP09AppearancePartType.HairStyle => hairStyleId,
            BattleP09AppearancePartType.HairColor => hairColorId,
            BattleP09AppearancePartType.Skin => skinId,
            BattleP09AppearancePartType.EyeColor => eyeColorId,
            BattleP09AppearancePartType.FacialHair => facialHairId,
            BattleP09AppearancePartType.BustSize => bustSizeId,
            BattleP09AppearancePartType.FaceType => faceTypeId,
            _ => 0
        };
    }

    public void EnsureDefaultColorOverrides()
    {
        EnsureColorOverride("갑옷", "Armor", new Color(0.82f, 0.78f, 0.70f, 1f), new Color(0.34f, 0.27f, 0.22f, 1f), Color.white);
        EnsureColorOverride("무기", "Weapon", new Color(0.78f, 0.76f, 0.72f, 1f), new Color(0.28f, 0.24f, 0.20f, 1f), Color.white);
        EnsureColorOverride("방패", "Shield", new Color(0.70f, 0.76f, 0.82f, 1f), new Color(0.25f, 0.26f, 0.30f, 1f), Color.white);
    }

    public void ApplyTo(Transform modelRoot, ICollection<Material> generatedMaterials)
    {
        if (modelRoot == null)
        {
            return;
        }

        var activeCatalog = ResolveCatalog();
        if (activeCatalog == null)
        {
            return;
        }

        ApplyRendererSelection(activeCatalog, modelRoot);
        ApplyHairColor(activeCatalog, modelRoot);
        ApplySkinColor(activeCatalog, modelRoot);
        ApplyEyeColor(activeCatalog, modelRoot);
        ApplyBustSize(activeCatalog, modelRoot);
        ApplyMaterialColorOverrides(modelRoot, generatedMaterials);
    }

    private BattleP09AppearanceCatalog? ResolveCatalog()
    {
        if (catalog != null)
        {
            return catalog;
        }

        fallbackCatalog ??= Resources.Load<BattleP09AppearanceCatalog>(BattleP09AppearanceCatalog.ResourcesPath);
        return fallbackCatalog;
    }

    private void EnsureColorOverride(string label, string targetContains, Color main, Color second, Color third)
    {
        foreach (var existing in materialColorOverrides)
        {
            if (string.Equals(existing.TargetContains, targetContains, StringComparison.Ordinal))
            {
                if (string.IsNullOrWhiteSpace(existing.Label)
                    || existing.Label is "Armor" or "Weapon" or "Shield")
                {
                    existing.Label = label;
                }

                return;
            }
        }

        materialColorOverrides.Add(new BattleP09MaterialColorOverride
        {
            Label = label,
            TargetContains = targetContains,
            MainColor = main,
            SecondColor = second,
            ThirdColor = third
        });
    }

    private void ApplyRendererSelection(BattleP09AppearanceCatalog activeCatalog, Transform modelRoot)
    {
        foreach (var child in modelRoot.GetComponentsInChildren<Transform>(true))
        {
            ApplyRendererSelection(activeCatalog, child, BattleP09AppearancePartType.Sex);
            ApplyRendererSelection(activeCatalog, child, BattleP09AppearancePartType.FaceType);
            ApplyRendererSelection(activeCatalog, child, BattleP09AppearancePartType.HairStyle);
            ApplyRendererSelection(activeCatalog, child, BattleP09AppearancePartType.FacialHair);
            ApplyRendererSelection(activeCatalog, child, BattleP09AppearancePartType.Head);
            ApplyRendererSelection(activeCatalog, child, BattleP09AppearancePartType.Chest);
            ApplyRendererSelection(activeCatalog, child, BattleP09AppearancePartType.Arm);
            ApplyRendererSelection(activeCatalog, child, BattleP09AppearancePartType.Waist);
            ApplyRendererSelection(activeCatalog, child, BattleP09AppearancePartType.Leg);
            ApplyRendererSelection(activeCatalog, child, BattleP09AppearancePartType.Weapon);
            ApplyRendererSelection(activeCatalog, child, BattleP09AppearancePartType.Shield);
        }
    }

    private void ApplyRendererSelection(BattleP09AppearanceCatalog activeCatalog, Transform child, BattleP09AppearancePartType type)
    {
        foreach (var option in activeCatalog.GetOptions(type, sexId))
        {
            var currentId = GetContentId(type);
            if (MatchesMeshName(child.name, option.MeshName, out var sexSpecific))
            {
                var active = option.ContentId == currentId;
                if (sexSpecific == SexSpecificMesh.Male)
                {
                    active = active && sexId == 1;
                }
                else if (sexSpecific == SexSpecificMesh.Female)
                {
                    active = active && sexId == 2;
                }

                child.gameObject.SetActive(active);
            }
        }
    }

    private void ApplyHairColor(BattleP09AppearanceCatalog activeCatalog, Transform modelRoot)
    {
        if (!activeCatalog.TryGetOption(BattleP09AppearancePartType.HairColor, sexId, hairColorId, out var hairColor))
        {
            return;
        }

        var material = hairColor.ResolveHairMaterial(hairStyleId);
        if (material == null)
        {
            return;
        }

        var meshName = SafeFormat(hairColor.MeshName, hairStyleId);
        foreach (var child in modelRoot.GetComponentsInChildren<Transform>(true))
        {
            if (!string.Equals(child.name, meshName, StringComparison.Ordinal))
            {
                continue;
            }

            var renderer = child.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.sharedMaterial = material;
            }
        }
    }

    private void ApplySkinColor(BattleP09AppearanceCatalog activeCatalog, Transform modelRoot)
    {
        if (!activeCatalog.TryGetOption(BattleP09AppearancePartType.Skin, sexId, skinId, out var skin) || skin.Material == null)
        {
            return;
        }

        foreach (var renderer in modelRoot.GetComponentsInChildren<Renderer>(true))
        {
            ReplaceMatchingMaterial(renderer, SkinMaterialPattern, skin.Material);
        }
    }

    private void ApplyEyeColor(BattleP09AppearanceCatalog activeCatalog, Transform modelRoot)
    {
        if (!activeCatalog.TryGetOption(BattleP09AppearancePartType.EyeColor, sexId, eyeColorId, out var eye) || eye.Material == null)
        {
            return;
        }

        foreach (var child in modelRoot.GetComponentsInChildren<Transform>(true))
        {
            if (!string.IsNullOrWhiteSpace(eye.MeshName) && !child.name.Contains(eye.MeshName, StringComparison.Ordinal))
            {
                continue;
            }

            foreach (var renderer in child.GetComponentsInChildren<Renderer>(true))
            {
                ReplaceMatchingMaterial(renderer, EyeMaterialPattern, eye.Material);
            }
        }
    }

    private void ApplyBustSize(BattleP09AppearanceCatalog activeCatalog, Transform modelRoot)
    {
        if (sexId != 2
            || !activeCatalog.TryGetOption(BattleP09AppearancePartType.BustSize, sexId, bustSizeId, out var bust)
            || string.IsNullOrWhiteSpace(bust.MeshName))
        {
            return;
        }

        var rightName = SafeFormat(bust.MeshName, "R");
        var leftName = SafeFormat(bust.MeshName, "L");
        foreach (var child in modelRoot.GetComponentsInChildren<Transform>(true))
        {
            if (string.Equals(child.name, rightName, StringComparison.Ordinal)
                || string.Equals(child.name, leftName, StringComparison.Ordinal))
            {
                child.localScale = bust.BustScale;
            }
        }
    }

    private void ApplyMaterialColorOverrides(Transform modelRoot, ICollection<Material> generatedMaterials)
    {
        foreach (var renderer in modelRoot.GetComponentsInChildren<Renderer>(true))
        {
            var materials = renderer.sharedMaterials;
            var changed = false;
            for (var i = 0; i < materials.Length; i++)
            {
                var source = materials[i];
                if (source == null)
                {
                    continue;
                }

                var colorOverride = FindColorOverride(source.name);
                if (colorOverride == null || !colorOverride.Enabled)
                {
                    continue;
                }

                var clone = new Material(source)
                {
                    hideFlags = HideFlags.DontSave
                };
                colorOverride.ApplyTo(clone);
                generatedMaterials.Add(clone);
                materials[i] = clone;
                changed = true;
            }

            if (changed)
            {
                renderer.sharedMaterials = materials;
            }
        }
    }

    private BattleP09MaterialColorOverride? FindColorOverride(string materialName)
    {
        foreach (var colorOverride in materialColorOverrides)
        {
            if (!colorOverride.Enabled || string.IsNullOrWhiteSpace(colorOverride.TargetContains))
            {
                continue;
            }

            if (materialName.Contains(colorOverride.TargetContains, StringComparison.OrdinalIgnoreCase))
            {
                return colorOverride;
            }
        }

        return null;
    }

    private static void ReplaceMatchingMaterial(Renderer renderer, string materialPattern, Material replacement)
    {
        var materials = renderer.sharedMaterials;
        var changed = false;
        for (var i = 0; i < materials.Length; i++)
        {
            var material = materials[i];
            if (material != null && Regex.IsMatch(material.name, materialPattern))
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

    private static bool MatchesMeshName(string childName, string meshName, out SexSpecificMesh sexSpecific)
    {
        sexSpecific = SexSpecificMesh.None;
        if (string.IsNullOrWhiteSpace(meshName))
        {
            return false;
        }

        if (string.Equals(childName, meshName, StringComparison.Ordinal))
        {
            return true;
        }

        var maleName = SafeFormat(meshName, "Male");
        if (string.Equals(childName, maleName, StringComparison.Ordinal))
        {
            sexSpecific = SexSpecificMesh.Male;
            return true;
        }

        var femaleName = SafeFormat(meshName, "Female");
        if (string.Equals(childName, femaleName, StringComparison.Ordinal))
        {
            sexSpecific = SexSpecificMesh.Female;
            return true;
        }

        var femName = SafeFormat(meshName, "Fem");
        if (string.Equals(childName, femName, StringComparison.Ordinal))
        {
            sexSpecific = SexSpecificMesh.Female;
            return true;
        }

        return false;
    }

    private static string SafeFormat(string format, object value)
    {
        if (string.IsNullOrWhiteSpace(format) || !format.Contains("{", StringComparison.Ordinal))
        {
            return format;
        }

        try
        {
            return string.Format(format, value);
        }
        catch (FormatException)
        {
            return format;
        }
    }

    private enum SexSpecificMesh
    {
        None,
        Male,
        Female
    }
}

[Serializable]
public sealed class BattleP09MaterialColorOverride
{
    [InspectorName("표시 이름")]
    public string Label = string.Empty;

    [InspectorName("대상 머티리얼 이름 포함")]
    public string TargetContains = string.Empty;

    [InspectorName("사용")]
    public bool Enabled = true;

    [InspectorName("주 색상")]
    public Color MainColor = Color.white;

    [InspectorName("보조 색상")]
    public Color SecondColor = Color.white;

    [InspectorName("세 번째 색상")]
    public Color ThirdColor = Color.white;

    public void ApplyTo(Material material)
    {
        SetColorIfPresent(material, "_Color", MainColor);
        SetColorIfPresent(material, "_BaseColor", MainColor);
        SetColorIfPresent(material, "_Color2nd", SecondColor);
        SetColorIfPresent(material, "_Color3rd", ThirdColor);
    }

    private static void SetColorIfPresent(Material material, string propertyName, Color color)
    {
        if (material.HasProperty(propertyName))
        {
            material.SetColor(propertyName, color);
        }
    }
}
}
