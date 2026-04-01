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
