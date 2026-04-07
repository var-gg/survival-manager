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

public sealed record BattlePresentationCue(
    BattlePresentationCueType CueType,
    int StepIndex,
    string SubjectActorId,
    string? RelatedActorId = null,
    BattleActionType? ActionType = null,
    float Magnitude = 0f,
    BattlePresentationAnchorId SubjectAnchor = BattlePresentationAnchorId.Feet,
    BattlePresentationAnchorId RelatedAnchor = BattlePresentationAnchorId.Center,
    string Note = "");
