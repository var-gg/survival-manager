using System.Collections.Generic;

namespace SM.Combat.Model;

public sealed record BattleLoadoutSnapshot(
    string SnapshotId,
    string CompileVersion,
    string CompileHash,
    TeamTacticProfile TeamTactic,
    IReadOnlyList<BattleUnitLoadout> Allies,
    IReadOnlyList<string> BattleDeployHeroIds,
    IReadOnlyList<string> TeamTags);

public sealed record BattleReplayHeader(
    string MatchId,
    string ContentVersion,
    string SimVersion,
    int Seed,
    string PlayerSnapshotHash,
    string EnemySnapshotHash,
    string StartedAtUtc,
    string CompletedAtUtc,
    TeamSide Winner,
    string FinalStateHash);

public sealed record BattleInputSnapshot(
    string CompileVersion,
    string CompileHash,
    TeamPostureType TeamPosture,
    IReadOnlyList<BattleUnitLoadout> Allies,
    IReadOnlyList<BattleUnitLoadout> Enemies,
    IReadOnlyList<string> TeamTags);

public sealed record BattleKeyframeDigest(
    int StepIndex,
    float TimeSeconds,
    string StateHash);

public sealed record BattleReplayBundle(
    BattleReplayHeader Header,
    BattleInputSnapshot Input,
    IReadOnlyList<BattleEvent> EventStream,
    IReadOnlyList<BattleKeyframeDigest> Keyframes);
