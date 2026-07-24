using System;
using SM.Core.Content;
using SM.Meta.Model;

namespace SM.Meta.Services;

/// <summary>C_m(g,c)의 단일 소유자. 각 level을 ceiling한 뒤 bundle 합계를 계산한다.</summary>
public static class RefitCostCurve
{
    public static int GetLevelCost(
        RefitBalanceTemplate balance,
        int firstFarmRunEcho,
        int refitLevel,
        ItemRarityTierValue grade,
        double chapterMeanGrade)
    {
        if (balance == null)
        {
            throw new ArgumentNullException(nameof(balance));
        }

        if (firstFarmRunEcho <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(firstFarmRunEcho));
        }

        if (refitLevel <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(refitLevel));
        }

        if (!double.IsFinite(chapterMeanGrade))
        {
            throw new ArgumentOutOfRangeException(nameof(chapterMeanGrade));
        }

        var rawCost = balance.CostBaseFirstFarmEchoMultiplier
                      * firstFarmRunEcho
                      * Math.Pow(balance.CostGrowthPerLevel, refitLevel - 1)
                      * Math.Pow(balance.GradeCostRatio, (int)grade - chapterMeanGrade);
        if (!double.IsFinite(rawCost) || rawCost <= 0d || rawCost > int.MaxValue)
        {
            throw new OverflowException($"Refit level {refitLevel} produced invalid Echo cost {rawCost:R}.");
        }

        return checked((int)Math.Ceiling(rawCost));
    }

    public static int GetBundleCost(
        RefitBalanceTemplate balance,
        int firstFarmRunEcho,
        int currentRefitLevel,
        int targetRefitLevel,
        ItemRarityTierValue grade,
        double chapterMeanGrade)
    {
        if (currentRefitLevel < 0 || targetRefitLevel <= currentRefitLevel)
        {
            throw new ArgumentOutOfRangeException(nameof(targetRefitLevel));
        }

        var total = 0;
        for (var level = currentRefitLevel + 1; level <= targetRefitLevel; level++)
        {
            total = checked(total + GetLevelCost(
                balance,
                firstFarmRunEcho,
                level,
                grade,
                chapterMeanGrade));
        }

        return total;
    }
}
