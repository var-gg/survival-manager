using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace SM.Unity;

[CreateAssetMenu(menuName = "SM/Battle/P09 Appearance Catalog", fileName = "P09AppearanceCatalog")]
public sealed class BattleP09AppearanceCatalog : ScriptableObject
{
    public const string ResourcesPath = "_Game/Battle/P09AppearanceCatalog";
    public const string AssetPath = "Assets/Resources/_Game/Battle/P09AppearanceCatalog.asset";

    [SerializeField] private List<BattleP09AppearanceOption> options = new();

    public IReadOnlyList<BattleP09AppearanceOption> Options => options;

    public IEnumerable<BattleP09AppearanceOption> GetOptions(BattleP09AppearancePartType type, int sexId)
    {
        return options
            .Where(option => option.Type == type && (option.SexId == 0 || option.SexId == sexId))
            .OrderBy(option => option.ContentId)
            .ThenBy(option => option.DisplayName, StringComparer.Ordinal);
    }

    public bool TryGetOption(BattleP09AppearancePartType type, int sexId, int contentId, out BattleP09AppearanceOption option)
    {
        option = options.FirstOrDefault(candidate =>
            candidate.Type == type
            && candidate.ContentId == contentId
            && (candidate.SexId == 0 || candidate.SexId == sexId))!;
        return option != null;
    }

    public void SetOptions(IEnumerable<BattleP09AppearanceOption> sourceOptions)
    {
        options = sourceOptions
            .OrderBy(option => option.Type)
            .ThenBy(option => option.SexId)
            .ThenBy(option => option.ContentId)
            .ThenBy(option => option.DisplayName, StringComparer.Ordinal)
            .ToList();
    }
}

[Serializable]
public sealed class BattleP09AppearanceOption
{
    public BattleP09AppearancePartType Type;
    public int SexId;
    public int ContentId;
    public string DisplayName = string.Empty;
    public string MeshName = string.Empty;
    public Material Material = null!;
    public Vector3 BustScale = Vector3.one;
    public List<BattleP09HairMaterialOverride> HairMaterials = new();

    public string Label
    {
        get
        {
            var name = string.IsNullOrWhiteSpace(DisplayName) ? MeshName : DisplayName;
            return string.IsNullOrWhiteSpace(name) ? $"#{ContentId}" : $"{ContentId}: {name}";
        }
    }

    public Material? ResolveHairMaterial(int hairStyleId)
    {
        foreach (var hairMaterial in HairMaterials)
        {
            if (hairMaterial.HairStyleId == hairStyleId && hairMaterial.Material != null)
            {
                return hairMaterial.Material;
            }
        }

        return Material;
    }
}

[Serializable]
public sealed class BattleP09HairMaterialOverride
{
    public int HairStyleId;
    public Material Material = null!;
}
