using System;
using System.Collections.Generic;
using System.IO;
using SM.Content.Definitions;
using SM.Core.Content;
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
            definition.AppliesPeriodicDamage = ExtractBool(lines, "AppliesPeriodicDamage:");
            definition.IncomingDamageDelta = ExtractFloat(lines, "IncomingDamageDelta:");
            // 기본 1(=magnitude 직소비) 필드 — 미저작 asset이 0으로 추락해 채널이 통째로 꺼지지 않도록 fallback 필수.
            definition.MagnitudeScale = ExtractFloat(lines, "MagnitudeScale:", 1f);
            // enum 0=Flat이라 구버전 asset도 기존 절대량 기본 계약으로 결정적으로 폴백한다.
            definition.MagnitudeUnit = (MagnitudeUnit)ExtractInt(lines, "MagnitudeUnit:");
            definition.GrantsBarrierOnApply = ExtractBool(lines, "GrantsBarrierOnApply:");
            definition.GrantsUnstoppable = ExtractBool(lines, "GrantsUnstoppable:");
            definition.BlocksActiveSkills = ExtractBool(lines, "BlocksActiveSkills:");
            definition.BlocksMovement = ExtractBool(lines, "BlocksMovement:");
            definition.BlocksAction = ExtractBool(lines, "BlocksAction:");
            definition.AmplifiesIncomingDamage = ExtractBool(lines, "AmplifiesIncomingDamage:");
            definition.GrantsGuardedDefense = ExtractBool(lines, "GrantsGuardedDefense:");
            definition.ShredsDefense = ExtractBool(lines, "ShredsDefense:");
            definition.ReducesHealing = ExtractBool(lines, "ReducesHealing:");
            definition.DampensTempo = ExtractBool(lines, "DampensTempo:");
            definition.MarksTarget = ExtractBool(lines, "MarksTarget:");
            definition.VfxCueId = ExtractValue(lines, "VfxCueId:");
            definition.SfxHookId = ExtractValue(lines, "SfxHookId:");
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
            // 부여 상태 id 미저작(구버전 asset)은 기존 리터럴과 동일한 "unstoppable"로 접는다(기본 non-zero 필드 함정 축).
            var grantedStatusId = ExtractValue(lines, "GrantedStatusId:");
            definition.GrantedStatusId = string.IsNullOrWhiteSpace(grantedStatusId) ? "unstoppable" : grantedStatusId;
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
                else if (trimmed.StartsWith("StackCap:", StringComparison.Ordinal))
                {
                    rule.StackCap = ParseInt(trimmed["StackCap:".Length..].Trim());
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
        // 저작값 우선 + id 폴백(효과 종류 데이터화 3e 동반 교정) — 과거 강제 대입은 파일 파서 레인에서
        // 신규 hard-control 저작을 소거했다. committed 3종(root/silence/stun)은 저작값 1이라 동작 항등.
        definition.IsHardControl = definition.IsHardControl || definition.Id is "root" or "silence" or "stun";
        definition.UsesControlDiminishing = definition.UsesControlDiminishing || definition.IsHardControl;
        definition.AppliesPeriodicDamage = definition.AppliesPeriodicDamage || definition.Id is "burn" or "bleed";
        // 미저작(구버전) asset 안전망 — barrier의 즉시 보호막 전환이 파서 레인에서 꺼지지 않게.
        // 신규 family 저작은 || 라 저작값이 그대로 살아난다(효과 종류 데이터화 3보).
        definition.GrantsBarrierOnApply = definition.GrantsBarrierOnApply || definition.Id is "barrier";
        // 동일 축 안전망(3b) — unstoppable의 저지불가 kind가 파서 레인에서 꺼지지 않게.
        definition.GrantsUnstoppable = definition.GrantsUnstoppable || definition.Id is "unstoppable";
        // 동일 축 안전망(3c) — silence의 액티브 시전 차단 kind가 파서 레인에서 꺼지지 않게.
        definition.BlocksActiveSkills = definition.BlocksActiveSkills || definition.Id is "silence";
        // 동일 축 안전망(3d) — root의 자발 이동 차단 kind가 파서 레인에서 꺼지지 않게.
        definition.BlocksMovement = definition.BlocksMovement || definition.Id is "root";
        // 동일 축 안전망(3e) — stun의 행동 차단 kind가 파서 레인에서 꺼지지 않게.
        definition.BlocksAction = definition.BlocksAction || definition.Id is "stun";
        // 동일 축 안전망(3f) — 채널 membership 5종이 파서 레인에서 꺼지지 않게.
        definition.AmplifiesIncomingDamage = definition.AmplifiesIncomingDamage || definition.Id is "marked" or "exposed";
        definition.GrantsGuardedDefense = definition.GrantsGuardedDefense || definition.Id is "guarded";
        definition.ShredsDefense = definition.ShredsDefense || definition.Id is "sunder";
        definition.ReducesHealing = definition.ReducesHealing || definition.Id is "wound";
        definition.DampensTempo = definition.DampensTempo || definition.Id is "slow";
        definition.MarksTarget = definition.MarksTarget || definition.Id is "marked";
        if (string.IsNullOrWhiteSpace(definition.VfxCueId))
        {
            definition.VfxCueId = $"vfx.status_{definition.Id}";
        }

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
        definition.BudgetCard = BuildBudgetCard(BudgetDomain.Status, ContentRarity.Common, band, CombatRoleBudgetProfile.None, vector, isMinor ? 2 : 1, 0, 0, threats, counters);
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
