namespace SM.Core.Stats;

public enum ModifierOp
{
    Flat = 0,
    Increased = 1,
    More = 2,
    ClampMin = 3,
    ClampMax = 4,

    [System.Obsolete("Use Increased instead.")]
    AdditivePercent = Increased,

    [System.Obsolete("Use More instead.")]
    MultiplicativePercent = More,
}
