using System;
using System.Collections.Generic;
using System.Linq;
using SM.Combat.Model;
using SM.Content.Definitions;
using SM.Core.Content;
using SM.Core.Contracts;
using SM.Core.Stats;
using SM.Meta.Model;
using UnityEngine;
using static SM.Unity.ContentConversion.ContentConversionShared;
using static SM.Unity.ContentConversion.ContentFallbackData;

namespace SM.Unity.ContentConversion;

internal sealed class ArchetypeConverter
{
    private readonly IReadOnlyDictionary<string, SkillDefinitionAsset> _skillDefinitions;
    private readonly FirstPlayableSliceDefinition? _firstPlayableSlice;

    internal ArchetypeConverter(
        IReadOnlyDictionary<string, SkillDefinitionAsset> skillDefinitions,
        FirstPlayableSliceDefinition? firstPlayableSlice)
    {
        _skillDefinitions = skillDefinitions;
        _firstPlayableSlice = firstPlayableSlice;
    }

    internal CombatArchetypeTemplate BuildArchetypeTemplate(UnitArchetypeDefinition definition)
    {
        var resolvedSkills = ResolveArchetypeSkills(definition);
        var compiledSkills = resolvedSkills
            .Select(SkillConverter.BuildSkillSpec)
            .ToList();
        var loopALoadout = ResolveLoopALoadout(definition, resolvedSkills, compiledSkills);
        var recruitFlexActivePool = ResolveRecruitSkillPool(
                definition.Class?.Id,
            ResolveRecruitPool(
                definition.RecruitFlexActivePool,
                definition.FlexUtilitySkillPool,
                definition.Loadout?.FlexActive ?? resolvedSkills.FirstOrDefault(skill => NormalizeSkillSlot(skill) == SkillSlotKindValue.UtilityActive)),
                LoopBFlexActivePoolFallbacks)
            .Select(SkillConverter.BuildSkillSpec)
            .Where(skill => _firstPlayableSlice == null || _firstPlayableSlice.Contains(ContentKind.FlexActive, skill.Id))
            .Where(skill => skill.EffectiveSlotKind == ActionSlotKind.FlexActive)
            .ToList();
        var recruitFlexPassivePool = ResolveRecruitSkillPool(
                definition.Class?.Id,
                ResolveRecruitPool(
                definition.RecruitFlexPassivePool,
                definition.FlexSupportSkillPool,
                resolvedSkills.FirstOrDefault(skill => NormalizeSkillSlot(skill) == SkillSlotKindValue.Support)),
                LoopBFlexPassivePoolFallbacks)
            .Select(SkillConverter.BuildSkillSpec)
            .Where(skill => _firstPlayableSlice == null || _firstPlayableSlice.Contains(ContentKind.FlexPassive, skill.Id))
            .Where(skill => skill.EffectiveSlotKind == ActionSlotKind.FlexPassive)
            .ToList();
        return new CombatArchetypeTemplate(
            definition.Id,
            ResolveLegacyName(definition.NameKey, definition.LegacyDisplayName, definition.Id),
            definition.Race.Id,
            definition.Class.Id,
            (DeploymentAnchorId)definition.DefaultAnchor,
            BuildBaseStats(definition),
            Enumerate(definition.TacticPreset)
                .OrderBy(entry => entry.Priority)
                .Select(entry => new TacticRule(
                    entry.Priority,
                    (TacticConditionType)entry.ConditionType,
                    entry.Threshold,
                    (BattleActionType)entry.ActionType,
                    (TargetSelectorType)entry.TargetSelector,
                    entry.Skill == null ? null : entry.Skill.Id))
                .ToList(),
            compiledSkills,
            string.IsNullOrWhiteSpace(definition.RoleTag) ? "auto" : definition.RoleTag,
            BuildFootprintProfile(definition),
            BuildBehaviorProfile(definition),
            BuildMobilityProfile(definition),
            PreferPrimaryOrFallback(definition.BasePreferredDistance, definition.BaseAttackRange),
            definition.BaseProtectRadius,
            new ManaEnvelope(
                definition.BaseManaMax,
                definition.BaseManaGainOnAttack,
                definition.BaseManaGainOnHit),
            null,
            ResolveRecruitTier(definition),
            definition.IsRecruitable
            && !definition.IsSummonOnly
            && !definition.IsEventOnly
            && !definition.IsBossOnly
            && !definition.IsUnreleased
            && !definition.IsTestOnly
            && (_firstPlayableSlice == null || _firstPlayableSlice.Contains(ContentKind.UnitBlueprint, definition.Id)),
            ResolveRecruitPlanTags(definition),
            ResolveScoutBiasTags(definition),
            recruitFlexActivePool,
            recruitFlexPassivePool,
            ResolveRecruitBannedPairings(definition),
            loopALoadout.BasicAttack,
            loopALoadout.SignatureActive,
            loopALoadout.FlexActive,
            loopALoadout.SignaturePassive,
            loopALoadout.FlexPassive,
            loopALoadout.MobilityReaction,
            new EnergyProfile(
                Mathf.Max(1f, definition.BaseMaxEnergy),
                Mathf.Clamp(definition.BaseStartingEnergy, 0f, Mathf.Max(1f, definition.BaseMaxEnergy))),
            CombatEntityKind.RosterUnit,
            null,
            null,
            BuildGovernanceSummary(definition.BudgetCard));
    }

    internal static FootprintProfile BuildFootprintProfile(UnitArchetypeDefinition definition)
    {
        if (definition.FootprintProfile != null)
        {
            return new FootprintProfile(
                Mathf.Max(0.15f, definition.FootprintProfile.NavigationRadius),
                Mathf.Max(0.2f, definition.FootprintProfile.SeparationRadius),
                Mathf.Max(0.4f, definition.FootprintProfile.CombatReach),
                new FloatRange(
                    Mathf.Max(0f, definition.FootprintProfile.PreferredRangeMin),
                    Mathf.Max(definition.FootprintProfile.PreferredRangeMin, definition.FootprintProfile.PreferredRangeMax)),
                Mathf.Max(1, definition.FootprintProfile.EngagementSlotCount),
                Mathf.Max(0.4f, definition.FootprintProfile.EngagementSlotRadius),
                (BodySizeCategory)definition.FootprintProfile.BodySizeCategory,
                Mathf.Max(1.1f, definition.FootprintProfile.HeadAnchorHeight));
        }

        var attackRange = Mathf.Max(0.8f, definition.BaseAttackRange);
        var collisionRadius = Mathf.Max(0.15f, definition.BaseCollisionRadius);
        return definition.Class != null ? definition.Class.Id switch
        {
            "vanguard" => new FootprintProfile(
                Mathf.Max(0.45f, collisionRadius + 0.1f),
                Mathf.Max(0.65f, collisionRadius + 0.22f),
                Mathf.Min(1.4f, attackRange),
                new FloatRange(0.9f, 1.25f),
                5,
                1.3f,
                definition.BaseMaxHealth >= 25f || collisionRadius >= 0.55f ? BodySizeCategory.Large : BodySizeCategory.Medium,
                2.15f),
            "duelist" => new FootprintProfile(
                Mathf.Max(0.4f, collisionRadius),
                Mathf.Max(0.62f, collisionRadius + 0.18f),
                Mathf.Min(1.3f, attackRange),
                new FloatRange(0.95f, 1.45f),
                6,
                1.3f,
                BodySizeCategory.Medium,
                2.0f),
            "ranger" => new FootprintProfile(
                Mathf.Max(0.32f, collisionRadius * 0.8f),
                Mathf.Max(0.72f, collisionRadius + 0.24f),
                attackRange,
                new FloatRange(Mathf.Max(2.3f, attackRange - 0.65f), Mathf.Max(2.8f, attackRange + 0.15f)),
                3,
                Mathf.Max(1.5f, attackRange * 0.7f),
                BodySizeCategory.Small,
                1.92f),
            "mystic" => new FootprintProfile(
                Mathf.Max(0.32f, collisionRadius * 0.8f),
                Mathf.Max(0.76f, collisionRadius + 0.28f),
                attackRange,
                new FloatRange(Mathf.Max(2.1f, attackRange - 0.55f), Mathf.Max(2.7f, attackRange + 0.2f)),
                3,
                Mathf.Max(1.45f, attackRange * 0.68f),
                BodySizeCategory.Small,
                1.96f),
            _ => new FootprintProfile(
                Mathf.Max(0.4f, collisionRadius),
                Mathf.Max(0.64f, collisionRadius + 0.2f),
                Mathf.Min(1.3f, attackRange),
                new FloatRange(Mathf.Max(0.9f, attackRange - 0.2f), attackRange),
                4,
                1.2f,
                BodySizeCategory.Medium,
                2f),
        } : new FootprintProfile(
            Mathf.Max(0.4f, collisionRadius),
            Mathf.Max(0.64f, collisionRadius + 0.2f),
            Mathf.Min(1.3f, attackRange),
            new FloatRange(Mathf.Max(0.9f, attackRange - 0.2f), attackRange),
            4,
            1.2f,
            BodySizeCategory.Medium,
            2f);
    }

    internal static BehaviorProfile BuildBehaviorProfile(UnitArchetypeDefinition definition)
    {
        if (definition.BehaviorProfile != null)
        {
            return new BehaviorProfile(
                Mathf.Max(0.1f, definition.BehaviorProfile.ReevaluationInterval),
                Mathf.Max(0f, definition.BehaviorProfile.RangeHysteresis),
                Mathf.Clamp01(definition.BehaviorProfile.RetreatBias),
                Mathf.Clamp01(definition.BehaviorProfile.MaintainRangeBias),
                Mathf.Clamp01(definition.BehaviorProfile.Opportunism),
                Mathf.Clamp01(definition.BehaviorProfile.Discipline),
                Mathf.Clamp01(definition.BehaviorProfile.DodgeChance),
                Mathf.Clamp01(definition.BehaviorProfile.BlockChance),
                Mathf.Clamp(definition.BehaviorProfile.BlockMitigation, 0f, 0.9f),
                Mathf.Clamp01(definition.BehaviorProfile.Stability),
                Mathf.Max(0f, definition.BehaviorProfile.BlockCooldownSeconds),
                definition.BehaviorProfile.FormationLine,
                definition.BehaviorProfile.RangeDiscipline,
                Mathf.Max(0f, definition.BehaviorProfile.PreferredRangeMin),
                Mathf.Max(definition.BehaviorProfile.PreferredRangeMin, definition.BehaviorProfile.PreferredRangeMax),
                Mathf.Max(0f, definition.BehaviorProfile.ApproachBuffer),
                Mathf.Max(0f, definition.BehaviorProfile.RetreatBuffer),
                Mathf.Max(0.5f, definition.BehaviorProfile.ChaseLeashMeters),
                Mathf.Clamp01(definition.BehaviorProfile.RetreatAtHpPercent),
                Mathf.Max(0.5f, definition.BehaviorProfile.FrontlineGuardRadius));
        }

        return definition.Class != null ? definition.Class.Id switch
        {
            "vanguard" => new BehaviorProfile(0.25f, 0.16f, 0.04f, 0.05f, 0.34f, 0.82f, 0.02f, 0.28f, 0.38f, 0.88f, 1f, FormationLine.Frontline, RangeDiscipline.Collapse, 0.9f, 1.25f, 0.4f, 0.2f, 5f, 0f, 3f),
            "duelist" => new BehaviorProfile(0.25f, 0.22f, 0.22f, 0.24f, 0.72f, 0.58f, 0.08f, 0.12f, 0.18f, 0.62f, 1.15f, FormationLine.Frontline, RangeDiscipline.HoldBand, 0.95f, 1.45f, 0.4f, 0.25f, 5.5f, 0.2f, 2.2f),
            "ranger" => new BehaviorProfile(0.25f, 0.28f, 0.72f, 0.84f, 0.58f, 0.74f, 0.12f, 0.04f, 0.12f, 0.34f, 1.5f, FormationLine.Backline, RangeDiscipline.KiteBackward, 2.3f, 3.1f, 0.45f, 0.3f, 6.5f, 0.35f, 1.5f),
            "mystic" => new BehaviorProfile(0.25f, 0.3f, 0.68f, 0.78f, 0.5f, 0.84f, 0.06f, 0.06f, 0.18f, 0.45f, 1.35f, FormationLine.Backline, RangeDiscipline.AnchorNearFrontline, 2.1f, 2.9f, 0.4f, 0.25f, 6f, 0.3f, 1.8f),
            _ => new BehaviorProfile(0.25f, 0.2f, 0.15f, 0.15f, 0.5f, 0.5f, 0.04f, 0.08f, 0.2f, 0.5f, 1.2f, FormationLine.Midline, RangeDiscipline.HoldBand, 1f, 2f, 0.4f, 0.25f, 5f, 0.25f, 2.5f),
        } : new BehaviorProfile(0.25f, 0.2f, 0.15f, 0.15f, 0.5f, 0.5f, 0.04f, 0.08f, 0.2f, 0.5f, 1.2f, FormationLine.Midline, RangeDiscipline.HoldBand, 1f, 2f, 0.4f, 0.25f, 5f, 0.25f, 2.5f);
    }

    internal static MobilityActionProfile? BuildMobilityProfile(UnitArchetypeDefinition definition)
    {
        if (definition.MobilityProfile != null)
        {
            return new MobilityActionProfile(
                (MobilityStyle)definition.MobilityProfile.Style,
                (MobilityPurpose)definition.MobilityProfile.Purpose,
                Mathf.Max(0f, definition.MobilityProfile.Distance),
                Mathf.Max(0f, definition.MobilityProfile.Cooldown),
                Mathf.Max(0f, definition.MobilityProfile.CastTime),
                Mathf.Max(0f, definition.MobilityProfile.Recovery),
                Mathf.Max(0f, definition.MobilityProfile.TriggerMinDistance),
                Mathf.Max(0f, definition.MobilityProfile.TriggerMaxDistance),
                Mathf.Clamp(definition.MobilityProfile.LateralBias, -1f, 1f));
        }

        return definition.Class != null ? definition.Class.Id switch
        {
            "vanguard" => new MobilityActionProfile(MobilityStyle.Dash, MobilityPurpose.Engage, 0.9f, 5f, 0f, 0.18f, 1.4f, 2.8f, 0f),
            "duelist" => new MobilityActionProfile(MobilityStyle.Dash, MobilityPurpose.Engage, 1.15f, 4.2f, 0f, 0.16f, 1.3f, 3f, 0.2f),
            "ranger" => new MobilityActionProfile(MobilityStyle.Roll, MobilityPurpose.MaintainRange, 1.45f, 3.4f, 0f, 0.22f, 0f, 1.45f, 0.68f),
            "mystic" => new MobilityActionProfile(MobilityStyle.Blink, MobilityPurpose.Disengage, 1.85f, 4.4f, 0f, 0.3f, 0f, 1.35f, 0.35f),
            _ => null,
        } : null;
    }

    private IReadOnlyList<SkillDefinitionAsset> ResolveArchetypeSkills(UnitArchetypeDefinition definition)
    {
        var resolved = new List<SkillDefinitionAsset>();
        var occupiedSlots = new HashSet<SkillSlotKindValue>();

        void AddSkill(SkillDefinitionAsset? skill)
        {
            if (skill == null || string.IsNullOrWhiteSpace(skill.Id))
            {
                return;
            }

            var slot = NormalizeSkillSlot(skill);
            if (!occupiedSlots.Add(slot))
            {
                return;
            }

            resolved.Add(skill);
        }

        foreach (var skill in Enumerate(definition.Skills))
        {
            AddSkill(skill);
        }

        foreach (var skillId in GetDefaultArchetypeSkillIds(definition.Id))
        {
            if (_skillDefinitions.TryGetValue(skillId, out var skill))
            {
                AddSkill(skill);
            }
        }

        foreach (var skillId in GetDefaultClassSkillIds(definition.Class != null ? definition.Class.Id : string.Empty))
        {
            if (_skillDefinitions.TryGetValue(skillId, out var skill))
            {
                AddSkill(skill);
            }
        }

        return resolved
            .OrderBy(skill => GetSkillSlotOrder(NormalizeSkillSlot(skill)))
            .ThenBy(skill => skill.Id, StringComparer.Ordinal)
            .ToList();
    }

    private static IEnumerable<string> GetDefaultArchetypeSkillIds(string archetypeId)
    {
        return archetypeId switch
        {
            "warden" => new[] { "skill_power_strike", "skill_warden_utility" },
            "guardian" => new[] { "skill_guardian_core", "skill_guardian_utility" },
            "bulwark" => new[] { "skill_bulwark_core", "skill_bulwark_utility" },
            "slayer" => new[] { "skill_slayer_core", "skill_slayer_utility" },
            "raider" => new[] { "skill_raider_core", "skill_raider_utility" },
            "reaver" => new[] { "skill_reaver_core", "skill_reaver_utility" },
            "hunter" => new[] { "skill_precision_shot", "skill_hunter_utility" },
            "scout" => new[] { "skill_scout_core", "skill_scout_utility" },
            "marksman" => new[] { "skill_marksman_core", "skill_marksman_utility" },
            "priest" => new[] { "skill_priest_core", "skill_minor_heal" },
            "hexer" => new[] { "skill_hexer_core", "skill_hexer_utility" },
            "shaman" => new[] { "skill_shaman_core", "skill_shaman_utility" },
            _ => Array.Empty<string>(),
        };
    }

    private static IEnumerable<string> GetDefaultClassSkillIds(string classId)
    {
        return classId switch
        {
            "vanguard" => new[] { "skill_vanguard_passive_1", "skill_vanguard_support_1" },
            "duelist" => new[] { "skill_duelist_passive_1", "skill_duelist_support_1" },
            "ranger" => new[] { "skill_ranger_passive_1", "skill_ranger_support_1" },
            "mystic" => new[] { "skill_mystic_passive_1", "skill_mystic_support_1" },
            _ => Array.Empty<string>(),
        };
    }

    private static SkillSlotKindValue NormalizeSkillSlot(SkillDefinitionAsset skill)
    {
        return skill.SlotKind switch
        {
            SkillSlotKindValue.UtilityActive => SkillSlotKindValue.UtilityActive,
            SkillSlotKindValue.Passive => SkillSlotKindValue.Passive,
            SkillSlotKindValue.Support => SkillSlotKindValue.Support,
            _ => SkillSlotKindValue.CoreActive,
        };
    }

    private static int GetSkillSlotOrder(SkillSlotKindValue slotKind)
    {
        return slotKind switch
        {
            SkillSlotKindValue.CoreActive => 0,
            SkillSlotKindValue.UtilityActive => 1,
            SkillSlotKindValue.Passive => 2,
            SkillSlotKindValue.Support => 3,
            _ => int.MaxValue,
        };
    }

    private (BattleBasicAttackSpec BasicAttack, BattleSkillSpec? SignatureActive, BattleSkillSpec? FlexActive, BattlePassiveSpec SignaturePassive, BattlePassiveSpec FlexPassive, BattleMobilitySpec? MobilityReaction) ResolveLoopALoadout(
        UnitArchetypeDefinition definition,
        IReadOnlyList<SkillDefinitionAsset> resolvedSkillAssets,
        IReadOnlyList<BattleSkillSpec> compiledSkills)
    {
        var signatureActiveAsset = definition.Loadout?.SignatureActive
            ?? definition.LockedSignatureActiveSkill
            ?? resolvedSkillAssets.FirstOrDefault(skill => NormalizeSkillSlot(skill) == SkillSlotKindValue.CoreActive);
        var flexActiveAsset = definition.Loadout?.FlexActive
            ?? resolvedSkillAssets.FirstOrDefault(skill => NormalizeSkillSlot(skill) == SkillSlotKindValue.UtilityActive);
        var signatureSkill = signatureActiveAsset == null ? null : SkillConverter.BuildSkillSpec(signatureActiveAsset) with
        {
            SlotKind = CompiledSkillSlots.CoreActive,
            ResolvedSlotKind = ActionSlotKind.SignatureActive,
            ActivationModel = ActivationModel.Energy,
            Lane = ActionLane.Primary,
            LockRule = ActionLockRule.HardCommit,
            ManaCost = 0f,
            BaseCooldownSeconds = 0f,
            TargetRuleData = SkillConverter.CloneTargetRule(signatureActiveAsset.TargetRuleData) ?? new TargetRule(),
        };
        var flexSkill = flexActiveAsset == null ? null : SkillConverter.BuildSkillSpec(flexActiveAsset) with
        {
            SlotKind = CompiledSkillSlots.UtilityActive,
            ResolvedSlotKind = ActionSlotKind.FlexActive,
            ActivationModel = flexActiveAsset.ActivationModel is ActivationModel.Trigger ? ActivationModel.Trigger : ActivationModel.Cooldown,
            Lane = ActionLane.Primary,
            LockRule = ActionLockRule.HardCommit,
            TargetRuleData = SkillConverter.CloneTargetRule(flexActiveAsset.TargetRuleData) ?? new TargetRule(),
        };
        var passiveBySlot = compiledSkills
            .Where(skill => skill.EffectiveSlotKind is ActionSlotKind.SignaturePassive or ActionSlotKind.FlexPassive)
            .GroupBy(skill => skill.EffectiveSlotKind)
            .ToDictionary(group => group.Key, group => group.First());
        var signaturePassive = SkillConverter.BuildPassiveSpec(
            definition.Loadout?.SignaturePassive,
            passiveBySlot.TryGetValue(ActionSlotKind.SignaturePassive, out var compiledSignaturePassive) ? compiledSignaturePassive : null,
            ActionSlotKind.SignaturePassive,
            $"{definition.Id}:signature_passive",
            "Signature Passive");
        var flexPassive = SkillConverter.BuildPassiveSpec(
            definition.Loadout?.FlexPassive,
            passiveBySlot.TryGetValue(ActionSlotKind.FlexPassive, out var compiledFlexPassive) ? compiledFlexPassive : null,
            ActionSlotKind.FlexPassive,
            $"{definition.Id}:flex_passive",
            "Flex Passive");
        var defaultMobilityProfile = BuildMobilityProfile(definition);
        var mobilityReaction = definition.Loadout?.MobilityReaction is { Profile: not null } authoredMobility
            ? SkillConverter.BuildMobilitySpec(definition, authoredMobility)
            : defaultMobilityProfile is { IsEnabled: true }
                ? new BattleMobilitySpec(
                    $"{definition.Id}:mobility",
                    "Mobility Reaction",
                    defaultMobilityProfile,
                    new TargetRule(),
                    Governance: BuildGovernanceSummary(definition.Loadout?.MobilityReaction?.BudgetCard))
                : null;
        return (
            SkillConverter.BuildBasicAttackSpec(definition),
            signatureSkill,
            flexSkill,
            signaturePassive,
            flexPassive,
            mobilityReaction);
    }

    private static IReadOnlyList<SkillDefinitionAsset> ResolveRecruitPool(
        IReadOnlyList<SkillDefinitionAsset> primary,
        IReadOnlyList<SkillDefinitionAsset> legacyFallback,
        SkillDefinitionAsset? finalFallback)
    {
        var resolved = Enumerate(primary)
            .Where(skill => skill != null)
            .ToList();
        if (resolved.Count > 0)
        {
            return resolved;
        }

        resolved = Enumerate(legacyFallback)
            .Where(skill => skill != null)
            .ToList();
        if (resolved.Count > 0)
        {
            return resolved;
        }

        if (finalFallback != null)
        {
            resolved.Add(finalFallback);
        }

        return resolved;
    }

    private IReadOnlyList<SkillDefinitionAsset> ResolveRecruitSkillPool(
        string? classId,
        IReadOnlyList<SkillDefinitionAsset> authoredPool,
        IReadOnlyDictionary<string, string[]> fallbackMap)
    {
        if (!string.IsNullOrWhiteSpace(classId)
            && fallbackMap.TryGetValue(classId, out var fallbackIds))
        {
            var fallbackPool = ResolveSkillAssets(fallbackIds);
            if (fallbackPool.Count > authoredPool.Count)
            {
                return fallbackPool;
            }
        }

        return authoredPool;
    }

    private IReadOnlyList<SkillDefinitionAsset> ResolveSkillAssets(IEnumerable<string> skillIds)
    {
        return skillIds
            .Where(id => !string.IsNullOrWhiteSpace(id) && _skillDefinitions.ContainsKey(id))
            .Select(id => _skillDefinitions[id])
            .Distinct()
            .ToList();
    }

    private static RecruitTier ResolveRecruitTier(UnitArchetypeDefinition definition)
    {
        if (definition.BudgetCard != null)
        {
            return LoopCContentGovernance.ToRecruitTier(definition.BudgetCard.Rarity);
        }

        return LoopBRecruitTierFallbacks.TryGetValue(definition.Id, out var tier)
            ? tier
            : definition.RecruitTier;
    }

    private static IReadOnlyList<string> ResolveRecruitPlanTags(UnitArchetypeDefinition definition)
    {
        var tags = ExtractTagIds(definition.RecruitPlanTags);
        return tags.Count > 0
            ? tags
            : LoopBRecruitPlanTagFallbacks.TryGetValue(definition.Id, out var fallback)
                ? fallback
                : InferArchetypeRecruitTags(definition);
    }

    private static IReadOnlyList<string> ResolveScoutBiasTags(UnitArchetypeDefinition definition)
    {
        var tags = ExtractTagIds(definition.ScoutBiasTags);
        return tags.Count > 0
            ? tags
            : LoopBScoutBiasTagFallbacks.TryGetValue(definition.Id, out var fallback)
                ? fallback
                : InferArchetypeRecruitTags(definition);
    }

    private static IReadOnlyList<RecruitBannedPairingTemplate> ResolveRecruitBannedPairings(UnitArchetypeDefinition definition)
    {
        var authored = Enumerate(definition.RecruitBannedPairings)
            .Where(pairing => pairing != null && !string.IsNullOrWhiteSpace(pairing.FlexActiveId) && !string.IsNullOrWhiteSpace(pairing.FlexPassiveId))
            .Select(pairing => new RecruitBannedPairingTemplate(pairing.FlexActiveId, pairing.FlexPassiveId))
            .ToList();
        if (authored.Count > 0)
        {
            return authored;
        }

        return !string.IsNullOrWhiteSpace(definition.Class?.Id)
               && LoopBRecruitBannedPairingFallbacks.TryGetValue(definition.Class.Id, out var fallback)
            ? fallback
            : Array.Empty<RecruitBannedPairingTemplate>();
    }

    private static string[] InferArchetypeRecruitTags(UnitArchetypeDefinition definition)
    {
        var tags = new HashSet<string>(StringComparer.Ordinal);
        if (!string.IsNullOrWhiteSpace(definition.Class?.Id))
        {
            tags.Add(definition.Class.Id);
        }

        if (!string.IsNullOrWhiteSpace(definition.Race?.Id))
        {
            tags.Add(definition.Race.Id);
        }

        switch (definition.Class?.Id)
        {
            case "vanguard":
            case "duelist":
                tags.Add("frontline");
                break;
            case "ranger":
            case "mystic":
                tags.Add("backline");
                break;
        }

        if (definition.BaseMagPower > definition.BasePhysPower)
        {
            tags.Add("magical");
        }
        else if (definition.BasePhysPower > 0f)
        {
            tags.Add("physical");
        }

        if (definition.BaseHealPower > 0f || string.Equals(definition.RoleTag, "support", StringComparison.Ordinal))
        {
            tags.Add("support");
        }

        return tags.Where(tag => !string.IsNullOrWhiteSpace(tag)).ToArray();
    }
}
