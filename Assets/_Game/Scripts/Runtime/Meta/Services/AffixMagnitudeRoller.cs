using System;

namespace SM.Meta.Services;

/// <summary>
/// Produces a deterministic uniform magnitude from an item acquisition seed and affix identity.
/// Authored ranges are centered on the legacy applied value, so the distribution preserves that mean.
/// </summary>
public static class AffixMagnitudeRoller
{
    private const ulong FnvOffset = 14695981039346656037UL;
    private const ulong FnvPrime = 1099511628211UL;
    private const double UnitDenominator = 16777216d;

    public static float Roll(
        int stableItemSeed,
        string affixId,
        int affixOrdinal,
        float valueMin,
        float valueMax)
    {
        if (!IsFinite(valueMin) || !IsFinite(valueMax))
        {
            throw new ArgumentOutOfRangeException(nameof(valueMin), "Affix magnitude bounds must be finite.");
        }

        var minimum = Math.Min(valueMin, valueMax);
        var maximum = Math.Max(valueMin, valueMax);
        if (minimum == maximum)
        {
            return minimum;
        }

        var hash = FnvOffset;
        MixInt32(ref hash, stableItemSeed);
        MixSeparator(ref hash);
        MixString(ref hash, affixId ?? string.Empty);
        MixSeparator(ref hash);
        MixInt32(ref hash, affixOrdinal);

        // A 24-bit midpoint sample maps deterministically to float without ever selecting outside the bounds.
        var sample = (hash >> 40) & 0xFFFFFFUL;
        var unit = (sample + 0.5d) / UnitDenominator;
        var rolled = (float)(minimum + ((maximum - minimum) * unit));
        return Math.Max(minimum, Math.Min(maximum, rolled));
    }

    public static double ExpectedMagnitude(float valueMin, float valueMax)
        => (Math.Min(valueMin, valueMax) + Math.Max(valueMin, valueMax)) * 0.5d;

    private static void MixInt32(ref ulong hash, int value)
    {
        unchecked
        {
            var bits = (uint)value;
            for (var shift = 0; shift < 32; shift += 8)
            {
                hash ^= (byte)(bits >> shift);
                hash *= FnvPrime;
            }
        }
    }

    private static void MixString(ref ulong hash, string value)
    {
        unchecked
        {
            foreach (var character in value)
            {
                hash ^= (byte)character;
                hash *= FnvPrime;
                hash ^= (byte)(character >> 8);
                hash *= FnvPrime;
            }
        }
    }

    private static void MixSeparator(ref ulong hash)
    {
        unchecked
        {
            hash ^= 0xFF;
            hash *= FnvPrime;
        }
    }

    private static bool IsFinite(float value)
        => !float.IsNaN(value) && !float.IsInfinity(value);
}
