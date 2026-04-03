using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using SM.Content.Definitions;
using SM.Core.Contracts;
using SM.Core.Stats;
using UnityEngine;
using static SM.Unity.ContentParsing.YamlFieldExtractor;

namespace SM.Unity.ContentParsing;

internal static class ArchetypeFileParser
{
    internal static IReadOnlyList<UnitArchetypeDefinition> LoadArchetypes(
        IReadOnlyDictionary<string, string> guidToPath,
        IReadOnlyDictionary<string, RaceDefinition> races,
        IReadOnlyDictionary<string, ClassDefinition> classes,
        IReadOnlyDictionary<string, TraitPoolDefinition> traitPools,
        IReadOnlyDictionary<string, SkillDefinitionAsset> skills)
    {
        return RuntimeCombatContentFileParser.LoadAssets("Archetypes", path =>
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
            definition.RoleTag = ExtractValue(lines, "RoleTag:");
            definition.BaseMaxHealth = ExtractFloat(lines, "BaseMaxHealth:");
            definition.RoleFamilyTag = ExtractValue(lines, "RoleFamilyTag:");
            definition.PrimaryWeaponFamilyTag = ExtractValue(lines, "PrimaryWeaponFamilyTag:");
            definition.RecruitTier = (RecruitTier)ExtractInt(lines, "RecruitTier:");
            definition.BudgetCard = ParseBudgetCard(lines, "BudgetCard:") ?? definition.BudgetCard;
            definition.IsRecruitable = ExtractBool(lines, "IsRecruitable:");
            definition.IsSummonOnly = ExtractBool(lines, "IsSummonOnly:");
            definition.IsEventOnly = ExtractBool(lines, "IsEventOnly:");
            definition.IsBossOnly = ExtractBool(lines, "IsBossOnly:");
            definition.IsUnreleased = ExtractBool(lines, "IsUnreleased:");
            definition.IsTestOnly = ExtractBool(lines, "IsTestOnly:");
            definition.RecruitPlanTags = ParseStableTagList(lines, "RecruitPlanTags:", guidToPath);
            definition.ScoutBiasTags = ParseStableTagList(lines, "ScoutBiasTags:", guidToPath);
            definition.SupportModifierBiasTags = ParseStableTagList(lines, "SupportModifierBiasTags:", guidToPath);
            definition.LockedAttackProfileId = ExtractValue(lines, "LockedAttackProfileId:");
            definition.LockedAttackProfileTag = ExtractValue(lines, "LockedAttackProfileTag:");
            definition.LockedSignatureActiveSkill = ResolveReferenceFromLine(ExtractLine(lines, "LockedSignatureActiveSkill:"), guidToPath, skills);
            definition.LockedSignaturePassiveSkill = ResolveReferenceFromLine(ExtractLine(lines, "LockedSignaturePassiveSkill:"), guidToPath, skills);
            definition.FlexUtilitySkillPool = ParseReferenceList(lines, "FlexUtilitySkillPool:", guidToPath, skills);
            definition.FlexSupportSkillPool = ParseReferenceList(lines, "FlexSupportSkillPool:", guidToPath, skills);
            definition.RecruitFlexActivePool = ParseReferenceList(lines, "RecruitFlexActivePool:", guidToPath, skills);
            definition.RecruitFlexPassivePool = ParseReferenceList(lines, "RecruitFlexPassivePool:", guidToPath, skills);
            definition.RecruitBannedPairings = ParseRecruitBannedPairings(lines, "RecruitBannedPairings:");
            definition.Loadout = ParseUnitLoadout(lines, guidToPath, skills);
            definition.BaseAttack = ExtractFloat(lines, "BaseAttack:");
            definition.BaseDefense = ExtractFloat(lines, "BaseDefense:");
            definition.BaseSpeed = ExtractFloat(lines, "BaseSpeed:");
            definition.BaseHealPower = ExtractFloat(lines, "BaseHealPower:");
            definition.BaseMoveSpeed = ExtractFloat(lines, "BaseMoveSpeed:");
            definition.BaseAttackRange = ExtractFloat(lines, "BaseAttackRange:");
            definition.BaseMaxEnergy = ExtractFloat(lines, "BaseMaxEnergy:");
            definition.BaseStartingEnergy = ExtractFloat(lines, "BaseStartingEnergy:");
            definition.BaseSkillHaste = ExtractFloat(lines, "BaseSkillHaste:");
            definition.BaseManaMax = ExtractFloat(lines, "BaseManaMax:");
            definition.BaseManaGainOnAttack = ExtractFloat(lines, "BaseManaGainOnAttack:");
            definition.BaseManaGainOnHit = ExtractFloat(lines, "BaseManaGainOnHit:");
            definition.BaseCooldownRecovery = ExtractFloat(lines, "BaseCooldownRecovery:");
            definition.BaseCritChance = ExtractFloat(lines, "BaseCritChance:");
            definition.BaseCritMultiplier = ExtractFloat(lines, "BaseCritMultiplier:");
            definition.BasePhysPen = ExtractFloat(lines, "BasePhysPen:");
            definition.BaseMagPen = ExtractFloat(lines, "BaseMagPen:");
            definition.BaseAggroRadius = ExtractFloat(lines, "BaseAggroRadius:");
            definition.BasePreferredDistance = ExtractFloat(lines, "BasePreferredDistance:");
            definition.BaseProtectRadius = ExtractFloat(lines, "BaseProtectRadius:");
            definition.BaseAttackWindup = ExtractFloat(lines, "BaseAttackWindup:");
            definition.BaseCastWindup = ExtractFloat(lines, "BaseCastWindup:");
            definition.BaseProjectileSpeed = ExtractFloat(lines, "BaseProjectileSpeed:");
            definition.BaseCollisionRadius = ExtractFloat(lines, "BaseCollisionRadius:");
            definition.BaseRepositionCooldown = ExtractFloat(lines, "BaseRepositionCooldown:");
            definition.BaseAttackCooldown = ExtractFloat(lines, "BaseAttackCooldown:");
            definition.BaseLeashDistance = ExtractFloat(lines, "BaseLeashDistance:");
            definition.BaseTargetSwitchDelay = ExtractFloat(lines, "BaseTargetSwitchDelay:");
            ApplyFallbackIdentity(definition, path);
            ApplyArchetypeFallbacks(definition, races, classes, traitPools, skills);
            return definition;
        }).Values.ToList();
    }

    internal static Dictionary<string, RaceDefinition> LoadRaces(IReadOnlyDictionary<string, string> guidToPath)
    {
        return RuntimeCombatContentFileParser.LoadAssets("Races", path =>
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

    internal static Dictionary<string, ClassDefinition> LoadClasses(IReadOnlyDictionary<string, string> guidToPath)
    {
        return RuntimeCombatContentFileParser.LoadAssets("Classes", path =>
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

    internal static Dictionary<string, TraitPoolDefinition> LoadTraitPools(IReadOnlyDictionary<string, string> guidToPath)
    {
        return RuntimeCombatContentFileParser.LoadAssets("Traits", path =>
        {
            var lines = File.ReadAllLines(path);
            var definition = ScriptableObject.CreateInstance<TraitPoolDefinition>();
            definition.Id = ExtractValue(lines, "Id:");
            definition.ArchetypeId = ExtractValue(lines, "ArchetypeId:");
            definition.PositiveTraits = ParseTraitEntries(lines, "PositiveTraits:", "NegativeTraits:");
            definition.NegativeTraits = ParseTraitEntries(lines, "NegativeTraits:", null);
            ApplyFallbackIdentity(definition, path);
            ApplyTraitPoolFallbacks(definition);
            return definition;
        }, guidToPath);
    }

    internal static List<TraitEntry> ParseTraitEntries(string[] lines, string sectionHeader, string? endSectionHeader)
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

    internal static List<TacticPresetEntry> ParseTacticPreset(
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

    internal static UnitLoadoutDefinition ParseUnitLoadout(
        string[] lines,
        IReadOnlyDictionary<string, string> guidToPath,
        IReadOnlyDictionary<string, SkillDefinitionAsset> skills)
    {
        var loadoutLines = ExtractSection(lines, "Loadout:", 2);
        if (loadoutLines.Length == 0)
        {
            return new UnitLoadoutDefinition();
        }

        return new UnitLoadoutDefinition
        {
            BasicAttack = ParseBasicAttackDefinition(loadoutLines),
            SignatureActive = ResolveReferenceFromLine(ExtractLine(loadoutLines, "SignatureActive:"), guidToPath, skills),
            FlexActive = ResolveReferenceFromLine(ExtractLine(loadoutLines, "FlexActive:"), guidToPath, skills),
            SignaturePassive = ParsePassiveDefinition(loadoutLines, "SignaturePassive:"),
            FlexPassive = ParsePassiveDefinition(loadoutLines, "FlexPassive:"),
            MobilityReaction = ParseMobilityDefinition(loadoutLines, "MobilityReaction:")
        };
    }

    internal static BasicAttackDefinition ParseBasicAttackDefinition(string[] lines)
    {
        var basicAttackLines = ExtractSection(lines, "BasicAttack:", 4);
        if (basicAttackLines.Length == 0)
        {
            return new BasicAttackDefinition();
        }

        return new BasicAttackDefinition
        {
            Id = ExtractValue(basicAttackLines, "Id:"),
            NameKey = ExtractValue(basicAttackLines, "NameKey:"),
            DamageType = (DamageTypeValue)ExtractInt(basicAttackLines, "DamageType:"),
            Lane = (ActionLane)ExtractInt(basicAttackLines, "Lane:"),
            LockRule = (ActionLockRule)ExtractInt(basicAttackLines, "LockRule:"),
            TargetRule = ParseTargetRule(basicAttackLines, "TargetRule:", 6),
            Effects = ParseEffectDescriptors(basicAttackLines, "Effects:", 6)
        };
    }

    internal static PassiveDefinition ParsePassiveDefinition(string[] lines, string sectionHeader)
    {
        var passiveLines = ExtractSection(lines, sectionHeader, 4);
        if (passiveLines.Length == 0)
        {
            return new PassiveDefinition();
        }

        return new PassiveDefinition
        {
            Id = ExtractValue(passiveLines, "Id:"),
            NameKey = ExtractValue(passiveLines, "NameKey:"),
            DescriptionKey = ExtractValue(passiveLines, "DescriptionKey:"),
            EffectFamilyId = ExtractValue(passiveLines, "EffectFamilyId:"),
            MutuallyExclusiveGroupId = ExtractValue(passiveLines, "MutuallyExclusiveGroupId:"),
            BudgetCard = ParseBudgetCard(passiveLines, "BudgetCard:", 6) ?? new BudgetCard { Domain = BudgetDomain.Passive },
            RecruitNativeTags = ParseStableTagList(passiveLines, "RecruitNativeTags:"),
            RecruitPlanTags = ParseStableTagList(passiveLines, "RecruitPlanTags:"),
            RecruitScoutTags = ParseStableTagList(passiveLines, "RecruitScoutTags:"),
            ActivationModel = (ActivationModel)ExtractInt(passiveLines, "ActivationModel:"),
            Lane = (ActionLane)ExtractInt(passiveLines, "Lane:"),
            LockRule = (ActionLockRule)ExtractInt(passiveLines, "LockRule:"),
            Effects = ParseEffectDescriptors(passiveLines, "Effects:", 6),
            AllowMirroredOwnedSummonKill = ExtractBool(passiveLines, "AllowMirroredOwnedSummonKill:")
        };
    }

    internal static MobilityDefinition ParseMobilityDefinition(string[] lines, string sectionHeader)
    {
        var mobilityLines = ExtractSection(lines, sectionHeader, 4);
        if (mobilityLines.Length == 0)
        {
            return new MobilityDefinition();
        }

        return new MobilityDefinition
        {
            Id = ExtractValue(mobilityLines, "Id:"),
            NameKey = ExtractValue(mobilityLines, "NameKey:"),
            DescriptionKey = ExtractValue(mobilityLines, "DescriptionKey:"),
            BudgetCard = ParseBudgetCard(mobilityLines, "BudgetCard:", 6) ?? new BudgetCard { Domain = BudgetDomain.Mobility },
            ActivationModel = (ActivationModel)ExtractInt(mobilityLines, "ActivationModel:"),
            Lane = (ActionLane)ExtractInt(mobilityLines, "Lane:"),
            LockRule = (ActionLockRule)ExtractInt(mobilityLines, "LockRule:"),
            TargetRule = ParseTargetRule(mobilityLines, "TargetRule:", 6),
            Effects = ParseEffectDescriptors(mobilityLines, "Effects:", 6)
        };
    }

    internal static List<RecruitBannedPairingDefinition> ParseRecruitBannedPairings(string[] lines, string sectionHeader)
    {
        var result = new List<RecruitBannedPairingDefinition>();
        var index = FindLineIndex(lines, sectionHeader);
        if (index < 0)
        {
            return result;
        }

        for (index++; index < lines.Length; index++)
        {
            var trimmed = lines[index].Trim();
            if (!trimmed.StartsWith("- FlexActiveId:", StringComparison.Ordinal))
            {
                if (GetIndent(lines[index]) <= 2 && trimmed.EndsWith(":", StringComparison.Ordinal))
                {
                    break;
                }

                continue;
            }

            var definition = new RecruitBannedPairingDefinition
            {
                FlexActiveId = trimmed["- FlexActiveId:".Length..].Trim()
            };

            for (index++; index < lines.Length; index++)
            {
                trimmed = lines[index].Trim();
                if (trimmed.StartsWith("- FlexActiveId:", StringComparison.Ordinal)
                    || (GetIndent(lines[index]) <= 2 && trimmed.EndsWith(":", StringComparison.Ordinal)))
                {
                    index--;
                    break;
                }

                if (trimmed.StartsWith("FlexPassiveId:", StringComparison.Ordinal))
                {
                    definition.FlexPassiveId = trimmed["FlexPassiveId:".Length..].Trim();
                }
            }

            result.Add(definition);
        }

        return result;
    }

    internal static List<StableTagDefinition> ParseStableTagList(
        string[] lines,
        string sectionHeader,
        IReadOnlyDictionary<string, string>? guidToPath = null)
    {
        var tags = ItemFileParser.LoadStableTags();
        return guidToPath == null
            ? new List<StableTagDefinition>()
            : ParseReferenceList(lines, sectionHeader, guidToPath, tags);
    }

    internal static void ApplyTraitPoolFallbacks(TraitPoolDefinition definition)
    {
        definition.ArchetypeId = CoalesceId(definition.ArchetypeId, definition.Id.Replace("traitpool_", string.Empty, StringComparison.Ordinal));
        if (definition.PositiveTraits.Count == 3 && definition.NegativeTraits.Count == 3)
        {
            return;
        }

        definition.PositiveTraits = new List<TraitEntry>
        {
            BuildFallbackTrait(definition.ArchetypeId, $"{definition.ArchetypeId}_positive_brave", "phys_power", 2f),
            BuildFallbackTrait(definition.ArchetypeId, $"{definition.ArchetypeId}_positive_sturdy", "armor", 1f),
            BuildFallbackTrait(definition.ArchetypeId, $"{definition.ArchetypeId}_positive_swift", "attack_speed", 1f),
        };
        definition.NegativeTraits = new List<TraitEntry>
        {
            BuildFallbackTrait(definition.ArchetypeId, $"{definition.ArchetypeId}_negative_frail", "max_health", -3f),
            BuildFallbackTrait(definition.ArchetypeId, $"{definition.ArchetypeId}_negative_clumsy", "phys_power", -1f),
            BuildFallbackTrait(definition.ArchetypeId, $"{definition.ArchetypeId}_negative_slow", "attack_speed", -1f),
        };
    }

    internal static void ApplyArchetypeFallbacks(
        UnitArchetypeDefinition definition,
        IReadOnlyDictionary<string, RaceDefinition> races,
        IReadOnlyDictionary<string, ClassDefinition> classes,
        IReadOnlyDictionary<string, TraitPoolDefinition> traitPools,
        IReadOnlyDictionary<string, SkillDefinitionAsset> skills)
    {
        var spec = GetArchetypeFallbackSpec(definition.Id);
        if (spec == null)
        {
            return;
        }

        if (definition.Race == null && races.TryGetValue(spec.RaceId, out var race))
        {
            definition.Race = race;
        }

        if (definition.Class == null && classes.TryGetValue(spec.ClassId, out var @class))
        {
            definition.Class = @class;
        }

        if (definition.TraitPool == null && traitPools.TryGetValue(spec.TraitPoolId, out var traitPool))
        {
            definition.TraitPool = traitPool;
        }

        definition.ScopeKind = ArchetypeScopeValue.Core;
        definition.RoleFamilyTag = Coalesce(definition.RoleFamilyTag, spec.RoleFamilyTag);
        definition.PrimaryWeaponFamilyTag = Coalesce(definition.PrimaryWeaponFamilyTag, spec.WeaponFamilyTag);
        definition.RoleTag = Coalesce(definition.RoleTag is "auto" ? string.Empty : definition.RoleTag, spec.RoleTag);
        definition.RecruitTier = spec.RecruitTier;

        if (definition.Skills == null || definition.Skills.Count == 0)
        {
            definition.Skills = spec.DefaultSkillIds
                .Where(skills.ContainsKey)
                .Select(id => skills[id])
                .ToList();
        }

        if (definition.Loadout != null)
        {
            definition.Loadout.SignatureActive ??= definition.Skills.FirstOrDefault(skill => skill != null && skill.SlotKind == SkillSlotKindValue.CoreActive);
            definition.Loadout.FlexActive ??= definition.Skills.FirstOrDefault(skill => skill != null && skill.SlotKind == SkillSlotKindValue.UtilityActive);
        }

        if (definition.RecruitFlexActivePool == null || definition.RecruitFlexActivePool.Count == 0)
        {
            definition.RecruitFlexActivePool = definition.Skills
                .Where(skill => skill != null && skill.SlotKind == SkillSlotKindValue.UtilityActive)
                .ToList();
        }

        if (definition.RecruitFlexPassivePool == null || definition.RecruitFlexPassivePool.Count == 0)
        {
            definition.RecruitFlexPassivePool = definition.Skills
                .Where(skill => skill != null && skill.SlotKind == SkillSlotKindValue.Support)
                .ToList();
        }

        if (definition.TacticPreset == null || definition.TacticPreset.Count == 0)
        {
            var tacticSkill = definition.Skills.FirstOrDefault(skill => skill != null);
            if (tacticSkill != null)
            {
                definition.TacticPreset = SkillFileParser.BuildFallbackTacticPreset(spec.ClassId, tacticSkill);
            }
        }

        if (definition.BudgetCard == null || definition.BudgetCard.Vector == null || definition.BudgetCard.Vector.FinalScore == 0)
        {
            var rarity = LoopCContentGovernance.FromRecruitTier(definition.RecruitTier);
            var roleProfile = spec.ClassId switch
            {
                "vanguard" => CombatRoleBudgetProfile.Vanguard,
                "ranger" => CombatRoleBudgetProfile.Ranger,
                "mystic" when string.Equals(definition.Id, "priest", StringComparison.Ordinal) => CombatRoleBudgetProfile.Support,
                "mystic" => CombatRoleBudgetProfile.Arcanist,
                "duelist" when string.Equals(definition.Id, "raider", StringComparison.Ordinal) || string.Equals(definition.Id, "reaver", StringComparison.Ordinal) => CombatRoleBudgetProfile.Bruiser,
                "duelist" => CombatRoleBudgetProfile.Duelist,
                _ => CombatRoleBudgetProfile.Vanguard,
            };
            var target = LoopCContentGovernance.UnitBudgetTargets[rarity].Target;
            var vector = roleProfile switch
            {
                CombatRoleBudgetProfile.Vanguard => MakeBudgetVector(10, 4, 36, 24, 4, 6, 8, 8),
                CombatRoleBudgetProfile.Bruiser => MakeBudgetVector(24, 12, 28, 6, 14, 2, 6, 8),
                CombatRoleBudgetProfile.Duelist => MakeBudgetVector(16, 28, 10, 4, 22, 2, 6, 12),
                CombatRoleBudgetProfile.Ranger => MakeBudgetVector(28, 8, 8, 4, 12, 4, 8, 28),
                CombatRoleBudgetProfile.Arcanist => MakeBudgetVector(8, 30, 6, 20, 8, 6, 8, 14),
                CombatRoleBudgetProfile.Support => MakeBudgetVector(6, 6, 18, 8, 6, 32, 6, 18),
                _ => MakeBudgetVector(14, 12, 18, 8, 8, 8, 8, 24),
            };
            AdjustBudgetFinalScore(vector, target);
            var counters = roleProfile switch
            {
                CombatRoleBudgetProfile.Vanguard => new[] { MakeCounter(CounterTool.InterceptPeel, CounterCoverageStrength.Standard) },
                CombatRoleBudgetProfile.Bruiser => new[] { MakeCounter(CounterTool.GuardBreakMultiHit, CounterCoverageStrength.Standard) },
                CombatRoleBudgetProfile.Duelist => new[] { MakeCounter(CounterTool.ArmorShred, CounterCoverageStrength.Standard) },
                CombatRoleBudgetProfile.Ranger => new[] { MakeCounter(CounterTool.TrackingArea, CounterCoverageStrength.Standard) },
                CombatRoleBudgetProfile.Arcanist => new[] { MakeCounter(CounterTool.Exposure, CounterCoverageStrength.Standard), MakeCounter(CounterTool.AntiHealShatter, CounterCoverageStrength.Light) },
                CombatRoleBudgetProfile.Support when rarity == ContentRarity.Common => new[] { MakeCounter(CounterTool.TenacityStability, CounterCoverageStrength.Standard) },
                CombatRoleBudgetProfile.Support => new[] { MakeCounter(CounterTool.TenacityStability, CounterCoverageStrength.Standard), MakeCounter(CounterTool.AntiHealShatter, CounterCoverageStrength.Light) },
                _ => Array.Empty<CounterToolContribution>(),
            };
            var threats = roleProfile switch
            {
                CombatRoleBudgetProfile.Vanguard => new[] { ThreatPattern.ArmorFrontline },
                CombatRoleBudgetProfile.Bruiser or CombatRoleBudgetProfile.Duelist => new[] { ThreatPattern.DiveBackline },
                CombatRoleBudgetProfile.Ranger => new[] { ThreatPattern.EvasiveSkirmish },
                CombatRoleBudgetProfile.Arcanist => new[] { ThreatPattern.ControlChain },
                CombatRoleBudgetProfile.Support => new[] { ThreatPattern.SustainBall },
                _ => Array.Empty<ThreatPattern>(),
            };
            definition.BudgetCard = BuildBudgetCard(BudgetDomain.UnitBlueprint, rarity, PowerBand.Standard, roleProfile, vector, rarity == ContentRarity.Common ? 2 : rarity == ContentRarity.Rare ? 3 : 4, rarity == ContentRarity.Common ? 1 : 2, rarity == ContentRarity.Epic ? 1 : 0, threats, counters);
        }
    }

    internal static TraitEntry BuildFallbackTrait(string archetypeId, string id, string statId, float value)
    {
        return new TraitEntry
        {
            Id = id,
            NameKey = ContentLocalizationTables.BuildTraitNameKey(archetypeId, id),
            DescriptionKey = ContentLocalizationTables.BuildTraitDescriptionKey(archetypeId, id),
            Modifiers = new List<SerializableStatModifier>
            {
                new() { StatId = statId, Value = value },
            },
        };
    }

    internal sealed record ArchetypeFallbackSpec(
        string RaceId,
        string ClassId,
        string TraitPoolId,
        string RoleFamilyTag,
        string WeaponFamilyTag,
        string RoleTag,
        RecruitTier RecruitTier,
        string[] DefaultSkillIds);

    internal static ArchetypeFallbackSpec? GetArchetypeFallbackSpec(string archetypeId)
    {
        return archetypeId switch
        {
            "warden" => new ArchetypeFallbackSpec("human", "vanguard", "traitpool_warden", "vanguard", "shield", "anchor", RecruitTier.Common, new[] { "skill_guardian_core", "skill_warden_utility", "skill_vanguard_passive_1", "skill_vanguard_support_1" }),
            "guardian" => new ArchetypeFallbackSpec("undead", "vanguard", "traitpool_guardian", "vanguard", "shield", "anchor", RecruitTier.Rare, new[] { "skill_guardian_core", "skill_guardian_utility", "skill_vanguard_passive_2", "skill_vanguard_support_2" }),
            "bulwark" => new ArchetypeFallbackSpec("beastkin", "vanguard", "traitpool_bulwark", "vanguard", "shield", "anchor", RecruitTier.Rare, new[] { "skill_bulwark_core", "skill_guardian_core", "skill_warden_utility", "support_guarded" }),
            "slayer" => new ArchetypeFallbackSpec("human", "duelist", "traitpool_slayer", "striker", "blade", "bruiser", RecruitTier.Common, new[] { "skill_slayer_core", "skill_slayer_utility", "skill_duelist_passive_1", "skill_duelist_support_1" }),
            "raider" => new ArchetypeFallbackSpec("beastkin", "duelist", "traitpool_raider", "striker", "blade", "bruiser", RecruitTier.Rare, new[] { "skill_raider_core", "skill_raider_utility", "skill_duelist_passive_2", "skill_duelist_support_2" }),
            "reaver" => new ArchetypeFallbackSpec("undead", "duelist", "traitpool_reaver", "striker", "blade", "bruiser", RecruitTier.Rare, new[] { "skill_reaver_core", "skill_raider_core", "skill_reaver_utility", "support_executioner" }),
            "hunter" => new ArchetypeFallbackSpec("human", "ranger", "traitpool_hunter", "ranger", "bow", "carry", RecruitTier.Common, new[] { "skill_precision_shot", "skill_hunter_utility", "skill_ranger_passive_1", "skill_ranger_support_1" }),
            "scout" => new ArchetypeFallbackSpec("beastkin", "ranger", "traitpool_scout", "ranger", "bow", "carry", RecruitTier.Common, new[] { "skill_scout_core", "skill_scout_utility", "skill_ranger_passive_2", "skill_ranger_support_2" }),
            "marksman" => new ArchetypeFallbackSpec("undead", "ranger", "traitpool_marksman", "ranger", "bow", "carry", RecruitTier.Rare, new[] { "skill_marksman_core", "skill_hunter_utility", "skill_marksman_utility", "support_longshot" }),
            "priest" => new ArchetypeFallbackSpec("human", "mystic", "traitpool_priest", "mystic", "focus", "support", RecruitTier.Common, new[] { "skill_priest_core", "skill_minor_heal", "skill_mystic_passive_1", "skill_mystic_support_1" }),
            "hexer" => new ArchetypeFallbackSpec("undead", "mystic", "traitpool_hexer", "mystic", "focus", "support", RecruitTier.Epic, new[] { "skill_hexer_core", "skill_hexer_utility", "skill_mystic_passive_2", "skill_mystic_support_2" }),
            "shaman" => new ArchetypeFallbackSpec("beastkin", "mystic", "traitpool_shaman", "mystic", "focus", "support", RecruitTier.Epic, new[] { "skill_shaman_core", "skill_priest_core", "skill_shaman_utility", "support_siphon" }),
            _ => null,
        };
    }
}
