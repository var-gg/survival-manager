using System;

namespace SM.Persistence.Abstractions.Models;

[Serializable]
public sealed class ArenaMatchRecordRecord
{
    public string MatchId = string.Empty;
    public string SeasonId = string.Empty;
    public string OffenseSnapshotId = string.Empty;
    public string DefenseSnapshotId = string.Empty;
    public int Seed;
    public string MatchRecordId = string.Empty;
    public int RatingDelta;
    public string Result = string.Empty;
    public string CreatedAtUtc = string.Empty;
}
