using System;

namespace SM.Combat.Model;

public enum CinematicMomentId
{
    SaveMoment = 0,
    BacklineDive = 1,
    InterposeSuccess = 2,
    SilenceCancel = 3,
    BarrierAbsorb = 4,
    SunderBurst = 5,
    ControlBreak = 6,
    StanceSwing = 7,
    SynergyPulse = 8,
    TacticPerfectRead = 9,
}

[Serializable]
public sealed record CinematicMomentRecord(
    CinematicMomentId Id,
    float TimeSeconds,
    string ActorUnitId,
    string TargetUnitId,
    string SkillId,
    string StatusId,
    string Evidence,
    float Score = 1f)
{
    public string StableKey => $"{Id}:{TimeSeconds:0.###}:{ActorUnitId}:{TargetUnitId}:{SkillId}:{StatusId}:{Evidence}";
}
