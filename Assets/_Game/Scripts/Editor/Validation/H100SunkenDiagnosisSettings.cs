using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using SM.Combat.Services;
using SM.HeadlessPolicies;

namespace SM.Editor.Validation;

/// <summary>Stage 5 sunken 방향 판별의 small-N/right-size 실행 설정.</summary>
internal sealed record H100SunkenDiagnosisSettings(
    int CampaignsPerPolicy,
    int ArrivalSeedAttempts,
    int SeedBase,
    int CampaignSiteSafety,
    int MaxBattleSteps,
    int OwnedBuildLimit,
    int LookbackBuildLimit,
    int MedoidCount,
    string OutputDirectory,
    IReadOnlyList<string> PolicyIds)
{
    public const string TargetSiteId = "site_sunken_bastion";

    public string RunId =>
        $"h100-stage5-sunken-s{SeedBase}-c{CampaignsPerPolicy}-a{ArrivalSeedAttempts}-p{PolicyIds.Count}"
        + $"-m{MaxBattleSteps}-owned{OwnedBuildLimit}-lookback{LookbackBuildLimit}-medoids{MedoidCount}";

    public static H100SunkenDiagnosisSettings FromEnvironment()
    {
        var policies = ParsePolicies(Environment.GetEnvironmentVariable("SM_H100_SUNKEN_POLICIES"));
        var settings = new H100SunkenDiagnosisSettings(
            CampaignsPerPolicy: ReadAtLeast("SM_H100_SUNKEN_CAMPAIGNS_PER_POLICY", 1, 1),
            ArrivalSeedAttempts: ReadAtLeast("SM_H100_SUNKEN_ARRIVAL_SEED_ATTEMPTS", 32, 1),
            SeedBase: ReadInt("SM_H100_SUNKEN_SEED_BASE", 1701),
            CampaignSiteSafety: ReadAtLeast("SM_H100_SUNKEN_SITE_SAFETY", 3, 3),
            MaxBattleSteps: ReadAtLeast(
                "SM_H100_SUNKEN_MAX_BATTLE_STEPS",
                BattleSimulator.DefaultMaxSteps,
                1),
            OwnedBuildLimit: ReadAtLeast("SM_H100_SUNKEN_OWNED_BUILD_LIMIT", 0, 0),
            LookbackBuildLimit: ReadAtLeast("SM_H100_SUNKEN_LOOKBACK_BUILD_LIMIT", 12, 1),
            MedoidCount: ReadRange("SM_H100_SUNKEN_MEDOID_COUNT", 8, 1, 8),
            OutputDirectory: Environment.GetEnvironmentVariable("SM_H100_SUNKEN_OUTPUT")
                             ?? "Logs/h100-sunken-diagnosis",
            PolicyIds: policies);
        if (settings.ArrivalSeedAttempts < settings.CampaignsPerPolicy)
        {
            throw new InvalidOperationException(
                "SM_H100_SUNKEN_ARRIVAL_SEED_ATTEMPTS must be >= SM_H100_SUNKEN_CAMPAIGNS_PER_POLICY.");
        }

        return settings;
    }

    private static IReadOnlyList<string> ParsePolicies(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return HeadlessPolicyFactory.ProductionPolicyIds.ToArray();
        }

        var policies = raw.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(HeadlessPolicyFactory.NormalizePolicyId)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        if (policies.Length == 0 || policies.Contains(HeadlessPolicyFactory.CoverageId, StringComparer.Ordinal))
        {
            throw new InvalidOperationException("Sunken diagnosis requires one or more production policy ids.");
        }

        return policies;
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

    private static int ReadRange(string name, int fallback, int minimum, int maximum)
    {
        var value = ReadInt(name, fallback);
        if (value < minimum || value > maximum)
        {
            throw new InvalidOperationException($"{name} must be in [{minimum}, {maximum}] (actual={value}).");
        }

        return value;
    }

    private static int ReadInt(string name, int fallback)
    {
        var raw = Environment.GetEnvironmentVariable(name);
        return string.IsNullOrWhiteSpace(raw)
            ? fallback
            : int.Parse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture);
    }
}
