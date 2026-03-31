using System;

namespace SM.Persistence.Abstractions.Models;

[Serializable]
public sealed class ArenaRewardLedgerEntryRecord
{
    public string EntryId = string.Empty;
    public string SeasonId = string.Empty;
    public string RewardId = string.Empty;
    public int Amount;
    public string CreatedAtUtc = string.Empty;
    public string Summary = string.Empty;
}
