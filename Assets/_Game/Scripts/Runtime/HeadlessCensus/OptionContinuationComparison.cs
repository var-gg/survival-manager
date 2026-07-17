using System;
using System.Collections.Generic;

namespace SM.HeadlessCensus;

/// <summary>E05.5 IntentTrackEvaluator를 같은 offer/state pair에 적용한 continuation 결과.</summary>
public sealed record OptionContinuationComparison(
    string OptionId,
    string ContextId,
    bool Measured,
    bool WithOptionTrackAvailable,
    bool WithoutOptionTrackAvailable,
    double WithOptionBestScore,
    double WithoutOptionBestScore,
    bool UniqueOptionAdvantage,
    IReadOnlyList<string> WithOptionChoicePath,
    IReadOnlyList<string> WithoutOptionChoicePath)
{
    public static OptionContinuationComparison Unmeasured(string optionId, string contextId, string note)
        => new(
            optionId,
            $"{contextId}:{note}",
            false,
            false,
            false,
            0d,
            0d,
            false,
            Array.Empty<string>(),
            Array.Empty<string>());
}
