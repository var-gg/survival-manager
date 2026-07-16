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
        IReadOnlyList<HeadlessRewardOption> rewardOptions)
    {
        DecisionSeed = decisionSeed;
        DeployCapacity = deployCapacity;
        ChapterId = chapterId;
        SiteId = siteId;
        Roster = roster;
        Anchors = anchors;
        EnemyPreview = enemyPreview;
        RewardOptions = rewardOptions;
    }

    public int DecisionSeed { get; }
    public int DeployCapacity { get; }
    public string ChapterId { get; }
    public string SiteId { get; }
    public IReadOnlyList<HeadlessHeroObservation> Roster { get; }
    public IReadOnlyList<DeploymentAnchorId> Anchors { get; }
    public HeadlessEnemyPreview EnemyPreview { get; }
    public IReadOnlyList<HeadlessRewardOption> RewardOptions { get; }
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
        DeploymentAnchorId preferredAnchor)
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
        int permanentSlotAmount)
    {
        Index = index;
        Kind = kind;
        PayloadId = payloadId;
        GoldAmount = goldAmount;
        EchoAmount = echoAmount;
        PermanentSlotAmount = permanentSlotAmount;
    }

    public int Index { get; }
    public HeadlessRewardKind Kind { get; }
    public string PayloadId { get; }
    public int GoldAmount { get; }
    public int EchoAmount { get; }
    public int PermanentSlotAmount { get; }
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
