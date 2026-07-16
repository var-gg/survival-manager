using System;
using System.Globalization;
using System.Linq;
using SM.Combat.Services;
using SM.HeadlessCensus;

namespace SM.Editor.Validation;

internal sealed record H100IntentTrackRunSettings(
    int SeedCount,
    int SeedBase,
    int CampaignSiteSafety,
    int MaxBattleSteps,
    int SystemMedoidSampleCount,
    string OutputDirectory,
    string[] EnabledLeverIds)
{
    public static H100IntentTrackRunSettings FromEnvironment()
        => new(
            ReadRange("SM_H100_TRACK_SEED_COUNT", 16, 1, 64),
            ReadInt("SM_H100_TRACK_SEED_BASE", 1701),
            ReadRange("SM_H100_TRACK_SITE_SAFETY", 32, 1, 128),
            ReadRange("SM_H100_TRACK_MAX_BATTLE_STEPS", BattleSimulator.DefaultMaxSteps, 1, 1_000_000),
            ReadRange("SM_H100_TRACK_MEDOID_COUNT", 8, 0, 451),
            Environment.GetEnvironmentVariable("SM_H100_TRACK_OUTPUT") ?? "Logs/h100-intent-track",
            ReadLevers(Environment.GetEnvironmentVariable("SM_H100_TRACK_LEVERS")));

    private static string[] ReadLevers(string? raw)
    {
        var supported = new[]
        {
            IntentTrackLeverId.Deployment,
            IntentTrackLeverId.Reward,
            IntentTrackLeverId.Recruit,
            IntentTrackLeverId.LevelNode,
            IntentTrackLeverId.Refit,
        };
        var values = (string.IsNullOrWhiteSpace(raw) ? "deployment,reward" : raw)
            .Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(value => value.Trim().ToLowerInvariant())
            .Distinct(StringComparer.Ordinal)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        var unknown = values.FirstOrDefault(value => !supported.Contains(value, StringComparer.Ordinal));
        if (unknown != null)
        {
            throw new InvalidOperationException($"Unsupported intent-track lever: {unknown}");
        }

        return values;
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
