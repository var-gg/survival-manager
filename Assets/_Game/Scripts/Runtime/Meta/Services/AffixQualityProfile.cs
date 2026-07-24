using System;
using System.Collections.Generic;
using System.Numerics;
using SM.Core.Content;

namespace SM.Meta.Services;

public readonly record struct QualityProfileKey(
    string ItemBaseId,
    string SlotType,
    ItemRarityTierValue Grade,
    int GradeStepBudgetScoreQ,
    int SelectorRulesVersion,
    string AffixCatalogVersion);

/// <summary>한 generated-item selector profile의 attainable score support와 inclusive Q0.64 CDF.</summary>
public sealed class AffixQualityProfile
{
    public const ulong ProbabilityOneQ64 = ulong.MaxValue;

    private readonly int[] _supportScoreQ;
    private readonly ulong[] _massQ64;
    private readonly ulong[] _cdfQ64;

    internal AffixQualityProfile(
        QualityProfileKey key,
        int[] supportScoreQ,
        ulong[] massQ64,
        ulong[] cdfQ64,
        AffixQualityProfileCompiler.CompiledProfileGraph compiledGraph)
    {
        Key = key;
        _supportScoreQ = supportScoreQ;
        _massQ64 = massQ64;
        _cdfQ64 = cdfQ64;
        CompiledGraph = compiledGraph;
    }

    public QualityProfileKey Key { get; }
    public IReadOnlyList<int> SupportScoreQ => _supportScoreQ;
    public IReadOnlyList<ulong> MassQ64 => _massQ64;
    public IReadOnlyList<ulong> CdfQ64 => _cdfQ64;
    internal AffixQualityProfileCompiler.CompiledProfileGraph CompiledGraph { get; }

    public ulong GetInclusivePercentileQ64(int totalScoreQ)
    {
        var index = UpperBound(_supportScoreQ, totalScoreQ) - 1;
        return index < 0 ? 0UL : _cdfQ64[index];
    }

    public ulong GetExclusivePercentileQ64(int totalScoreQ)
    {
        var index = LowerBound(_supportScoreQ, totalScoreQ) - 1;
        return index < 0 ? 0UL : _cdfQ64[index];
    }

    public int GetQuantileScoreQ(ulong percentileQ64)
    {
        if (_supportScoreQ.Length == 0)
        {
            throw new InvalidOperationException("An affix quality profile must have non-empty support.");
        }

        var low = 0;
        var high = _cdfQ64.Length;
        while (low < high)
        {
            var midpoint = low + ((high - low) / 2);
            if (_cdfQ64[midpoint] >= percentileQ64)
            {
                high = midpoint;
            }
            else
            {
                low = midpoint + 1;
            }
        }

        return _supportScoreQ[Math.Min(low, _supportScoreQ.Length - 1)];
    }

    public ulong GetMassQ64(int totalScoreQ)
    {
        var index = Array.BinarySearch(_supportScoreQ, totalScoreQ);
        return index < 0 ? 0UL : _massQ64[index];
    }

    public static ulong ProbabilityFromFraction(ulong numerator, ulong denominator)
    {
        if (denominator == 0 || numerator > denominator)
        {
            throw new ArgumentOutOfRangeException(nameof(denominator));
        }

        return (ulong)((new BigInteger(numerator) * ProbabilityOneQ64) / denominator);
    }

    private static int LowerBound(IReadOnlyList<int> values, int target)
    {
        var low = 0;
        var high = values.Count;
        while (low < high)
        {
            var midpoint = low + ((high - low) / 2);
            if (values[midpoint] < target)
            {
                low = midpoint + 1;
            }
            else
            {
                high = midpoint;
            }
        }

        return low;
    }

    private static int UpperBound(IReadOnlyList<int> values, int target)
    {
        var low = 0;
        var high = values.Count;
        while (low < high)
        {
            var midpoint = low + ((high - low) / 2);
            if (values[midpoint] <= target)
            {
                low = midpoint + 1;
            }
            else
            {
                high = midpoint;
            }
        }

        return low;
    }
}

public sealed record AffixQualityProfileCompilationMetrics(
    int DistinctMemoizedStates,
    BigInteger TerminalSequences,
    TimeSpan Elapsed,
    long PeakMemoryBytes,
    int SupportSize);
