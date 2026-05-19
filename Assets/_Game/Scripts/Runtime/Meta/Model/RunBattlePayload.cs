using System;
using System.Collections.Generic;

namespace SM.Meta.Model;

/// <summary>
/// Atlas 선택부터 Battle 진입·Reward 정산까지 한 cycle을 deterministic하게 운반하는 payload.
/// Battle/Reward/save-resume이 같은 source-of-truth를 받게 하기 위한 immutable contract다.
/// task-run-node-battle-payload-v1 / analysis-next-cycle-atlas-reward-loop-v1 참고.
/// </summary>
public sealed record RunBattlePayload(
    string RunId,
    string ChapterId,
    string SiteId,
    int SiteNodeIndex,
    string EncounterId,
    string ExpeditionNodeId,
    string SquadSnapshotId,
    string StageCandidatePathHash,
    string NodeOverlayHash,
    string BattleContextHash,
    int RewardBiasPercent,
    int ThreatPressurePercent,
    int AffinityBoostPercent,
    IReadOnlyList<string> ResolvedModifierIds)
{
    public bool HasAnyModifier =>
        RewardBiasPercent != 0 || ThreatPressurePercent != 0 || AffinityBoostPercent != 0;

    public bool IsValid =>
        !string.IsNullOrWhiteSpace(RunId)
        && !string.IsNullOrWhiteSpace(EncounterId)
        && SiteNodeIndex >= 0
        && !string.IsNullOrWhiteSpace(BattleContextHash);

    public static RunBattlePayload Empty { get; } = new(
        RunId: string.Empty,
        ChapterId: string.Empty,
        SiteId: string.Empty,
        SiteNodeIndex: -1,
        EncounterId: string.Empty,
        ExpeditionNodeId: string.Empty,
        SquadSnapshotId: string.Empty,
        StageCandidatePathHash: string.Empty,
        NodeOverlayHash: string.Empty,
        BattleContextHash: string.Empty,
        RewardBiasPercent: 0,
        ThreatPressurePercent: 0,
        AffinityBoostPercent: 0,
        ResolvedModifierIds: Array.Empty<string>());
}
