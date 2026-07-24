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
    private const double CampaignPowerTolerance = 0.00005d;
    private const double MaximumJackpotWeight = 0.05d;
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

            if (table.GradeJackpotWeight <= 0f
                || table.GradeJackpotWeight > MaximumJackpotWeight
                || table.GradeJackpotLatentMean < 3.5f
                || table.GradeJackpotStandardDeviation <= 0f
                || table.GradeJackpotStandardDeviation > 1f)
            {
                ContentValidationIssueFactory.AddError(
                    issues,
                    "reward.drop_grade_jackpot_contract",
                    $"Drop table '{table.Id}' requires a low-probability (0, {MaximumJackpotWeight:P0}] "
                    + "high-grade Gaussian jackpot component.",
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
                if (profile.InitialStandardDeviation <= 0f
                    || Math.Abs(profile.InitialStandardDeviation - 0.78d) > MeanTolerance
                    || profile.StandardDeviation <= 0f
                    || Math.Abs(profile.StandardDeviation - 0.5d) > MeanTolerance
                    || Math.Abs(profile.InitialLatentMean - expectedInitial) > MeanTolerance
                    || !float.IsFinite(profile.MeanPreservingLatentMean))
                {
                    ContentValidationIssueFactory.AddError(
                        issues,
                        "reward.drop_grade_profile_calibration",
                        $"Drop table '{table.Id}' chapter '{profile.ChapterId}' must preserve the formula initial mean, "
                        + "initial s_q=0.78, runtime s_q=0.5, and a finite runtime mean.",
                        assetPath);
                }
            }

            var orderedProfiles = profiles
                .OrderBy(profile => context.Chapters[profile.ChapterId].StoryOrder)
                .ToArray();
            var chapterSteps = orderedProfiles
                .Zip(
                    orderedProfiles.Skip(1),
                    (previous, next) =>
                        (double)next.MeanPreservingLatentMean - previous.MeanPreservingLatentMean)
                .ToArray();
            if (chapterSteps.Length == 0
                || chapterSteps.Any(step => step <= MeanTolerance)
                || chapterSteps.Max() - chapterSteps.Min() > MeanTolerance)
            {
                ContentValidationIssueFactory.AddError(
                    issues,
                    "reward.drop_grade_chapter_progression",
                    $"Drop table '{table.Id}' runtime means must rise by one consistent positive step per chapter.",
                    assetPath);
            }

            var actualCampaignPower = profiles.Average(profile =>
                DropGradeEconomy.ExpectedItemPower(
                    profile.MeanPreservingLatentMean,
                    profile.StandardDeviation,
                    table.GradePowerKappa,
                    table.GradeJackpotWeight,
                    table.GradeJackpotLatentMean,
                    table.GradeJackpotStandardDeviation));
            var targetCampaignPower = Math.Exp(
                DropGradeEconomy.FirstClearReferenceKappa
                * (int)DropGradeEconomy.MapRarityBracket(specification.Bracket));
            if (Math.Abs(actualCampaignPower - targetCampaignPower) > CampaignPowerTolerance)
            {
                ContentValidationIssueFactory.AddError(
                    issues,
                    "reward.drop_grade_campaign_power",
                    $"Drop table '{table.Id}' campaign-average item power drifted: "
                    + $"actual={actualCampaignPower:F9}, target={targetCampaignPower:F9}.",
                    assetPath);
            }
        }

        ValidateSharedMixture(context, issues);
    }

    private static void ValidateSharedMixture(
        CatalogValidationContext context,
        ICollection<ContentValidationIssue> issues)
    {
        var tables = RequiredTables.Keys
            .Where(context.DropTables.ContainsKey)
            .Select(tableId => context.DropTables[tableId])
            .ToArray();
        if (tables.Length != RequiredTables.Count)
        {
            return;
        }

        var first = tables[0];
        if (tables.Skip(1).Any(table =>
                Math.Abs(table.GradeJackpotWeight - first.GradeJackpotWeight) > MeanTolerance
                || Math.Abs(table.GradeJackpotLatentMean - first.GradeJackpotLatentMean) > MeanTolerance
                || Math.Abs(
                    table.GradeJackpotStandardDeviation
                    - first.GradeJackpotStandardDeviation) > MeanTolerance))
        {
            ContentValidationIssueFactory.AddError(
                issues,
                "reward.drop_grade_shared_jackpot",
                "First-clear skirmish, elite, and boss drop tables must share one jackpot mixture.",
                context.GetPath(first));
        }
    }
}
