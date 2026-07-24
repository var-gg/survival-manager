using System;
using System.Collections.Generic;
using System.Linq;
using SM.Core.Content;
using SM.Meta.Model;

namespace SM.Meta.Services;

/// <summary>
/// 캠페인 재방문 recovery budget의 단일 수치 소유자.
/// 첫 클리어 보상에는 적용하지 않고, chapter-pooled rewarded revisit와 패배 위로 Echo만 계산한다.
/// </summary>
public static class CampaignRecoveryRewardPolicy
{
    // Owner-ratifiable knobs. Keep these together so a later tuning pass has one edit surface.
    public const int RewardedRevisitLimit = 4;
    public const double RewardedRevisitDecayRho = 0.65d;
    public const double RevisitGoldBudgetRatioOfMedianRecruit = 0.30d;
    public const int RewardedDefeatLimit = 2;
    public const double FirstDefeatEchoCoefficient = 0.25d;
    public const double SubsequentDefeatEchoMultiplier = 0.50d;

    private static readonly int[] ItemRollsByRewardedRevisit = { 4, 3, 2, 1 };
    private static readonly ItemRarityTierValue[] MinimumItemGradesByRewardedRevisit =
    {
        ItemRarityTierValue.Epic,
        ItemRarityTierValue.Legendary,
        ItemRarityTierValue.Legendary,
        ItemRarityTierValue.Legendary,
    };

    public static int GetItemRollCount(int rewardedRevisitIndex)
        => rewardedRevisitIndex >= 1 && rewardedRevisitIndex <= ItemRollsByRewardedRevisit.Length
            ? ItemRollsByRewardedRevisit[rewardedRevisitIndex - 1]
            : 0;

    public static int GetItemRollCountBefore(int rewardedRevisitIndex)
        => rewardedRevisitIndex <= 1
            ? 0
            : ItemRollsByRewardedRevisit
                .Take(Math.Min(rewardedRevisitIndex - 1, ItemRollsByRewardedRevisit.Length))
                .Sum();

    public static ItemRarityTierValue GetMinimumItemGrade(int rewardedRevisitIndex)
        => rewardedRevisitIndex >= 1
           && rewardedRevisitIndex <= MinimumItemGradesByRewardedRevisit.Length
            ? MinimumItemGradesByRewardedRevisit[rewardedRevisitIndex - 1]
            : ItemRarityTierValue.Common;

    public static int GetRevisitEcho(int firstFarmRunEcho, int rewardedRevisitIndex)
    {
        if (firstFarmRunEcho <= 0
            || rewardedRevisitIndex < 1
            || rewardedRevisitIndex > RewardedRevisitLimit)
        {
            return 0;
        }

        return Math.Max(
            0,
            (int)Math.Round(
                firstFarmRunEcho * Math.Pow(RewardedRevisitDecayRho, rewardedRevisitIndex - 1),
                MidpointRounding.AwayFromZero));
    }

    public static int GetDefeatConsolationEcho(int firstFarmRunEcho, int rewardedDefeatIndex)
    {
        if (firstFarmRunEcho <= 0
            || rewardedDefeatIndex < 1
            || rewardedDefeatIndex > RewardedDefeatLimit)
        {
            return 0;
        }

        var coefficient = FirstDefeatEchoCoefficient
                          * Math.Pow(SubsequentDefeatEchoMultiplier, rewardedDefeatIndex - 1);
        return Math.Max(
            0,
            (int)Math.Round(
                firstFarmRunEcho * coefficient,
                MidpointRounding.AwayFromZero));
    }

    public static int GetMedianRecruitGoldCost()
    {
        var costs = new[]
        {
            RecruitmentBalanceCatalog.DefaultRecruitTierCosts.CommonGoldCost,
            RecruitmentBalanceCatalog.DefaultRecruitTierCosts.RareGoldCost,
            RecruitmentBalanceCatalog.DefaultRecruitTierCosts.EpicGoldCost,
        };
        Array.Sort(costs);
        return costs[costs.Length / 2];
    }

    public static int GetChapterRevisitGoldBudget()
        => Math.Max(
            0,
            (int)Math.Round(
                GetMedianRecruitGoldCost() * RevisitGoldBudgetRatioOfMedianRecruit,
                MidpointRounding.AwayFromZero));

    public static int GetRevisitGold(int rewardedRevisitIndex)
    {
        if (rewardedRevisitIndex < 1 || rewardedRevisitIndex > RewardedRevisitLimit)
        {
            return 0;
        }

        return BuildIntegerGoldAllocation(GetChapterRevisitGoldBudget())[rewardedRevisitIndex - 1];
    }

    /// <summary>
    /// E1 is authored by the chapter's first site's extract reward source. Guaranteed Echo is summed;
    /// weighted Echo uses its authored expected value against the complete weighted pool.
    /// </summary>
    public static int ResolveFirstFarmRunEcho(CombatContentSnapshot content, string chapterId)
    {
        if (content == null
            || string.IsNullOrWhiteSpace(chapterId)
            || content.CampaignChapters is not { } chapters
            || content.ExpeditionSites is not { } sites
            || content.RewardSources is not { } rewardSources
            || content.DropTables is not { } dropTables
            || !chapters.TryGetValue(chapterId, out var chapter))
        {
            return 0;
        }

        var site = chapter.SiteIds
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Select(id => sites.TryGetValue(id, out var candidate) ? candidate : null)
            .Where(candidate => candidate != null)
            .OrderBy(candidate => candidate!.SiteOrder)
            .ThenBy(candidate => candidate!.Id, StringComparer.Ordinal)
            .FirstOrDefault();
        if (site == null
            || string.IsNullOrWhiteSpace(site.ExtractRewardSourceId)
            || !rewardSources.TryGetValue(site.ExtractRewardSourceId, out var source)
            || !dropTables.TryGetValue(source.DropTableId, out var dropTable))
        {
            return 0;
        }

        var eligible = dropTable.Entries
            .Where(entry => entry.RequiredContextTags == null || entry.RequiredContextTags.Count == 0)
            .ToArray();
        var guaranteedEcho = eligible
            .Where(entry => entry.IsGuaranteed && entry.RewardType == RewardType.Echo)
            .Sum(entry => Math.Max(0, entry.Amount));
        var weighted = eligible.Where(entry => !entry.IsGuaranteed).ToArray();
        var totalWeight = weighted.Sum(entry => Math.Max(1, entry.Weight));
        var weightedEcho = totalWeight == 0
            ? 0d
            : weighted
                .Where(entry => entry.RewardType == RewardType.Echo)
                .Sum(entry => Math.Max(0, entry.Amount) * (double)Math.Max(1, entry.Weight))
              / totalWeight;
        return Math.Max(
            0,
            guaranteedEcho + (int)Math.Round(weightedEcho, MidpointRounding.AwayFromZero));
    }

    private static IReadOnlyList<int> BuildIntegerGoldAllocation(int totalBudget)
    {
        if (totalBudget <= 0)
        {
            return new int[RewardedRevisitLimit];
        }

        var normalization = 1d - Math.Pow(RewardedRevisitDecayRho, RewardedRevisitLimit);
        var exact = Enumerable.Range(0, RewardedRevisitLimit)
            .Select(index =>
                totalBudget
                * ((1d - RewardedRevisitDecayRho) * Math.Pow(RewardedRevisitDecayRho, index))
                / normalization)
            .ToArray();
        var allocation = exact.Select(value => (int)Math.Floor(value)).ToArray();
        var remainder = totalBudget - allocation.Sum();
        foreach (var index in Enumerable.Range(0, RewardedRevisitLimit)
                     .OrderByDescending(index => exact[index] - allocation[index])
                     .ThenBy(index => index)
                     .Take(remainder))
        {
            allocation[index] += 1;
        }

        return allocation;
    }
}
