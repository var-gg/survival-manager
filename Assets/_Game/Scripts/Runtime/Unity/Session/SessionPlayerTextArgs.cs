using System;
using SM.Content.Definitions;

namespace SM.Unity;

internal static class SessionPlayerTextArgs
{
    internal static SessionTextArg BuildExpeditionNodeNameArg(ExpeditionNodeViewModel node)
    {
        if (node.RequiresBattle)
        {
            return SessionTextArg.EncounterName(node.Id);
        }

        if (node.LabelKey.StartsWith("content.site_event.", StringComparison.Ordinal))
        {
            return SessionTextArg.Localized(
                ContentLocalizationTables.Campaign,
                node.LabelKey,
                "Unknown event");
        }

        if (node.LabelKey.StartsWith("ui.", StringComparison.Ordinal))
        {
            return SessionTextArg.Localized(
                GameLocalizationTables.UIExpedition,
                node.LabelKey,
                "Unknown route");
        }

        return SessionTextArg.Localized(
            GameLocalizationTables.UICommon,
            "ui.common.unknown_route",
            "Unknown route");
    }

    internal static SessionTextArg BuildRewardSourceNameArg(ExpeditionNodeViewModel node)
    {
        if (!string.IsNullOrWhiteSpace(node.RewardSourceId))
        {
            return SessionTextArg.RewardSourceName(node.RewardSourceId);
        }

        if (node.PlannedRewardKey.StartsWith("ui.", StringComparison.Ordinal))
        {
            return SessionTextArg.Localized(
                GameLocalizationTables.UIExpedition,
                node.PlannedRewardKey,
                "No reward");
        }

        return SessionTextArg.Localized(
            GameLocalizationTables.UICommon,
            "ui.common.unknown_reward_source",
            "Unknown reward source");
    }
}
