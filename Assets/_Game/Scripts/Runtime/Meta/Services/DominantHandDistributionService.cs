using System;
using SM.Core.Contracts;

namespace SM.Meta.Services;

public static class DominantHandDistributionService
{
    public static DominantHand ResolveGenerated(string seedId, string classId, bool isNamed = false)
    {
        var ambidextrousRoll = StablePercent($"{seedId}:ambidextrous");
        if (ambidextrousRoll < 4)
        {
            return DominantHand.Ambidextrous;
        }

        var leftThreshold = isNamed
            ? IsDuelist(classId) ? 28 : 23
            : 11;
        return StablePercent($"{seedId}:left") < leftThreshold
            ? DominantHand.Left
            : DominantHand.Right;
    }

    private static bool IsDuelist(string classId)
    {
        return string.Equals(classId, "duelist", StringComparison.Ordinal)
               || string.Equals(classId, "assassin", StringComparison.Ordinal);
    }

    private static int StablePercent(string value)
    {
        unchecked
        {
            var hash = 17;
            foreach (var ch in value)
            {
                hash = (hash * 31) + ch;
            }

            var remainder = hash % 100;
            return remainder < 0 ? remainder + 100 : remainder;
        }
    }
}
