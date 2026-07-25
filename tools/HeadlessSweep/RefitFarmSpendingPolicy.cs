using System.Globalization;
using SM.Core.Content;
using SM.Meta.Services;

internal sealed record RefitFarmSpendingResult(
    IReadOnlyList<RefitFarmPurchaseObservation> Purchases,
    IReadOnlyList<RefitFarmPreviewObservation> PreviewDiagnostics);

internal static class RefitFarmSpendingPolicy
{
    private static readonly IReadOnlyDictionary<string, int> SlotOrder =
        new Dictionary<string, int>(StringComparer.Ordinal)
        {
            ["Weapon"] = 0,
            ["Armor"] = 1,
            ["Accessory"] = 2,
        };

    internal static RefitFarmSpendingResult SpendAfterMap(
        HeadlessCampaignState state,
        int mapIndex,
        RefitService service,
        RefitChapterEconomy economy)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(service);
        ArgumentNullException.ThrowIfNull(economy);

        var purchases = new List<RefitFarmPurchaseObservation>();
        var diagnostics = new List<RefitFarmPreviewObservation>();
        var purchaseRound = 0;
        while (true)
        {
            _ = HeadlessCampaignEquipmentPowerPolicy.Apply(state);
            var baselinePower = HeadlessCampaignEffectivePower.Measure(state);
            var candidates = new List<Candidate>();
            foreach (var item in state.Inventory
                         .Where(value => value.RarityTier >= ItemRarityTierValue.Epic)
                         .OrderBy(value => value.AcquisitionIndex)
                         .ThenBy(value => value.InstanceId, StringComparer.Ordinal))
            {
                var itemState = ToRefitState(item);
                var quote = service.QuoteNextEffective(itemState, economy);
                if (!quote.CanPurchase || quote.EchoCost > state.Echo)
                {
                    continue;
                }

                var stableCommandSeed = DeriveStableCommandSeed(
                    state.CampaignSeed,
                    mapIndex,
                    item.AcquisitionIndex,
                    quote.TargetRefitLevel);
                var execution = service.RefitNextEffective(
                    itemState,
                    economy,
                    stableCommandSeed);
                if (!execution.Applied || execution.InvariantFailure)
                {
                    throw new InvalidOperationException(
                        $"Refit preview failed for '{item.InstanceId}': {execution.Error}");
                }

                var oldAffixes = item.AffixIds;
                var oldRefitLevel = item.RefitLevel;
                try
                {
                    item.AffixIds = execution.AffixIds.ToArray();
                    item.RefitLevel = quote.TargetRefitLevel;
                    var resultingLoadout = HeadlessCampaignEquipmentPowerPolicy.Apply(state);
                    var resultingPower = HeadlessCampaignEffectivePower.Measure(state);
                    var deltaLogPower = resultingPower.LogPower - baselinePower.LogPower;
                    var transitionKey =
                        $"{mapIndex.ToString(CultureInfo.InvariantCulture)}|"
                        + $"{purchaseRound.ToString(CultureInfo.InvariantCulture)}|"
                        + $"{item.InstanceId}|{quote.CurrentRefitLevel.ToString(CultureInfo.InvariantCulture)}"
                        + $"->{quote.TargetRefitLevel.ToString(CultureInfo.InvariantCulture)}";
                    var budgetScoreIncreased = quote.TargetScoreQ > quote.CurrentScoreQ;
                    diagnostics.Add(new RefitFarmPreviewObservation(
                        transitionKey,
                        deltaLogPower,
                        budgetScoreIncreased,
                        budgetScoreIncreased && deltaLogPower < -1e-12d));

                    var placement = resultingLoadout.Slots
                        .FirstOrDefault(slot => string.Equals(
                            slot.ItemInstanceId,
                            item.InstanceId,
                            StringComparison.Ordinal));
                    var heroIndex = placement == null
                        ? int.MaxValue
                        : IndexOf(state.ExpeditionSquadHeroIds, placement.HeroId);
                    var slotIndex = placement == null
                        ? int.MaxValue
                        : SlotOrder.GetValueOrDefault(placement.SlotType, int.MaxValue);
                    candidates.Add(new Candidate(
                        item,
                        execution.AffixIds.ToArray(),
                        quote,
                        deltaLogPower,
                        deltaLogPower / quote.EchoCost,
                        heroIndex,
                        slotIndex,
                        resultingPower.LogPower));
                }
                finally
                {
                    item.AffixIds = oldAffixes;
                    item.RefitLevel = oldRefitLevel;
                }
            }

            _ = HeadlessCampaignEquipmentPowerPolicy.Apply(state);
            var selected = candidates
                .Where(candidate => candidate.DeltaLogPower > 0d)
                .OrderByDescending(candidate => candidate.Ratio)
                .ThenBy(candidate => candidate.HeroIndex)
                .ThenBy(candidate => candidate.SlotIndex)
                .ThenBy(candidate => candidate.Item.InstanceId, StringComparer.Ordinal)
                .ThenBy(candidate => candidate.Quote.TargetRefitLevel)
                .FirstOrDefault();
            if (selected == null)
            {
                break;
            }

            var powerBefore = HeadlessCampaignEffectivePower.Measure(state);
            if (!state.TrySpendEcho(selected.Quote.EchoCost))
            {
                throw new InvalidOperationException(
                    $"Selected Refit '{selected.Item.InstanceId}' became unaffordable before commit.");
            }

            selected.Item.AffixIds = selected.AffixIds;
            selected.Item.RefitLevel = selected.Quote.TargetRefitLevel;
            _ = HeadlessCampaignEquipmentPowerPolicy.Apply(state);
            var powerAfter = HeadlessCampaignEffectivePower.Measure(state);
            var actualDelta = powerAfter.LogPower - powerBefore.LogPower;
            if (actualDelta <= 0d
                || Math.Abs(powerAfter.LogPower - selected.PreviewResultLogPower) > 1e-10d)
            {
                throw new InvalidOperationException(
                    $"Committed Refit '{selected.Item.InstanceId}' did not reproduce its positive preview: "
                    + $"preview={selected.DeltaLogPower:R}, actual={actualDelta:R}.");
            }

            var postQuote = service.QuoteNextEffective(
                ToRefitState(selected.Item),
                economy);
            var resultPercentile = RefitFloorSchedule.ToDouble(postQuote.CurrentPercentileQ64);
            var targetFloor = RefitFloorSchedule.ToDouble(selected.Quote.TargetFloorQ64);
            purchases.Add(new RefitFarmPurchaseObservation(
                mapIndex,
                selected.Item.InstanceId,
                selected.Quote.TargetRefitLevel,
                selected.Quote.EchoCost,
                selected.Quote.CurrentScoreQ,
                selected.Quote.TargetScoreQ,
                actualDelta,
                Math.Max(0d, resultPercentile - targetFloor)));
            purchaseRound++;
        }

        return new RefitFarmSpendingResult(purchases, diagnostics);
    }

    internal static RefitItemState ToRefitState(HeadlessCampaignItem item)
        => new(
            item.ItemBaseId,
            item.InstanceId,
            item.RarityTier,
            item.AffixIds,
            item.RefitLevel);

    private static int IndexOf(IReadOnlyList<string> values, string value)
    {
        for (var index = 0; index < values.Count; index++)
        {
            if (string.Equals(values[index], value, StringComparison.Ordinal))
            {
                return index;
            }
        }

        return int.MaxValue;
    }

    private static ulong DeriveStableCommandSeed(
        int campaignSeed,
        int mapIndex,
        int acquisitionIndex,
        int targetRefitLevel)
    {
        const ulong offset = 14695981039346656037UL;
        const ulong prime = 1099511628211UL;
        var hash = offset;
        foreach (var part in new[]
                 {
                     campaignSeed.ToString(CultureInfo.InvariantCulture),
                     mapIndex.ToString(CultureInfo.InvariantCulture),
                     acquisitionIndex.ToString(CultureInfo.InvariantCulture),
                     targetRefitLevel.ToString(CultureInfo.InvariantCulture),
                     "REFIT_FARM_PROFILE",
                 })
        {
            foreach (var character in part)
            {
                unchecked
                {
                    hash ^= character;
                    hash *= prime;
                }
            }

            unchecked
            {
                hash ^= 0xff;
                hash *= prime;
            }
        }

        return hash;
    }

    private sealed record Candidate(
        HeadlessCampaignItem Item,
        IReadOnlyList<string> AffixIds,
        RefitQuote Quote,
        double DeltaLogPower,
        double Ratio,
        int HeroIndex,
        int SlotIndex,
        double PreviewResultLogPower);
}
