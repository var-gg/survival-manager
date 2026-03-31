using System;

namespace SM.Persistence.Abstractions.Models;

[Serializable]
public sealed class InventoryLedgerEntryRecord
{
    public string EntryId = string.Empty;
    public string RunId = string.Empty;
    public string ItemInstanceId = string.Empty;
    public string ItemBaseId = string.Empty;
    public string ChangeKind = string.Empty;
    public int Amount = 0;
    public string CreatedAtUtc = string.Empty;
    public string Summary = string.Empty;
    public string SourceId = string.Empty;
    public string SourceKind = string.Empty;
}
