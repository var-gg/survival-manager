using System.Collections.Generic;
using SM.Combat.Model;

namespace SM.Unity;

public enum BattleReplayFrameKind
{
    Intro = 0,
    Event = 1,
    Result = 2
}

public sealed record BattleReplayFrame(
    BattleReplayFrameKind FrameKind,
    int SequenceIndex,
    int Tick,
    BattleActionType? ActionType,
    string? SourceId,
    string? SourceName,
    string? TargetId,
    string? TargetName,
    float Value,
    string Note,
    float? BeforeSourceHealth,
    float? AfterSourceHealth,
    float? BeforeTargetHealth,
    float? AfterTargetHealth,
    float DurationSeconds,
    IReadOnlyList<BattleReplayActorSnapshot> ActorStates);
