using System;

namespace SM.Persistence.Abstractions.Models;

[Serializable]
public sealed class RewardLedgerEntryRecord
{
    public string EntryId = string.Empty;
    public string RunId = string.Empty;
    public string RewardId = string.Empty;
    public string RewardType = string.Empty;
    public int Amount = 0;
    public string CreatedAtUtc = string.Empty;
    public string Summary = string.Empty;
}
