using System;
using System.Globalization;
using SM.Combat.Services;

namespace SM.Editor.Validation;

internal sealed record H100TacticalAttributionRunSettings(
    int CompositionCount,
    int SeedCount,
    int SeedBase,
    int MaxBattleSteps,
    string OutputDirectory,
    string FormationReportPath,
    string IntentTrackReportPath,
    string PreviewPolicyReportPath)
{
    public string RunId
        => $"h100-bt1-e09-c{CompositionCount}-s{SeedBase}-n{SeedCount}-m{MaxBattleSteps}";

    public static H100TacticalAttributionRunSettings FromEnvironment()
        => new(
            ReadPositiveInt("SM_H100_TACTICAL_ATTRIBUTION_COMPOSITION_COUNT", 8),
            ReadPositiveInt("SM_H100_TACTICAL_ATTRIBUTION_SEED_COUNT", 2),
            ReadInt("SM_H100_TACTICAL_ATTRIBUTION_SEED_BASE", 1701),
            ReadPositiveInt("SM_H100_TACTICAL_ATTRIBUTION_MAX_BATTLE_STEPS", BattleSimulator.DefaultMaxSteps),
            Environment.GetEnvironmentVariable("SM_H100_TACTICAL_ATTRIBUTION_OUTPUT")
            ?? "Logs/h100-tactical-attribution",
            Environment.GetEnvironmentVariable("SM_H100_TACTICAL_ATTRIBUTION_FORMATION_REPORT")
            ?? "Logs/h100-formation/formation-report.json",
            Environment.GetEnvironmentVariable("SM_H100_TACTICAL_ATTRIBUTION_INTENT_REPORT")
            ?? "Logs/h100-intent-track/intent_track_report.json",
            Environment.GetEnvironmentVariable("SM_H100_TACTICAL_ATTRIBUTION_PREVIEW_REPORT")
            ?? "Logs/h100-preview-policy/preview-policy-acceptance.json");

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
