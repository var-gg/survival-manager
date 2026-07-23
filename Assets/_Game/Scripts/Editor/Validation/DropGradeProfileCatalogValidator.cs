using System;
using System.Collections.Generic;
using System.Linq;
using SM.Content.Definitions;
using SM.Core.Content;
using SM.Meta.Services;

namespace SM.Editor.Validation;

internal static class DropGradeProfileCatalogValidator
{
    private const double MeanTolerance = 0.00005d;
    private static readonly IReadOnlyDictionary<string, (double Delta, RarityBracketValue Bracket)>
        RequiredTables = new Dictionary<string, (double, RarityBracketValue)>(StringComparer.Ordinal)
        {
            ["drop_table_skirmish"] = (0d, RarityBracketValue.Advanced),
            ["drop_table_elite"] = (0.35d, RarityBracketValue.Elite),
            ["drop_table_boss"] = (0.65d, RarityBracketValue.Boss),
        };

    internal static void Validate(
        CatalogValidationContext context,
        ICollection<ContentValidationIssue> issues)
    {
        foreach (var (tableId, specification) in RequiredTables)
        {
            if (!context.DropTables.TryGetValue(tableId, out var table))
            {
                continue;
            }

            var assetPath = context.GetPath(table);
            if (table.GradePowerKappa <= 0f || table.GradeStepBudgetScore <= 0f)
            {
                ContentValidationIssueFactory.AddError(
                    issues,
                    "reward.drop_grade_power_contract",
                    $"Drop table '{table.Id}' requires positive measured kappa and grade-step BudgetScore.",
                    assetPath);
            }

            var profiles = table.GradeProfiles
                .Where(profile => profile != null)
                .ToArray();
            var duplicateChapterIds = profiles
                .GroupBy(profile => profile.ChapterId, StringComparer.Ordinal)
                .Where(group => group.Count() > 1)
                .Select(group => group.Key)
                .ToArray();
            if (duplicateChapterIds.Length > 0
                || !profiles.Select(profile => profile.ChapterId).ToHashSet(StringComparer.Ordinal)
                    .SetEquals(context.Chapters.Keys))
            {
                ContentValidationIssueFactory.AddError(
                    issues,
                    "reward.drop_grade_chapter_coverage",
                    $"Drop table '{table.Id}' must author exactly one grade profile for every campaign chapter.",
                    assetPath);
                continue;
            }

            foreach (var profile in profiles)
            {
                var chapter = context.Chapters[profile.ChapterId];
                var expectedInitial = 0.30d
                                      + (0.52d * (Math.Max(1, chapter.StoryOrder) - 1))
                                      + specification.Delta;
                var expectedCalibrated = DropGradeEconomy.CalibrateMean(
                    DropGradeEconomy.MapRarityBracket(specification.Bracket),
                    profile.StandardDeviation,
                    table.GradePowerKappa);
                if (profile.InitialStandardDeviation <= 0f
                    || Math.Abs(profile.InitialStandardDeviation - 0.78d) > MeanTolerance
                    || profile.StandardDeviation <= 0f
                    || Math.Abs(profile.InitialLatentMean - expectedInitial) > MeanTolerance
                    || Math.Abs(profile.MeanPreservingLatentMean - expectedCalibrated) > MeanTolerance)
                {
                    ContentValidationIssueFactory.AddError(
                        issues,
                        "reward.drop_grade_profile_calibration",
                        $"Drop table '{table.Id}' chapter '{profile.ChapterId}' must preserve the formula initial mean, "
                        + "initial s_q=0.78, and the measured-kappa mean-preserving calibration.",
                        assetPath);
                }
            }
        }
    }
}
