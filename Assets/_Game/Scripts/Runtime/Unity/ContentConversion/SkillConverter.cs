using System;
using System.Collections.Generic;
using System.Linq;
using SM.Combat.Model;
using SM.Combat.Services;
using SM.Content.Definitions;
using SM.Core.Content;
using SM.Core.Contracts;
using SM.Core.Stats;
using SM.Meta.Model;
using UnityEngine;
using static SM.Unity.ContentConversion.ContentConversionShared;
using static SM.Unity.ContentConversion.ContentFallbackData;

namespace SM.Unity.ContentConversion;

internal static class SkillConverter
{
    internal static BattleSkillSpec BuildSkillSpec(SkillDefinitionAsset skill)
    {
        var fallback = ResolveRecruitSkillFallback(skill);
        IReadOnlyList<string> recruitNativeTags = ExtractTagIds(skill.RecruitNativeTags);
        if (recruitNativeTags.Count == 0)
        {
            recruitNativeTags = fallback.NativeTags;
        }

        IReadOnlyList<string> recruitPlanTags = ExtractTagIds(skill.RecruitPlanTags);
        if (recruitPlanTags.Count == 0)
        {
            recruitPlanTags = fallback.PlanTags;
        }

        IReadOnlyList<string> recruitScoutTags = ExtractTagIds(skill.RecruitScoutTags);
        if (recruitScoutTags.Count == 0)
        {
            recruitScoutTags = fallback.ScoutTags;
        }

        return new BattleSkillSpec(
            skill.Id,
            ResolveLegacyName(skill.NameKey, skill.LegacyDisplayName, skill.Id),
            (SkillKind)skill.Kind,
            skill.Power,
            skill.Range,
            skill.SlotKind switch
            {
                SkillSlotKindValue.UtilityActive => CompiledSkillSlots.UtilityActive,
                SkillSlotKindValue.Passive => CompiledSkillSlots.Passive,
                SkillSlotKindValue.Support => CompiledSkillSlots.Support,
                _ => CompiledSkillSlots.CoreActive,
            },
            ExtractTagIds(skill.CompileTags),
            skill.DamageType switch
            {
                DamageTypeValue.Magical => DamageType.Magical,
                DamageTypeValue.Healing => DamageType.Healing,
                DamageTypeValue.True => DamageType.True,
                _ => DamageType.Physical,
            },
            skill.PowerFlat,
            skill.PhysCoeff,
            skill.MagCoeff,
            skill.HealCoeff,
            skill.ManaCost,
            skill.BaseCooldownSeconds,
            skill.CastWindupSeconds,
            ExtractTagIds(skill.RuleModifierTags),
            skill.HealthCoeff,
            skill.CanCrit,
            (SkillDelivery)skill.Delivery,
            (SkillTargetRule)skill.TargetRule,
            ExtractTagIds(skill.SupportAllowedTags),
            ExtractTagIds(skill.SupportBlockedTags),
            ExtractTagIds(skill.RequiredWeaponTags),
            ExtractTagIds(skill.RequiredClassTags),
            Enumerate(skill.AppliedStatuses)
                .Where(rule => rule != null && !string.IsNullOrWhiteSpace(rule.StatusId))
                .Select(rule => new StatusApplicationSpec(
                    string.IsNullOrWhiteSpace(rule.Id) ? $"{skill.Id}:{rule.StatusId}" : rule.Id,
                    rule.StatusId,
                    rule.DurationSeconds,
                    rule.Magnitude,
                    Math.Max(1, rule.MaxStacks),
                    rule.RefreshDurationOnReapply))
                .ToList(),
            skill.CleanseProfileId ?? string.Empty,
            CompiledSkillSlots.ToActionSlotKind(skill.SlotKind switch
            {
                SkillSlotKindValue.UtilityActive => CompiledSkillSlots.UtilityActive,
                SkillSlotKindValue.Passive => CompiledSkillSlots.Passive,
                SkillSlotKindValue.Support => CompiledSkillSlots.Support,
                _ => CompiledSkillSlots.CoreActive,
            }),
            skill.ActivationModel,
            skill.Lane,
            skill.LockRule,
            skill.AuthorityLayer,
            CloneTargetRule(skill.TargetRuleData),
            CloneEffects(skill.Effects),
            CloneSummonProfile(skill.SummonProfile),
            Mathf.Clamp01(skill.InterruptRefundScalar),
            string.IsNullOrWhiteSpace(skill.EffectFamilyId) ? fallback.EffectFamilyId : skill.EffectFamilyId,
            string.IsNullOrWhiteSpace(skill.MutuallyExclusiveGroupId) ? fallback.MutuallyExclusiveGroupId : skill.MutuallyExclusiveGroupId,
            recruitNativeTags,
            recruitPlanTags,
            recruitScoutTags,
            BuildGovernanceSummary(skill.BudgetCard));
    }

    internal static BattleBasicAttackSpec BuildBasicAttackSpec(UnitArchetypeDefinition definition)
    {
        var authored = definition.Loadout?.BasicAttack;
        return new BattleBasicAttackSpec(
            string.IsNullOrWhiteSpace(authored?.Id) ? $"{definition.Id}:basic_attack" : authored.Id,
            ResolveLegacyName(authored?.NameKey ?? string.Empty, string.Empty, "Basic Attack"),
            authored?.DamageType switch
            {
                DamageTypeValue.Magical => DamageType.Magical,
                DamageTypeValue.Healing => DamageType.Healing,
                DamageTypeValue.True => DamageType.True,
                _ => DamageType.Physical,
            },
            CloneTargetRule(authored?.TargetRule) ?? new TargetRule(),
            authored?.Lane ?? ActionLane.Primary,
            authored?.LockRule ?? ActionLockRule.SoftCommit,
            CloneEffects(authored?.Effects),
            ResolveBasicAttackActionProfile(definition),
            WeaponHandedness: authored?.WeaponHandedness
                              ?? HandednessDecisionService.ResolveWeaponProfile(definition.PrimaryWeaponFamilyTag));
    }

    internal static BattlePassiveSpec BuildPassiveSpec(
        PassiveDefinition? authored,
        BattleSkillSpec? fallbackSkill,
        ActionSlotKind slotKind,
        string fallbackId,
        string fallbackName)
    {
        return new BattlePassiveSpec(
            string.IsNullOrWhiteSpace(authored?.Id) ? fallbackSkill?.Id ?? fallbackId : authored.Id,
            ResolveLegacyName(authored?.NameKey ?? string.Empty, string.Empty, fallbackSkill?.Name ?? fallbackName),
            slotKind,
            authored?.ActivationModel ?? ActivationModel.Passive,
            authored != null ? CloneEffects(authored.Effects) : fallbackSkill?.EffectDescriptors ?? Array.Empty<EffectDescriptor>(),
            authored?.AllowMirroredOwnedSummonKill ?? false,
            authored?.EffectFamilyId ?? fallbackSkill?.EffectFamilyId ?? string.Empty,
            BuildGovernanceSummary(authored?.BudgetCard));
    }

    internal static BattleMobilitySpec BuildMobilitySpec(UnitArchetypeDefinition definition, MobilityDefinition authored)
    {
        var profile = authored.Profile == null
            ? ArchetypeConverter.BuildMobilityProfile(definition) ?? new MobilityActionProfile(MobilityStyle.None, MobilityPurpose.None, 0f, 0f, 0f, 0f, 0f, 0f, 0f)
            : new MobilityActionProfile(
                (MobilityStyle)authored.Profile.Style,
                (MobilityPurpose)authored.Profile.Purpose,
                Mathf.Max(0f, authored.Profile.Distance),
                Mathf.Max(0f, authored.Profile.Cooldown),
                Mathf.Max(0f, authored.Profile.CastTime),
                Mathf.Max(0f, authored.Profile.Recovery),
                Mathf.Max(0f, authored.Profile.TriggerMinDistance),
                Mathf.Max(0f, authored.Profile.TriggerMaxDistance),
                Mathf.Clamp(authored.Profile.LateralBias, -1f, 1f));
        return new BattleMobilitySpec(
            string.IsNullOrWhiteSpace(authored.Id) ? $"{definition.Id}:mobility" : authored.Id,
            ResolveLegacyName(authored.NameKey, string.Empty, "Mobility Reaction"),
            profile,
            CloneTargetRule(authored.TargetRule) ?? new TargetRule(),
            authored.ActivationModel,
            authored.Lane,
            authored.LockRule,
            CloneEffects(authored.Effects),
            BuildGovernanceSummary(authored.BudgetCard));
    }

    internal static IReadOnlyList<EffectDescriptor> CloneEffects(IEnumerable<EffectDescriptor>? effects)
    {
        if (effects == null)
        {
            return Array.Empty<EffectDescriptor>();
        }

        return effects
            .Where(effect => effect != null)
            .Select(effect => new EffectDescriptor
            {
                Layer = effect.Layer,
                Scope = effect.Scope,
                Capabilities = effect.Capabilities,
                AllowMirroredOwnedSummonKill = effect.AllowMirroredOwnedSummonKill,
                AllowsPersistentSummonChain = effect.AllowsPersistentSummonChain,
                LoadoutTopologyDelta = effect.LoadoutTopologyDelta,
            })
            .ToList();
    }

    internal static TargetRule? CloneTargetRule(TargetRule? rule)
    {
        if (rule == null)
        {
            return null;
        }

        return new TargetRule
        {
            Domain = rule.Domain,
            PrimarySelector = rule.PrimarySelector,
            FallbackPolicy = rule.FallbackPolicy,
            Filters = rule.Filters,
            ReevaluateIntervalSeconds = rule.ReevaluateIntervalSeconds,
            MinimumCommitSeconds = rule.MinimumCommitSeconds,
            MaxAcquireRange = rule.MaxAcquireRange,
            PreferredMinTargets = rule.PreferredMinTargets,
            ClusterRadius = rule.ClusterRadius,
            LockTargetAtCastStart = rule.LockTargetAtCastStart,
            RetargetLockMode = rule.RetargetLockMode,
        };
    }

    internal static SummonProfile? CloneSummonProfile(SummonProfile? profile)
    {
        if (profile == null)
        {
            return null;
        }

        return new SummonProfile
        {
            EntityKind = profile.EntityKind,
            BehaviorKind = profile.BehaviorKind,
            Eligibility = profile.Eligibility,
            CreditPolicy = profile.CreditPolicy,
            MaxConcurrentPerSource = profile.MaxConcurrentPerSource,
            MaxConcurrentPerOwner = profile.MaxConcurrentPerOwner,
            DespawnOnOwnerDeath = profile.DespawnOnOwnerDeath,
            OwnerDeathDespawnDelaySeconds = profile.OwnerDeathDespawnDelaySeconds,
            InheritOwnerTarget = profile.InheritOwnerTarget,
            IsPersistent = profile.IsPersistent,
            Inheritance = profile.Inheritance,
        };
    }

    private static BasicAttackActionProfile ResolveBasicAttackActionProfile(UnitArchetypeDefinition definition)
    {
        var tag = $"{definition.LockedAttackProfileId} {definition.LockedAttackProfileTag}".ToLowerInvariant();
        if (tag.Contains("dash", StringComparison.Ordinal))
        {
            return BasicAttackActionProfile.DashStrike;
        }

        if (tag.Contains("lunge", StringComparison.Ordinal))
        {
            return BasicAttackActionProfile.LungeStrike;
        }

        if (tag.Contains("step", StringComparison.Ordinal) || tag.Contains("advance", StringComparison.Ordinal))
        {
            return BasicAttackActionProfile.StepInStrike;
        }

        if (tag.Contains("stationary", StringComparison.Ordinal) || tag.Contains("stand", StringComparison.Ordinal))
        {
            return BasicAttackActionProfile.StationaryStrike;
        }

        return BasicAttackActionProfile.Auto;
    }

    private static RecruitSkillFallback ResolveRecruitSkillFallback(SkillDefinitionAsset skill)
    {
        if (LoopBRecruitSkillFallbacks.TryGetValue(skill.Id, out var fallback))
        {
            return fallback;
        }

        var inferred = InferSkillRecruitTags(skill);
        return new RecruitSkillFallback(skill.Id, string.Empty, inferred, inferred, inferred);
    }

    private static string[] InferSkillRecruitTags(SkillDefinitionAsset skill)
    {
        var tags = new HashSet<string>(StringComparer.Ordinal);
        var classTags = ExtractTagIds(skill.RequiredClassTags);
        foreach (var classTag in classTags)
        {
            tags.Add(classTag);
            if (string.Equals(classTag, "vanguard", StringComparison.Ordinal) || string.Equals(classTag, "duelist", StringComparison.Ordinal))
            {
                tags.Add("frontline");
            }
            else if (string.Equals(classTag, "ranger", StringComparison.Ordinal) || string.Equals(classTag, "mystic", StringComparison.Ordinal))
            {
                tags.Add("backline");
            }
        }

        if (skill.DamageType == DamageTypeValue.Healing || skill.Kind == SkillKindValue.Heal)
        {
            tags.Add("support");
            tags.Add("heal");
        }
        else if (skill.DamageType == DamageTypeValue.Magical)
        {
            tags.Add("magical");
        }
        else if (skill.DamageType == DamageTypeValue.Physical || skill.DamageType == DamageTypeValue.True)
        {
            tags.Add("physical");
        }

        return tags.Where(tag => !string.IsNullOrWhiteSpace(tag)).ToArray();
    }
}
