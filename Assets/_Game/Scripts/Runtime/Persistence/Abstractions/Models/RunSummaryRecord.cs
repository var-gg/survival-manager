using System;

namespace SM.Persistence.Abstractions.Models;

[Serializable]
public sealed class RunSummaryRecord
{
    public string RunId = string.Empty;
    public string ExpeditionId = string.Empty;
    public string Result = string.Empty;
    public int GoldEarned;
    public int NodesCleared;
    public string CompletedAtUtc = string.Empty;
}
