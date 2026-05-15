using System;
using System.Collections.Generic;
using System.Linq;
using SM.Combat.Model;
using SM.Content.Definitions;
using SM.Core.Content;
using SM.Meta.Model;
using static SM.Unity.ContentConversion.ContentConversionShared;

namespace SM.Unity.ContentConversion;

internal static class CampaignConverter
{
    internal static CampaignChapterTemplate BuildCampaignChapterTemplate(CampaignChapterDefinition definition)
    {
        return new CampaignChapterTemplate(
            definition.Id,
            ResolveLegacyName(definition.NameKey, definition.LegacyDisplayName, definition.Id),
            definition.StoryOrder,
            Enumerate(definition.SiteIds)
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .Distinct(StringComparer.Ordinal)
                .ToList(),
            definition.UnlocksEndlessOnClear);
    }

    internal static ExpeditionSiteTemplate BuildExpeditionSiteTemplate(ExpeditionSiteDefinition definition)
    {
        return new ExpeditionSiteTemplate(
            definition.Id,
            definition.ChapterId,
            ResolveLegacyName(definition.NameKey, definition.LegacyDisplayName, definition.Id),
            definition.SiteOrder,
            definition.FactionId,
            Enumerate(definition.EncounterIds)
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .Distinct(StringComparer.Ordinal)
                .ToList(),
            definition.ExtractRewardSourceId,
            (int)definition.ThreatTier);
    }

    internal static EncounterTemplate BuildEncounterTemplate(EncounterDefinition definition)
    {
        return new EncounterTemplate(
            definition.Id,
            ResolveLegacyName(definition.NameKey, definition.LegacyDisplayName, definition.Id),
            definition.SiteId,
            definition.EnemySquadTemplateId,
            definition.BossOverlayId,
            definition.RewardSourceId,
            definition.FactionId,
            (int)definition.ThreatTier,
            Math.Max(1, definition.ThreatCost),
            Math.Max(1, definition.ThreatSkulls),
            definition.DifficultyBand,
            definition.Kind,
            Enumerate(definition.RewardDropTags)
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .Distinct(StringComparer.Ordinal)
                .ToList());
    }

    internal static EnemySquadTemplate BuildEnemySquadTemplate(EnemySquadTemplateDefinition definition)
    {
        return new EnemySquadTemplate(
            definition.Id,
            ResolveLegacyName(definition.NameKey, definition.LegacyDisplayName, definition.Id),
            definition.FactionId,
            (TeamPostureType)definition.EnemyPosture,
            (int)definition.ThreatTier,
            Math.Max(1, definition.ThreatCost),
            Enumerate(definition.RewardDropTags)
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .Distinct(StringComparer.Ordinal)
                .ToList(),
            Enumerate(definition.Members)
                .Where(member => member != null && !string.IsNullOrWhiteSpace(member.ArchetypeId))
                .Select(member => new EnemySquadMemberTemplate(
                    string.IsNullOrWhiteSpace(member.Id) ? $"{definition.Id}:{member.ArchetypeId}:{member.Anchor}" : member.Id,
                    ResolveLegacyName(member.NameKey, member.LegacyDisplayName, string.IsNullOrWhiteSpace(member.CharacterId) ? member.ArchetypeId : member.CharacterId),
                    member.ArchetypeId,
                    member.CharacterId,
                    (DeploymentAnchorId)member.Anchor,
                    member.PositiveTraitId,
                    member.NegativeTraitId,
                    member.Role,
                    Enumerate(member.RuleModifierTags)
                        .Where(tag => !string.IsNullOrWhiteSpace(tag))
                        .Distinct(StringComparer.Ordinal)
                        .ToList()))
                .ToList());
    }

    internal static BossOverlayTemplate BuildBossOverlayTemplate(BossOverlayDefinition definition)
    {
        return new BossOverlayTemplate(
            definition.Id,
            ResolveLegacyName(definition.NameKey, definition.LegacyDisplayName, definition.Id),
            definition.PhaseTrigger,
            Math.Max(1, definition.ThreatCost),
            definition.SignatureAuraTag,
            definition.SignatureUtilityTag,
            Enumerate(definition.RewardDropTags)
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .Distinct(StringComparer.Ordinal)
                .ToList(),
            Enumerate(definition.AppliedStatuses)
                .Where(rule => rule != null && !string.IsNullOrWhiteSpace(rule.StatusId))
                .Select(rule => new StatusApplicationSpec(
                    string.IsNullOrWhiteSpace(rule.Id) ? $"{definition.Id}:{rule.StatusId}" : rule.Id,
                    rule.StatusId,
                    rule.DurationSeconds,
                    rule.Magnitude,
                    Math.Max(1, rule.MaxStacks),
                    rule.RefreshDurationOnReapply))
                .ToList());
    }
}
