namespace SM.HeadlessCensus;

/// <summary>가중합하지 않는 paired counterfactual 다차원 결과 벡터. 모든 축은 클수록 낫다.</summary>
public sealed record OptionOutcomeVector(
    double WinScore,
    double RemainingHpFraction,
    double RemainingResource,
    double ConceptMilestoneCount,
    double UniquePayoffWitnessCount,
    double CampaignContinuationScore);
