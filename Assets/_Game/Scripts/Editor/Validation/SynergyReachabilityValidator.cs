using System;
using System.Collections.Generic;
using System.Linq;
using SM.Meta.Model;

namespace SM.Editor.Validation;

/// <summary>
/// Verifies that every synergy breakpoint declared in the first-playable slice
/// is reachable given the archetypes available in the slice.
/// Race families require MajorThreshold (4) matching archetypes;
/// class families require MajorThreshold (3) matching archetypes.
/// </summary>
internal static class SynergyReachabilityValidator
{
    private const string SliceAssetPath = "Assets/Resources/_Game/Content/Definitions/FirstPlayable/first_playable_slice.asset";

    public static void Validate(
        CombatContentSnapshot snapshot,
        FirstPlayableSliceDefinition slice,
        ICollection<ContentValidationIssue> issues)
    {
        if (snapshot == null || slice == null)
        {
            return;
        }

        var sliceArchetypes = slice.UnitBlueprintIds
            .Where(id => snapshot.Archetypes.ContainsKey(id))
            .Select(id => snapshot.Archetypes[id])
            .ToList();

        if (sliceArchetypes.Count == 0)
        {
            ContentValidationIssueFactory.AddWarning(
                issues,
                "synergy.reachability.no_archetypes",
                "No archetypes resolved from slice — synergy reachability cannot be verified.",
                SliceAssetPath);
            return;
        }

        if (slice.SynergyGrammar.Count > 0)
        {
            ValidateFromGrammar(sliceArchetypes, slice, issues);
        }
        else
        {
            ValidateFromCatalog(sliceArchetypes, snapshot, slice, issues);
        }
    }

    private static void ValidateFromGrammar(
        IReadOnlyList<CombatArchetypeTemplate> archetypes,
        FirstPlayableSliceDefinition slice,
        ICollection<ContentValidationIssue> issues)
    {
        foreach (var entry in slice.SynergyGrammar)
        {
            if (string.IsNullOrWhiteSpace(entry.FamilyId))
            {
                continue;
            }

            var matchCount = CountMatchingArchetypes(archetypes, entry.FamilyId, entry.FamilyType);

            if (entry.MajorThreshold > 0 && matchCount < entry.MajorThreshold)
            {
                ContentValidationIssueFactory.AddError(
                    issues,
                    "synergy.reachability.major_unreachable",
                    $"Synergy '{entry.FamilyId}' ({entry.FamilyType}) major threshold {entry.MajorThreshold} "
                    + $"is unreachable: only {matchCount} matching archetype(s) in slice.",
                    SliceAssetPath,
                    $"SynergyGrammar.{entry.FamilyId}");
            }

            if (entry.MinorThreshold > 0 && matchCount < entry.MinorThreshold)
            {
                ContentValidationIssueFactory.AddError(
                    issues,
                    "synergy.reachability.minor_unreachable",
                    $"Synergy '{entry.FamilyId}' ({entry.FamilyType}) minor threshold {entry.MinorThreshold} "
                    + $"is unreachable: only {matchCount} matching archetype(s) in slice.",
                    SliceAssetPath,
                    $"SynergyGrammar.{entry.FamilyId}");
            }
        }
    }

    /// <summary>
    /// Fallback when SynergyGrammar is empty: infer families from the SynergyCatalog
    /// tier rules and check that each family's maximum threshold is reachable.
    /// </summary>
    private static void ValidateFromCatalog(
        IReadOnlyList<CombatArchetypeTemplate> archetypes,
        CombatContentSnapshot snapshot,
        FirstPlayableSliceDefinition slice,
        ICollection<ContentValidationIssue> issues)
    {
        if (snapshot.SynergyCatalog == null || snapshot.SynergyCatalog.Count == 0)
        {
            return;
        }

        ContentValidationIssueFactory.AddWarning(
            issues,
            "synergy.reachability.no_grammar",
            "SynergyGrammar is empty; falling back to SynergyCatalog inference for reachability checks.",
            SliceAssetPath);

        var families = snapshot.SynergyCatalog.Values
            .GroupBy(tier => tier.Rule.SynergyId, StringComparer.Ordinal);

        foreach (var family in families)
        {
            var synergyId = family.Key;
            if (!slice.SynergyFamilyIds.Contains(synergyId, StringComparer.Ordinal))
            {
                continue;
            }

            var countedTagId = family.First().Rule.CountedTagId;
            var maxThreshold = family.Max(tier => tier.Rule.Threshold);

            var isRace = archetypes.Any(a => string.Equals(a.RaceId, countedTagId, StringComparison.Ordinal));
            var familyType = isRace ? SynergyFamilyType.Race : SynergyFamilyType.Class;
            var matchCount = CountMatchingArchetypes(archetypes, countedTagId, familyType);

            if (matchCount == 0 && !isRace
                && !archetypes.Any(a => string.Equals(a.ClassId, countedTagId, StringComparison.Ordinal)))
            {
                ContentValidationIssueFactory.AddWarning(
                    issues,
                    "synergy.reachability.unknown_tag",
                    $"Synergy '{synergyId}' counted tag '{countedTagId}' matches neither RaceId "
                    + "nor ClassId of any slice archetype.",
                    SliceAssetPath,
                    $"SynergyCatalog.{synergyId}");
                continue;
            }

            if (matchCount < maxThreshold)
            {
                ContentValidationIssueFactory.AddError(
                    issues,
                    "synergy.reachability.major_unreachable",
                    $"Synergy '{synergyId}' max threshold {maxThreshold} is unreachable: "
                    + $"only {matchCount} archetype(s) with tag '{countedTagId}' in slice.",
                    SliceAssetPath,
                    $"SynergyCatalog.{synergyId}");
            }
        }
    }

    private static int CountMatchingArchetypes(
        IReadOnlyList<CombatArchetypeTemplate> archetypes,
        string tagId,
        SynergyFamilyType familyType)
    {
        return familyType switch
        {
            SynergyFamilyType.Race => archetypes.Count(a =>
                string.Equals(a.RaceId, tagId, StringComparison.Ordinal)),
            SynergyFamilyType.Class => archetypes.Count(a =>
                string.Equals(a.ClassId, tagId, StringComparison.Ordinal)),
            _ => 0,
        };
    }
}
