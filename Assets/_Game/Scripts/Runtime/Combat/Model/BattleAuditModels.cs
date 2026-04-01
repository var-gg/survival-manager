using System.Collections.Generic;
using SM.Core.Stats;

namespace SM.Combat.Model;

public sealed record CompileProvenanceEntry(
    string SubjectId,
    ModifierSource Source,
    string SourceId,
    string ArtifactKind,
    IReadOnlyList<string> Details);

public sealed record BattleLoadoutSnapshot(
    string SnapshotId,
    string CompileVersion,
    string CompileHash,
    TeamTacticProfile TeamTactic,
    IReadOnlyList<BattleUnitLoadout> Allies,
    IReadOnlyList<string> BattleDeployHeroIds,
    IReadOnlyList<string> TeamTags,
    IReadOnlyList<CompileProvenanceEntry>? Provenance = null,
    TeamCounterCoverageReport? TeamCounterCoverage = null);

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
    IReadOnlyList<string> TeamTags,
    IReadOnlyList<CompileProvenanceEntry>? Provenance = null,
    TeamCounterCoverageReport? AllyCounterCoverage = null,
    TeamCounterCoverageReport? EnemyCounterCoverage = null);

public sealed record BattleKeyframeDigest(
    int StepIndex,
    float TimeSeconds,
    string StateHash);

public sealed record BattleReplayBundle(
    BattleReplayHeader Header,
    BattleInputSnapshot Input,
    IReadOnlyList<BattleEvent> EventStream,
    IReadOnlyList<BattleKeyframeDigest> Keyframes);
