using System;
using System.Collections.Generic;
using System.Linq;
using SM.Meta.Model;

namespace SM.Meta.Services;

/// <summary>
/// Atlas/Run 상태에서 RunBattlePayload를 만드는 pure builder.
/// Unity·Sprite·Scene 의존성을 가지지 않는다 — caller(SM.Unity)가 input record를 모아 전달한다.
/// task-run-node-battle-payload-v1 / analysis-next-cycle-atlas-reward-loop-v1 참고.
/// </summary>
public sealed record RunBattlePayloadInput(
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
    IReadOnlyList<string>? ResolvedModifierIds);

public static class RunBattlePayloadBuilder
{
    /// <summary>
    /// Validates input and returns immutable payload. active run / current node 부재 시 fail-closed
    /// (`Empty` payload + false). caller는 false 시 Battle entry를 막아야 한다.
    /// </summary>
    public static bool TryBuild(RunBattlePayloadInput input, out RunBattlePayload payload)
    {
        if (input == null)
        {
            payload = RunBattlePayload.Empty;
            return false;
        }

        if (string.IsNullOrWhiteSpace(input.RunId)
            || string.IsNullOrWhiteSpace(input.EncounterId)
            || input.SiteNodeIndex < 0
            || string.IsNullOrWhiteSpace(input.BattleContextHash))
        {
            payload = RunBattlePayload.Empty;
            return false;
        }

        IReadOnlyList<string> ids = input.ResolvedModifierIds == null
            ? Array.Empty<string>()
            : input.ResolvedModifierIds
                .Where(static id => !string.IsNullOrWhiteSpace(id))
                .ToArray();

        payload = new RunBattlePayload(
            RunId: input.RunId,
            ChapterId: input.ChapterId ?? string.Empty,
            SiteId: input.SiteId ?? string.Empty,
            SiteNodeIndex: input.SiteNodeIndex,
            EncounterId: input.EncounterId,
            ExpeditionNodeId: input.ExpeditionNodeId ?? string.Empty,
            SquadSnapshotId: input.SquadSnapshotId ?? string.Empty,
            StageCandidatePathHash: input.StageCandidatePathHash ?? string.Empty,
            NodeOverlayHash: input.NodeOverlayHash ?? string.Empty,
            BattleContextHash: input.BattleContextHash,
            RewardBiasPercent: input.RewardBiasPercent,
            ThreatPressurePercent: input.ThreatPressurePercent,
            AffinityBoostPercent: input.AffinityBoostPercent,
            ResolvedModifierIds: ids);

        return true;
    }
}
