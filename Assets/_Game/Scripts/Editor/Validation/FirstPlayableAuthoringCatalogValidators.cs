using System;
using System.Collections.Generic;
using System.Linq;
using SM.Content.Definitions;
using SM.Core.Content;
using SM.Unity;

namespace SM.Editor.Validation;

internal sealed class FirstPlayableSliceCatalogValidator : ICatalogValidationRule
{
    public void Validate(CatalogValidationContext context, ICollection<ContentValidationIssue> issues)
    {
        if (context.FirstPlayableSliceAsset == null)
        {
            ContentValidationIssueFactory.AddError(issues, "first_playable.asset_missing", "First playable slice asset is missing.", ContentValidationPolicyCatalog.ReportFolderName);
            return;
        }

        var assetPath = context.GetPath(context.FirstPlayableSliceAsset);
        var slice = context.FirstPlayableSliceAsset.ToRuntime();
        if (slice.SignaturePassiveCap != FirstPlayableAuthoringContract.LiveSignaturePassiveCap)
        {
            ContentValidationIssueFactory.AddError(issues, "first_playable.signature_passive_cap", $"SignaturePassiveCap must stay at {FirstPlayableAuthoringContract.LiveSignaturePassiveCap}.", assetPath, nameof(FirstPlayableSliceDefinitionAsset.SignaturePassiveCap));
        }

        if (slice.SignaturePassiveIds.Count != FirstPlayableAuthoringContract.LiveSignaturePassiveCap)
        {
            ContentValidationIssueFactory.AddError(issues, "first_playable.signature_passive_count", $"SignaturePassiveIds must stay at exact live count {FirstPlayableAuthoringContract.LiveSignaturePassiveCap}.", assetPath, nameof(FirstPlayableSliceDefinitionAsset.SignaturePassiveIds));
        }

        ValidateExactLiveCount(
            issues,
            assetPath,
            "flex_active",
            "FlexActive",
            nameof(FirstPlayableSliceDefinitionAsset.FlexActiveCap),
            nameof(FirstPlayableSliceDefinitionAsset.FlexActiveIds),
            slice.FlexActiveCap,
            slice.FlexActiveIds.Count,
            FirstPlayableAuthoringContract.LiveFlexActiveCap);
        ValidateExactLiveCount(
            issues,
            assetPath,
            "flex_passive",
            "FlexPassive",
            nameof(FirstPlayableSliceDefinitionAsset.FlexPassiveCap),
            nameof(FirstPlayableSliceDefinitionAsset.FlexPassiveIds),
            slice.FlexPassiveCap,
            slice.FlexPassiveIds.Count,
            FirstPlayableAuthoringContract.LiveFlexPassiveCap);

        var boardIds = slice.PassiveBoardIds
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(id => id, StringComparer.Ordinal)
            .ToList();
        var requiredBoards = FirstPlayableAuthoringContract.RequiredPassiveBoardIds
            .OrderBy(id => id, StringComparer.Ordinal)
            .ToList();
        if (!boardIds.SequenceEqual(requiredBoards))
        {
            ContentValidationIssueFactory.AddError(issues, "first_playable.passive_board_set", $"PassiveBoardIds must match [{string.Join(", ", requiredBoards)}].", assetPath, nameof(FirstPlayableSliceDefinitionAsset.PassiveBoardIds));
        }

        foreach (var boardId in boardIds)
        {
            if (!context.PassiveBoards.ContainsKey(boardId))
            {
                ContentValidationIssueFactory.AddError(issues, "first_playable.passive_board_ref", $"Passive board '{boardId}' does not resolve.", assetPath, nameof(FirstPlayableSliceDefinitionAsset.PassiveBoardIds));
            }
        }

        var liveIds = new HashSet<string>(StringComparer.Ordinal);
        AddLiveIds(liveIds, slice.UnitBlueprintIds);
        AddLiveIds(liveIds, slice.SignatureActiveIds);
        AddLiveIds(liveIds, slice.SignaturePassiveIds);
        AddLiveIds(liveIds, slice.FlexActiveIds);
        AddLiveIds(liveIds, slice.FlexPassiveIds);
        AddLiveIds(liveIds, slice.AffixIds);
        AddLiveIds(liveIds, slice.SynergyFamilyIds);
        AddLiveIds(liveIds, slice.TemporaryAugmentIds);
        AddLiveIds(liveIds, slice.PermanentAugmentIds);
        AddLiveIds(liveIds, slice.PassiveBoardIds);
        var overlaps = slice.ParkingLotContentIds
            .Where(id => !string.IsNullOrWhiteSpace(id) && liveIds.Contains(id))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(id => id, StringComparer.Ordinal)
            .ToList();
        if (overlaps.Count > 0)
        {
            ContentValidationIssueFactory.AddError(issues, "first_playable.parking_overlap", $"ParkingLotContentIds must not include live ids: {string.Join(", ", overlaps)}.", assetPath, nameof(FirstPlayableSliceDefinitionAsset.ParkingLotContentIds));
        }

        var grammarFamilies = slice.SynergyGrammar
            .Where(entry => !string.IsNullOrWhiteSpace(entry.FamilyId))
            .Select(entry => entry.FamilyId)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(id => id, StringComparer.Ordinal)
            .ToList();
        var liveFamilies = slice.SynergyFamilyIds
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(id => id, StringComparer.Ordinal)
            .ToList();
        if (!grammarFamilies.SequenceEqual(liveFamilies))
        {
            ContentValidationIssueFactory.AddError(issues, "first_playable.synergy_grammar_alignment", "SynergyGrammar must define the same live family ids as SynergyFamilyIds.", assetPath, nameof(FirstPlayableSliceDefinitionAsset.SynergyGrammar));
        }
    }

    private static void AddLiveIds(ISet<string> target, IEnumerable<string> ids)
    {
        foreach (var id in ids.Where(id => !string.IsNullOrWhiteSpace(id)))
        {
            target.Add(id);
        }
    }

    private static void ValidateExactLiveCount(
        ICollection<ContentValidationIssue> issues,
        string assetPath,
        string codePrefix,
        string fieldLabel,
        string capFieldName,
        string idsFieldName,
        int actualCap,
        int actualCount,
        int expectedCount)
    {
        if (actualCap != expectedCount)
        {
            ContentValidationIssueFactory.AddError(issues, $"first_playable.{codePrefix}_cap", $"{fieldLabel}Cap must stay at {expectedCount}.", assetPath, capFieldName);
        }

        if (actualCount != expectedCount)
        {
            ContentValidationIssueFactory.AddError(issues, $"first_playable.{codePrefix}_count", $"{fieldLabel}Ids must stay at exact live count {expectedCount}.", assetPath, idsFieldName);
        }
    }
}

internal sealed class EncounterAuthoringCatalogValidator : ICatalogValidationRule
{
    public void Validate(CatalogValidationContext context, ICollection<ContentValidationIssue> issues)
    {
        var familyCounts = new Dictionary<string, int>(StringComparer.Ordinal);
        var encounterFamilies = new Dictionary<string, string>(StringComparer.Ordinal);
        var encounterAnswerLanes = new Dictionary<string, string>(StringComparer.Ordinal);
        var eliteOrBossPresence = new Dictionary<string, int>(StringComparer.Ordinal);
        var totalPresence = new Dictionary<string, int>(StringComparer.Ordinal);
        var squadKinds = context.Encounters.Values
            .Where(encounter => !string.IsNullOrWhiteSpace(encounter.EnemySquadTemplateId))
            .GroupBy(encounter => encounter.EnemySquadTemplateId, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.Select(encounter => encounter.Kind).Distinct().ToList(), StringComparer.Ordinal);

        foreach (var encounter in context.Encounters.Values)
        {
            var assetPath = context.GetPath(encounter);
            var familyId = FirstPlayableAuthoringContract.ExtractEncounterFamily(encounter.RewardDropTags);
            if (string.IsNullOrWhiteSpace(familyId))
            {
                ContentValidationIssueFactory.AddError(issues, "encounter.family_assignment", "Encounter must declare exactly one allowed encounter family tag.", assetPath);
                continue;
            }

            encounterFamilies[encounter.Id] = familyId;
            familyCounts[familyId] = familyCounts.TryGetValue(familyId, out var count) ? count + 1 : 1;

            var answerLanes = FirstPlayableAuthoringContract.ExtractAnswerLanes(encounter.RewardDropTags);
            if (answerLanes.Count != 1)
            {
                ContentValidationIssueFactory.AddError(issues, "encounter.answer_lane_assignment", "Encounter must declare exactly one primary answer lane tag.", assetPath);
                continue;
            }

            encounterAnswerLanes[encounter.Id] = answerLanes[0];

            var askCount = 1;
            if (encounter.Kind == EncounterKindValue.Boss
                && !string.IsNullOrWhiteSpace(encounter.BossOverlayId)
                && context.Overlays.TryGetValue(encounter.BossOverlayId, out var overlay))
            {
                askCount += FirstPlayableAuthoringContract.ExtractOverlayAskTags(overlay.RewardDropTags).Count;
            }

            if (encounter.Kind == EncounterKindValue.Boss && askCount > 2)
            {
                ContentValidationIssueFactory.AddError(issues, "encounter.boss_readability_budget", "Boss readability budget must stay at 2 asks or fewer.", assetPath);
            }
        }

        foreach (var familyId in FirstPlayableAuthoringContract.AllowedEncounterFamilyIds)
        {
            if (!familyCounts.TryGetValue(familyId, out var count) || count < 2 || count > 4)
            {
                ContentValidationIssueFactory.AddError(issues, "encounter.family_distribution", $"Encounter family '{familyId}' must appear 2~4 times. Found {count}.", ContentValidationPolicyCatalog.ReportFolderName);
            }
        }

        var chapterAnswerLanes = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
        foreach (var site in context.Sites.Values)
        {
            var assetPath = context.GetPath(site);
            var sequence = site.EncounterIds
                .Where(encounterFamilies.ContainsKey)
                .Select(id => encounterFamilies[id])
                .ToList();
            if (sequence.Count == 4)
            {
                var duplicateSkirmish = string.Equals(sequence[0], sequence[1], StringComparison.Ordinal);
                if (duplicateSkirmish)
                {
                    ContentValidationIssueFactory.AddError(issues, "encounter.site_skirmish_repeat", $"Site '{site.Id}' reuses the same encounter family in both skirmishes.", assetPath);
                }
            }

            var answerLanes = site.EncounterIds
                .Where(encounterAnswerLanes.ContainsKey)
                .Select(id => encounterAnswerLanes[id])
                .Distinct(StringComparer.Ordinal)
                .OrderBy(id => id, StringComparer.Ordinal)
                .ToList();
            if (answerLanes.Count != 1)
            {
                ContentValidationIssueFactory.AddError(issues, "reward.answer_lane_site_contract", $"Site '{site.Id}' must expose exactly one primary answer lane. Found [{string.Join(", ", answerLanes)}].", assetPath);
                continue;
            }

            if (!chapterAnswerLanes.TryGetValue(site.ChapterId, out var lanes))
            {
                lanes = new HashSet<string>(StringComparer.Ordinal);
                chapterAnswerLanes[site.ChapterId] = lanes;
            }

            if (!lanes.Add(answerLanes[0]))
            {
                ContentValidationIssueFactory.AddError(issues, "reward.answer_lane_chapter_overlap", $"Chapter '{site.ChapterId}' reuses primary answer lane '{answerLanes[0]}' across sites.", assetPath);
            }

            ValidateRewardRouting(context, issues, site, answerLanes[0]);
        }

        var siteSequences = context.Sites.Values
            .Select(site => new
            {
                SiteId = site.Id,
                Sequence = string.Join(">", site.EncounterIds.Where(encounterFamilies.ContainsKey).Select(id => encounterFamilies[id])),
                AssetPath = context.GetPath(site),
            })
            .ToList();
        foreach (var duplicate in siteSequences
                     .Where(entry => !string.IsNullOrWhiteSpace(entry.Sequence))
                     .GroupBy(entry => entry.Sequence, StringComparer.Ordinal)
                     .Where(group => group.Count() > 1))
        {
            foreach (var site in duplicate)
            {
                ContentValidationIssueFactory.AddError(issues, "encounter.site_sequence_uniqueness", $"Site sequence '{duplicate.Key}' must be unique across all sites.", site.AssetPath);
            }
        }

        foreach (var squad in context.Squads.Values)
        {
            foreach (var member in squad.Members.Where(member => !string.IsNullOrWhiteSpace(member.ArchetypeId)))
            {
                totalPresence[member.ArchetypeId] = totalPresence.TryGetValue(member.ArchetypeId, out var count) ? count + 1 : 1;
                if (squadKinds.TryGetValue(squad.Id, out var kinds)
                    && kinds.Any(kind => kind == EncounterKindValue.Elite || kind == EncounterKindValue.Boss))
                {
                    eliteOrBossPresence[member.ArchetypeId] = eliteOrBossPresence.TryGetValue(member.ArchetypeId, out var value) ? value + 1 : 1;
                }
            }
        }

        foreach (var archetypeId in context.ArchetypeIds.OrderBy(id => id, StringComparer.Ordinal))
        {
            totalPresence.TryGetValue(archetypeId, out var total);
            if (total < 2)
            {
                ContentValidationIssueFactory.AddWarning(issues, "encounter.archetype_enemy_usage_total", $"Archetype '{archetypeId}' must appear at least twice across enemy squads. Found {total}.", ContentValidationPolicyCatalog.ReportFolderName);
            }

            eliteOrBossPresence.TryGetValue(archetypeId, out var eliteBossCount);
            if (eliteBossCount < 1)
            {
                ContentValidationIssueFactory.AddWarning(issues, "encounter.archetype_enemy_usage_elite_boss", $"Archetype '{archetypeId}' must appear at least once in elite/boss squads. Found {eliteBossCount}.", ContentValidationPolicyCatalog.ReportFolderName);
            }
        }
    }

    private static void ValidateRewardRouting(
        CatalogValidationContext context,
        ICollection<ContentValidationIssue> issues,
        ExpeditionSiteDefinition site,
        string answerLane)
    {
        var rewardSources = site.EncounterIds
            .Where(context.Encounters.ContainsKey)
            .Select(id => context.Encounters[id].RewardSourceId)
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Distinct(StringComparer.Ordinal)
            .ToList();
        foreach (var rewardSourceId in rewardSources)
        {
            if (!context.RewardSources.TryGetValue(rewardSourceId, out var rewardSource)
                || !context.DropTables.TryGetValue(rewardSource.DropTableId, out var dropTable))
            {
                continue;
            }

            var hasRoutedEntry = dropTable.Entries.Any(entry =>
            {
                var tags = FirstPlayableAuthoringContract.ExtractContextTags(entry);
                return tags.Contains(site.Id, StringComparer.Ordinal)
                       && tags.Contains(answerLane, StringComparer.Ordinal);
            });
            if (!hasRoutedEntry)
            {
                ContentValidationIssueFactory.AddError(issues, "reward.answer_lane_coverage", $"Reward source '{rewardSourceId}' is missing a site answer-lane entry for '{site.Id}' / '{answerLane}'.", context.GetPath(dropTable));
            }
        }
    }
}

internal sealed class BuildLaneCoverageCatalogValidator : ICatalogValidationRule
{
    public void Validate(CatalogValidationContext context, ICollection<ContentValidationIssue> issues)
    {
        foreach (var pair in FirstPlayableAuthoringContract.ClassLaneSupportIds)
        {
            var availableSupports = context.Archetypes.Values
                .Where(archetype => string.Equals(archetype.Class?.Id, pair.Key, StringComparison.Ordinal))
                .SelectMany(FirstPlayableAuthoringContract.CollectSupportSkillIds)
                .Distinct(StringComparer.Ordinal)
                .ToHashSet(StringComparer.Ordinal);
            foreach (var supportId in pair.Value)
            {
                if (!availableSupports.Contains(supportId))
                {
                    ContentValidationIssueFactory.AddWarning(issues, "build_lane.class_lane_missing", $"Class '{pair.Key}' is missing support lane surface '{supportId}'.", ContentValidationPolicyCatalog.ReportFolderName);
                }
            }
        }

        foreach (var expectation in FirstPlayableAuthoringContract.ArchetypeBuildLaneExpectations.Values)
        {
            if (!context.Archetypes.TryGetValue(expectation.ArchetypeId, out var archetype))
            {
                ContentValidationIssueFactory.AddWarning(issues, "build_lane.archetype_missing", $"Archetype '{expectation.ArchetypeId}' is missing from build-lane coverage.", ContentValidationPolicyCatalog.ReportFolderName);
                continue;
            }

            var supports = FirstPlayableAuthoringContract.CollectSupportSkillIds(archetype)
                .ToHashSet(StringComparer.Ordinal);
            if (!supports.Contains(expectation.BaselineSupportId))
            {
                ContentValidationIssueFactory.AddWarning(issues, "build_lane.archetype_baseline_missing", $"Archetype '{expectation.ArchetypeId}' is missing baseline lane '{expectation.BaselineLaneId}' via '{expectation.BaselineSupportId}'.", context.GetPath(archetype));
            }

            if (!supports.Contains(expectation.AltSupportId))
            {
                ContentValidationIssueFactory.AddWarning(issues, "build_lane.archetype_alt_missing", $"Archetype '{expectation.ArchetypeId}' is missing alt lane '{expectation.AltLaneId}' via '{expectation.AltSupportId}'.", context.GetPath(archetype));
            }
        }
    }
}
