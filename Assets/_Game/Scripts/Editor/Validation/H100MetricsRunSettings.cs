using System;
using SM.Combat.Services;

namespace SM.Editor.Validation;

internal sealed record H100MetricsRunSettings(
    int BattleCount,
    int CampaignCount,
    int ReplayCopies,
    int SeedBase,
    int CampaignSiteSafety,
    int MaxBattleSteps,
    bool WriteCsv,
    string OutputDirectory)
{
    public const string PolicyId = "scripted-player-view-v1";

    public string RunId => $"h100-stage1-s{SeedBase}-b{BattleCount}-c{CampaignCount}-r{ReplayCopies}-m{MaxBattleSteps}-sites{CampaignSiteSafety}";

    public static H100MetricsRunSettings Smoke { get; } = new(
        BattleCount: 4,
        CampaignCount: 1,
        ReplayCopies: 2,
        SeedBase: 1701,
        CampaignSiteSafety: 2,
        MaxBattleSteps: BattleSimulator.DefaultMaxSteps,
        WriteCsv: true,
        OutputDirectory: "Logs/h100-metrics");

    public static H100MetricsRunSettings FromEnvironment()
    {
        return new H100MetricsRunSettings(
            BattleCount: ReadPositiveInt("SM_H100_BATTLE_COUNT", 10000),
            CampaignCount: ReadPositiveInt("SM_H100_CAMPAIGN_COUNT", 10000),
            ReplayCopies: ReadAtLeast("SM_H100_REPLAY_COPIES", 2, 2),
            SeedBase: ReadInt("SM_H100_SEED_BASE", 1701),
            CampaignSiteSafety: ReadPositiveInt("SM_H100_SITE_SAFETY", 32),
            MaxBattleSteps: ReadPositiveInt("SM_H100_MAX_BATTLE_STEPS", BattleSimulator.DefaultMaxSteps),
            WriteCsv: ReadBool("SM_H100_WRITE_CSV", true),
            OutputDirectory: Environment.GetEnvironmentVariable("SM_H100_OUTPUT") ?? "Logs/h100-metrics");
    }

    private static int ReadPositiveInt(string name, int fallback)
    {
        var value = ReadInt(name, fallback);
        if (value <= 0)
        {
            throw new InvalidOperationException($"{name} must be > 0 (actual={value}).");
        }

        return value;
    }

    private static int ReadAtLeast(string name, int fallback, int minimum)
    {
        var value = ReadInt(name, fallback);
        if (value < minimum)
        {
            throw new InvalidOperationException($"{name} must be >= {minimum} (actual={value}).");
        }

        return value;
    }

    private static int ReadInt(string name, int fallback)
    {
        var raw = Environment.GetEnvironmentVariable(name);
        return string.IsNullOrWhiteSpace(raw) ? fallback : int.Parse(raw, System.Globalization.CultureInfo.InvariantCulture);
    }

    private static bool ReadBool(string name, bool fallback)
    {
        var raw = Environment.GetEnvironmentVariable(name);
        return string.IsNullOrWhiteSpace(raw) ? fallback : bool.Parse(raw);
    }
}
