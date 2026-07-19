using System;
using System.Collections.Generic;
using System.Linq;
using SM.Combat.Model;
using SM.Meta.Services;

namespace SM.Editor.Validation;

/// <summary>
/// authored enemy member/mechanic은 그대로 두고 8개 결정적 order/anchor variant를 만든다.
/// 콘텐츠 자산이나 EncounterResolutionService를 수정하지 않는 measurement-input adapter다.
/// </summary>
internal static class CampaignBalanceGridProjector
{
    public static IReadOnlyList<BattleUnitLoadout> ProjectEnemyComposition(
        IReadOnlyList<BattleUnitLoadout> authored,
        int variantIndex)
        => EnemyCompositionVariantProjector.Project(authored, variantIndex);
}
