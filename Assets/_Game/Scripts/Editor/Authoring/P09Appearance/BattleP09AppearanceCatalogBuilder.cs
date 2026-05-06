using System.Collections.Generic;
using System.IO;
using SM.Content.Definitions;
using SM.Unity;
using UnityEditor;
using UnityEngine;

namespace SM.Editor.Authoring.P09Appearance;

public static class BattleP09AppearanceCatalogBuilder
{
    private const string P09DataRoot = "Assets/P09_Modular_Humanoid/Scenes/DemoScene_Data/ScriptableObject";
    private const string CharacterRoot = "Assets/Resources/_Game/Content/Definitions/Characters";

    [MenuItem("SM/Internal/Content/Rebuild P09 Appearance Catalog")]
    public static void RebuildCatalogMenu()
    {
        EnsureCatalog();
        EnsureMissingPresets();
    }

    public static BattleP09AppearanceCatalog EnsureCatalog()
    {
        EnsureFolder("Assets/Resources/_Game/Battle");

        var catalog = LoadCatalog();
        if (catalog == null)
        {
            catalog = ScriptableObject.CreateInstance<BattleP09AppearanceCatalog>();
            AssetDatabase.CreateAsset(catalog, BattleP09AppearanceCatalog.AssetPath);
        }

        catalog.SetOptions(ReadP09Options());
        EditorUtility.SetDirty(catalog);
        AssetDatabase.SaveAssets();
        return catalog;
    }

    public static BattleP09AppearanceCatalog? LoadCatalog()
    {
        return AssetDatabase.LoadAssetAtPath<BattleP09AppearanceCatalog>(BattleP09AppearanceCatalog.AssetPath);
    }

    public static IReadOnlyList<BattleP09AppearancePreset> EnsureMissingPresets()
    {
        var catalog = EnsureCatalog();
        EnsureFolder(BattleP09AppearancePreset.AssetFolder);

        var presets = new List<BattleP09AppearancePreset>();
        var characters = LoadCharacters();
        for (var i = 0; i < characters.Count; i++)
        {
            var character = characters[i];
            if (string.IsNullOrWhiteSpace(character.Id))
            {
                continue;
            }

            var preset = EnsurePreset(character, catalog, i);
            presets.Add(preset);
        }

        AssetDatabase.SaveAssets();
        return presets;
    }

    public static IReadOnlyList<CharacterDefinition> LoadCharacters()
    {
        var characters = new List<CharacterDefinition>();
        foreach (var guid in AssetDatabase.FindAssets("t:CharacterDefinition", new[] { CharacterRoot }))
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            var character = AssetDatabase.LoadAssetAtPath<CharacterDefinition>(path);
            if (character != null)
            {
                characters.Add(character);
            }
        }

        characters.Sort((left, right) => string.CompareOrdinal(left.Id, right.Id));
        return characters;
    }

    public static BattleP09AppearancePreset? FindPreset(string characterId)
    {
        if (string.IsNullOrWhiteSpace(characterId))
        {
            return null;
        }

        var path = BuildPresetPath(characterId);
        return AssetDatabase.LoadAssetAtPath<BattleP09AppearancePreset>(path);
    }

    public static BattleP09AppearancePreset EnsurePreset(
        CharacterDefinition character,
        BattleP09AppearanceCatalog catalog,
        int seedIndex)
    {
        var path = BuildPresetPath(character.Id);
        var preset = AssetDatabase.LoadAssetAtPath<BattleP09AppearancePreset>(path);
        if (preset == null)
        {
            preset = ScriptableObject.CreateInstance<BattleP09AppearancePreset>();
            AssetDatabase.CreateAsset(preset, path);
            ApplySeedDefaults(preset, catalog, seedIndex);
        }

        var displayName = !string.IsNullOrWhiteSpace(character.LegacyDisplayName)
            ? character.LegacyDisplayName
            : character.Id;
        preset.ConfigureIdentity(character.Id, displayName, catalog);
        preset.EnsureDefaultColorOverrides();
        EditorUtility.SetDirty(preset);
        return preset;
    }

    private static string BuildPresetPath(string characterId)
    {
        return $"{BattleP09AppearancePreset.AssetFolder}/p09_appearance_{SanitizeFileName(characterId)}.asset";
    }

    private static IReadOnlyList<BattleP09AppearanceOption> ReadP09Options()
    {
        var options = new List<BattleP09AppearanceOption>();
        foreach (var guid in AssetDatabase.FindAssets("t:ScriptableObject", new[] { P09DataRoot }))
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            if (!Path.GetFileNameWithoutExtension(path).StartsWith("EditPartDataContainer_", System.StringComparison.Ordinal))
            {
                continue;
            }

            var container = AssetDatabase.LoadAssetAtPath<ScriptableObject>(path);
            if (container == null)
            {
                continue;
            }

            ReadContainerOptions(container, options);
        }

        return options;
    }

    private static void ReadContainerOptions(Object container, ICollection<BattleP09AppearanceOption> options)
    {
        var serialized = new SerializedObject(container);
        var type = (BattleP09AppearancePartType)serialized.FindProperty("_type").intValue;
        var sexId = serialized.FindProperty("_sexId").intValue;
        var partList = serialized.FindProperty("_partDataList");
        if (partList == null || !partList.isArray)
        {
            return;
        }

        for (var i = 0; i < partList.arraySize; i++)
        {
            var source = partList.GetArrayElementAtIndex(i).objectReferenceValue;
            if (source == null)
            {
                continue;
            }

            options.Add(ReadOption(type, sexId, source));
        }
    }

    private static BattleP09AppearanceOption ReadOption(BattleP09AppearancePartType type, int sexId, Object source)
    {
        var serialized = new SerializedObject(source);
        var option = new BattleP09AppearanceOption
        {
            Type = type,
            SexId = sexId,
            ContentId = FindInt(serialized, "_contentId"),
            DisplayName = FindString(serialized, "_displayName"),
            MeshName = FindString(serialized, "_meshName"),
            Material = FindObject<Material>(serialized, "_material"),
            BustScale = FindVector3(serialized, "_size", Vector3.one),
        };

        var hairStyleMaterials = serialized.FindProperty("_hairStyleMaterials");
        if (hairStyleMaterials != null && hairStyleMaterials.isArray)
        {
            for (var i = 0; i < hairStyleMaterials.arraySize; i++)
            {
                var item = hairStyleMaterials.GetArrayElementAtIndex(i);
                option.HairMaterials.Add(new BattleP09HairMaterialOverride
                {
                    HairStyleId = item.FindPropertyRelative("HairStyleId").intValue,
                    Material = item.FindPropertyRelative("Material").objectReferenceValue as Material
                });
            }
        }

        return option;
    }

    private static void ApplySeedDefaults(BattleP09AppearancePreset preset, BattleP09AppearanceCatalog catalog, int seedIndex)
    {
        preset.SetContentId(BattleP09AppearancePartType.Sex, seedIndex % 3 == 0 ? 2 : 1);
        preset.SetContentId(BattleP09AppearancePartType.FaceType, PickContentId(catalog, BattleP09AppearancePartType.FaceType, 1, seedIndex));
        preset.SetContentId(BattleP09AppearancePartType.HairStyle, PickContentId(catalog, BattleP09AppearancePartType.HairStyle, 1, seedIndex));
        preset.SetContentId(BattleP09AppearancePartType.HairColor, PickContentId(catalog, BattleP09AppearancePartType.HairColor, 1, seedIndex + 2));
        preset.SetContentId(BattleP09AppearancePartType.Skin, PickContentId(catalog, BattleP09AppearancePartType.Skin, 1, seedIndex));
        preset.SetContentId(BattleP09AppearancePartType.EyeColor, PickContentId(catalog, BattleP09AppearancePartType.EyeColor, 1, seedIndex + 1));
        preset.SetContentId(BattleP09AppearancePartType.FacialHair, seedIndex % 4 == 0 ? PickContentId(catalog, BattleP09AppearancePartType.FacialHair, 0, seedIndex) : 0);
        preset.SetContentId(BattleP09AppearancePartType.BustSize, 2);
        preset.SetContentId(BattleP09AppearancePartType.Head, PickContentId(catalog, BattleP09AppearancePartType.Head, 0, seedIndex));
        preset.SetContentId(BattleP09AppearancePartType.Chest, PickContentId(catalog, BattleP09AppearancePartType.Chest, 0, seedIndex + 1));
        preset.SetContentId(BattleP09AppearancePartType.Arm, PickContentId(catalog, BattleP09AppearancePartType.Arm, 0, seedIndex + 1));
        preset.SetContentId(BattleP09AppearancePartType.Waist, PickContentId(catalog, BattleP09AppearancePartType.Waist, 0, seedIndex + 2));
        preset.SetContentId(BattleP09AppearancePartType.Leg, PickContentId(catalog, BattleP09AppearancePartType.Leg, 0, seedIndex + 3));
        preset.SetContentId(BattleP09AppearancePartType.Weapon, PickContentId(catalog, BattleP09AppearancePartType.Weapon, 0, seedIndex));
        preset.SetContentId(BattleP09AppearancePartType.Shield, PickContentId(catalog, BattleP09AppearancePartType.Shield, 0, seedIndex));
    }

    private static int PickContentId(BattleP09AppearanceCatalog catalog, BattleP09AppearancePartType type, int fallback, int seedIndex)
    {
        var ids = new List<int>();
        foreach (var option in catalog.GetOptions(type, 1))
        {
            ids.Add(option.ContentId);
        }

        if (ids.Count == 0)
        {
            return fallback;
        }

        return ids[Mathf.Abs(seedIndex) % ids.Count];
    }

    private static int FindInt(SerializedObject serialized, string propertyName)
    {
        var property = serialized.FindProperty(propertyName);
        return property != null ? property.intValue : 0;
    }

    private static string FindString(SerializedObject serialized, string propertyName)
    {
        var property = serialized.FindProperty(propertyName);
        return property != null ? property.stringValue : string.Empty;
    }

    private static Vector3 FindVector3(SerializedObject serialized, string propertyName, Vector3 fallback)
    {
        var property = serialized.FindProperty(propertyName);
        return property != null ? property.vector3Value : fallback;
    }

    private static T FindObject<T>(SerializedObject serialized, string propertyName)
        where T : Object
    {
        var property = serialized.FindProperty(propertyName);
        return property != null ? property.objectReferenceValue as T : null;
    }

    private static string SanitizeFileName(string value)
    {
        foreach (var invalid in Path.GetInvalidFileNameChars())
        {
            value = value.Replace(invalid, '_');
        }

        return value;
    }

    private static void EnsureFolder(string path)
    {
        var parts = path.Split('/');
        var current = parts[0];
        for (var i = 1; i < parts.Length; i++)
        {
            var next = $"{current}/{parts[i]}";
            if (!AssetDatabase.IsValidFolder(next))
            {
                AssetDatabase.CreateFolder(current, parts[i]);
            }

            current = next;
        }
    }
}
