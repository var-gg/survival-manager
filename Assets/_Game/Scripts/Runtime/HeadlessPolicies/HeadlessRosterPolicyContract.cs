using System;
using System.Collections.Generic;
using System.Linq;

namespace SM.HeadlessPolicies;

/// <summary>Town UI가 현재 공개한 roster agency만 담는 순수 observation.</summary>
public sealed class HeadlessRosterPolicyObservation
{
    public HeadlessRosterPolicyObservation(
        int decisionSeed,
        string chapterId,
        string siteId,
        int rosterCapacity,
        IReadOnlyList<HeadlessHeroObservation> roster,
        HeadlessWalletObservation wallet,
        IReadOnlyList<HeadlessRecruitOfferObservation> recruitOffers,
        IReadOnlyList<HeadlessPassiveHeroObservation> passiveHeroes,
        IReadOnlyList<HeadlessRefitItemObservation> refitItems,
        IReadOnlyDictionary<string, string> evidenceFactIdsBySignal = null)
    {
        DecisionSeed = decisionSeed;
        ChapterId = chapterId ?? string.Empty;
        SiteId = siteId ?? string.Empty;
        RosterCapacity = rosterCapacity;
        Roster = roster ?? Array.Empty<HeadlessHeroObservation>();
        Wallet = wallet ?? HeadlessWalletObservation.Empty;
        RecruitOffers = recruitOffers ?? Array.Empty<HeadlessRecruitOfferObservation>();
        PassiveHeroes = passiveHeroes ?? Array.Empty<HeadlessPassiveHeroObservation>();
        RefitItems = refitItems ?? Array.Empty<HeadlessRefitItemObservation>();
        var evidence = new SortedDictionary<string, string>(StringComparer.Ordinal);
        if (evidenceFactIdsBySignal != null)
        {
            foreach (var pair in evidenceFactIdsBySignal)
            {
                evidence[pair.Key] = pair.Value;
            }
        }

        EvidenceFactIdsBySignal = new System.Collections.ObjectModel.ReadOnlyDictionary<string, string>(evidence);
    }

    public int DecisionSeed { get; }
    public string ChapterId { get; }
    public string SiteId { get; }
    public int RosterCapacity { get; }
    public IReadOnlyList<HeadlessHeroObservation> Roster { get; }
    public HeadlessWalletObservation Wallet { get; }
    public IReadOnlyList<HeadlessRecruitOfferObservation> RecruitOffers { get; }
    public IReadOnlyList<HeadlessPassiveHeroObservation> PassiveHeroes { get; }
    public IReadOnlyList<HeadlessRefitItemObservation> RefitItems { get; }
    public IReadOnlyDictionary<string, string> EvidenceFactIdsBySignal { get; }

    public HeadlessRosterPolicyObservation WithEvidenceFactIds(
        IReadOnlyDictionary<string, string> evidenceFactIdsBySignal)
        => new(
            DecisionSeed,
            ChapterId,
            SiteId,
            RosterCapacity,
            Roster,
            Wallet,
            RecruitOffers,
            PassiveHeroes,
            RefitItems,
            evidenceFactIdsBySignal);
}

public sealed class HeadlessRecruitOfferObservation
{
    public HeadlessRecruitOfferObservation(
        int offerIndex,
        string archetypeId,
        string raceId,
        string classId,
        string roleTag,
        string flexActiveSkillId,
        string flexPassiveSkillId,
        int goldCost,
        string tier,
        string planFit,
        bool isDuplicate)
    {
        OfferIndex = offerIndex;
        ArchetypeId = archetypeId ?? string.Empty;
        RaceId = raceId ?? string.Empty;
        ClassId = classId ?? string.Empty;
        RoleTag = roleTag ?? string.Empty;
        FlexActiveSkillId = flexActiveSkillId ?? string.Empty;
        FlexPassiveSkillId = flexPassiveSkillId ?? string.Empty;
        GoldCost = goldCost;
        Tier = tier ?? string.Empty;
        PlanFit = planFit ?? string.Empty;
        IsDuplicate = isDuplicate;
    }

    public int OfferIndex { get; }
    public string ArchetypeId { get; }
    public string RaceId { get; }
    public string ClassId { get; }
    public string RoleTag { get; }
    public string FlexActiveSkillId { get; }
    public string FlexPassiveSkillId { get; }
    public int GoldCost { get; }
    public string Tier { get; }
    public string PlanFit { get; }
    public bool IsDuplicate { get; }
}

public sealed class HeadlessPassiveHeroObservation
{
    public HeadlessPassiveHeroObservation(
        string heroId,
        int level,
        string selectedBoardId,
        IReadOnlyList<string> selectedNodeIds,
        int maxActiveNodeCount,
        int maxKeystoneCount,
        IReadOnlyList<HeadlessPassiveBoardObservation> boards)
    {
        HeroId = heroId ?? string.Empty;
        Level = level;
        SelectedBoardId = selectedBoardId ?? string.Empty;
        SelectedNodeIds = selectedNodeIds ?? Array.Empty<string>();
        MaxActiveNodeCount = maxActiveNodeCount;
        MaxKeystoneCount = maxKeystoneCount;
        Boards = boards ?? Array.Empty<HeadlessPassiveBoardObservation>();
    }

    public string HeroId { get; }
    public int Level { get; }
    public string SelectedBoardId { get; }
    public IReadOnlyList<string> SelectedNodeIds { get; }
    public int MaxActiveNodeCount { get; }
    public int MaxKeystoneCount { get; }
    public IReadOnlyList<HeadlessPassiveBoardObservation> Boards { get; }
}

public sealed class HeadlessPassiveBoardObservation
{
    public HeadlessPassiveBoardObservation(string boardId, IReadOnlyList<HeadlessPassiveNodeObservation> nodes)
    {
        BoardId = boardId ?? string.Empty;
        Nodes = nodes ?? Array.Empty<HeadlessPassiveNodeObservation>();
    }

    public string BoardId { get; }
    public IReadOnlyList<HeadlessPassiveNodeObservation> Nodes { get; }
}

public sealed class HeadlessPassiveNodeObservation
{
    public HeadlessPassiveNodeObservation(
        string nodeId,
        int boardDepth,
        string nodeKind,
        IReadOnlyList<string> prerequisiteNodeIds,
        IReadOnlyList<string> mutualExclusionTagIds,
        string grantedSkillId,
        IReadOnlyList<string> compileTags,
        IReadOnlyList<HeadlessStatModifierObservation> statModifiers,
        IReadOnlyList<HeadlessRuleModifierObservation> ruleModifiers)
    {
        NodeId = nodeId ?? string.Empty;
        BoardDepth = boardDepth;
        NodeKind = nodeKind ?? string.Empty;
        PrerequisiteNodeIds = prerequisiteNodeIds ?? Array.Empty<string>();
        MutualExclusionTagIds = mutualExclusionTagIds ?? Array.Empty<string>();
        GrantedSkillId = grantedSkillId ?? string.Empty;
        CompileTags = compileTags ?? Array.Empty<string>();
        StatModifiers = statModifiers ?? Array.Empty<HeadlessStatModifierObservation>();
        RuleModifiers = ruleModifiers ?? Array.Empty<HeadlessRuleModifierObservation>();
    }

    public string NodeId { get; }
    public int BoardDepth { get; }
    public string NodeKind { get; }
    public IReadOnlyList<string> PrerequisiteNodeIds { get; }
    public IReadOnlyList<string> MutualExclusionTagIds { get; }
    public string GrantedSkillId { get; }
    public IReadOnlyList<string> CompileTags { get; }
    public IReadOnlyList<HeadlessStatModifierObservation> StatModifiers { get; }
    public IReadOnlyList<HeadlessRuleModifierObservation> RuleModifiers { get; }
}

public sealed class HeadlessRefitItemObservation
{
    public HeadlessRefitItemObservation(
        string itemId,
        string itemInstanceId,
        string equippedHeroId,
        IReadOnlyList<string> tags,
        string weaponFamilyTag,
        int echoCost,
        IReadOnlyList<HeadlessRefitSlotObservation> affixSlots,
        bool allowsSeal = false,
        IReadOnlyList<HeadlessSealCostObservation> sealCosts = null)
    {
        ItemId = itemId ?? string.Empty;
        ItemInstanceId = itemInstanceId ?? string.Empty;
        EquippedHeroId = equippedHeroId ?? string.Empty;
        Tags = tags ?? Array.Empty<string>();
        WeaponFamilyTag = weaponFamilyTag ?? string.Empty;
        EchoCost = echoCost;
        AffixSlots = affixSlots ?? Array.Empty<HeadlessRefitSlotObservation>();
        AllowsSeal = allowsSeal;
        SealCosts = sealCosts ?? Array.Empty<HeadlessSealCostObservation>();
    }

    public string ItemId { get; }
    public string ItemInstanceId { get; }
    public string EquippedHeroId { get; }
    public IReadOnlyList<string> Tags { get; }
    public string WeaponFamilyTag { get; }
    public int EchoCost { get; }
    public IReadOnlyList<HeadlessRefitSlotObservation> AffixSlots { get; }
    public bool AllowsSeal { get; }
    public IReadOnlyList<HeadlessSealCostObservation> SealCosts { get; }
}

/// <summary>
/// Town UI가 현재 item에 표시할 수 있는 lock 개수별 exact Seal 비용.
/// 목록에 없는 lock 개수는 현재 floor/operation 계약에서 사용할 수 없다.
/// </summary>
public sealed class HeadlessSealCostObservation
{
    public HeadlessSealCostObservation(int lockedAffixCount, int echoCost)
    {
        LockedAffixCount = lockedAffixCount;
        EchoCost = echoCost;
    }

    public int LockedAffixCount { get; }
    public int EchoCost { get; }
}

public sealed class HeadlessRefitSlotObservation
{
    public HeadlessRefitSlotObservation(
        int slotIndex,
        HeadlessAffixMechanicsObservation currentAffix,
        bool canRefit,
        double rollQuality = 0d)
    {
        SlotIndex = slotIndex;
        CurrentAffix = currentAffix;
        CanRefit = canRefit;
        RollQuality = rollQuality;
    }

    public int SlotIndex { get; }
    public HeadlessAffixMechanicsObservation CurrentAffix { get; }
    public bool CanRefit { get; }
    public double RollQuality { get; }
}

public sealed class HeadlessRecruitDecision
{
    public HeadlessRecruitDecision(int offerIndex, string rationale, double estimatedValue, IReadOnlyList<string> evidenceFactIds)
    {
        OfferIndex = offerIndex;
        Rationale = rationale ?? string.Empty;
        EstimatedValue = estimatedValue;
        EvidenceFactIds = evidenceFactIds ?? Array.Empty<string>();
    }

    public int OfferIndex { get; }
    public string Rationale { get; }
    public double EstimatedValue { get; }
    public IReadOnlyList<string> EvidenceFactIds { get; }
    public bool IsNoOp => OfferIndex < 0;

    public HeadlessRecruitDecision WithRationale(string rationale)
        => new(OfferIndex, rationale, EstimatedValue, EvidenceFactIds);
}

public sealed class HeadlessPassiveDecision
{
    public HeadlessPassiveDecision(
        string heroId,
        string boardId,
        string nodeId,
        string rationale,
        double estimatedValue,
        IReadOnlyList<string> evidenceFactIds)
    {
        HeroId = heroId ?? string.Empty;
        BoardId = boardId ?? string.Empty;
        NodeId = nodeId ?? string.Empty;
        Rationale = rationale ?? string.Empty;
        EstimatedValue = estimatedValue;
        EvidenceFactIds = evidenceFactIds ?? Array.Empty<string>();
    }

    public string HeroId { get; }
    public string BoardId { get; }
    public string NodeId { get; }
    public string Rationale { get; }
    public double EstimatedValue { get; }
    public IReadOnlyList<string> EvidenceFactIds { get; }
    public bool IsNoOp => string.IsNullOrWhiteSpace(HeroId) || string.IsNullOrWhiteSpace(NodeId);

    public HeadlessPassiveDecision WithOutcome(
        string rationale,
        double estimatedValue,
        IReadOnlyList<string> evidenceFactIds)
        => new(HeroId, BoardId, NodeId, rationale, estimatedValue, evidenceFactIds);
}

public sealed class HeadlessRefitDecision
{
    public HeadlessRefitDecision(
        string itemInstanceId,
        int affixSlotIndex,
        string rationale,
        double estimatedValue,
        IReadOnlyList<string> evidenceFactIds,
        IReadOnlyList<string> sealedAffixIds = null)
    {
        ItemInstanceId = itemInstanceId ?? string.Empty;
        AffixSlotIndex = affixSlotIndex;
        Rationale = rationale ?? string.Empty;
        EstimatedValue = estimatedValue;
        EvidenceFactIds = evidenceFactIds ?? Array.Empty<string>();
        SealedAffixIds = (sealedAffixIds ?? Array.Empty<string>())
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
    }

    public string ItemInstanceId { get; }
    public int AffixSlotIndex { get; }
    public string Rationale { get; }
    public double EstimatedValue { get; }
    public IReadOnlyList<string> EvidenceFactIds { get; }
    public IReadOnlyList<string> SealedAffixIds { get; }
    public bool IsNoOp => string.IsNullOrWhiteSpace(ItemInstanceId) || AffixSlotIndex < 0;

    public HeadlessRefitDecision WithRationale(string rationale)
        => new(
            ItemInstanceId,
            AffixSlotIndex,
            rationale,
            EstimatedValue,
            EvidenceFactIds,
            SealedAffixIds);
}
