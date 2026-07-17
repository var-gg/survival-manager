using System;

namespace SM.HeadlessCensus;

/// <summary>후보에만 E05.5 탐색기를 재사용해 당장 약한 enabler의 미래 이점을 보존한다.</summary>
public static class OptionContinuationOracle
{
    public static OptionContinuationComparison Evaluate(
        string optionId,
        string contextId,
        IntentTrackSearchInput withOption,
        IntentTrackSearchInput withoutOption)
    {
        if (string.IsNullOrWhiteSpace(optionId)) throw new ArgumentException("option id is required", nameof(optionId));
        if (withOption == null) throw new ArgumentNullException(nameof(withOption));
        if (withoutOption == null) throw new ArgumentNullException(nameof(withoutOption));

        var withResult = IntentTrackEvaluator.Evaluate(withOption);
        var withoutResult = IntentTrackEvaluator.Evaluate(withoutOption);
        var withScore = Score(withResult);
        var withoutScore = Score(withoutResult);
        return new OptionContinuationComparison(
            optionId,
            contextId ?? string.Empty,
            true,
            withResult.TrackAvailable,
            withoutResult.TrackAvailable,
            withScore,
            withoutScore,
            withResult.TrackAvailable && (!withoutResult.TrackAvailable || withScore > withoutScore + 1e-9d),
            withResult.ChoicePath,
            withoutResult.ChoicePath);
    }

    private static double Score(IntentTrackSearchResult result)
    {
        var completion = result.TargetIdentityPredicateCount <= 0
            ? 0d
            : (double)result.FinalIdentityPredicateCount / result.TargetIdentityPredicateCount;
        if (!result.TrackAvailable)
        {
            return completion;
        }

        var realizationBonus = result.RealizationTime < 0 ? 0d : 1d / (1d + result.RealizationTime);
        return 1d + completion + realizationBonus;
    }
}
