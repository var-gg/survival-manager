using System;

namespace SM.Atlas.Model;

public readonly record struct AtlasHexCoordinate(int Q, int R) : IComparable<AtlasHexCoordinate>
{
    public int S => -Q - R;

    public int DistanceTo(AtlasHexCoordinate other)
    {
        return (Math.Abs(Q - other.Q) + Math.Abs(R - other.R) + Math.Abs(S - other.S)) / 2;
    }

    public int CompareTo(AtlasHexCoordinate other)
    {
        var q = Q.CompareTo(other.Q);
        return q != 0 ? q : R.CompareTo(other.R);
    }

    public override string ToString()
    {
        return $"{Q},{R}";
    }
}
