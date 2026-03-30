using System.Collections.Generic;
using SM.Combat.Model;

namespace SM.Unity.Sandbox;

public sealed record CombatSandboxRunRequest(
    BattleLoadoutSnapshot PlayerSnapshot,
    IReadOnlyList<BattleUnitLoadout> EnemyLoadout,
    int Seed,
    int BatchCount,
    string RequestedConfigId);

public sealed record CombatSandboxMetrics(
    float WinRate,
    float AverageDurationSeconds,
    float AverageEventCount,
    float AverageFirstActionSeconds,
    int BatchCount);

public sealed record CombatSandboxRunResult(
    BattleLoadoutSnapshot PlayerSnapshot,
    IReadOnlyList<BattleUnitLoadout> EnemyLoadout,
    BattleReplayBundle LastReplay,
    CombatSandboxMetrics Metrics,
    string ReplayHash,
    IReadOnlyList<CompileProvenanceEntry> Provenance);
