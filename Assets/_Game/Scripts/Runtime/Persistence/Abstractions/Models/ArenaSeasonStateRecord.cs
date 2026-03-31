using System;

namespace SM.Persistence.Abstractions.Models;

[Serializable]
public sealed class ArenaSeasonStateRecord
{
    public string SeasonId = string.Empty;
    public string StartedAtUtc = string.Empty;
    public string EndsAtUtc = string.Empty;
    public int CurrentRating;
    public int WeeklyChestClaimCount;
    public bool IsActive;
}
