using System;
using SM.Combat.Services;
using SM.HeadlessPolicies;

namespace SM.Editor.Validation;

internal sealed record H100MetricsRunSettings(
    int BattleCount,
    int CampaignCount,
    int ReplayCopies,
    int SeedBase,
    int CampaignSiteSafety,
    int MaxBattleSteps,
    bool WriteCsv,
    string OutputDirectory,
    string PolicyId)
{
    public string RunId => $"h100-stage2-{PolicyId}-s{SeedBase}-b{BattleCount}-c{CampaignCount}-r{ReplayCopies}-m{MaxBattleSteps}-sites{CampaignSiteSafety}";

    /// <summary>정책 비교에서도 sim/reward identity가 같도록 policy id를 제외한 profile id를 만든다.</summary>
    public string PairingProfileId(string corpusId) => $"h100-paired-s{SeedBase}-{corpusId}";

    public static H100MetricsRunSettings Smoke { get; } = new(
        BattleCount: 4,
        CampaignCount: 1,
        ReplayCopies: 2,
        SeedBase: 1701,
        CampaignSiteSafety: 2,
        MaxBattleSteps: BattleSimulator.DefaultMaxSteps,
        WriteCsv: true,
        OutputDirectory: "Logs/h100-metrics",
        PolicyId: HeadlessPolicyFactory.GreedyId);

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
            OutputDirectory: Environment.GetEnvironmentVariable("SM_H100_OUTPUT") ?? "Logs/h100-metrics",
            PolicyId: HeadlessPolicyFactory.NormalizePolicyId(Environment.GetEnvironmentVariable("SM_H100_POLICY")));
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
