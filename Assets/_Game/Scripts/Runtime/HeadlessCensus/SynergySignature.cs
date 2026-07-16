using System;
using System.Collections.Generic;

namespace SM.HeadlessCensus;

public sealed record SynergySignature(
    IReadOnlyList<string> TierIds,
    IReadOnlyList<string> DoctrineRuleIds,
    int RaceTier2Count,
    int RaceTier4Count,
    int ClassTier2Count,
    int ClassTier3Count)
{
    public string Signature => $"tiers={string.Join("+", TierIds ?? Array.Empty<string>())};"
                               + $"doctrines={string.Join("+", DoctrineRuleIds ?? Array.Empty<string>())}";
}
