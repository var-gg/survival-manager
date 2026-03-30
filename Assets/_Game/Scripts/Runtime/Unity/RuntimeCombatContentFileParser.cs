using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Reflection;
using SM.Content.Definitions;
using SM.Core.Stats;
using UnityEngine;

namespace SM.Unity;

internal sealed record RuntimeCombatParsedContent(
    IReadOnlyList<UnitArchetypeDefinition> Archetypes,
    IReadOnlyList<ItemBaseDefinition> Items,
    IReadOnlyList<AffixDefinition> Affixes,
    IReadOnlyList<AugmentDefinition> Augments);

internal static class RuntimeCombatContentFileParser
{
    private const string RootPath = "Assets/Resources/_Game/Content/Definitions";
    private static readonly Regex GuidRegex = new(@"guid:\s*([0-9a-fA-F]+)", RegexOptions.Compiled);

    public static bool TryLoad(out RuntimeCombatParsedContent parsed, out string error)
    {
        try
        {
            if (!Directory.Exists(RootPath))
            {
                parsed = null!;
                error = $"전투 content root를 찾을 수 없습니다. Root={RootPath}";
                return false;
            }

            var guidToPath = BuildGuidMap(RootPath);
            var races = LoadRaces(guidToPath);
            var classes = LoadClasses(guidToPath);
            var skills = LoadSkills(guidToPath);
            var traitPools = LoadTraitPools(guidToPath);
            var items = LoadItems();
            var affixes = LoadAffixes();
            var augments = LoadAugments();
            var archetypes = LoadArchetypes(guidToPath, races, classes, traitPools, skills);

            parsed = new RuntimeCombatParsedContent(archetypes, items, affixes, augments);
            error = string.Empty;
            return true;
        }
        catch (Exception ex)
        {
            parsed = null!;
            error = ex.Message;
            return false;
        }
    }

    private static Dictionary<string, string> BuildGuidMap(string rootPath)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var metaPath in Directory.EnumerateFiles(rootPath, "*.meta", SearchOption.AllDirectories))
        {
            var guid = ExtractValue(File.ReadAllLines(metaPath), "guid:");
            if (string.IsNullOrWhiteSpace(guid))
            {
                continue;
            }

            result[guid] = ToUnityPath(metaPath[..^5]);
        }

        return result;
    }

    private static Dictionary<string, RaceDefinition> LoadRaces(IReadOnlyDictionary<string, string> guidToPath)
    {
        return LoadAssets("Races", path =>
        {
            var lines = File.ReadAllLines(path);
            var definition = ScriptableObject.CreateInstance<RaceDefinition>();
            definition.Id = ExtractValue(lines, "Id:");
            definition.NameKey = ContentLocalizationTables.BuildRaceNameKey(definition.Id);
            definition.DescriptionKey = ContentLocalizationTables.BuildRaceDescriptionKey(definition.Id);
            SetLegacyField(definition, "legacyDisplayName", ExtractValue(lines, "DisplayName:"));
            SetLegacyField(definition, "legacyDescription", ExtractValue(lines, "Description:"));
            return definition;
        }, guidToPath);
    }

    private static Dictionary<string, ClassDefinition> LoadClasses(IReadOnlyDictionary<string, string> guidToPath)
    {
        return LoadAssets("Classes", path =>
        {
            var lines = File.ReadAllLines(path);
            var definition = ScriptableObject.CreateInstance<ClassDefinition>();
            definition.Id = ExtractValue(lines, "Id:");
            definition.NameKey = ContentLocalizationTables.BuildClassNameKey(definition.Id);
            definition.DescriptionKey = ContentLocalizationTables.BuildClassDescriptionKey(definition.Id);
            SetLegacyField(definition, "legacyDisplayName", ExtractValue(lines, "DisplayName:"));
            return definition;
        }, guidToPath);
    }

    private static Dictionary<string, SkillDefinitionAsset> LoadSkills(IReadOnlyDictionary<string, string> guidToPath)
    {
        return LoadAssets("Skills", path =>
        {
            var lines = File.ReadAllLines(path);
            var definition = ScriptableObject.CreateInstance<SkillDefinitionAsset>();
            definition.Id = ExtractValue(lines, "Id:");
            definition.NameKey = ContentLocalizationTables.BuildSkillNameKey(definition.Id);
            definition.DescriptionKey = ContentLocalizationTables.BuildSkillDescriptionKey(definition.Id);
            SetLegacyField(definition, "legacyDisplayName", ExtractValue(lines, "DisplayName:"));
            definition.Kind = (SkillKindValue)ExtractInt(lines, "Kind:");
            definition.SlotKind = (SkillSlotKindValue)ExtractInt(lines, "SlotKind:");
            definition.DamageType = (DamageTypeValue)ExtractInt(lines, "DamageType:");
            definition.Delivery = (SkillDeliveryValue)ExtractInt(lines, "Delivery:");
            definition.TargetRule = (SkillTargetRuleValue)ExtractInt(lines, "TargetRule:");
            definition.Power = ExtractFloat(lines, "Power:");
            definition.Range = ExtractFloat(lines, "Range:");
            definition.PowerFlat = ExtractFloat(lines, "PowerFlat:");
            definition.PhysCoeff = ExtractFloat(lines, "PhysCoeff:");
            definition.MagCoeff = ExtractFloat(lines, "MagCoeff:");
            definition.HealCoeff = ExtractFloat(lines, "HealCoeff:");
            definition.HealthCoeff = ExtractFloat(lines, "HealthCoeff:");
            definition.CanCrit = ExtractBool(lines, "CanCrit:");
            definition.ManaCost = ExtractFloat(lines, "ManaCost:");
            definition.BaseCooldownSeconds = ExtractFloat(lines, "BaseCooldownSeconds:");
            definition.CastWindupSeconds = ExtractFloat(lines, "CastWindupSeconds:");
            return definition;
        }, guidToPath);
    }

    private static Dictionary<string, TraitPoolDefinition> LoadTraitPools(IReadOnlyDictionary<string, string> guidToPath)
    {
        return LoadAssets("Traits", path =>
        {
            var lines = File.ReadAllLines(path);
            var definition = ScriptableObject.CreateInstance<TraitPoolDefinition>();
            definition.Id = ExtractValue(lines, "Id:");
            definition.ArchetypeId = ExtractValue(lines, "ArchetypeId:");
            definition.PositiveTraits = ParseTraitEntries(lines, "PositiveTraits:", "NegativeTraits:");
            definition.NegativeTraits = ParseTraitEntries(lines, "NegativeTraits:", null);
            return definition;
        }, guidToPath);
    }

    private static IReadOnlyList<ItemBaseDefinition> LoadItems()
    {
        return LoadAssets("Items", path =>
        {
            var lines = File.ReadAllLines(path);
            var definition = ScriptableObject.CreateInstance<ItemBaseDefinition>();
            definition.Id = ExtractValue(lines, "Id:");
            definition.NameKey = ContentLocalizationTables.BuildItemNameKey(definition.Id);
            definition.DescriptionKey = ContentLocalizationTables.BuildItemDescriptionKey(definition.Id);
            SetLegacyField(definition, "legacyDisplayName", ExtractValue(lines, "DisplayName:"));
            definition.SlotType = (ItemSlotType)ExtractInt(lines, "SlotType:");
            definition.IdentityKind = (ItemIdentityValue)ExtractInt(lines, "IdentityKind:");
            definition.BudgetBand = ExtractValue(lines, "BudgetBand:");
            definition.BaseModifiers = ParseModifiers(lines, "BaseModifiers:");
            return definition;
        }).Values.ToList();
    }

    private static IReadOnlyList<AffixDefinition> LoadAffixes()
    {
        return LoadAssets("Affixes", path =>
        {
            var lines = File.ReadAllLines(path);
            var definition = ScriptableObject.CreateInstance<AffixDefinition>();
            definition.Id = ExtractValue(lines, "Id:");
            definition.NameKey = $"content.affix.{ContentLocalizationTables.NormalizeId(definition.Id)}.name";
            definition.DescriptionKey = $"content.affix.{ContentLocalizationTables.NormalizeId(definition.Id)}.desc";
            SetLegacyField(definition, "legacyDisplayName", ExtractValue(lines, "DisplayName:"));
            SetLegacyField(definition, "legacyDescription", ExtractValue(lines, "Description:"));
            definition.Category = (AffixCategoryValue)ExtractInt(lines, "Category:");
            definition.Modifiers = ParseModifiers(lines, "Modifiers:");
            return definition;
        }).Values.ToList();
    }

    private static IReadOnlyList<AugmentDefinition> LoadAugments()
    {
        return LoadAssets("Augments", path =>
        {
            var lines = File.ReadAllLines(path);
            var definition = ScriptableObject.CreateInstance<AugmentDefinition>();
            definition.Id = ExtractValue(lines, "Id:");
            definition.NameKey = ContentLocalizationTables.BuildAugmentNameKey(definition.Id);
            definition.DescriptionKey = ContentLocalizationTables.BuildAugmentDescriptionKey(definition.Id);
            SetLegacyField(definition, "legacyDisplayName", ExtractValue(lines, "DisplayName:"));
            SetLegacyField(definition, "legacyDescription", ExtractValue(lines, "Description:"));
            definition.Rarity = (AugmentRarity)ExtractInt(lines, "Rarity:");
            definition.IsPermanent = ExtractInt(lines, "IsPermanent:") != 0;
            definition.Modifiers = ParseModifiers(lines, "Modifiers:");
            return definition;
        }).Values.ToList();
    }

    private static IReadOnlyList<UnitArchetypeDefinition> LoadArchetypes(
        IReadOnlyDictionary<string, string> guidToPath,
        IReadOnlyDictionary<string, RaceDefinition> races,
        IReadOnlyDictionary<string, ClassDefinition> classes,
        IReadOnlyDictionary<string, TraitPoolDefinition> traitPools,
        IReadOnlyDictionary<string, SkillDefinitionAsset> skills)
    {
        return LoadAssets("Archetypes", path =>
        {
            var lines = File.ReadAllLines(path);
            var definition = ScriptableObject.CreateInstance<UnitArchetypeDefinition>();
            definition.Id = ExtractValue(lines, "Id:");
            definition.NameKey = ContentLocalizationTables.BuildArchetypeNameKey(definition.Id);
            SetLegacyField(definition, "legacyDisplayName", ExtractValue(lines, "DisplayName:"));
            definition.Race = ResolveReference(lines, "Race:", guidToPath, races);
            definition.Class = ResolveReference(lines, "Class:", guidToPath, classes);
            definition.TraitPool = ResolveReference(lines, "TraitPool:", guidToPath, traitPools);
            definition.ScopeKind = (ArchetypeScopeValue)ExtractInt(lines, "ScopeKind:");
            definition.RoleFamilyTag = ExtractValue(lines, "RoleFamilyTag:");
            definition.PrimaryWeaponFamilyTag = ExtractValue(lines, "PrimaryWeaponFamilyTag:");
            definition.Skills = ParseGuidList(lines, "Skills:")
                .Select(guid => ResolveGuid(guid, guidToPath, skills))
                .Where(skill => skill != null)
                .Select(skill => skill!)
                .ToList();
            definition.TacticPreset = ParseTacticPreset(lines, guidToPath, skills);
            definition.DefaultAnchor = (DeploymentAnchorValue)ExtractInt(lines, "DefaultAnchor:");
            definition.PreferredTeamPosture = (TeamPostureTypeValue)ExtractInt(lines, "PreferredTeamPosture:");
            definition.BaseMaxHealth = ExtractFloat(lines, "BaseMaxHealth:");
            definition.BaseAttack = ExtractFloat(lines, "BaseAttack:");
            definition.BaseDefense = ExtractFloat(lines, "BaseDefense:");
            definition.BaseSpeed = ExtractFloat(lines, "BaseSpeed:");
            definition.BaseHealPower = ExtractFloat(lines, "BaseHealPower:");
            definition.BaseMoveSpeed = ExtractFloat(lines, "BaseMoveSpeed:");
            definition.BaseAttackRange = ExtractFloat(lines, "BaseAttackRange:");
            definition.BaseAggroRadius = ExtractFloat(lines, "BaseAggroRadius:");
            definition.BaseAttackWindup = ExtractFloat(lines, "BaseAttackWindup:");
            definition.BaseAttackCooldown = ExtractFloat(lines, "BaseAttackCooldown:");
            definition.BaseLeashDistance = ExtractFloat(lines, "BaseLeashDistance:");
            definition.BaseTargetSwitchDelay = ExtractFloat(lines, "BaseTargetSwitchDelay:");
            return definition;
        }).Values.ToList();
    }

    private static Dictionary<string, T> LoadAssets<T>(
        string folderName,
        Func<string, T> loader,
        IReadOnlyDictionary<string, string>? guidToPath = null)
        where T : UnityEngine.Object
    {
        var result = new Dictionary<string, T>(StringComparer.Ordinal);
        var folderPath = Path.Combine(RootPath, folderName);
        if (!Directory.Exists(folderPath))
        {
            return result;
        }

        foreach (var assetPath in Directory.EnumerateFiles(folderPath, "*.asset", SearchOption.TopDirectoryOnly))
        {
            var definition = loader(assetPath);
            if (definition == null)
            {
                continue;
            }

            definition.name = Path.GetFileNameWithoutExtension(assetPath);
            var id = ExtractDefinitionId(definition);
            if (string.IsNullOrWhiteSpace(id))
            {
                continue;
            }

            result[id] = definition;
            if (guidToPath != null)
            {
                var metaGuid = ExtractValue(File.ReadAllLines(assetPath + ".meta"), "guid:");
                if (!string.IsNullOrWhiteSpace(metaGuid) && !guidToPath.ContainsKey(metaGuid))
                {
                    ((Dictionary<string, string>)guidToPath)[metaGuid] = ToUnityPath(assetPath);
                }
            }
        }

        return result;
    }

    private static string ExtractDefinitionId(UnityEngine.Object definition)
    {
        return definition switch
        {
            RaceDefinition race => race.Id,
            ClassDefinition @class => @class.Id,
            SkillDefinitionAsset skill => skill.Id,
            TraitPoolDefinition traitPool => traitPool.Id,
            ItemBaseDefinition item => item.Id,
            AffixDefinition affix => affix.Id,
            AugmentDefinition augment => augment.Id,
            UnitArchetypeDefinition archetype => archetype.Id,
            _ => string.Empty
        };
    }

    private static List<TraitEntry> ParseTraitEntries(string[] lines, string sectionHeader, string? endSectionHeader)
    {
        var result = new List<TraitEntry>();
        var index = FindLineIndex(lines, sectionHeader);
        if (index < 0)
        {
            return result;
        }

        for (index++; index < lines.Length; index++)
        {
            var trimmed = lines[index].Trim();
            if (!string.IsNullOrWhiteSpace(endSectionHeader) && string.Equals(trimmed, endSectionHeader, StringComparison.Ordinal))
            {
                break;
            }

            if (!trimmed.StartsWith("- Id:", StringComparison.Ordinal))
            {
                continue;
            }

            var trait = new TraitEntry
            {
                Id = trimmed["- Id:".Length..].Trim()
            };
            trait.NameKey = ContentLocalizationTables.BuildTraitNameKey(string.Empty, trait.Id);
            trait.DescriptionKey = ContentLocalizationTables.BuildTraitDescriptionKey(string.Empty, trait.Id);

            for (index++; index < lines.Length; index++)
            {
                trimmed = lines[index].Trim();
                if (trimmed.StartsWith("- Id:", StringComparison.Ordinal)
                    || (!string.IsNullOrWhiteSpace(endSectionHeader) && string.Equals(trimmed, endSectionHeader, StringComparison.Ordinal))
                    || (GetIndent(lines[index]) <= 2 && trimmed.EndsWith(":", StringComparison.Ordinal)))
                {
                    index--;
                    break;
                }

                if (trimmed.StartsWith("DisplayName:", StringComparison.Ordinal))
                {
                    SetLegacyField(trait, "legacyDisplayName", trimmed["DisplayName:".Length..].Trim());
                    continue;
                }

                if (trimmed.StartsWith("Description:", StringComparison.Ordinal))
                {
                    SetLegacyField(trait, "legacyDescription", trimmed["Description:".Length..].Trim());
                    continue;
                }

                if (string.Equals(trimmed, "Modifiers:", StringComparison.Ordinal))
                {
                    trait.Modifiers = ParseNestedModifiers(lines, ref index);
                }
            }

            result.Add(trait);
        }

        return result;
    }

    private static List<TacticPresetEntry> ParseTacticPreset(
        string[] lines,
        IReadOnlyDictionary<string, string> guidToPath,
        IReadOnlyDictionary<string, SkillDefinitionAsset> skills)
    {
        var result = new List<TacticPresetEntry>();
        var index = FindLineIndex(lines, "TacticPreset:");
        if (index < 0)
        {
            return result;
        }

        for (index++; index < lines.Length; index++)
        {
            var trimmed = lines[index].Trim();
            if (!trimmed.StartsWith("- Priority:", StringComparison.Ordinal))
            {
                if (GetIndent(lines[index]) <= 2 && trimmed.EndsWith(":", StringComparison.Ordinal))
                {
                    break;
                }

                continue;
            }

            var entry = new TacticPresetEntry
            {
                Priority = ParseInt(trimmed["- Priority:".Length..].Trim())
            };

            for (index++; index < lines.Length; index++)
            {
                trimmed = lines[index].Trim();
                if (trimmed.StartsWith("- Priority:", StringComparison.Ordinal)
                    || (GetIndent(lines[index]) <= 2 && trimmed.EndsWith(":", StringComparison.Ordinal)))
                {
                    index--;
                    break;
                }

                if (trimmed.StartsWith("ConditionType:", StringComparison.Ordinal))
                {
                    entry.ConditionType = (TacticConditionTypeValue)ParseInt(trimmed["ConditionType:".Length..].Trim());
                }
                else if (trimmed.StartsWith("Threshold:", StringComparison.Ordinal))
                {
                    entry.Threshold = ParseFloat(trimmed["Threshold:".Length..].Trim());
                }
                else if (trimmed.StartsWith("ActionType:", StringComparison.Ordinal))
                {
                    entry.ActionType = (BattleActionTypeValue)ParseInt(trimmed["ActionType:".Length..].Trim());
                }
                else if (trimmed.StartsWith("TargetSelector:", StringComparison.Ordinal))
                {
                    entry.TargetSelector = (TargetSelectorTypeValue)ParseInt(trimmed["TargetSelector:".Length..].Trim());
                }
                else if (trimmed.StartsWith("Skill:", StringComparison.Ordinal))
                {
                    var guid = ExtractGuid(trimmed);
                    entry.Skill = string.IsNullOrWhiteSpace(guid) ? null : ResolveGuid(guid, guidToPath, skills);
                }
            }

            result.Add(entry);
        }

        return result;
    }

    private static List<SerializableStatModifier> ParseModifiers(string[] lines, string sectionHeader)
    {
        var index = FindLineIndex(lines, sectionHeader);
        if (index < 0)
        {
            return new List<SerializableStatModifier>();
        }

        return ParseNestedModifiers(lines, ref index);
    }

    private static List<SerializableStatModifier> ParseNestedModifiers(string[] lines, ref int index)
    {
        var result = new List<SerializableStatModifier>();
        for (index++; index < lines.Length; index++)
        {
            var trimmed = lines[index].Trim();
            if (!trimmed.StartsWith("- StatId:", StringComparison.Ordinal))
            {
                if (GetIndent(lines[index]) <= 4 && trimmed.EndsWith(":", StringComparison.Ordinal))
                {
                    index--;
                    break;
                }

                continue;
            }

            var modifier = new SerializableStatModifier
            {
                StatId = trimmed["- StatId:".Length..].Trim()
            };

            for (index++; index < lines.Length; index++)
            {
                trimmed = lines[index].Trim();
                if (trimmed.StartsWith("- StatId:", StringComparison.Ordinal)
                    || (GetIndent(lines[index]) <= 4 && trimmed.EndsWith(":", StringComparison.Ordinal))
                    || trimmed.StartsWith("- Id:", StringComparison.Ordinal))
                {
                    index--;
                    break;
                }

                if (trimmed.StartsWith("Op:", StringComparison.Ordinal))
                {
                    modifier.Op = (ModifierOp)ParseInt(trimmed["Op:".Length..].Trim());
                }
                else if (trimmed.StartsWith("Value:", StringComparison.Ordinal))
                {
                    modifier.Value = ParseFloat(trimmed["Value:".Length..].Trim());
                }
            }

            result.Add(modifier);
        }

        return result;
    }

    private static List<string> ParseGuidList(string[] lines, string sectionHeader)
    {
        var result = new List<string>();
        var index = FindLineIndex(lines, sectionHeader);
        if (index < 0)
        {
            return result;
        }

        for (index++; index < lines.Length; index++)
        {
            var trimmed = lines[index].Trim();
            if (!trimmed.StartsWith("- {", StringComparison.Ordinal))
            {
                if (GetIndent(lines[index]) <= 2 && trimmed.EndsWith(":", StringComparison.Ordinal))
                {
                    break;
                }

                continue;
            }

            var guid = ExtractGuid(trimmed);
            if (!string.IsNullOrWhiteSpace(guid))
            {
                result.Add(guid);
            }
        }

        return result;
    }

    private static T ResolveReference<T>(
        string[] lines,
        string key,
        IReadOnlyDictionary<string, string> guidToPath,
        IReadOnlyDictionary<string, T> definitions)
        where T : UnityEngine.Object
    {
        var guid = ExtractGuid(ExtractLine(lines, key));
        return ResolveGuid(guid, guidToPath, definitions)!;
    }

    private static T? ResolveGuid<T>(
        string? guid,
        IReadOnlyDictionary<string, string> guidToPath,
        IReadOnlyDictionary<string, T> definitions)
        where T : UnityEngine.Object
    {
        if (string.IsNullOrWhiteSpace(guid) || !guidToPath.TryGetValue(guid, out var path))
        {
            return null;
        }

        var fileName = Path.GetFileNameWithoutExtension(path);
        var candidateId = fileName[(fileName.IndexOf('_') + 1)..];
        if (definitions.TryGetValue(candidateId, out var byId))
        {
            return byId;
        }

        return definitions.Values.FirstOrDefault(definition => string.Equals(definition.name, fileName, StringComparison.Ordinal));
    }

    private static string ExtractLine(string[] lines, string key)
    {
        return lines.FirstOrDefault(line => line.TrimStart().StartsWith(key, StringComparison.Ordinal)) ?? string.Empty;
    }

    private static string ExtractValue(string[] lines, string key)
    {
        var line = ExtractLine(lines, key);
        if (string.IsNullOrWhiteSpace(line))
        {
            return string.Empty;
        }

        return line.TrimStart()[key.Length..].Trim();
    }

    private static int ExtractInt(string[] lines, string key)
    {
        return ParseInt(ExtractValue(lines, key));
    }

    private static float ExtractFloat(string[] lines, string key)
    {
        return ParseFloat(ExtractValue(lines, key));
    }

    private static bool ExtractBool(string[] lines, string key)
    {
        return ParseBool(ExtractValue(lines, key));
    }

    private static int ParseInt(string value)
    {
        return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) ? parsed : 0;
    }

    private static float ParseFloat(string value)
    {
        return float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed) ? parsed : 0f;
    }

    private static bool ParseBool(string value)
    {
        return bool.TryParse(value, out var parsed)
            ? parsed
            : ParseInt(value) != 0;
    }

    private static int FindLineIndex(string[] lines, string key)
    {
        for (var i = 0; i < lines.Length; i++)
        {
            if (string.Equals(lines[i].Trim(), key, StringComparison.Ordinal))
            {
                return i;
            }
        }

        return -1;
    }

    private static int GetIndent(string line)
    {
        var count = 0;
        while (count < line.Length && line[count] == ' ')
        {
            count++;
        }

        return count;
    }

    private static string ExtractGuid(string line)
    {
        if (string.IsNullOrWhiteSpace(line))
        {
            return string.Empty;
        }

        var match = GuidRegex.Match(line);
        return match.Success ? match.Groups[1].Value : string.Empty;
    }

    private static string ToUnityPath(string path)
    {
        return path.Replace('\\', '/');
    }

    private static void SetLegacyField(object instance, string fieldName, string value)
    {
        var field = instance.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        if (field != null)
        {
            field.SetValue(instance, value);
        }
    }
}
