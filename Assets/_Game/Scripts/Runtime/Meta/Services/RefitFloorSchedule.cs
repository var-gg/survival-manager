using System;
using System.Collections.Generic;
using System.Numerics;
using SM.Meta.Model;

namespace SM.Meta.Services;

/// <summary>f_m = u_inf * (1 - eta^m)를 exact rational arithmetic로 Q0.64 schedule로 만든다.</summary>
public static class RefitFloorSchedule
{
    private const int HardLevelLimit = 512;

    public static IReadOnlyList<ulong> Generate(
        int maximumFloorNumerator,
        int maximumFloorDenominator,
        int decayNumerator,
        int decayDenominator)
    {
        if (maximumFloorNumerator <= 0
            || maximumFloorDenominator <= 0
            || maximumFloorNumerator >= maximumFloorDenominator)
        {
            throw new ArgumentOutOfRangeException(
                nameof(maximumFloorNumerator),
                "Maximum floor must be a rational value strictly between zero and one.");
        }

        if (decayNumerator <= 0
            || decayDenominator <= 0
            || decayNumerator >= decayDenominator)
        {
            throw new ArgumentOutOfRangeException(
                nameof(decayNumerator),
                "Floor decay must be a rational value strictly between zero and one.");
        }

        var maximumQ64 = ToProbabilityQ64(
            maximumFloorNumerator,
            maximumFloorDenominator);
        var decayPowerNumerator = BigInteger.One;
        var decayPowerDenominator = BigInteger.One;
        var schedule = new List<ulong>();

        for (var level = 1; level <= HardLevelLimit; level++)
        {
            decayPowerNumerator *= decayNumerator;
            decayPowerDenominator *= decayDenominator;
            var numerator = new BigInteger(maximumFloorNumerator)
                            * (decayPowerDenominator - decayPowerNumerator);
            var denominator = new BigInteger(maximumFloorDenominator)
                              * decayPowerDenominator;
            var floorQ64 = ToProbabilityQ64(numerator, denominator);
            schedule.Add(floorQ64);

            // The closed form only reaches u_inf asymptotically. Q0.64 is the serialized
            // balance representation, so stop once both values map to the same exact datum.
            if (floorQ64 == maximumQ64)
            {
                return schedule;
            }
        }

        throw new InvalidOperationException(
            $"Refit floor schedule did not converge in Q0.64 within {HardLevelLimit} levels.");
    }

    public static double ToDouble(ulong probabilityQ64)
        => probabilityQ64 / (double)AffixQualityProfile.ProbabilityOneQ64;

    private static ulong ToProbabilityQ64(int numerator, int denominator)
        => ToProbabilityQ64(new BigInteger(numerator), new BigInteger(denominator));

    private static ulong ToProbabilityQ64(BigInteger numerator, BigInteger denominator)
    {
        if (denominator <= BigInteger.Zero
            || numerator < BigInteger.Zero
            || numerator > denominator)
        {
            throw new ArgumentOutOfRangeException(nameof(denominator));
        }

        return (ulong)((numerator * AffixQualityProfile.ProbabilityOneQ64) / denominator);
    }
}
