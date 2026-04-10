using System;
using System.Collections.Generic;

namespace SM.Meta;

public sealed record EndlessCycleStateRecord
{
    public static EndlessCycleStateRecord Empty { get; } = new();

    public int CycleIndex { get; init; }
    public int Heat { get; init; }
    public Dictionary<string, int> Modifiers { get; init; } = new(StringComparer.Ordinal);
}
