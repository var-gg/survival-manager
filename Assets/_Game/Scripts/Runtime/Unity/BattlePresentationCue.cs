using SM.Combat.Model;

namespace SM.Unity;

public enum BattlePresentationAnchorId
{
    Root = 0,
    Feet = 1,
    Center = 2,
    Head = 3,
    Cast = 4,
}

public enum BattlePresentationCueType
{
    WindupEnter = 0,
    TargetChanged = 1,
    ActionCommitBasic = 2,
    ActionCommitSkill = 3,
    ActionCommitHeal = 4,
    ImpactDamage = 5,
    ImpactHeal = 6,
    GuardEnter = 7,
    GuardExit = 8,
    RepositionStart = 9,
    RepositionStop = 10,
    DeathStart = 11,
    BattleResolved = 12,
    PlaybackReset = 13,
    SeekSnapshotApplied = 14,
}

public enum BattleAnimationSemantic
{
    None = 0,
    HitLight = 1,
    HitHeavy = 2,
    CriticalImpact = 3,
    Dodge = 4,
    BlockImpact = 5,
    DashEngage = 6,
    BackstepDisengage = 7,
    LateralStrafe = 8,
    Knockdown = 9,
    Death = 10,
    Miss = 11,
    BowShot = 12,
    ProjectileCast = 13,
    BowDraw = 14,
    ProjectileWindup = 15,
}

public enum BattleAnimationDirection
{
    Any = 0,
    Forward = 1,
    Backward = 2,
    Left = 3,
    Right = 4,
    Lateral = 5,
}

public enum BattleAnimationIntensity
{
    Any = 0,
    Light = 1,
    Medium = 2,
    Heavy = 3,
}

public enum BattleActorPresentationPhase
{
    RelaxedIdle = 0,
    CombatReady = 1,
    ResolvedIdle = 2,
}

public sealed record BattlePresentationCue(
    BattlePresentationCueType CueType,
    int StepIndex,
    string SubjectActorId,
    string? RelatedActorId = null,
    BattleActionType? ActionType = null,
    float Magnitude = 0f,
    BattlePresentationAnchorId SubjectAnchor = BattlePresentationAnchorId.Feet,
    BattlePresentationAnchorId RelatedAnchor = BattlePresentationAnchorId.Center,
    string Note = "",
    BattleAnimationSemantic AnimationSemantic = BattleAnimationSemantic.None,
    BattleAnimationDirection AnimationDirection = BattleAnimationDirection.Any,
    BattleAnimationIntensity AnimationIntensity = BattleAnimationIntensity.Any);
