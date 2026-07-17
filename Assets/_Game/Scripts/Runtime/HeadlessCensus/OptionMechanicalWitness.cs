namespace SM.HeadlessCensus;

/// <summary>한 합법 context에서 promise의 발화·상태 변화·부호·규칙 일치를 관측한 값.</summary>
public sealed record OptionMechanicalWitness(
    string OptionId,
    string PromiseId,
    string ContextId,
    bool Eligible,
    int FiredCount,
    bool StateChanged,
    string ActualDeltaDirection,
    bool StackRuleMatches,
    bool TargetRuleMatches,
    bool PrerequisiteReachable,
    bool CostConsumed,
    string StateHashBefore,
    string StateHashAfter,
    bool FullCensus,
    bool PositiveWitness,
    string Note);
