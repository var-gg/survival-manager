using System;
using System.Collections.Generic;
using SM.Combat.Model;

namespace SM.HeadlessPolicies;

/// <summary>
/// 정책에 허용된 player-visible snapshot. 현재 결정 표면만 담고 session, content lookup,
/// 미래 node/RNG, 적 내부 stat을 운반할 수 있는 참조는 의도적으로 없다.
/// </summary>
public sealed class HeadlessPolicyObservation
{
    public HeadlessPolicyObservation(
        int decisionSeed,
        int deployCapacity,
        string chapterId,
        string siteId,
        IReadOnlyList<HeadlessHeroObservation> roster,
        IReadOnlyList<DeploymentAnchorId> anchors,
        HeadlessEnemyPreview enemyPreview,
        IReadOnlyList<HeadlessRewardOption> rewardOptions,
        HeadlessWalletObservation wallet = null,
        IReadOnlyList<HeadlessAugmentMechanicsObservation> temporaryAugments = null,
        IReadOnlyList<HeadlessSynergyCountObservation> synergyCounts = null,
        IReadOnlyList<HeadlessSynergyObservation> synergyCatalog = null)
    {
        DecisionSeed = decisionSeed;
        DeployCapacity = deployCapacity;
        ChapterId = chapterId;
        SiteId = siteId;
        Roster = roster;
        Anchors = anchors;
        EnemyPreview = enemyPreview;
        RewardOptions = rewardOptions;
        Wallet = wallet ?? HeadlessWalletObservation.Empty;
        TemporaryAugments = temporaryAugments ?? Array.Empty<HeadlessAugmentMechanicsObservation>();
        SynergyCounts = synergyCounts ?? Array.Empty<HeadlessSynergyCountObservation>();
        SynergyCatalog = synergyCatalog ?? Array.Empty<HeadlessSynergyObservation>();
    }

    public int DecisionSeed { get; }
    public int DeployCapacity { get; }
    public string ChapterId { get; }
    public string SiteId { get; }
    public IReadOnlyList<HeadlessHeroObservation> Roster { get; }
    public IReadOnlyList<DeploymentAnchorId> Anchors { get; }
    public HeadlessEnemyPreview EnemyPreview { get; }
    public IReadOnlyList<HeadlessRewardOption> RewardOptions { get; }
    public HeadlessWalletObservation Wallet { get; }
    public IReadOnlyList<HeadlessAugmentMechanicsObservation> TemporaryAugments { get; }
    public IReadOnlyList<HeadlessSynergyCountObservation> SynergyCounts { get; }
    public IReadOnlyList<HeadlessSynergyObservation> SynergyCatalog { get; }
}

/// <summary>roster UI에서 확인 가능한 영웅 identity, role, 현재 성장/상태의 순수 projection.</summary>
public sealed class HeadlessHeroObservation
{
    public HeadlessHeroObservation(
        string heroId,
        string archetypeId,
        string raceId,
        string classId,
        string roleTag,
        int level,
        int currentHp,
        int maxHp,
        int equippedItemCount,
        bool isDeployed,
        DeploymentAnchorId preferredAnchor,
        IReadOnlyList<HeadlessSkillObservation> skillCards = null,
        string flexActiveSkillId = "",
        string flexPassiveSkillId = "",
        IReadOnlyList<HeadlessItemMechanicsObservation> equippedItems = null,
        IReadOnlyList<string> selectedPassiveNodeIds = null)
    {
        HeroId = heroId;
        ArchetypeId = archetypeId;
        RaceId = raceId;
        ClassId = classId;
        RoleTag = roleTag;
        Level = level;
        CurrentHp = currentHp;
        MaxHp = maxHp;
        EquippedItemCount = equippedItemCount;
        IsDeployed = isDeployed;
        PreferredAnchor = preferredAnchor;
        SkillCards = skillCards ?? Array.Empty<HeadlessSkillObservation>();
        FlexActiveSkillId = flexActiveSkillId;
        FlexPassiveSkillId = flexPassiveSkillId;
        EquippedItems = equippedItems ?? Array.Empty<HeadlessItemMechanicsObservation>();
        SelectedPassiveNodeIds = selectedPassiveNodeIds ?? Array.Empty<string>();
    }

    public string HeroId { get; }
    public string ArchetypeId { get; }
    public string RaceId { get; }
    public string ClassId { get; }
    public string RoleTag { get; }
    public int Level { get; }
    public int CurrentHp { get; }
    public int MaxHp { get; }
    public int EquippedItemCount { get; }
    public bool IsDeployed { get; }
    public DeploymentAnchorId PreferredAnchor { get; }
    public IReadOnlyList<HeadlessSkillObservation> SkillCards { get; }
    public string FlexActiveSkillId { get; }
    public string FlexPassiveSkillId { get; }
    public IReadOnlyList<HeadlessItemMechanicsObservation> EquippedItems { get; }
    public IReadOnlyList<string> SelectedPassiveNodeIds { get; }
}

/// <summary>전투 상세/도감 tooltip이 보여 주는 단일 skill의 공개 mechanics.</summary>
public sealed class HeadlessSkillObservation
{
    public HeadlessSkillObservation(
        string skillId,
        SkillKind kind,
        string slotKind,
        float power,
        float range,
        DamageType damageType,
        float powerFlat,
        float physicalCoefficient,
        float magicalCoefficient,
        float healingCoefficient,
        float healthCoefficient,
        float manaCost,
        float cooldownSeconds,
        float windupSeconds,
        bool canCrit,
        SkillDelivery delivery,
        SkillTargetRule targetRule,
        IReadOnlyList<HeadlessStatusApplicationObservation> appliedStatuses)
    {
        SkillId = skillId;
        Kind = kind;
        SlotKind = slotKind;
        Power = power;
        Range = range;
        DamageType = damageType;
        PowerFlat = powerFlat;
        PhysicalCoefficient = physicalCoefficient;
        MagicalCoefficient = magicalCoefficient;
        HealingCoefficient = healingCoefficient;
        HealthCoefficient = healthCoefficient;
        ManaCost = manaCost;
        CooldownSeconds = cooldownSeconds;
        WindupSeconds = windupSeconds;
        CanCrit = canCrit;
        Delivery = delivery;
        TargetRule = targetRule;
        AppliedStatuses = appliedStatuses ?? Array.Empty<HeadlessStatusApplicationObservation>();
    }

    public string SkillId { get; }
    public SkillKind Kind { get; }
    public string SlotKind { get; }
    public float Power { get; }
    public float Range { get; }
    public DamageType DamageType { get; }
    public float PowerFlat { get; }
    public float PhysicalCoefficient { get; }
    public float MagicalCoefficient { get; }
    public float HealingCoefficient { get; }
    public float HealthCoefficient { get; }
    public float ManaCost { get; }
    public float CooldownSeconds { get; }
    public float WindupSeconds { get; }
    public bool CanCrit { get; }
    public SkillDelivery Delivery { get; }
    public SkillTargetRule TargetRule { get; }
    public IReadOnlyList<HeadlessStatusApplicationObservation> AppliedStatuses { get; }
}

public sealed class HeadlessStatusApplicationObservation
{
    public HeadlessStatusApplicationObservation(
        string applicationId,
        string statusId,
        float durationSeconds,
        float magnitude,
        int maxStacks)
    {
        ApplicationId = applicationId;
        StatusId = statusId;
        DurationSeconds = durationSeconds;
        Magnitude = magnitude;
        MaxStacks = maxStacks;
    }

    public string ApplicationId { get; }
    public string StatusId { get; }
    public float DurationSeconds { get; }
    public float Magnitude { get; }
    public int MaxStacks { get; }
}

public sealed class HeadlessStatModifierObservation
{
    public HeadlessStatModifierObservation(string statId, string operation, float value, string tagId)
    {
        StatId = statId;
        Operation = operation;
        Value = value;
        TagId = tagId;
    }

    public string StatId { get; }
    public string Operation { get; }
    public float Value { get; }
    public string TagId { get; }
}

public sealed class HeadlessRuleModifierObservation
{
    public HeadlessRuleModifierObservation(string kind, string value, float magnitude)
    {
        Kind = kind;
        Value = value;
        Magnitude = magnitude;
    }

    public string Kind { get; }
    public string Value { get; }
    public float Magnitude { get; }
}

public sealed class HeadlessTriggeredEffectObservation
{
    public HeadlessTriggeredEffectObservation(
        string trigger,
        string operation,
        string scope,
        float magnitude,
        float thresholdRatio,
        string statusId,
        float durationSeconds,
        int maxStacks)
    {
        Trigger = trigger;
        Operation = operation;
        Scope = scope;
        Magnitude = magnitude;
        ThresholdRatio = thresholdRatio;
        StatusId = statusId;
        DurationSeconds = durationSeconds;
        MaxStacks = maxStacks;
    }

    public string Trigger { get; }
    public string Operation { get; }
    public string Scope { get; }
    public float Magnitude { get; }
    public float ThresholdRatio { get; }
    public string StatusId { get; }
    public float DurationSeconds { get; }
    public int MaxStacks { get; }
}

public sealed class HeadlessAffixMechanicsObservation
{
    public HeadlessAffixMechanicsObservation(
        string affixId,
        IReadOnlyList<string> compileTags,
        IReadOnlyList<string> requiredTags,
        IReadOnlyList<string> excludedTags,
        IReadOnlyList<HeadlessStatModifierObservation> statModifiers,
        IReadOnlyList<HeadlessRuleModifierObservation> ruleModifiers)
    {
        AffixId = affixId;
        CompileTags = compileTags ?? Array.Empty<string>();
        RequiredTags = requiredTags ?? Array.Empty<string>();
        ExcludedTags = excludedTags ?? Array.Empty<string>();
        StatModifiers = statModifiers ?? Array.Empty<HeadlessStatModifierObservation>();
        RuleModifiers = ruleModifiers ?? Array.Empty<HeadlessRuleModifierObservation>();
    }

    public string AffixId { get; }
    public IReadOnlyList<string> CompileTags { get; }
    public IReadOnlyList<string> RequiredTags { get; }
    public IReadOnlyList<string> ExcludedTags { get; }
    public IReadOnlyList<HeadlessStatModifierObservation> StatModifiers { get; }
    public IReadOnlyList<HeadlessRuleModifierObservation> RuleModifiers { get; }
}

public sealed class HeadlessItemMechanicsObservation
{
    public HeadlessItemMechanicsObservation(
        string itemId,
        string itemInstanceId,
        IReadOnlyList<string> tags,
        string weaponFamilyTag,
        IReadOnlyList<HeadlessStatModifierObservation> statModifiers,
        IReadOnlyList<HeadlessAffixMechanicsObservation> affixes,
        IReadOnlyList<HeadlessSkillObservation> grantedSkills)
    {
        ItemId = itemId;
        ItemInstanceId = itemInstanceId;
        Tags = tags ?? Array.Empty<string>();
        WeaponFamilyTag = weaponFamilyTag;
        StatModifiers = statModifiers ?? Array.Empty<HeadlessStatModifierObservation>();
        Affixes = affixes ?? Array.Empty<HeadlessAffixMechanicsObservation>();
        GrantedSkills = grantedSkills ?? Array.Empty<HeadlessSkillObservation>();
    }

    public string ItemId { get; }
    public string ItemInstanceId { get; }
    public IReadOnlyList<string> Tags { get; }
    public string WeaponFamilyTag { get; }
    public IReadOnlyList<HeadlessStatModifierObservation> StatModifiers { get; }
    public IReadOnlyList<HeadlessAffixMechanicsObservation> Affixes { get; }
    public IReadOnlyList<HeadlessSkillObservation> GrantedSkills { get; }
}

public sealed class HeadlessAugmentMechanicsObservation
{
    public HeadlessAugmentMechanicsObservation(
        string augmentId,
        string category,
        string familyId,
        int tier,
        IReadOnlyList<string> tags,
        IReadOnlyList<string> buildBiasTags,
        IReadOnlyList<HeadlessStatModifierObservation> statModifiers,
        IReadOnlyList<HeadlessRuleModifierObservation> ruleModifiers,
        IReadOnlyList<HeadlessTriggeredEffectObservation> triggeredEffects)
    {
        AugmentId = augmentId;
        Category = category;
        FamilyId = familyId;
        Tier = tier;
        Tags = tags ?? Array.Empty<string>();
        BuildBiasTags = buildBiasTags ?? Array.Empty<string>();
        StatModifiers = statModifiers ?? Array.Empty<HeadlessStatModifierObservation>();
        RuleModifiers = ruleModifiers ?? Array.Empty<HeadlessRuleModifierObservation>();
        TriggeredEffects = triggeredEffects ?? Array.Empty<HeadlessTriggeredEffectObservation>();
    }

    public string AugmentId { get; }
    public string Category { get; }
    public string FamilyId { get; }
    public int Tier { get; }
    public IReadOnlyList<string> Tags { get; }
    public IReadOnlyList<string> BuildBiasTags { get; }
    public IReadOnlyList<HeadlessStatModifierObservation> StatModifiers { get; }
    public IReadOnlyList<HeadlessRuleModifierObservation> RuleModifiers { get; }
    public IReadOnlyList<HeadlessTriggeredEffectObservation> TriggeredEffects { get; }
}

public sealed class HeadlessWalletObservation
{
    public HeadlessWalletObservation(int gold, int echo)
    {
        Gold = gold;
        Echo = echo;
    }

    public static HeadlessWalletObservation Empty { get; } = new(0, 0);

    public int Gold { get; }
    public int Echo { get; }
}

public sealed class HeadlessSynergyCountObservation
{
    public HeadlessSynergyCountObservation(string countedTagId, int currentCount)
    {
        CountedTagId = countedTagId;
        CurrentCount = currentCount;
    }

    public string CountedTagId { get; }
    public int CurrentCount { get; }
}

public sealed class HeadlessSynergyObservation
{
    public HeadlessSynergyObservation(
        string synergyId,
        string countedTagId,
        IReadOnlyList<HeadlessSynergyTierObservation> tiers)
    {
        SynergyId = synergyId;
        CountedTagId = countedTagId;
        Tiers = tiers ?? Array.Empty<HeadlessSynergyTierObservation>();
    }

    public string SynergyId { get; }
    public string CountedTagId { get; }
    public IReadOnlyList<HeadlessSynergyTierObservation> Tiers { get; }
}

public sealed class HeadlessSynergyTierObservation
{
    public HeadlessSynergyTierObservation(
        int threshold,
        IReadOnlyList<HeadlessStatModifierObservation> statModifiers,
        string grantedTeamRuleId)
    {
        Threshold = threshold;
        StatModifiers = statModifiers ?? Array.Empty<HeadlessStatModifierObservation>();
        GrantedTeamRuleId = grantedTeamRuleId;
    }

    public int Threshold { get; }
    public IReadOnlyList<HeadlessStatModifierObservation> StatModifiers { get; }
    public string GrantedTeamRuleId { get; }
}

/// <summary>현재 공개된 encounter preview 한 개. 미공개 node 목록이나 전투 실수치는 포함하지 않는다.</summary>
public sealed class HeadlessEnemyPreview
{
    public HeadlessEnemyPreview(
        bool isAvailable,
        string encounterId,
        string factionId,
        string difficultyBand,
        int threatSkulls,
        IReadOnlyList<HeadlessEnemyUnitPreview> units,
        string bossAuraTag,
        string bossUtilityTag,
        IReadOnlyList<string> rewardDropTags)
    {
        IsAvailable = isAvailable;
        EncounterId = encounterId;
        FactionId = factionId;
        DifficultyBand = difficultyBand;
        ThreatSkulls = threatSkulls;
        Units = units;
        BossAuraTag = bossAuraTag;
        BossUtilityTag = bossUtilityTag;
        RewardDropTags = rewardDropTags;
    }

    public static HeadlessEnemyPreview Unavailable { get; } = new(
        false,
        string.Empty,
        string.Empty,
        string.Empty,
        0,
        Array.Empty<HeadlessEnemyUnitPreview>(),
        string.Empty,
        string.Empty,
        Array.Empty<string>());

    public bool IsAvailable { get; }
    public string EncounterId { get; }
    public string FactionId { get; }
    public string DifficultyBand { get; }
    public int ThreatSkulls { get; }
    public IReadOnlyList<HeadlessEnemyUnitPreview> Units { get; }
    public string BossAuraTag { get; }
    public string BossUtilityTag { get; }
    public IReadOnlyList<string> RewardDropTags { get; }
}

/// <summary>공개된 enemy identity에서 알 수 있는 class/race/role/anchor 정보만 담는다.</summary>
public sealed class HeadlessEnemyUnitPreview
{
    public HeadlessEnemyUnitPreview(
        string archetypeId,
        string raceId,
        string classId,
        string roleTag,
        DeploymentAnchorId preferredAnchor)
    {
        ArchetypeId = archetypeId;
        RaceId = raceId;
        ClassId = classId;
        RoleTag = roleTag;
        PreferredAnchor = preferredAnchor;
    }

    public string ArchetypeId { get; }
    public string RaceId { get; }
    public string ClassId { get; }
    public string RoleTag { get; }
    public DeploymentAnchorId PreferredAnchor { get; }
}

public enum HeadlessRewardKind
{
    Gold = 0,
    Item = 1,
    TemporaryAugment = 2,
    Echo = 3,
    PermanentAugmentSlot = 4,
}

/// <summary>Reward 화면에 이미 제시된 카드 한 장의 공개 정보.</summary>
public sealed class HeadlessRewardOption
{
    public HeadlessRewardOption(
        int index,
        HeadlessRewardKind kind,
        string payloadId,
        int goldAmount,
        int echoAmount,
        int permanentSlotAmount,
        HeadlessRewardMechanicsObservation mechanics = null)
    {
        Index = index;
        Kind = kind;
        PayloadId = payloadId;
        GoldAmount = goldAmount;
        EchoAmount = echoAmount;
        PermanentSlotAmount = permanentSlotAmount;
        Mechanics = mechanics ?? HeadlessRewardMechanicsObservation.Empty;
    }

    public int Index { get; }
    public HeadlessRewardKind Kind { get; }
    public string PayloadId { get; }
    public int GoldAmount { get; }
    public int EchoAmount { get; }
    public int PermanentSlotAmount { get; }
    public HeadlessRewardMechanicsObservation Mechanics { get; }
}

public sealed class HeadlessRewardMechanicsObservation
{
    public HeadlessRewardMechanicsObservation(
        HeadlessItemMechanicsObservation item,
        HeadlessAugmentMechanicsObservation temporaryAugment)
    {
        Item = item;
        TemporaryAugment = temporaryAugment;
    }

    public static HeadlessRewardMechanicsObservation Empty { get; } = new(null, null);

    public HeadlessItemMechanicsObservation Item { get; }
    public HeadlessAugmentMechanicsObservation TemporaryAugment { get; }
}

public sealed class HeadlessPlacement
{
    public HeadlessPlacement(DeploymentAnchorId anchor, string heroId)
    {
        Anchor = anchor;
        HeroId = heroId;
    }

    public DeploymentAnchorId Anchor { get; }
    public string HeroId { get; }
}

public sealed class HeadlessDeploymentDecision
{
    public HeadlessDeploymentDecision(
        IReadOnlyList<HeadlessPlacement> placements,
        string rationale,
        double estimatedValue)
    {
        Placements = placements;
        Rationale = rationale;
        EstimatedValue = estimatedValue;
    }

    public IReadOnlyList<HeadlessPlacement> Placements { get; }
    public string Rationale { get; }
    public double EstimatedValue { get; }
}

public sealed class HeadlessRewardDecision
{
    public HeadlessRewardDecision(int optionIndex, string rationale, double estimatedValue)
    {
        OptionIndex = optionIndex;
        Rationale = rationale;
        EstimatedValue = estimatedValue;
    }

    public int OptionIndex { get; }
    public string Rationale { get; }
    public double EstimatedValue { get; }
}
