using SM.Core.Content;
using SM.Meta.Model;
using SM.Meta.Services;

internal static class RefitFarmScenarioSimulator
{
    internal static RefitFarmScenarioResult Run(
        EndlessHeatPreparedScenario scenario,
        int horizonMaps,
        int heat,
        RefitService service,
        RefitChapterEconomy economy,
        RefitBalanceTemplate balance)
    {
        ArgumentNullException.ThrowIfNull(scenario);
        if (horizonMaps <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(horizonMaps));
        }

        var initial = scenario.State.CloneWithHeat(heat);
        var initialPower = HeadlessCampaignEffectivePower.Measure(initial);
        var dropsOnly = initial.CloneWithHeat(heat);
        var dropsAndRefit = initial.CloneWithHeat(heat);
        if (!string.Equals(
                RefitFarmPairingVerifier.HashInitialState(dropsOnly),
                RefitFarmPairingVerifier.HashInitialState(dropsAndRefit),
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Refit farm initial-save pairing failed ({scenario.Cell.CellId}/seed={scenario.SeedSalt}).");
        }

        if (dropsOnly.CampaignSeed != dropsAndRefit.CampaignSeed)
        {
            throw new InvalidOperationException(
                $"Refit farm campaign-seed pairing failed ({scenario.Cell.CellId}/seed={scenario.SeedSalt}).");
        }

        var purchases = new List<RefitFarmPurchaseObservation>();
        var previewDiagnostics = new List<RefitFarmPreviewObservation>();
        var echoEarned = 0;
        var pairingChecks = 2;
        for (var mapIndex = 0; mapIndex < horizonMaps; mapIndex++)
        {
            var dropsOnlyFarm = dropsOnly.FarmSiteMap(
                EndlessHeatSweepRunner.TargetSiteId,
                mapIndex);
            var dropsAndRefitFarm = dropsAndRefit.FarmSiteMap(
                EndlessHeatSweepRunner.TargetSiteId,
                mapIndex);
            if (!RefitFarmPairingVerifier.NaturalDropsEqual(
                    dropsOnlyFarm.NaturalDrops,
                    dropsAndRefitFarm.NaturalDrops))
            {
                throw new InvalidOperationException(
                    $"Refit farm natural-drop pairing failed "
                    + $"({scenario.Cell.CellId}/seed={scenario.SeedSalt}/map={mapIndex}).");
            }

            if (dropsOnlyFarm.EchoEarned != dropsAndRefitFarm.EchoEarned)
            {
                throw new InvalidOperationException(
                    $"Refit farm Echo pairing failed "
                    + $"({scenario.Cell.CellId}/seed={scenario.SeedSalt}/map={mapIndex}): "
                    + $"{dropsOnlyFarm.EchoEarned} != {dropsAndRefitFarm.EchoEarned}.");
            }

            pairingChecks += 2;
            echoEarned = checked(echoEarned + dropsAndRefitFarm.EchoEarned);
            var dropsReady = HeadlessCampaignEquipmentPowerPolicy.TryApply(
                dropsOnly,
                out _);
            var refitReady = HeadlessCampaignEquipmentPowerPolicy.TryApply(
                dropsAndRefit,
                out _);
            if (dropsReady != refitReady)
            {
                throw new InvalidOperationException(
                    $"Refit farm loadout readiness diverged between paired arms "
                    + $"({scenario.Cell.CellId}/seed={scenario.SeedSalt}/map={mapIndex}).");
            }

            if (refitReady)
            {
                var spending = RefitFarmSpendingPolicy.SpendAfterMap(
                    dropsAndRefit,
                    mapIndex,
                    service,
                    economy);
                purchases.AddRange(spending.Purchases);
                previewDiagnostics.AddRange(spending.PreviewDiagnostics);
            }
        }

        _ = HeadlessCampaignEquipmentPowerPolicy.Apply(dropsOnly);
        _ = HeadlessCampaignEquipmentPowerPolicy.Apply(dropsAndRefit);
        var dropsOnlyPower = HeadlessCampaignEffectivePower.Measure(dropsOnly);
        var dropsAndRefitPower = HeadlessCampaignEffectivePower.Measure(dropsAndRefit);
        var dropsIdentity = RefitFarmPairingVerifier.ComposeBattleIdentity(
            dropsOnly,
            scenario.Cell);
        var refitIdentity = RefitFarmPairingVerifier.ComposeBattleIdentity(
            dropsAndRefit,
            scenario.Cell);
        if (dropsIdentity.BattleSeed != refitIdentity.BattleSeed
            || !dropsIdentity.OrderedEntityIds.SequenceEqual(
                refitIdentity.OrderedEntityIds,
                StringComparer.Ordinal))
        {
            throw new InvalidOperationException(
                $"Refit farm ordered entity pairing failed "
                + $"({scenario.Cell.CellId}/seed={scenario.SeedSalt}).");
        }

        pairingChecks++;
        var refitItems = dropsAndRefit.Inventory.ToDictionary(
            item => item.InstanceId,
            StringComparer.Ordinal);
        var top20Threshold = AffixQualityProfile.ProbabilityFromFraction(4UL, 5UL);
        var top20NaturalItems = 0;
        var top20NaturalItemsChanged = 0;
        foreach (var naturalItem in dropsOnly.Inventory)
        {
            var naturalQuote = service.QuoteNextEffective(
                RefitFarmSpendingPolicy.ToRefitState(naturalItem),
                economy);
            if (naturalQuote.CurrentPercentileQ64 < top20Threshold)
            {
                continue;
            }

            top20NaturalItems++;
            if (!refitItems.TryGetValue(naturalItem.InstanceId, out var refitItem)
                || !naturalItem.AffixIds.SequenceEqual(
                    refitItem.AffixIds,
                    StringComparer.Ordinal)
                || !MagnitudeBytesEqual(naturalItem, refitItem))
            {
                top20NaturalItemsChanged++;
            }
        }

        var maximumFloorQ64 = balance.FloorScheduleQ64[^1];
        var finalRefittedItems = dropsAndRefit.Inventory
            .Where(item => item.RefitLevel > 0)
            .OrderBy(item => item.AcquisitionIndex)
            .ThenBy(item => item.InstanceId, StringComparer.Ordinal)
            .Select(item =>
            {
                var quote = service.QuoteNextEffective(
                    RefitFarmSpendingPolicy.ToRefitState(item),
                    economy);
                return new RefitFarmFinalItemObservation(
                    item.InstanceId,
                    item.RefitLevel,
                    quote.RefitMaxed && quote.CurrentPercentileQ64 >= maximumFloorQ64);
            })
            .ToArray();
        return new RefitFarmScenarioResult(
            horizonMaps,
            heat,
            scenario.SeedSalt,
            scenario.Cell.Squad.SquadId,
            initialPower.EffectivePower,
            dropsOnlyPower.EffectivePower,
            dropsAndRefitPower.EffectivePower,
            dropsOnly.Inventory.Count,
            dropsOnly.Inventory.Count(item => service.QuoteNextEffective(
                RefitFarmSpendingPolicy.ToRefitState(item),
                economy).CanPurchase),
            top20NaturalItems,
            top20NaturalItemsChanged,
            echoEarned,
            purchases,
            previewDiagnostics,
            finalRefittedItems,
            pairingChecks);
    }

    private static bool MagnitudeBytesEqual(
        HeadlessCampaignItem left,
        HeadlessCampaignItem right)
    {
        foreach (var affixId in left.AffixIds)
        {
            if (!left.AffixMagnitudes.TryGetValue(affixId, out var leftMagnitude)
                || !right.AffixMagnitudes.TryGetValue(affixId, out var rightMagnitude)
                || BitConverter.SingleToInt32Bits(leftMagnitude)
                != BitConverter.SingleToInt32Bits(rightMagnitude))
            {
                return false;
            }
        }

        return left.AffixMagnitudes.Count == right.AffixMagnitudes.Count;
    }
}
