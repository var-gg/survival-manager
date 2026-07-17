using System;
using System.Globalization;
using SM.Combat.Services;

namespace SM.Editor.Validation;

internal sealed record H100OptionTrapRunSettings(
    string OutputDirectory,
    int SeedBase,
    int SeedCount,
    int MedoidCount,
    int HealthySampleCount,
    int MaxBattleSteps)
{
    public string RunId => $"h100-option-trap-s{SeedBase}-n{SeedCount}-p{MedoidCount}-h{HealthySampleCount}-m{MaxBattleSteps}";

    public static H100OptionTrapRunSettings FromEnvironment()
        => new(
            Environment.GetEnvironmentVariable("SM_H100_TRAP_OUTPUT") ?? "Logs/h100-option-trap",
            ReadInt("SM_H100_TRAP_SEED_BASE", 1708),
            ReadRange("SM_H100_TRAP_SEED_COUNT", 2, 1, 16),
            ReadRange("SM_H100_TRAP_MEDOID_COUNT", 8, 1, 24),
            ReadRange("SM_H100_TRAP_HEALTHY_SAMPLE_COUNT", 12, 1, 64),
            ReadRange("SM_H100_TRAP_MAX_BATTLE_STEPS", BattleSimulator.DefaultMaxSteps, 1, 1000000));

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
            : int.Parse(raw, CultureInfo.InvariantCulture);
    }
}
