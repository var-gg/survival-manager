using System.Collections.Generic;

namespace SM.Meta.Model;

/// <summary>
/// 사이트 run 동안만 유지되는 전상 규칙. 저작 balance asset에서 변환되며 영구 hero truth는 바꾸지 않는다.
/// </summary>
public sealed record WarWoundSpec(
    float WoundTriggerHpRatio,
    float WoundAbilityScalar,
    int MaxWoundsAppliedPerBattle,
    int MaxActiveWounds,
    int WoundStacksPerUnitMax,
    bool ApplyWoundOnLoss);

/// <summary>전상 판정에 필요한 battle outcome의 최소 투영.</summary>
public sealed record WarWoundCandidate(
    string HeroId,
    float EndHealth,
    float MaxHealth);

public sealed record WarWoundResolutionResult(
    ActiveRunState UpdatedRun,
    IReadOnlyList<string> AppliedHeroIds);
