using System;
using System.Globalization;
using SM.Combat.Services;

namespace SM.Editor.Validation;

internal sealed record H100BuildSpaceCensusSettings(
    string OutputDirectory,
    int ScreeningBuildCount,
    int ScreeningSeedCount,
    int SeedBase,
    int MaxBattleSteps)
{
    public string RunId => $"h100-stage3-census-s{SeedBase}-b{ScreeningBuildCount}-n{ScreeningSeedCount}-m{MaxBattleSteps}";

    public static H100BuildSpaceCensusSettings FromEnvironment()
    {
        return new H100BuildSpaceCensusSettings(
            Environment.GetEnvironmentVariable("SM_H100_CENSUS_OUTPUT") ?? "Logs/h100-build-space",
            ReadRange("SM_H100_CENSUS_BUILD_COUNT", 3, 1, 12),
            ReadRange("SM_H100_CENSUS_SEED_COUNT", 2, 1, 16),
            ReadInt("SM_H100_CENSUS_SEED_BASE", 1701),
            ReadRange("SM_H100_CENSUS_MAX_BATTLE_STEPS", BattleSimulator.DefaultMaxSteps, 1, 1000000));
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
        return string.IsNullOrWhiteSpace(raw) ? fallback : int.Parse(raw, CultureInfo.InvariantCulture);
    }
}
