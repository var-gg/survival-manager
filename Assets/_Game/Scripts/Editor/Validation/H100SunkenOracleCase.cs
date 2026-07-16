using System.Collections.Generic;
using SM.Combat.Model;

namespace SM.Editor.Validation;

/// <summary>한 profile snapshot에 적용할 합법 편성·배치와 진단 provenance.</summary>
internal sealed record H100SunkenOracleCase(
    string CandidateId,
    string BuildId,
    string PlacementId,
    string CounterFamilyId,
    IReadOnlyList<H100SunkenOracleMember> Members,
    string Scope,
    string StateVariantId,
    bool IsPolicyChoice,
    string AddedRosterArchetypeId,
    int RewardOptionIndex,
    string RewardPayloadId);

internal sealed record H100SunkenOracleMember(
    string HeroId,
    string ArchetypeId,
    DeploymentAnchorId Anchor);
