using System.Collections.Generic;
using SM.Combat.Model;

namespace SM.Unity;

public sealed record BattleReplayActorSnapshot(
    string Id,
    string Name,
    TeamSide Side,
    RowPosition Row,
    string RaceId,
    string ClassId,
    float CurrentHealth,
    float MaxHealth,
    bool IsAlive);

public sealed record BattleReplayTrack(
    IReadOnlyList<BattleReplayActorSnapshot> InitialRoster,
    IReadOnlyList<BattleReplayFrame> Frames,
    TeamSide Winner,
    int TickCount,
    int EventCount);
