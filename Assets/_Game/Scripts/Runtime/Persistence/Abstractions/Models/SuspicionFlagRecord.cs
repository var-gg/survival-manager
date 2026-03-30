using System;

namespace SM.Persistence.Abstractions.Models;

[Serializable]
public sealed class SuspicionFlagRecord
{
    public string FlagId = string.Empty;
    public string RunId = string.Empty;
    public string MatchId = string.Empty;
    public string Reason = string.Empty;
    public string ExpectedHash = string.Empty;
    public string ObservedHash = string.Empty;
    public string CreatedAtUtc = string.Empty;
}
