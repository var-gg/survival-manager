using System.Collections.Generic;

namespace SM.Combat.Model;

public sealed record BattleResult(
    TeamSide Winner,
    int TickCount,
    IReadOnlyList<BattleEvent> Events);
