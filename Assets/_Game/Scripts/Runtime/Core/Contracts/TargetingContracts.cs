using System;

namespace SM.Core.Contracts;

public enum FormationLine
{
    Frontline = 0,
    Midline = 1,
    Backline = 2,
}

public enum RangeDiscipline
{
    Collapse = 0,
    HoldBand = 1,
    KiteBackward = 2,
    SideStepHold = 3,
    AnchorNearFrontline = 4,
}

public enum TargetDomain
{
    None = 0,
    Self = 1,
    EnemyUnit = 2,
    AlliedUnit = 3,
    GroundPoint = 4,
}

public enum TargetSelector
{
    CurrentTarget = 0,
    NearestReachableEnemy = 1,
    NearestFrontlineEnemy = 2,
    LowestCurrentHpEnemy = 3,
    LowestHpPercentEnemy = 4,
    LowestEhpEnemy = 5,
    MarkedEnemy = 6,
    LargestEnemyCluster = 7,
    BacklineExposedEnemy = 8,
    Self = 9,
    LowestCurrentHpAlly = 10,
    LowestHpPercentAlly = 11,
    LowestEhpAlly = 12,
    NearestInjuredAlly = 13,
    EmptyPointNearSelf = 14,
    EmptyPointNearTarget = 15,
}

public enum TargetFallbackPolicy
{
    Abort = 0,
    KeepCurrentIfStillValid = 1,
    NearestReachableEnemy = 2,
    LowestCurrentHpEnemy = 3,
    Self = 4,
}

[Flags]
public enum TargetFilterFlags
{
    None = 0,
    InRange = 1 << 0,
    InLineOfSight = 1 << 1,
    ExcludeUntargetable = 1 << 2,
    ExcludeSummons = 1 << 3,
    ExcludeFullHealthAllies = 1 << 4,
    RequireMarked = 1 << 5,
    RequireBacklineExposed = 1 << 6,
    PreferCluster2Plus = 1 << 7,
    PreferCluster3Plus = 1 << 8,
}

public enum RetargetLockMode
{
    None = 0,
    Soft075 = 1,
    Hard125 = 2,
    UntilCastComplete = 3,
}

/// <summary>
/// P1 플레이어 전술 레버 — 유닛별 교전 타게팅 지시. 기본공격(=교전 대상 결정)의 authored
/// TargetRule에서 PrimarySelector만 교체하는 curated 소수 집합이다. 저작된 스킬 규칙·Dive/Peel
/// 의도·melee 최근접 가드(Q5)는 지시보다 우선한다 — 지시는 "선호"지 "강제"가 아니다.
/// </summary>
public enum PlayerTargetDirective
{
    /// <summary>지시 없음 — authored 규칙 그대로.</summary>
    Default = 0,

    /// <summary>최근접 교전 — 안정적으로 가장 가까운 적을 문다.</summary>
    NearestEnemy = 1,

    /// <summary>마무리 — 체력 비율이 가장 낮은 적부터 끊는다.</summary>
    FinishLowestHp = 2,

    /// <summary>후열 사냥 — 스크린이 깨진 후열이 보이면 그쪽을 노린다.</summary>
    HuntExposedBackline = 3,

    /// <summary>밀집 격파 — 적이 가장 뭉친 곳을 노린다(광역 가치).</summary>
    BreakLargestCluster = 4,
}

/// <summary>
/// <see cref="PlayerTargetDirective"/>의 단일 진실: authored 규칙 변환과 persistence 문자열 id.
/// </summary>
public static class PlayerTargetDirectiveRules
{
    /// <summary>밀집 격파 지시가 보장하는 최소 클러스터 반경(m).</summary>
    public const float MinClusterRadius = 2f;

    /// <summary>
    /// 지시를 authored 규칙에 적용한 유효 규칙을 돌려준다. 적 도메인이 아니거나(힐/지원 보호),
    /// 강제 의미(marked/current-target)가 있거나, 지시가 없으면 authored를 그대로 돌려준다.
    /// </summary>
    public static TargetRule Apply(PlayerTargetDirective directive, TargetRule? authored)
    {
        var rule = authored ?? new TargetRule();
        if (directive == PlayerTargetDirective.Default
            || rule.Domain != TargetDomain.EnemyUnit
            || rule.PrimarySelector is TargetSelector.MarkedEnemy or TargetSelector.CurrentTarget
            || rule.Filters.HasFlag(TargetFilterFlags.RequireMarked))
        {
            return rule;
        }

        return new TargetRule
        {
            Domain = rule.Domain,
            PrimarySelector = directive switch
            {
                PlayerTargetDirective.FinishLowestHp => TargetSelector.LowestHpPercentEnemy,
                PlayerTargetDirective.HuntExposedBackline => TargetSelector.BacklineExposedEnemy,
                PlayerTargetDirective.BreakLargestCluster => TargetSelector.LargestEnemyCluster,
                _ => TargetSelector.NearestReachableEnemy,
            },
            FallbackPolicy = TargetFallbackPolicy.NearestReachableEnemy,
            Filters = rule.Filters,
            ReevaluateIntervalSeconds = rule.ReevaluateIntervalSeconds,
            MinimumCommitSeconds = rule.MinimumCommitSeconds,
            MaxAcquireRange = rule.MaxAcquireRange,
            PreferredMinTargets = rule.PreferredMinTargets,
            ClusterRadius = directive == PlayerTargetDirective.BreakLargestCluster
                ? Math.Max(MinClusterRadius, rule.ClusterRadius)
                : rule.ClusterRadius,
            LockTargetAtCastStart = rule.LockTargetAtCastStart,
            RetargetLockMode = rule.RetargetLockMode,
        };
    }

    /// <summary>persistence/세션용 안정 문자열 id. enum 이름 리네임에 영향받지 않는다.</summary>
    public static string ToStableId(PlayerTargetDirective directive) => directive switch
    {
        PlayerTargetDirective.NearestEnemy => "nearest",
        PlayerTargetDirective.FinishLowestHp => "finish_lowest",
        PlayerTargetDirective.HuntExposedBackline => "hunt_backline",
        PlayerTargetDirective.BreakLargestCluster => "break_cluster",
        _ => "default",
    };

    /// <summary>안정 문자열 id 파싱 — 모르는 값은 Default로 강등(전방위 호환).</summary>
    public static PlayerTargetDirective ParseStableId(string? id) => id switch
    {
        "nearest" => PlayerTargetDirective.NearestEnemy,
        "finish_lowest" => PlayerTargetDirective.FinishLowestHp,
        "hunt_backline" => PlayerTargetDirective.HuntExposedBackline,
        "break_cluster" => PlayerTargetDirective.BreakLargestCluster,
        _ => PlayerTargetDirective.Default,
    };
}

[Serializable]
public sealed class TargetRule
{
    public TargetDomain Domain = TargetDomain.EnemyUnit;
    public TargetSelector PrimarySelector = TargetSelector.NearestReachableEnemy;
    public TargetFallbackPolicy FallbackPolicy = TargetFallbackPolicy.NearestReachableEnemy;
    public TargetFilterFlags Filters =
        TargetFilterFlags.InRange |
        TargetFilterFlags.ExcludeUntargetable;
    public float ReevaluateIntervalSeconds = 0.25f;
    public float MinimumCommitSeconds = 0.75f;
    public float MaxAcquireRange = 0f;
    public int PreferredMinTargets = 1;
    public float ClusterRadius = 2.5f;
    public bool LockTargetAtCastStart = true;
    public RetargetLockMode RetargetLockMode = RetargetLockMode.UntilCastComplete;
}

[Serializable]
public sealed class BehaviorProfileContract
{
    public FormationLine FormationLine = FormationLine.Frontline;
    public RangeDiscipline RangeDiscipline = RangeDiscipline.HoldBand;
    public float PreferredRangeMin = 0f;
    public float PreferredRangeMax = 0f;
    public float ApproachBuffer = 0.4f;
    public float RetreatBuffer = 0.25f;
    public float ChaseLeashMeters = 5f;
    public float RetreatAtHpPercent = 0f;
    public float ReevaluateIntervalSeconds = 0.25f;
}
