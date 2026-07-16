using System;
using System.Globalization;
using SM.Combat.Services;

namespace SM.Editor.Validation;

internal sealed record H100IntentTraceRunSettings(
    int SeedCount,
    int SeedBase,
    int CampaignSiteSafety,
    int MaxBattleSteps,
    string OutputDirectory,
    string Lanes,
    string CoverageAnchorId)
{
    public static H100IntentTraceRunSettings FromEnvironment()
        => new(
            ReadRange("SM_H100_INTENT_SEED_COUNT", 8, 1, 64),
            ReadInt("SM_H100_INTENT_SEED_BASE", 1701),
            ReadRange("SM_H100_INTENT_SITE_SAFETY", 2, 1, 128),
            ReadRange("SM_H100_INTENT_MAX_BATTLE_STEPS", BattleSimulator.DefaultMaxSteps, 1, 1_000_000),
            Environment.GetEnvironmentVariable("SM_H100_INTENT_OUTPUT") ?? "Logs/h100-intent-trace",
            NormalizeLanes(Environment.GetEnvironmentVariable("SM_H100_INTENT_LANES")),
            Environment.GetEnvironmentVariable("SM_H100_INTENT_COVERAGE_ANCHOR") ?? "anchor_iron_line");

    public bool IncludesCoverage => Lanes is "coverage" or "both";
    public bool IncludesDiscovery => Lanes is "discovery" or "both";

    private static string NormalizeLanes(string value)
    {
        var normalized = string.IsNullOrWhiteSpace(value) ? "both" : value.Trim().ToLowerInvariant();
        return normalized is "coverage" or "discovery" or "both"
            ? normalized
            : throw new InvalidOperationException(
                $"SM_H100_INTENT_LANES must be coverage, discovery, or both (actual={value}).");
    }

    private static int ReadRange(string name, int fallback, int minimum, int maximum)
    {
        var value = ReadInt(name, fallback);
        if (value < minimum || value > maximum)
        {
            throw new InvalidOperationException($"{name} must be within [{minimum}, {maximum}] (actual={value}).");
        }

        return value;
    }

    private static int ReadInt(string name, int fallback)
    {
        var raw = Environment.GetEnvironmentVariable(name);
        return string.IsNullOrWhiteSpace(raw)
            ? fallback
            : int.Parse(raw, CultureInfo.InvariantCulture);
    }
}
