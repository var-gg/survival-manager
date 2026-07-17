using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using SM.Combat.Services;
using SM.HeadlessPolicies;

namespace SM.Editor.Validation;

internal sealed record H100PreviewPolicyAcceptanceSettings(
    int ArrivalsPerPolicySite,
    int ArrivalSeedAttempts,
    int SeedBase,
    int CampaignSiteSafety,
    int MaxBattleSteps,
    int OwnedBuildLimit,
    int MedoidCount,
    string OutputDirectory,
    IReadOnlyList<string> BaselinePolicyIds)
{
    public static IReadOnlyList<string> TargetSiteIds { get; } = new[]
    {
        "site_ashen_gate",
        "site_wolfpine_trail",
        H100SunkenDiagnosisSettings.TargetSiteId,
    };

    public static IReadOnlyList<string> HeldOutSiteIds { get; } = TargetSiteIds
        .Where(value => !string.Equals(value, H100SunkenDiagnosisSettings.TargetSiteId, StringComparison.Ordinal))
        .ToArray();

    public string RunId =>
        $"h100-bt1-e06-preview-s{SeedBase}-c{ArrivalsPerPolicySite}-a{ArrivalSeedAttempts}"
        + $"-p{BaselinePolicyIds.Count}-m{MaxBattleSteps}-owned{OwnedBuildLimit}-medoids{MedoidCount}";

    public static H100PreviewPolicyAcceptanceSettings FromEnvironment()
    {
        var settings = new H100PreviewPolicyAcceptanceSettings(
            ArrivalsPerPolicySite: ReadAtLeast("SM_H100_PREVIEW_ARRIVALS_PER_POLICY_SITE", 1, 1),
            ArrivalSeedAttempts: ReadAtLeast("SM_H100_PREVIEW_ARRIVAL_SEED_ATTEMPTS", 32, 1),
            SeedBase: ReadInt("SM_H100_PREVIEW_SEED_BASE", 1701),
            CampaignSiteSafety: ReadAtLeast("SM_H100_PREVIEW_SITE_SAFETY", 3, 3),
            MaxBattleSteps: ReadAtLeast(
                "SM_H100_PREVIEW_MAX_BATTLE_STEPS",
                BattleSimulator.DefaultMaxSteps,
                1),
            OwnedBuildLimit: ReadAtLeast("SM_H100_PREVIEW_OWNED_BUILD_LIMIT", 0, 0),
            MedoidCount: ReadRange("SM_H100_PREVIEW_MEDOID_COUNT", 8, 1, 8),
            OutputDirectory: Environment.GetEnvironmentVariable("SM_H100_PREVIEW_OUTPUT")
                             ?? "Logs/h100-preview-policy",
            BaselinePolicyIds: ParsePolicies(Environment.GetEnvironmentVariable("SM_H100_PREVIEW_BASELINE_POLICIES")));
        if (settings.ArrivalSeedAttempts < settings.ArrivalsPerPolicySite)
        {
            throw new InvalidOperationException(
                "SM_H100_PREVIEW_ARRIVAL_SEED_ATTEMPTS must be >= SM_H100_PREVIEW_ARRIVALS_PER_POLICY_SITE.");
        }

        return settings;
    }

    private static IReadOnlyList<string> ParsePolicies(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return HeadlessPolicyFactory.ProductionPolicyIds.ToArray();
        }

        var policies = raw.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(HeadlessPolicyFactory.NormalizePolicyId)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        if (policies.Length == 0
            || policies.Any(value => !HeadlessPolicyFactory.ProductionPolicyIds.Contains(value, StringComparer.Ordinal)))
        {
            throw new InvalidOperationException("Preview acceptance baselines must be existing production policy ids.");
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
