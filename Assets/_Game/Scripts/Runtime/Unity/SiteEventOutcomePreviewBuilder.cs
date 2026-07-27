using System;
using System.Collections.Generic;
using System.Linq;
using SM.Core.Content;
using SM.Meta.Model;

namespace SM.Unity;

/// <summary>
/// 저작 결과를 정확한 수치 대신 범주와 상대 강도만 드러내는 표시 모델로 변환한다.
/// 결과 순서를 보존하고, 여러 결과를 하나로 합치지 않는다.
/// </summary>
public static class SiteEventOutcomePreviewBuilder
{
    public static IReadOnlyList<SiteEventOutcomePreviewViewModel> Build(
        IReadOnlyList<SiteEventOutcomeTemplate>? outcomes)
    {
        if (outcomes == null || outcomes.Count == 0)
        {
            return new[]
            {
                new SiteEventOutcomePreviewViewModel(
                    SiteEventOutcomePreviewCategory.NoChange,
                    0,
                    false,
                    SiteEventOutcomePreviewCertainty.Certain),
            };
        }

        return outcomes.Select(Build).ToArray();
    }

    private static SiteEventOutcomePreviewViewModel Build(SiteEventOutcomeTemplate outcome)
    {
        var category = outcome.Kind switch
        {
            OutcomeKind.GrantItem => SiteEventOutcomePreviewCategory.Item,
            OutcomeKind.GrantEcho => SiteEventOutcomePreviewCategory.Echo,
            OutcomeKind.GrantExp => SiteEventOutcomePreviewCategory.Experience,
            OutcomeKind.CureWound => SiteEventOutcomePreviewCategory.WoundRecovery,
            OutcomeKind.InflictWound => SiteEventOutcomePreviewCategory.WoundRisk,
            OutcomeKind.RouteToNode => SiteEventOutcomePreviewCategory.Route,
            OutcomeKind.GrantRecruitOffer => SiteEventOutcomePreviewCategory.Recruit,
            OutcomeKind.GrantConsumable => SiteEventOutcomePreviewCategory.Consumable,
            OutcomeKind.ExtractBonus => SiteEventOutcomePreviewCategory.ExtractBonus,
            _ => SiteEventOutcomePreviewCategory.Unknown,
        };
        var certainty = category == SiteEventOutcomePreviewCategory.Unknown
            ? SiteEventOutcomePreviewCertainty.Unknown
            : outcome.TargetRule == OutcomeTargetRule.None
                ? SiteEventOutcomePreviewCertainty.Certain
                : SiteEventOutcomePreviewCertainty.TargetVaries;
        return new SiteEventOutcomePreviewViewModel(
            category,
            ResolveIntensityPips(category, outcome.Amount),
            IsCost(outcome),
            certainty);
    }

    private static int ResolveIntensityPips(SiteEventOutcomePreviewCategory category, int amount)
    {
        if (category == SiteEventOutcomePreviewCategory.NoChange)
        {
            return 0;
        }

        var magnitude = Math.Max(1L, Math.Abs((long)amount));
        return magnitude switch
        {
            <= 1L => 1,
            <= 3L => 2,
            <= 7L => 3,
            <= 15L => 4,
            _ => 5,
        };
    }

    private static bool IsCost(SiteEventOutcomeTemplate outcome)
    {
        return outcome.Kind is OutcomeKind.GrantEcho or OutcomeKind.GrantExp or OutcomeKind.ExtractBonus
               && outcome.Amount < 0;
    }
}
