using System;
using System.Linq;
using SM.Unity;

namespace SM.Editor.Validation;

/// <summary>
/// Route-policy axis가 없는 기존 campaign harness가 authored graph의 첫 edge와 첫 event choice를
/// 결정적으로 따라가게 한다. 측정 정책을 늘리지 않고 canonical safe lane만 소비한다.
/// </summary>
internal static class CampaignDefaultRouteNavigator
{
    internal static bool TryAdvanceIntermediateNonBattle(GameSessionState session)
    {
        var node = session.GetSelectedExpeditionNode();
        if (node == null || node.RequiresBattle || node.NextNodeIndices.Count == 0)
        {
            return false;
        }

        if (!session.ResolveSelectedNodeToRewardSettlement())
        {
            throw new InvalidOperationException($"Failed to resolve default non-battle node '{node.GraphNodeId}'.");
        }

        if (session.PendingSiteEvent != null)
        {
            var defaultAction = session.SiteEvents.GetLegalActions().FirstOrDefault();
            if (defaultAction == null || !session.SiteEvents.ApplyChoice(defaultAction.ChoiceId))
            {
                throw new InvalidOperationException($"Failed to apply the default choice for site event '{node.EventId}'.");
            }
        }

        session.ReturnToTownAfterReward();
        return true;
    }
}
