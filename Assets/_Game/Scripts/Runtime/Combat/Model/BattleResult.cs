using System.Collections.Generic;

namespace SM.Combat.Model;

public sealed record BattleResult(
    TeamSide Winner,
    int StepCount,
    float DurationSeconds,
    IReadOnlyList<BattleEvent> Events,
    IReadOnlyList<BattleUnitReadModel> FinalUnits,
    IReadOnlyList<TelemetryEventRecord>? TelemetryEvents = null);
