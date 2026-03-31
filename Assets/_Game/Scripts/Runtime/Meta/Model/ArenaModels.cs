using System.Collections.Generic;

namespace SM.Meta.Model;

public sealed record ArenaDefenseSnapshot(
    string SnapshotId,
    string BlueprintId,
    string SnapshotHash,
    string CompileVersion,
    string CompileHash,
    string ContentVersion,
    int Rating,
    string CreatedAtUtc);

public sealed record ArenaBlueprintSlot(
    string SlotId,
    string BlueprintId,
    bool IsDefense,
    bool IsActive);

public sealed record ArenaMatchRecord(
    string MatchId,
    string SeasonId,
    string OffenseSnapshotId,
    string DefenseSnapshotId,
    int Seed,
    string MatchRecordId,
    int RatingDelta,
    string Result,
    string CreatedAtUtc);

public sealed record ArenaSeasonState(
    string SeasonId,
    string StartedAtUtc,
    string EndsAtUtc,
    int CurrentRating,
    int WeeklyChestClaimCount,
    bool IsActive);

public sealed record ArenaRewardLedgerEntry(
    string EntryId,
    string SeasonId,
    string RewardId,
    int Amount,
    string CreatedAtUtc,
    string Summary);
