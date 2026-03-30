using System;

namespace SM.Persistence.Abstractions.Models;

[Serializable]
public sealed class MatchRecordHeader
{
    public string MatchId = string.Empty;
    public string RunId = string.Empty;
    public string ContentVersion = string.Empty;
    public string SimVersion = string.Empty;
    public int Seed = 0;
    public string PlayerSnapshotHash = string.Empty;
    public string EnemySnapshotHash = string.Empty;
    public string StartedAtUtc = string.Empty;
    public string CompletedAtUtc = string.Empty;
    public string Winner = string.Empty;
    public string FinalStateHash = string.Empty;
}
