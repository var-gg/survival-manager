using System;
using System.Collections.Generic;

namespace SM.Meta;

public sealed record RecruitConversionStateRecord
{
    public static RecruitConversionStateRecord Empty { get; } = new();

    public Dictionary<string, int> Conversions { get; init; } = new(StringComparer.Ordinal);
}
