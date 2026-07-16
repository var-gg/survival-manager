using System;
using System.Globalization;
using SM.Combat.Services;
using SM.HeadlessPolicies;

namespace SM.Editor.Validation;

internal sealed record H100FormationRunSettings(
    int SeedCount,
    int SeedBase,
    int MaxBattleSteps,
    string OutputDirectory,
    string CompetentPolicyId)
{
    public string RunId
        => $"h100-stage4-formation-{CompetentPolicyId}-s{SeedBase}-n{SeedCount}-m{MaxBattleSteps}";

    public string PairingProfileId(int seed) => $"h100-stage4-paired-{seed}";

    public static H100FormationRunSettings FromEnvironment()
    {
        var policyId = HeadlessPolicyFactory.NormalizePolicyId(
            Environment.GetEnvironmentVariable("SM_H100_FORMATION_COMPETENT_POLICY")
            ?? HeadlessPolicyFactory.FormationId);
        if (!policyId.StartsWith("competent-", StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Stage 4 competent policy must use the competent- cohort (actual={policyId}).");
        }

        return new H100FormationRunSettings(
            ReadPositiveInt("SM_H100_FORMATION_SEED_COUNT", 5),
            ReadInt("SM_H100_FORMATION_SEED_BASE", 1701),
            ReadPositiveInt("SM_H100_FORMATION_MAX_BATTLE_STEPS", BattleSimulator.DefaultMaxSteps),
            Environment.GetEnvironmentVariable("SM_H100_FORMATION_OUTPUT") ?? "Logs/h100-formation",
            policyId);
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

    private static int ReadInt(string name, int fallback)
    {
        var raw = Environment.GetEnvironmentVariable(name);
        return string.IsNullOrWhiteSpace(raw)
            ? fallback
            : int.Parse(raw, CultureInfo.InvariantCulture);
    }
}
