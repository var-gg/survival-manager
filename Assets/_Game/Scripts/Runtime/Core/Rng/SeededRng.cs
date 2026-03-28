using System;

namespace SM.Core.Rng;

public sealed class SeededRng : IRng
{
    private readonly Random _random;

    public SeededRng(int seed)
    {
        _random = new Random(seed);
    }

    public int Next(int minInclusive, int maxExclusive) => _random.Next(minInclusive, maxExclusive);
    public float NextFloat() => (float)_random.NextDouble();
}
