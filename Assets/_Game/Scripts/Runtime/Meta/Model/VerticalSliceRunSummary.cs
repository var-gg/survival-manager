using System;
using System.Collections.Generic;

namespace SM.Meta.Model;

/// <summary>
/// Town → Atlas → Battle → Reward → Town 한 traversal의 evidence summary.
/// task-slice-observability-summary-v1 + task-vertical-slice-smoke-evidence-v1 acceptance.
/// PlayMode smoke 종료 시 markdown/json report로 직렬화하고, runtime에서는 developer foldout이
/// 같은 record를 read해 normal HUD를 가리지 않는 위치에 표시한다.
/// </summary>
public sealed record VerticalSliceRunSummary(
    VerticalSliceAtlasSection Atlas,
    VerticalSliceBattleSection Battle,
    VerticalSliceRewardSection Reward)
{
    public static VerticalSliceRunSummary Empty { get; } = new(
        VerticalSliceAtlasSection.Empty,
        VerticalSliceBattleSection.Empty,
        VerticalSliceRewardSection.Empty);

    public bool IsPopulated =>
        Atlas.IsPopulated || Battle.IsPopulated || Reward.IsPopulated;
}

public sealed record VerticalSliceAtlasSection(
    string RunId,
    string ChapterId,
    string SiteId,
    int SiteNodeIndex,
    string SelectedRouteHash,
    string NodeOverlayHash,
    int RewardBiasPercent,
    int ThreatPressurePercent,
    int AffinityBoostPercent,
    IReadOnlyList<string> ResolvedModifierIds)
{
    public static VerticalSliceAtlasSection Empty { get; } = new(
        RunId: string.Empty,
        ChapterId: string.Empty,
        SiteId: string.Empty,
        SiteNodeIndex: -1,
        SelectedRouteHash: string.Empty,
        NodeOverlayHash: string.Empty,
        RewardBiasPercent: 0,
        ThreatPressurePercent: 0,
        AffinityBoostPercent: 0,
        ResolvedModifierIds: Array.Empty<string>());

    public bool IsPopulated => !string.IsNullOrWhiteSpace(RunId);
}

public sealed record VerticalSliceBattleSection(
    string EncounterId,
    string FactionId,
    string BattleContextHash,
    int BattleSeed,
    bool IsBoss,
    bool LastSettlementWasVictory)
{
    public static VerticalSliceBattleSection Empty { get; } = new(
        EncounterId: string.Empty,
        FactionId: string.Empty,
        BattleContextHash: string.Empty,
        BattleSeed: 0,
        IsBoss: false,
        LastSettlementWasVictory: false);

    public bool IsPopulated => !string.IsNullOrWhiteSpace(EncounterId);
}

public sealed record VerticalSliceRewardSection(
    string RewardSourceId,
    string RewardCommitId,
    int RewardChoiceLedgerCount,
    bool HasPendingRewardSettlement)
{
    public static VerticalSliceRewardSection Empty { get; } = new(
        RewardSourceId: string.Empty,
        RewardCommitId: string.Empty,
        RewardChoiceLedgerCount: 0,
        HasPendingRewardSettlement: false);

    public bool IsPopulated => !string.IsNullOrWhiteSpace(RewardCommitId);
}
