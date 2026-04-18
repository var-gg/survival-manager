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

internal static class SkillFileParser
{
    internal static Dictionary<string, SkillDefinitionAsset> LoadSkills(IReadOnlyDictionary<string, string> guidToPath)
    {
        return RuntimeCombatContentFileParser.LoadAssets("Skills", path =>
        {
            var lines = File.ReadAllLines(path);
            var definition = ScriptableObject.CreateInstance<SkillDefinitionAsset>();
            definition.Id = ExtractValue(lines, "Id:");
            definition.NameKey = ContentLocalizationTables.BuildSkillNameKey(definition.Id);
            definition.DescriptionKey = ContentLocalizationTables.BuildSkillDescriptionKey(definition.Id);
            SetLegacyField(definition, "legacyDisplayName", ExtractValue(lines, "DisplayName:"));
            definition.TemplateType = (SkillTemplateTypeValue)ExtractInt(lines, "TemplateType:");
            definition.Kind = (SkillKindValue)ExtractInt(lines, "Kind:");
            definition.SlotKind = (SkillSlotKindValue)ExtractInt(lines, "SlotKind:");
            definition.DamageType = (DamageTypeValue)ExtractInt(lines, "DamageType:");
            definition.Delivery = (SkillDeliveryValue)ExtractInt(lines, "Delivery:");
            definition.TargetRule = (SkillTargetRuleValue)ExtractInt(lines, "TargetRule:");
            definition.Power = ExtractFloat(lines, "Power:");
            definition.Range = ExtractFloat(lines, "Range:");
            definition.RangeMin = ExtractFloat(lines, "RangeMin:");
            definition.RangeMax = ExtractFloat(lines, "RangeMax:");
            definition.Radius = ExtractFloat(lines, "Radius:");
            definition.Width = ExtractFloat(lines, "Width:");
            definition.ArcDegrees = ExtractFloat(lines, "ArcDegrees:");
            definition.PowerFlat = ExtractFloat(lines, "PowerFlat:");
            definition.PhysCoeff = ExtractFloat(lines, "PhysCoeff:");
            definition.MagCoeff = ExtractFloat(lines, "MagCoeff:");
            definition.HealCoeff = ExtractFloat(lines, "HealCoeff:");
            definition.HealthCoeff = ExtractFloat(lines, "HealthCoeff:");
            definition.CanCrit = ExtractBool(lines, "CanCrit:");
            definition.ActivationModel = (ActivationModel)ExtractInt(lines, "ActivationModel:");
            definition.Lane = (ActionLane)ExtractInt(lines, "Lane:");
            definition.LockRule = (ActionLockRule)ExtractInt(lines, "LockRule:");
            definition.AuthorityLayer = (AuthorityLayer)ExtractInt(lines, "AuthorityLayer:");
            definition.BudgetCard = ParseBudgetCard(lines, "BudgetCard:") ?? definition.BudgetCard;
            definition.ManaCost = ExtractFloat(lines, "ManaCost:");
            definition.ResourceCost = ExtractFloat(lines, "ResourceCost:");
            definition.BaseCooldownSeconds = ExtractFloat(lines, "BaseCooldownSeconds:");
            definition.CooldownSeconds = ExtractFloat(lines, "CooldownSeconds:");
            definition.CastWindupSeconds = ExtractFloat(lines, "CastWindupSeconds:");
            definition.RecoverySeconds = ExtractFloat(lines, "RecoverySeconds:");
            definition.PowerBudget = ExtractFloat(lines, "PowerBudget:");
            definition.InterruptRefundScalar = ExtractFloat(lines, "InterruptRefundScalar:");
            definition.AiIntents = ParsePackedEnumList<SkillAiIntentValue>(ExtractValue(lines, "AiIntents:"));
            definition.AiScoreHints = ParseSkillAiScoreHints(lines, "AiScoreHints:");
            definition.AnimationHookId = ExtractValue(lines, "AnimationHookId:");
            definition.VfxHookId = ExtractValue(lines, "VfxHookId:");
            definition.SfxHookId = ExtractValue(lines, "SfxHookId:");
            definition.LearnSource = (SkillLearnSourceValue)ExtractInt(lines, "LearnSource:");
            definition.EffectFamilyId = ExtractValue(lines, "EffectFamilyId:");
            definition.MutuallyExclusiveGroupId = ExtractValue(lines, "MutuallyExclusiveGroupId:");
            definition.RecruitNativeTags = ArchetypeFileParser.ParseStableTagList(lines, "RecruitNativeTags:", guidToPath);
            definition.RecruitPlanTags = ArchetypeFileParser.ParseStableTagList(lines, "RecruitPlanTags:", guidToPath);
            definition.RecruitScoutTags = ArchetypeFileParser.ParseStableTagList(lines, "RecruitScoutTags:", guidToPath);
            definition.TargetRuleData = ParseTargetRule(lines, "TargetRuleData:");
            definition.SummonProfile = ParseSummonProfile(lines, "SummonProfile:");
            definition.Effects = ParseEffectDescriptors(lines, "Effects:");
            definition.CompileTags = ArchetypeFileParser.ParseStableTagList(lines, "CompileTags:", guidToPath);
            definition.RuleModifierTags = ArchetypeFileParser.ParseStableTagList(lines, "RuleModifierTags:", guidToPath);
            definition.SupportAllowedTags = ArchetypeFileParser.ParseStableTagList(lines, "SupportAllowedTags:", guidToPath);
            definition.SupportBlockedTags = ArchetypeFileParser.ParseStableTagList(lines, "SupportBlockedTags:", guidToPath);
            definition.RequiredWeaponTags = ArchetypeFileParser.ParseStableTagList(lines, "RequiredWeaponTags:", guidToPath);
            definition.RequiredClassTags = ArchetypeFileParser.ParseStableTagList(lines, "RequiredClassTags:", guidToPath);
            definition.AppliedStatuses = StatusFileParser.ParseStatusApplicationRules(lines, "AppliedStatuses:");
            definition.CleanseProfileId = ExtractValue(lines, "CleanseProfileId:");
            ApplyFallbackIdentity(definition, path);
            ApplySkillFallbacks(definition);
            return definition;
        }, guidToPath);
    }

    internal static void ApplySkillFallbacks(SkillDefinitionAsset definition)
    {
        definition.AuthorityLayer = AuthorityLayer.Skill;
        if (definition.SlotKind == 0)
        {
            definition.SlotKind = string.Equals(definition.Id, "skill_minor_heal", StringComparison.Ordinal)
                ? SkillSlotKindValue.UtilityActive
                : definition.Id.StartsWith("support_", StringComparison.Ordinal)
                ? SkillSlotKindValue.Support
                : definition.Id.Contains("_passive_", StringComparison.Ordinal)
                    ? SkillSlotKindValue.Passive
                    : definition.Id.Contains("_utility", StringComparison.Ordinal)
                        ? SkillSlotKindValue.UtilityActive
                        : SkillSlotKindValue.CoreActive;
        }

        if (!string.IsNullOrWhiteSpace(definition.CleanseProfileId))
        {
            var tags = ItemFileParser.LoadStableTags();
            if (tags.TryGetValue("cleanse", out var cleanseTag)
                && definition.CompileTags.All(tag => tag == null || !string.Equals(tag.Id, cleanseTag.Id, StringComparison.Ordinal)))
            {
                definition.CompileTags.Add(cleanseTag);
            }
        }

        if (definition.Id.StartsWith("support_", StringComparison.Ordinal) && definition.SupportAllowedTags.Count == 0)
        {
            var tags = ItemFileParser.LoadStableTags();
            var preferredTagId = definition.Id switch
            {
                "support_guarded" or "support_anchored" => "frontline",
                "support_longshot" or "support_hunter_mark" or "support_piercing" => "backline",
                _ => "support",
            };
            if (tags.TryGetValue(preferredTagId, out var allowedTag))
            {
                definition.SupportAllowedTags.Add(allowedTag);
            }
        }

        if (definition.BudgetCard == null || definition.BudgetCard.Vector == null || definition.BudgetCard.Vector.FinalScore == 0)
        {
            var band = definition.SlotKind switch
            {
                SkillSlotKindValue.UtilityActive when string.Equals(definition.Id, "skill_minor_heal", StringComparison.Ordinal) => PowerBand.Standard,
                SkillSlotKindValue.CoreActive => PowerBand.Standard,
                _ => PowerBand.Minor,
            };
            var target = LoopCContentGovernance.PowerBandTargets[band].Target;
            var counters = ResolveSkillCounters(definition);
            var vector = definition.Kind switch
            {
                SkillKindValue.Heal => MakeBudgetVector(0, 0, 0, 0, 0, target / 2, counters.Length > 0 ? 1 : 0, target / 2 - (counters.Length > 0 ? 1 : 0)),
                _ when definition.DamageType == DamageTypeValue.Magical => MakeBudgetVector(target / 4, target / 3, 0, Math.Max(1, target / 4), 0, 0, counters.Length > 0 ? 1 : 0, Math.Max(1, target - (target / 4 + target / 3 + Math.Max(1, target / 4) + (counters.Length > 0 ? 1 : 0)))),
                _ when definition.Delivery == SkillDeliveryValue.Projectile || definition.Delivery == SkillDeliveryValue.Ranged => MakeBudgetVector(target / 2, target / 6, 0, 0, 1, 0, counters.Length > 0 ? 1 : 0, target - (target / 2 + target / 6 + 1 + (counters.Length > 0 ? 1 : 0))),
                _ => MakeBudgetVector(target / 3, target / 3, 0, 0, Math.Max(1, target / 6), 0, counters.Length > 0 ? 1 : 0, Math.Max(1, target - (target / 3 + target / 3 + Math.Max(1, target / 6) + (counters.Length > 0 ? 1 : 0)))),
            };
            AdjustBudgetFinalScore(vector, target);
            definition.BudgetCard = BuildBudgetCard(BudgetDomain.Skill, ContentRarity.Common, band, CombatRoleBudgetProfile.None, vector, 2, 1, 0, Array.Empty<ThreatPattern>(), counters);
        }
        else if (definition.BudgetCard.Rarity == ContentRarity.Common)
        {
            definition.BudgetCard.KeywordCount = Math.Min(definition.BudgetCard.KeywordCount, 2);
            definition.BudgetCard.ConditionClauseCount = Math.Min(definition.BudgetCard.ConditionClauseCount, 1);
            definition.BudgetCard.RuleExceptionCount = 0;
            if (LoopCContentGovernance.PowerBandTargets.TryGetValue(definition.BudgetCard.PowerBand, out var target)
                && Math.Abs(definition.BudgetCard.Vector.FinalScore - target.Target) > target.Tolerance)
            {
                AdjustBudgetFinalScore(definition.BudgetCard.Vector, target.Target);
            }
        }
    }

    internal static CounterToolContribution[] ResolveSkillCounters(SkillDefinitionAsset definition)
    {
        if (definition.Id.Contains("shaman_utility", StringComparison.Ordinal) || definition.Id.Contains("lingering", StringComparison.Ordinal))
        {
            return new[] { MakeCounter(CounterTool.CleaveWaveclear, CounterCoverageStrength.Light) };
        }

        if (definition.Id.Contains("hunter_mark", StringComparison.Ordinal) || definition.Id.Contains("longshot", StringComparison.Ordinal) || definition.Id.Contains("piercing", StringComparison.Ordinal))
        {
            return new[] { MakeCounter(CounterTool.TrackingArea, CounterCoverageStrength.Light) };
        }

        if (definition.Id.Contains("hexer", StringComparison.Ordinal) || definition.AppliedStatuses.Any(status => string.Equals(status.StatusId, "silence", StringComparison.Ordinal)))
        {
            return new[] { MakeCounter(CounterTool.Exposure, CounterCoverageStrength.Light) };
        }

        if (definition.Id.Contains("purifying", StringComparison.Ordinal) || !string.IsNullOrWhiteSpace(definition.CleanseProfileId))
        {
            return new[] { MakeCounter(CounterTool.TenacityStability, CounterCoverageStrength.Light) };
        }

        return Array.Empty<CounterToolContribution>();
    }

    internal static List<TacticPresetEntry> BuildFallbackTacticPreset(string classId, SkillDefinitionAsset skill)
    {
        if (classId == "mystic")
        {
            return new List<TacticPresetEntry>
            {
                new() { Priority = 0, ConditionType = TacticConditionTypeValue.AllyHpBelow, Threshold = 0.6f, ActionType = BattleActionTypeValue.ActiveSkill, TargetSelector = TargetSelectorTypeValue.LowestHpAlly, Skill = skill },
                new() { Priority = 1, ConditionType = TacticConditionTypeValue.EnemyExposed, Threshold = 1.5f, ActionType = BattleActionTypeValue.BasicAttack, TargetSelector = TargetSelectorTypeValue.MostExposedEnemy },
                new() { Priority = 2, ConditionType = TacticConditionTypeValue.Fallback, Threshold = 0f, ActionType = BattleActionTypeValue.WaitDefend, TargetSelector = TargetSelectorTypeValue.Self },
            };
        }

        return new List<TacticPresetEntry>
        {
            new() { Priority = 0, ConditionType = TacticConditionTypeValue.EnemyExposed, Threshold = 1.5f, ActionType = BattleActionTypeValue.ActiveSkill, TargetSelector = TargetSelectorTypeValue.MostExposedEnemy, Skill = skill },
            new() { Priority = 1, ConditionType = TacticConditionTypeValue.LowestHpEnemy, Threshold = 0f, ActionType = BattleActionTypeValue.BasicAttack, TargetSelector = TargetSelectorTypeValue.LowestHpEnemy },
            new() { Priority = 2, ConditionType = TacticConditionTypeValue.Fallback, Threshold = 0f, ActionType = BattleActionTypeValue.WaitDefend, TargetSelector = TargetSelectorTypeValue.Self },
        };
    }
}
