using System.Collections.Generic;

namespace SM.Editor.Validation;

internal sealed record H100FormationBattleCase(
    string CaseId,
    string PairingId,
    string PlacementSetId,
    string PlacementVariantId,
    string PolicyId,
    int Seed,
    bool IsDefaultPlacement,
    bool IsPolicyChoice,
    bool IsHealerComparison,
    string HealerComparisonId,
    bool ContainsHealer,
    bool CompetentSelectedHealer,
    IReadOnlyList<H100BattleScreeningMember> Members,
    string CoverageProbeChannelId);
