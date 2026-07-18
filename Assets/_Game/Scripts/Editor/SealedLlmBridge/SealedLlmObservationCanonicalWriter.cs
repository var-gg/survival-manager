using System;
using System.Collections.Generic;
using System.IO;
using SM.Combat.Model;
using SM.HeadlessPolicies;

namespace SM.SealedLlmBridge;

/// <summary>
/// Observation의 모든 공개 field를 length-prefixed frames로 쓴다. ID/tag/catalog/list 표면은
/// choice ordinal을 따로 운반하므로 canonical object bytes 또는 string ordinal로 정렬하고 중복은 보존한다.
/// Reward/recruit option의 Index, passive depth, refit slot Index는 각 object bytes 안에 포함된다.
/// </summary>
internal static class SealedLlmObservationCanonicalWriter
{
    private const string PolicySchema = "SealedLlmPolicyObservationV1";
    private const string RosterPolicySchema = "SealedLlmRosterPolicyObservationV1";

    public static byte[] Write(HeadlessPolicyObservation value)
        => Create(payload =>
        {
            Append(payload, PolicySchema, nameof(PolicySchema));
            SealedLlmCanonicalValue.AppendInteger(payload, value.DecisionSeed);
            SealedLlmCanonicalValue.AppendInteger(payload, value.DeployCapacity);
            Append(payload, value.ChapterId, nameof(value.ChapterId));
            Append(payload, value.SiteId, nameof(value.SiteId));
            SealedLlmCanonicalValue.AppendSortedObjects(payload, value.Roster, nameof(value.Roster), HeroBytes);
            AppendAnchors(payload, value.Anchors, nameof(value.Anchors));
            Append(payload, EnemyPreviewBytes(Required(value.EnemyPreview, nameof(value.EnemyPreview))), nameof(value.EnemyPreview));
            SealedLlmCanonicalValue.AppendSortedObjects(
                payload,
                value.RewardOptions,
                nameof(value.RewardOptions),
                RewardOptionBytes);
            Append(payload, WalletBytes(Required(value.Wallet, nameof(value.Wallet))), nameof(value.Wallet));
            SealedLlmCanonicalValue.AppendSortedObjects(
                payload,
                value.TemporaryAugments,
                nameof(value.TemporaryAugments),
                AugmentBytes);
            SealedLlmCanonicalValue.AppendSortedObjects(
                payload,
                value.SynergyCounts,
                nameof(value.SynergyCounts),
                SynergyCountBytes);
            SealedLlmCanonicalValue.AppendSortedObjects(
                payload,
                value.SynergyCatalog,
                nameof(value.SynergyCatalog),
                SynergyBytes);
            AppendEvidenceMap(payload, value.EvidenceFactIdsBySignal, nameof(value.EvidenceFactIdsBySignal));
        });

    public static byte[] Write(HeadlessRosterPolicyObservation value)
        => Create(payload =>
        {
            Append(payload, RosterPolicySchema, nameof(RosterPolicySchema));
            SealedLlmCanonicalValue.AppendInteger(payload, value.DecisionSeed);
            Append(payload, value.ChapterId, nameof(value.ChapterId));
            Append(payload, value.SiteId, nameof(value.SiteId));
            SealedLlmCanonicalValue.AppendInteger(payload, value.RosterCapacity);
            SealedLlmCanonicalValue.AppendSortedObjects(payload, value.Roster, nameof(value.Roster), HeroBytes);
            Append(payload, WalletBytes(Required(value.Wallet, nameof(value.Wallet))), nameof(value.Wallet));
            SealedLlmCanonicalValue.AppendSortedObjects(
                payload,
                value.RecruitOffers,
                nameof(value.RecruitOffers),
                RecruitOfferBytes);
            SealedLlmCanonicalValue.AppendSortedObjects(
                payload,
                value.PassiveHeroes,
                nameof(value.PassiveHeroes),
                PassiveHeroBytes);
            SealedLlmCanonicalValue.AppendSortedObjects(
                payload,
                value.RefitItems,
                nameof(value.RefitItems),
                RefitItemBytes);
            AppendEvidenceMap(payload, value.EvidenceFactIdsBySignal, nameof(value.EvidenceFactIdsBySignal));
        });

    private static byte[] HeroBytes(HeadlessHeroObservation value)
        => Create(payload =>
        {
            Append(payload, "HeadlessHeroObservationV1", nameof(HeadlessHeroObservation));
            Append(payload, value.HeroId, nameof(value.HeroId));
            Append(payload, value.ArchetypeId, nameof(value.ArchetypeId));
            Append(payload, value.RaceId, nameof(value.RaceId));
            Append(payload, value.ClassId, nameof(value.ClassId));
            Append(payload, value.RoleTag, nameof(value.RoleTag));
            SealedLlmCanonicalValue.AppendInteger(payload, value.Level);
            SealedLlmCanonicalValue.AppendInteger(payload, value.CurrentHp);
            SealedLlmCanonicalValue.AppendInteger(payload, value.MaxHp);
            SealedLlmCanonicalValue.AppendInteger(payload, value.EquippedItemCount);
            SealedLlmCanonicalValue.AppendBoolean(payload, value.IsDeployed);
            SealedLlmCanonicalValue.AppendEnum(payload, value.PreferredAnchor, nameof(value.PreferredAnchor));
            SealedLlmCanonicalValue.AppendSortedObjects(payload, value.SkillCards, nameof(value.SkillCards), SkillBytes);
            Append(payload, value.FlexActiveSkillId, nameof(value.FlexActiveSkillId));
            Append(payload, value.FlexPassiveSkillId, nameof(value.FlexPassiveSkillId));
            SealedLlmCanonicalValue.AppendSortedObjects(
                payload,
                value.EquippedItems,
                nameof(value.EquippedItems),
                ItemBytes);
            SealedLlmCanonicalValue.AppendSortedStrings(
                payload,
                value.SelectedPassiveNodeIds,
                nameof(value.SelectedPassiveNodeIds));
        });

    private static byte[] SkillBytes(HeadlessSkillObservation value)
        => Create(payload =>
        {
            Append(payload, "HeadlessSkillObservationV1", nameof(HeadlessSkillObservation));
            Append(payload, value.SkillId, nameof(value.SkillId));
            SealedLlmCanonicalValue.AppendEnum(payload, value.Kind, nameof(value.Kind));
            Append(payload, value.SlotKind, nameof(value.SlotKind));
            SealedLlmCanonicalValue.AppendRoundTrip(payload, value.Power, nameof(value.Power));
            SealedLlmCanonicalValue.AppendRoundTrip(payload, value.Range, nameof(value.Range));
            SealedLlmCanonicalValue.AppendEnum(payload, value.DamageType, nameof(value.DamageType));
            SealedLlmCanonicalValue.AppendRoundTrip(payload, value.PowerFlat, nameof(value.PowerFlat));
            SealedLlmCanonicalValue.AppendRoundTrip(payload, value.PhysicalCoefficient, nameof(value.PhysicalCoefficient));
            SealedLlmCanonicalValue.AppendRoundTrip(payload, value.MagicalCoefficient, nameof(value.MagicalCoefficient));
            SealedLlmCanonicalValue.AppendRoundTrip(payload, value.HealingCoefficient, nameof(value.HealingCoefficient));
            SealedLlmCanonicalValue.AppendRoundTrip(payload, value.HealthCoefficient, nameof(value.HealthCoefficient));
            SealedLlmCanonicalValue.AppendRoundTrip(payload, value.ManaCost, nameof(value.ManaCost));
            SealedLlmCanonicalValue.AppendRoundTrip(payload, value.CooldownSeconds, nameof(value.CooldownSeconds));
            SealedLlmCanonicalValue.AppendRoundTrip(payload, value.WindupSeconds, nameof(value.WindupSeconds));
            SealedLlmCanonicalValue.AppendBoolean(payload, value.CanCrit);
            SealedLlmCanonicalValue.AppendEnum(payload, value.Delivery, nameof(value.Delivery));
            SealedLlmCanonicalValue.AppendEnum(payload, value.TargetRule, nameof(value.TargetRule));
            SealedLlmCanonicalValue.AppendSortedObjects(
                payload,
                value.AppliedStatuses,
                nameof(value.AppliedStatuses),
                StatusApplicationBytes);
        });

    private static byte[] StatusApplicationBytes(HeadlessStatusApplicationObservation value)
        => Create(payload =>
        {
            Append(payload, "HeadlessStatusApplicationObservationV1", nameof(HeadlessStatusApplicationObservation));
            Append(payload, value.ApplicationId, nameof(value.ApplicationId));
            Append(payload, value.StatusId, nameof(value.StatusId));
            SealedLlmCanonicalValue.AppendRoundTrip(payload, value.DurationSeconds, nameof(value.DurationSeconds));
            SealedLlmCanonicalValue.AppendRoundTrip(payload, value.Magnitude, nameof(value.Magnitude));
            SealedLlmCanonicalValue.AppendInteger(payload, value.MaxStacks);
        });

    private static byte[] StatModifierBytes(HeadlessStatModifierObservation value)
        => Create(payload =>
        {
            Append(payload, "HeadlessStatModifierObservationV1", nameof(HeadlessStatModifierObservation));
            Append(payload, value.StatId, nameof(value.StatId));
            Append(payload, value.Operation, nameof(value.Operation));
            SealedLlmCanonicalValue.AppendRoundTrip(payload, value.Value, nameof(value.Value));
            Append(payload, value.TagId, nameof(value.TagId));
        });

    private static byte[] RuleModifierBytes(HeadlessRuleModifierObservation value)
        => Create(payload =>
        {
            Append(payload, "HeadlessRuleModifierObservationV1", nameof(HeadlessRuleModifierObservation));
            Append(payload, value.Kind, nameof(value.Kind));
            Append(payload, value.Value, nameof(value.Value));
            SealedLlmCanonicalValue.AppendRoundTrip(payload, value.Magnitude, nameof(value.Magnitude));
        });

    private static byte[] TriggeredEffectBytes(HeadlessTriggeredEffectObservation value)
        => Create(payload =>
        {
            Append(payload, "HeadlessTriggeredEffectObservationV1", nameof(HeadlessTriggeredEffectObservation));
            Append(payload, value.Trigger, nameof(value.Trigger));
            Append(payload, value.Operation, nameof(value.Operation));
            Append(payload, value.Scope, nameof(value.Scope));
            SealedLlmCanonicalValue.AppendRoundTrip(payload, value.Magnitude, nameof(value.Magnitude));
            SealedLlmCanonicalValue.AppendRoundTrip(payload, value.ThresholdRatio, nameof(value.ThresholdRatio));
            Append(payload, value.StatusId, nameof(value.StatusId));
            SealedLlmCanonicalValue.AppendRoundTrip(payload, value.DurationSeconds, nameof(value.DurationSeconds));
            SealedLlmCanonicalValue.AppendInteger(payload, value.MaxStacks);
        });

    private static byte[] AffixBytes(HeadlessAffixMechanicsObservation value)
        => Create(payload =>
        {
            Append(payload, "HeadlessAffixMechanicsObservationV1", nameof(HeadlessAffixMechanicsObservation));
            Append(payload, value.AffixId, nameof(value.AffixId));
            SealedLlmCanonicalValue.AppendSortedStrings(payload, value.CompileTags, nameof(value.CompileTags));
            SealedLlmCanonicalValue.AppendSortedStrings(payload, value.RequiredTags, nameof(value.RequiredTags));
            SealedLlmCanonicalValue.AppendSortedStrings(payload, value.ExcludedTags, nameof(value.ExcludedTags));
            SealedLlmCanonicalValue.AppendSortedObjects(
                payload,
                value.StatModifiers,
                nameof(value.StatModifiers),
                StatModifierBytes);
            SealedLlmCanonicalValue.AppendSortedObjects(
                payload,
                value.RuleModifiers,
                nameof(value.RuleModifiers),
                RuleModifierBytes);
        });

    private static byte[] ItemBytes(HeadlessItemMechanicsObservation value)
        => Create(payload =>
        {
            Append(payload, "HeadlessItemMechanicsObservationV1", nameof(HeadlessItemMechanicsObservation));
            Append(payload, value.ItemId, nameof(value.ItemId));
            Append(payload, value.ItemInstanceId, nameof(value.ItemInstanceId));
            SealedLlmCanonicalValue.AppendSortedStrings(payload, value.Tags, nameof(value.Tags));
            Append(payload, value.WeaponFamilyTag, nameof(value.WeaponFamilyTag));
            SealedLlmCanonicalValue.AppendSortedObjects(
                payload,
                value.StatModifiers,
                nameof(value.StatModifiers),
                StatModifierBytes);
            SealedLlmCanonicalValue.AppendSortedObjects(payload, value.Affixes, nameof(value.Affixes), AffixBytes);
            SealedLlmCanonicalValue.AppendSortedObjects(
                payload,
                value.GrantedSkills,
                nameof(value.GrantedSkills),
                SkillBytes);
        });

    private static byte[] AugmentBytes(HeadlessAugmentMechanicsObservation value)
        => Create(payload =>
        {
            Append(payload, "HeadlessAugmentMechanicsObservationV1", nameof(HeadlessAugmentMechanicsObservation));
            Append(payload, value.AugmentId, nameof(value.AugmentId));
            Append(payload, value.Category, nameof(value.Category));
            Append(payload, value.FamilyId, nameof(value.FamilyId));
            SealedLlmCanonicalValue.AppendInteger(payload, value.Tier);
            SealedLlmCanonicalValue.AppendSortedStrings(payload, value.Tags, nameof(value.Tags));
            SealedLlmCanonicalValue.AppendSortedStrings(payload, value.BuildBiasTags, nameof(value.BuildBiasTags));
            SealedLlmCanonicalValue.AppendSortedObjects(
                payload,
                value.StatModifiers,
                nameof(value.StatModifiers),
                StatModifierBytes);
            SealedLlmCanonicalValue.AppendSortedObjects(
                payload,
                value.RuleModifiers,
                nameof(value.RuleModifiers),
                RuleModifierBytes);
            SealedLlmCanonicalValue.AppendSortedObjects(
                payload,
                value.TriggeredEffects,
                nameof(value.TriggeredEffects),
                TriggeredEffectBytes);
        });

    private static byte[] WalletBytes(HeadlessWalletObservation value)
        => Create(payload =>
        {
            Append(payload, "HeadlessWalletObservationV1", nameof(HeadlessWalletObservation));
            SealedLlmCanonicalValue.AppendInteger(payload, value.Gold);
            SealedLlmCanonicalValue.AppendInteger(payload, value.Echo);
        });

    private static byte[] SynergyCountBytes(HeadlessSynergyCountObservation value)
        => Create(payload =>
        {
            Append(payload, "HeadlessSynergyCountObservationV1", nameof(HeadlessSynergyCountObservation));
            Append(payload, value.CountedTagId, nameof(value.CountedTagId));
            SealedLlmCanonicalValue.AppendInteger(payload, value.CurrentCount);
        });

    private static byte[] SynergyBytes(HeadlessSynergyObservation value)
        => Create(payload =>
        {
            Append(payload, "HeadlessSynergyObservationV1", nameof(HeadlessSynergyObservation));
            Append(payload, value.SynergyId, nameof(value.SynergyId));
            Append(payload, value.CountedTagId, nameof(value.CountedTagId));
            SealedLlmCanonicalValue.AppendSortedObjects(payload, value.Tiers, nameof(value.Tiers), SynergyTierBytes);
        });

    private static byte[] SynergyTierBytes(HeadlessSynergyTierObservation value)
        => Create(payload =>
        {
            Append(payload, "HeadlessSynergyTierObservationV1", nameof(HeadlessSynergyTierObservation));
            SealedLlmCanonicalValue.AppendInteger(payload, value.Threshold);
            SealedLlmCanonicalValue.AppendSortedObjects(
                payload,
                value.StatModifiers,
                nameof(value.StatModifiers),
                StatModifierBytes);
            Append(payload, value.GrantedTeamRuleId, nameof(value.GrantedTeamRuleId));
        });

    private static byte[] EnemyPreviewBytes(HeadlessEnemyPreview value)
        => Create(payload =>
        {
            Append(payload, "HeadlessEnemyPreviewV1", nameof(HeadlessEnemyPreview));
            SealedLlmCanonicalValue.AppendBoolean(payload, value.IsAvailable);
            Append(payload, value.EncounterId, nameof(value.EncounterId));
            Append(payload, value.FactionId, nameof(value.FactionId));
            Append(payload, value.DifficultyBand, nameof(value.DifficultyBand));
            SealedLlmCanonicalValue.AppendInteger(payload, value.ThreatSkulls);
            SealedLlmCanonicalValue.AppendSortedObjects(payload, value.Units, nameof(value.Units), EnemyUnitBytes);
            Append(payload, value.BossAuraTag, nameof(value.BossAuraTag));
            Append(payload, value.BossUtilityTag, nameof(value.BossUtilityTag));
            SealedLlmCanonicalValue.AppendSortedStrings(payload, value.RewardDropTags, nameof(value.RewardDropTags));
        });

    private static byte[] EnemyUnitBytes(HeadlessEnemyUnitPreview value)
        => Create(payload =>
        {
            Append(payload, "HeadlessEnemyUnitPreviewV1", nameof(HeadlessEnemyUnitPreview));
            Append(payload, value.ArchetypeId, nameof(value.ArchetypeId));
            Append(payload, value.RaceId, nameof(value.RaceId));
            Append(payload, value.ClassId, nameof(value.ClassId));
            Append(payload, value.RoleTag, nameof(value.RoleTag));
            SealedLlmCanonicalValue.AppendEnum(payload, value.PreferredAnchor, nameof(value.PreferredAnchor));
        });

    private static byte[] RewardOptionBytes(HeadlessRewardOption value)
        => Create(payload =>
        {
            Append(payload, "HeadlessRewardOptionV1", nameof(HeadlessRewardOption));
            SealedLlmCanonicalValue.AppendInteger(payload, value.Index);
            SealedLlmCanonicalValue.AppendEnum(payload, value.Kind, nameof(value.Kind));
            Append(payload, value.PayloadId, nameof(value.PayloadId));
            SealedLlmCanonicalValue.AppendInteger(payload, value.GoldAmount);
            SealedLlmCanonicalValue.AppendInteger(payload, value.EchoAmount);
            SealedLlmCanonicalValue.AppendInteger(payload, value.PermanentSlotAmount);
            Append(
                payload,
                RewardMechanicsBytes(Required(value.Mechanics, nameof(value.Mechanics))),
                nameof(value.Mechanics));
        });

    private static byte[] RewardMechanicsBytes(HeadlessRewardMechanicsObservation value)
        => Create(payload =>
        {
            Append(payload, "HeadlessRewardMechanicsObservationV1", nameof(HeadlessRewardMechanicsObservation));
            SealedLlmCanonicalValue.AppendOptional(payload, value.Item, nameof(value.Item), ItemBytes);
            SealedLlmCanonicalValue.AppendOptional(
                payload,
                value.TemporaryAugment,
                nameof(value.TemporaryAugment),
                AugmentBytes);
        });

    private static byte[] RecruitOfferBytes(HeadlessRecruitOfferObservation value)
        => Create(payload =>
        {
            Append(payload, "HeadlessRecruitOfferObservationV1", nameof(HeadlessRecruitOfferObservation));
            SealedLlmCanonicalValue.AppendInteger(payload, value.OfferIndex);
            Append(payload, value.ArchetypeId, nameof(value.ArchetypeId));
            Append(payload, value.RaceId, nameof(value.RaceId));
            Append(payload, value.ClassId, nameof(value.ClassId));
            Append(payload, value.RoleTag, nameof(value.RoleTag));
            Append(payload, value.FlexActiveSkillId, nameof(value.FlexActiveSkillId));
            Append(payload, value.FlexPassiveSkillId, nameof(value.FlexPassiveSkillId));
            SealedLlmCanonicalValue.AppendInteger(payload, value.GoldCost);
            Append(payload, value.Tier, nameof(value.Tier));
            Append(payload, value.PlanFit, nameof(value.PlanFit));
            SealedLlmCanonicalValue.AppendBoolean(payload, value.IsDuplicate);
        });

    private static byte[] PassiveHeroBytes(HeadlessPassiveHeroObservation value)
        => Create(payload =>
        {
            Append(payload, "HeadlessPassiveHeroObservationV1", nameof(HeadlessPassiveHeroObservation));
            Append(payload, value.HeroId, nameof(value.HeroId));
            SealedLlmCanonicalValue.AppendInteger(payload, value.Level);
            Append(payload, value.SelectedBoardId, nameof(value.SelectedBoardId));
            SealedLlmCanonicalValue.AppendSortedStrings(payload, value.SelectedNodeIds, nameof(value.SelectedNodeIds));
            SealedLlmCanonicalValue.AppendInteger(payload, value.MaxActiveNodeCount);
            SealedLlmCanonicalValue.AppendInteger(payload, value.MaxKeystoneCount);
            SealedLlmCanonicalValue.AppendSortedObjects(payload, value.Boards, nameof(value.Boards), PassiveBoardBytes);
        });

    private static byte[] PassiveBoardBytes(HeadlessPassiveBoardObservation value)
        => Create(payload =>
        {
            Append(payload, "HeadlessPassiveBoardObservationV1", nameof(HeadlessPassiveBoardObservation));
            Append(payload, value.BoardId, nameof(value.BoardId));
            SealedLlmCanonicalValue.AppendSortedObjects(payload, value.Nodes, nameof(value.Nodes), PassiveNodeBytes);
        });

    private static byte[] PassiveNodeBytes(HeadlessPassiveNodeObservation value)
        => Create(payload =>
        {
            Append(payload, "HeadlessPassiveNodeObservationV1", nameof(HeadlessPassiveNodeObservation));
            Append(payload, value.NodeId, nameof(value.NodeId));
            SealedLlmCanonicalValue.AppendInteger(payload, value.BoardDepth);
            Append(payload, value.NodeKind, nameof(value.NodeKind));
            SealedLlmCanonicalValue.AppendSortedStrings(
                payload,
                value.PrerequisiteNodeIds,
                nameof(value.PrerequisiteNodeIds));
            SealedLlmCanonicalValue.AppendSortedStrings(
                payload,
                value.MutualExclusionTagIds,
                nameof(value.MutualExclusionTagIds));
            Append(payload, value.GrantedSkillId, nameof(value.GrantedSkillId));
            SealedLlmCanonicalValue.AppendSortedStrings(payload, value.CompileTags, nameof(value.CompileTags));
            SealedLlmCanonicalValue.AppendSortedObjects(
                payload,
                value.StatModifiers,
                nameof(value.StatModifiers),
                StatModifierBytes);
            SealedLlmCanonicalValue.AppendSortedObjects(
                payload,
                value.RuleModifiers,
                nameof(value.RuleModifiers),
                RuleModifierBytes);
        });

    private static byte[] RefitItemBytes(HeadlessRefitItemObservation value)
        => Create(payload =>
        {
            Append(payload, "HeadlessRefitItemObservationV1", nameof(HeadlessRefitItemObservation));
            Append(payload, value.ItemId, nameof(value.ItemId));
            Append(payload, value.ItemInstanceId, nameof(value.ItemInstanceId));
            Append(payload, value.EquippedHeroId, nameof(value.EquippedHeroId));
            SealedLlmCanonicalValue.AppendSortedStrings(payload, value.Tags, nameof(value.Tags));
            Append(payload, value.WeaponFamilyTag, nameof(value.WeaponFamilyTag));
            SealedLlmCanonicalValue.AppendInteger(payload, value.EchoCost);
            SealedLlmCanonicalValue.AppendSortedObjects(
                payload,
                value.AffixSlots,
                nameof(value.AffixSlots),
                RefitSlotBytes);
        });

    private static byte[] RefitSlotBytes(HeadlessRefitSlotObservation value)
        => Create(payload =>
        {
            Append(payload, "HeadlessRefitSlotObservationV1", nameof(HeadlessRefitSlotObservation));
            SealedLlmCanonicalValue.AppendInteger(payload, value.SlotIndex);
            SealedLlmCanonicalValue.AppendOptional(
                payload,
                value.CurrentAffix,
                nameof(value.CurrentAffix),
                AffixBytes);
            SealedLlmCanonicalValue.AppendBoolean(payload, value.CanRefit);
        });

    private static void AppendAnchors(
        Stream payload,
        IReadOnlyList<DeploymentAnchorId> anchors,
        string path)
    {
        if (anchors == null)
        {
            throw new ArgumentException($"{path} is required.", path);
        }

        var names = new string[anchors.Count];
        for (var index = 0; index < anchors.Count; index++)
        {
            names[index] = SealedLlmCanonicalValue.EnumName(anchors[index], $"{path}[{index}]");
        }

        SealedLlmCanonicalValue.AppendSortedStrings(payload, names, path);
    }

    private static void AppendEvidenceMap(
        Stream payload,
        IReadOnlyDictionary<string, string> evidence,
        string path)
    {
        if (evidence == null)
        {
            throw new ArgumentException($"{path} is required.", path);
        }

        var ordered = new KeyValuePair<string, string>[evidence.Count];
        var index = 0;
        foreach (var pair in evidence)
        {
            if (pair.Key == null || pair.Value == null)
            {
                throw new ArgumentException($"{path} cannot contain null keys or values.", path);
            }

            ordered[index++] = pair;
        }

        Array.Sort(ordered, CompareEvidence);
        SealedLlmCanonicalValue.AppendInteger(payload, ordered.Length);
        for (index = 0; index < ordered.Length; index++)
        {
            Append(payload, ordered[index].Key, $"{path}[{index}].Key");
            Append(payload, ordered[index].Value, $"{path}[{index}].Value");
        }
    }

    private static int CompareEvidence(KeyValuePair<string, string> left, KeyValuePair<string, string> right)
    {
        var result = StringComparer.Ordinal.Compare(left.Key, right.Key);
        return result != 0 ? result : StringComparer.Ordinal.Compare(left.Value, right.Value);
    }

    private static T Required<T>(T value, string path)
        where T : class
        => value ?? throw new ArgumentException($"{path} is required.", path);

    private static byte[] Create(Action<MemoryStream> write)
    {
        using var payload = new MemoryStream();
        write(payload);
        return payload.ToArray();
    }

    private static void Append(Stream payload, string value, string path)
        => SealedLlmCanonicalValue.Append(payload, value, path);

    private static void Append(Stream payload, byte[] value, string path)
        => SealedLlmCanonicalValue.Append(payload, value, path);
}
