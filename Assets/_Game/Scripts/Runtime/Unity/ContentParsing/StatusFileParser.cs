using System;
using System.Collections.Generic;
using System.IO;
using SM.Content.Definitions;
using SM.Core.Contracts;
using SM.Core.Stats;
using UnityEngine;
using static SM.Unity.ContentParsing.YamlFieldExtractor;

namespace SM.Unity.ContentParsing;

internal static class StatusFileParser
{
    internal static Dictionary<string, StatusFamilyDefinition> LoadStatusFamilies()
    {
        return RuntimeCombatContentFileParser.LoadAssets("StatusFamilies", path =>
        {
            var lines = File.ReadAllLines(path);
            var definition = ScriptableObject.CreateInstance<StatusFamilyDefinition>();
            definition.Id = ExtractValue(lines, "Id:");
            definition.NameKey = ExtractValue(lines, "NameKey:");
            definition.DescriptionKey = ExtractValue(lines, "DescriptionKey:");
            definition.Group = (StatusGroupValue)ExtractInt(lines, "Group:");
            definition.IsHardControl = ExtractBool(lines, "IsHardControl:");
            definition.UsesControlDiminishing = ExtractBool(lines, "UsesControlDiminishing:");
            definition.AffectedByTenacity = ExtractBool(lines, "AffectedByTenacity:");
            definition.TenacityScale = ExtractFloat(lines, "TenacityScale:");
            definition.BudgetCard = ParseBudgetCard(lines, "BudgetCard:") ?? definition.BudgetCard;
            definition.IsRuleModifierOnly = ExtractBool(lines, "IsRuleModifierOnly:");
            definition.AuthorityLayer = (AuthorityLayer)ExtractInt(lines, "AuthorityLayer:");
            definition.DefaultStackCap = ExtractInt(lines, "DefaultStackCap:");
            definition.DefaultStackPolicy = (StatusStackPolicyValue)ExtractInt(lines, "DefaultStackPolicy:");
            definition.DefaultRefreshPolicy = (StatusRefreshPolicyValue)ExtractInt(lines, "DefaultRefreshPolicy:");
            definition.DefaultProcAttributionPolicy = (StatusProcAttributionPolicyValue)ExtractInt(lines, "DefaultProcAttributionPolicy:");
            definition.DefaultOwnershipPolicy = (StatusOwnershipPolicyValue)ExtractInt(lines, "DefaultOwnershipPolicy:");
            definition.IsAiRelevant = ExtractBool(lines, "IsAiRelevant:");
            definition.VisualPriority = ExtractInt(lines, "VisualPriority:");
            definition.Effects = ParseEffectDescriptors(lines, "Effects:");
            definition.CompileTags = ParseStringList(lines, "CompileTags:");
            SetLegacyField(definition, "legacyDisplayName", ExtractValue(lines, "legacyDisplayName:"));
            SetLegacyField(definition, "legacyDescription", ExtractValue(lines, "legacyDescription:"));
            ApplyFallbackIdentity(definition, path);
            ApplyStatusFamilyFallbacks(definition);
            return definition;
        });
    }

    internal static Dictionary<string, CleanseProfileDefinition> LoadCleanseProfiles()
    {
        return RuntimeCombatContentFileParser.LoadAssets("CleanseProfiles", path =>
        {
            var lines = File.ReadAllLines(path);
            var definition = ScriptableObject.CreateInstance<CleanseProfileDefinition>();
            definition.Id = ExtractValue(lines, "Id:");
            definition.NameKey = ExtractValue(lines, "NameKey:");
            definition.DescriptionKey = ExtractValue(lines, "DescriptionKey:");
            definition.RemovesStatusIds = ParseStringList(lines, "RemovesStatusIds:");
            definition.RemovesOneHardControl = ExtractBool(lines, "RemovesOneHardControl:");
            definition.GrantsUnstoppable = ExtractBool(lines, "GrantsUnstoppable:");
            definition.GrantedUnstoppableDurationSeconds = ExtractFloat(lines, "GrantedUnstoppableDurationSeconds:");
            SetLegacyField(definition, "legacyDisplayName", ExtractValue(lines, "legacyDisplayName:"));
            SetLegacyField(definition, "legacyDescription", ExtractValue(lines, "legacyDescription:"));
            ApplyFallbackIdentity(definition, path);
            ApplyCleanseProfileFallbacks(definition);
            return definition;
        });
    }

    internal static Dictionary<string, ControlDiminishingRuleDefinition> LoadControlDiminishingRules()
    {
        return RuntimeCombatContentFileParser.LoadAssets("ControlDiminishingRules", path =>
        {
            var lines = File.ReadAllLines(path);
            var definition = ScriptableObject.CreateInstance<ControlDiminishingRuleDefinition>();
            definition.Id = ExtractValue(lines, "Id:");
            definition.NameKey = ExtractValue(lines, "NameKey:");
            definition.DescriptionKey = ExtractValue(lines, "DescriptionKey:");
            definition.ControlResistMultiplier = ExtractFloat(lines, "ControlResistMultiplier:");
            definition.WindowSeconds = ExtractFloat(lines, "WindowSeconds:");
            definition.FullTenacityStatusIds = ParseStringList(lines, "FullTenacityStatusIds:");
            definition.PartialTenacityStatusIds = ParseStringList(lines, "PartialTenacityStatusIds:");
            SetLegacyField(definition, "legacyDisplayName", ExtractValue(lines, "legacyDisplayName:"));
            SetLegacyField(definition, "legacyDescription", ExtractValue(lines, "legacyDescription:"));
            ApplyFallbackIdentity(definition, path);
            ApplyControlRuleFallbacks(definition);
            return definition;
        });
    }

    internal static Dictionary<string, TraitTokenDefinition> LoadTraitTokens()
    {
        return RuntimeCombatContentFileParser.LoadAssets("TraitTokens", path =>
        {
            var lines = File.ReadAllLines(path);
            var definition = ScriptableObject.CreateInstance<TraitTokenDefinition>();
            definition.Id = ExtractValue(lines, "Id:");
            definition.NameKey = ExtractValue(lines, "NameKey:");
            definition.DescriptionKey = ExtractValue(lines, "DescriptionKey:");
            definition.RewardType = (RewardType)ExtractInt(lines, "RewardType:");
            SetLegacyField(definition, "legacyDisplayName", ExtractValue(lines, "legacyDisplayName:"));
            SetLegacyField(definition, "legacyDescription", ExtractValue(lines, "legacyDescription:"));
            ApplyFallbackIdentity(definition, path);
            ApplyTraitTokenFallbacks(definition);
            return definition;
        });
    }

    internal static List<StatusApplicationRule> ParseStatusApplicationRules(string[] lines, string sectionHeader)
    {
        var result = new List<StatusApplicationRule>();
        var index = FindLineIndex(lines, sectionHeader);
        if (index < 0)
        {
            return result;
        }

        for (index++; index < lines.Length; index++)
        {
            var trimmed = lines[index].Trim();
            if (!trimmed.StartsWith("- Id:", StringComparison.Ordinal))
            {
                if (GetIndent(lines[index]) <= 2 && trimmed.EndsWith(":", StringComparison.Ordinal))
                {
                    break;
                }

                continue;
            }

            var rule = new StatusApplicationRule
            {
                Id = trimmed["- Id:".Length..].Trim()
            };

            for (index++; index < lines.Length; index++)
            {
                trimmed = lines[index].Trim();
                if (trimmed.StartsWith("- Id:", StringComparison.Ordinal)
                    || (GetIndent(lines[index]) <= 2 && trimmed.EndsWith(":", StringComparison.Ordinal)))
                {
                    index--;
                    break;
                }

                if (trimmed.StartsWith("StatusId:", StringComparison.Ordinal))
                {
                    rule.StatusId = trimmed["StatusId:".Length..].Trim();
                }
                else if (trimmed.StartsWith("DurationSeconds:", StringComparison.Ordinal))
                {
                    rule.DurationSeconds = ParseFloat(trimmed["DurationSeconds:".Length..].Trim());
                }
                else if (trimmed.StartsWith("Magnitude:", StringComparison.Ordinal))
                {
                    rule.Magnitude = ParseFloat(trimmed["Magnitude:".Length..].Trim());
                }
                else if (trimmed.StartsWith("MaxStacks:", StringComparison.Ordinal))
                {
                    rule.MaxStacks = ParseInt(trimmed["MaxStacks:".Length..].Trim());
                }
                else if (trimmed.StartsWith("RefreshDurationOnReapply:", StringComparison.Ordinal))
                {
                    rule.RefreshDurationOnReapply = ParseBool(trimmed["RefreshDurationOnReapply:".Length..].Trim());
                }
            }

            result.Add(rule);
        }

        return result;
    }

    internal static void ApplyStatusFamilyFallbacks(StatusFamilyDefinition definition)
    {
        definition.DefaultStackCap = Math.Max(definition.DefaultStackCap, 1);
        definition.VisualPriority = Math.Max(definition.VisualPriority, 0);
        definition.IsAiRelevant = true;
        definition.Group = definition.Id switch
        {
            "bleed" or "burn" or "wound" => StatusGroupValue.Attrition,
            "marked" or "exposed" or "sunder" => StatusGroupValue.TacticalMark,
            "barrier" or "guarded" or "unstoppable" => StatusGroupValue.DefensiveBoon,
            _ => definition.Group,
        };
        definition.IsHardControl = definition.Id is "root" or "silence" or "stun";
        definition.UsesControlDiminishing = definition.IsHardControl;

        if (definition.BudgetCard != null && definition.BudgetCard.Vector != null && definition.BudgetCard.Vector.FinalScore > 0)
        {
            return;
        }

        var isMinor = definition.IsHardControl || string.Equals(definition.Id, "root", StringComparison.Ordinal) || string.Equals(definition.Id, "silence", StringComparison.Ordinal);
        var band = isMinor ? PowerBand.Minor : PowerBand.Micro;
        var counters = definition.Id switch
        {
            "sunder" => new[] { MakeCounter(CounterTool.ArmorShred, CounterCoverageStrength.Standard) },
            "exposed" => new[] { MakeCounter(CounterTool.Exposure, CounterCoverageStrength.Standard) },
            "wound" => new[] { MakeCounter(CounterTool.AntiHealShatter, CounterCoverageStrength.Standard) },
            "unstoppable" => new[] { MakeCounter(CounterTool.TenacityStability, CounterCoverageStrength.Standard) },
            _ => Array.Empty<CounterToolContribution>(),
        };
        var threats = definition.Id switch
        {
            "guarded" => new[] { ThreatPattern.GuardBulwark },
            "barrier" => new[] { ThreatPattern.SustainBall },
            "marked" => new[] { ThreatPattern.DiveBackline },
            _ => Array.Empty<ThreatPattern>(),
        };
        var vector = definition.Group switch
        {
            StatusGroupValue.Control => MakeBudgetVector(0, 0, 0, isMinor ? 6 : 3, 0, 0, counters.Length > 0 ? 2 : 0, 0),
            StatusGroupValue.Attrition => MakeBudgetVector(4, 0, 0, 0, 0, 0, counters.Length > 0 ? 2 : 0, 0),
            StatusGroupValue.TacticalMark => MakeBudgetVector(0, 0, 0, 1, 0, 1, counters.Length > 0 ? 3 : 0, 0),
            StatusGroupValue.DefensiveBoon => MakeBudgetVector(0, 0, 2, 0, 0, 2, counters.Length > 0 ? 2 : 0, 0),
            _ => MakeBudgetVector(0, 0, 0, 0, 0, 0, 0, 0),
        };
        AdjustBudgetFinalScore(vector, LoopCContentGovernance.PowerBandTargets[band].Target);
        definition.BudgetCard = BuildBudgetCard(BudgetDomain.Status, ContentRarity.Common, band, null, vector, isMinor ? 2 : 1, 0, 0, threats, counters);
    }

    internal static void ApplyCleanseProfileFallbacks(CleanseProfileDefinition definition)
    {
        if (definition.RemovesStatusIds.Count > 0 || definition.GrantsUnstoppable || definition.RemovesOneHardControl)
        {
            return;
        }

        switch (definition.Id)
        {
            case "cleanse_basic":
                definition.RemovesStatusIds = new List<string> { "slow", "root" };
                break;
            case "cleanse_control":
                definition.RemovesStatusIds = new List<string> { "stun", "silence", "root" };
                definition.RemovesOneHardControl = true;
                break;
            case "break_and_unstoppable":
                definition.RemovesStatusIds = new List<string> { "slow", "root", "stun" };
                definition.RemovesOneHardControl = true;
                definition.GrantsUnstoppable = true;
                definition.GrantedUnstoppableDurationSeconds = 1.5f;
                break;
        }
    }

    internal static void ApplyControlRuleFallbacks(ControlDiminishingRuleDefinition definition)
    {
        if (definition.WindowSeconds <= 0f)
        {
            definition.WindowSeconds = 1.5f;
        }

        if (definition.ControlResistMultiplier <= 0f)
        {
            definition.ControlResistMultiplier = 0.5f;
        }
    }

    internal static void ApplyTraitTokenFallbacks(TraitTokenDefinition definition)
    {
        definition.RewardType = definition.Id switch
        {
            "trait_lock_token" => RewardType.TraitLockToken,
            "trait_purge_token" => RewardType.TraitPurgeToken,
            "trait_reroll_token" => RewardType.TraitRerollCurrency,
            _ => definition.RewardType,
        };
    }
}
