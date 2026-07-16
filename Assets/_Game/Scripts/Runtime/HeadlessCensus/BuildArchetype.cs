using SM.Combat.Model;

namespace SM.HeadlessCensus;

/// <summary>authored object를 반입하지 않는 build-space 입력 한 칸.</summary>
public sealed record BuildArchetype(
    string ArchetypeId,
    string RaceId,
    string ClassId,
    BuildRole Role,
    DeploymentAnchorId PreferredAnchor);
