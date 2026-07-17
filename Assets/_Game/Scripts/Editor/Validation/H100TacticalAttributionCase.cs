using System.Collections.Generic;
using SM.HeadlessMetrics;

namespace SM.Editor.Validation;

internal sealed record H100TacticalAttributionCase(
    string CaseId,
    string PairingId,
    string ComparisonKind,
    string CompositionId,
    string ConceptVariantId,
    string ChapterId,
    string SiteId,
    int Seed,
    int BattleSeed,
    string PlacementVariantId,
    bool IsBaseline,
    bool SemanticPreservationExpected,
    string FormationProfileId,
    PlacementAttributionBattleRecord.FormationFeatureSnapshot FormationFeatures,
    IReadOnlyList<int> AnchorIdsByMemberIndex,
    IReadOnlyList<H100BattleScreeningMember> Members);
