namespace SM.Core.Rng;

public interface IRng
{
    int Next(int minInclusive, int maxExclusive);
    float NextFloat();
}
