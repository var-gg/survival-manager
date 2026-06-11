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
    ActionCanceled = 15,
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
    GuardPose = 16,
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

/// <summary>
/// Sim-derived schedule for an actor commit (strike) cue, carried so the driver can pin the clip's contact
/// frame onto the canonical <see cref="ContactTick"/> (GPT Pro D2 firing). The group key
/// (<see cref="ActionInstanceId"/>, <see cref="ContactGroupIndex"/>) lets a cancel tombstone match the exact
/// scheduled one-shot and future-proofs J22-D2 multi-hit (one commit per scheduled group). These are
/// presentation inputs derived from sim ticks — never sim or save truth.
/// </summary>
public sealed record BattleCommitSchedule(
    ActionInstanceId ActionInstanceId,
    int ContactGroupIndex,
    int WindupStartTick,
    int ContactTick);

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
    BattleAnimationIntensity AnimationIntensity = BattleAnimationIntensity.Any,
    BattleCommitSchedule? CommitSchedule = null,
    // P2 극적 순간 typed 채널(J8) — 측면/후방/차단/구출/후열격파 강조의 유일한 소스.
    CombatContactAccent ContactAccent = CombatContactAccent.None);
