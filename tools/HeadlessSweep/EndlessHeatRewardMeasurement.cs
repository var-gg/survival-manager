using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using SM.Core.Content;
using SM.Editor.Validation;
using SM.Meta.Services;

internal sealed record EndlessHeatPreparedScenario(
    CampaignBalanceGridCell Cell,
    int SeedSalt,
    HeadlessCampaignState State);

internal sealed record EndlessHeatRewardScenarioResult(
    int Heat,
    int HorizonMaps,
    int ScenarioIndex,
    int SeedSalt,
    string SquadId,
    HeadlessEquippedLoadoutObservation Loadout,
    HeadlessCampaignFarmResult Farm,
    string OrderedInventoryAndEquipHash);

internal sealed record EndlessHeatRewardMeasurementResult(
    IReadOnlyList<EndlessHeatEquippedAggregate> Equipped,
    IReadOnlyList<EndlessHeatDropAggregate> Drops,
    IReadOnlyList<EndlessHeatRewardScenarioResult> Scenarios,
    int BattleRewardNodesPerMap);

internal static class EndlessHeatRewardMeasurement
{
    internal static readonly int[] Heats = { 0, 1, 2, 3, 4, 5 };

    internal static EndlessHeatRewardMeasurementResult Measure(
        IReadOnlyList<EndlessHeatPreparedScenario> prepared,
        string targetSiteId,
        IReadOnlyList<int> horizons,
        int degree)
        => Measure(
            prepared,
            targetSiteId,
            horizons,
            Heats,
            degree,
            EndlessCycleService.HeatDropLatentMeanNumerator,
            EndlessCycleService.HeatDropJackpotWeightStep);

    internal static EndlessHeatRewardMeasurementResult Measure(
        IReadOnlyList<EndlessHeatPreparedScenario> prepared,
        string targetSiteId,
        IReadOnlyList<int> horizons,
        IReadOnlyList<int> heats,
        int degree,
        double heatDropLatentMeanNumerator,
        double heatDropJackpotWeightStep)
    {
        if (!heats.Contains(0))
        {
            throw new ArgumentException(
                "Reward measurement heats must include H0 for paired deltas.",
                nameof(heats));
        }

        var scenarios = new List<EndlessHeatRewardScenarioResult>(
            prepared.Count * horizons.Count * heats.Count);
        int? observedNodesPerMap = null;
        foreach (var horizon in horizons)
        {
            foreach (var heat in heats)
            {
                var results = new EndlessHeatRewardScenarioResult[prepared.Count];
                Parallel.ForEach(
                    Enumerable.Range(0, prepared.Count),
                    new ParallelOptions { MaxDegreeOfParallelism = degree },
                    index =>
                    {
                        var scenario = prepared[index];
                        var state = scenario.State.CloneWithHeat(heat);
                        var farm = state.FarmSiteMaps(
                            targetSiteId,
                            horizon,
                            heatDropLatentMeanNumerator,
                            heatDropJackpotWeightStep);
                        var loadout = HeadlessCampaignEquipmentPowerPolicy.Apply(state);
                        results[index] = new EndlessHeatRewardScenarioResult(
                            heat,
                            horizon,
                            index,
                            scenario.SeedSalt,
                            scenario.Cell.Squad.SquadId,
                            loadout,
                            farm,
                            HashEquipmentScenario(state, loadout));
                    });

                foreach (var result in results)
                {
                    if (observedNodesPerMap.HasValue
                        && observedNodesPerMap.Value != result.Farm.BattleRewardNodesPerMap)
                    {
                        throw new InvalidDataException(
                            "Endless farm maps disagreed on the number of battle reward nodes.");
                    }

                    observedNodesPerMap = result.Farm.BattleRewardNodesPerMap;
                }

                scenarios.AddRange(results);
            }
        }

        var equipped = new List<EndlessHeatEquippedAggregate>();
        var drops = new List<EndlessHeatDropAggregate>();
        foreach (var horizon in horizons)
        {
            var baseline = SelectScenarios(scenarios, heat: 0, horizon);
            foreach (var heat in heats)
            {
                var current = SelectScenarios(scenarios, heat, horizon);
                equipped.Add(BuildEquippedAggregate(current, baseline));
                drops.Add(BuildDropAggregate(current, baseline));
            }
        }

        return new EndlessHeatRewardMeasurementResult(
            equipped
                .OrderBy(value => value.Heat)
                .ThenBy(value => value.HorizonMaps)
                .ToArray(),
            drops
                .OrderBy(value => value.Heat)
                .ThenBy(value => value.HorizonMaps)
                .ToArray(),
            scenarios,
            observedNodesPerMap
            ?? throw new InvalidDataException("No endless farm maps were measured."));
    }

    internal static IReadOnlyList<EndlessHeatAcquisitionAggregate> BuildAcquisition(
        IReadOnlyList<EndlessHeatRewardScenarioResult> scenarios,
        IReadOnlyList<EndlessHeatClearRateAggregate> pairedClearRates)
    {
        var result = new List<EndlessHeatAcquisitionAggregate>();
        foreach (var group in scenarios
                     .GroupBy(value => (value.Heat, value.HorizonMaps))
                     .OrderBy(value => value.Key.Heat)
                     .ThenBy(value => value.Key.HorizonMaps))
        {
            var clearRate = pairedClearRates.Single(value =>
                value.Heat == group.Key.Heat
                && value.GearHorizonMaps == group.Key.HorizonMaps);
            var current = group.ToArray();
            var epicClusters = BuildDropRatioClusters(
                current,
                observation => observation.Grade >= ItemRarityTierValue.Epic ? 1d : 0d,
                denominatorPerScenario: group.Key.HorizonMaps);
            var legendaryClusters = BuildDropRatioClusters(
                current,
                observation => observation.Grade == ItemRarityTierValue.Legendary ? 1d : 0d,
                denominatorPerScenario: group.Key.HorizonMaps);
            var epicPerSuccessfulMap = EndlessHeatSeedClusteredBootstrap.Ratio(
                epicClusters.Sum(value => value.Numerator),
                epicClusters.Sum(value => value.Denominator));
            var legendaryPerSuccessfulMap = EndlessHeatSeedClusteredBootstrap.Ratio(
                legendaryClusters.Sum(value => value.Numerator),
                legendaryClusters.Sum(value => value.Denominator));
            result.Add(new EndlessHeatAcquisitionAggregate(
                group.Key.Heat,
                group.Key.HorizonMaps,
                clearRate.WinRate,
                epicPerSuccessfulMap,
                legendaryPerSuccessfulMap,
                epicPerSuccessfulMap * clearRate.WinRate,
                legendaryPerSuccessfulMap * clearRate.WinRate,
                EndlessHeatSeedClusteredBootstrap.EstimateRatio(
                    epicClusters,
                    $"acquisition|epic|h={group.Key.Heat}|maps={group.Key.HorizonMaps}"),
                EndlessHeatSeedClusteredBootstrap.EstimateRatio(
                    legendaryClusters,
                    $"acquisition|legendary|h={group.Key.Heat}|maps={group.Key.HorizonMaps}"),
                clearRate.SeedClusteredCi95));
        }

        return result;
    }

    private static EndlessHeatEquippedAggregate BuildEquippedAggregate(
        IReadOnlyList<EndlessHeatRewardScenarioResult> current,
        IReadOnlyList<EndlessHeatRewardScenarioResult> baseline)
    {
        RequireAligned(current, baseline);
        var slots = current.SelectMany(value => value.Loadout.Slots).ToArray();
        var histogram = Enumerable.Range(0, 5)
            .Select(grade => slots.Count(slot => slot.Grade == grade))
            .ToArray();
        var meanClusters = BuildMeanClusters(current, value => value.Loadout.MeanGrade);
        var epicClusters = BuildMeanClusters(current, value => value.Loadout.EpicPlusShare);
        var legendaryClusters = BuildMeanClusters(current, value => value.Loadout.LegendaryShare);
        var baselineEpicClusters = BuildMeanClusters(baseline, value => value.Loadout.EpicPlusShare);
        var baselineLegendaryClusters = BuildMeanClusters(baseline, value => value.Loadout.LegendaryShare);
        var heat = current[0].Heat;
        var horizon = current[0].HorizonMaps;
        return new EndlessHeatEquippedAggregate(
            heat,
            horizon,
            SeedsPerCell: current.Count / 3,
            Cells: 3,
            EquippedSlots: slots.Length,
            ItemDrops: current.Sum(value => value.Farm.ItemDrops),
            MeanEquippedGrade: slots.Average(slot => slot.Grade),
            EpicPlusShare: slots.Count(slot => slot.Grade >= 3) / (double)slots.Length,
            LegendaryShare: slots.Count(slot => slot.Grade == 4) / (double)slots.Length,
            Histogram: histogram,
            OrderedInventoryAndEquipHash: HashStrings(
                current.Select(value => value.OrderedInventoryAndEquipHash)),
            MeanEquippedGradeClusteredCi95: EndlessHeatSeedClusteredBootstrap.EstimateMean(
                meanClusters,
                $"equipped|mean|h={heat}|maps={horizon}"),
            EpicPlusShareClusteredCi95: EndlessHeatSeedClusteredBootstrap.EstimateMean(
                epicClusters,
                $"equipped|epic|h={heat}|maps={horizon}"),
            LegendaryShareClusteredCi95: EndlessHeatSeedClusteredBootstrap.EstimateMean(
                legendaryClusters,
                $"equipped|legendary|h={heat}|maps={horizon}"),
            EpicPlusShareDeltaVsHeatZero: EndlessHeatSeedClusteredBootstrap.PairedMeanDelta(
                epicClusters,
                baselineEpicClusters,
                $"equipped|epic-delta|h={heat}|maps={horizon}"),
            LegendaryShareDeltaVsHeatZero: EndlessHeatSeedClusteredBootstrap.PairedMeanDelta(
                legendaryClusters,
                baselineLegendaryClusters,
                $"equipped|legendary-delta|h={heat}|maps={horizon}"));
    }

    private static EndlessHeatDropAggregate BuildDropAggregate(
        IReadOnlyList<EndlessHeatRewardScenarioResult> current,
        IReadOnlyList<EndlessHeatRewardScenarioResult> baseline)
    {
        RequireAligned(current, baseline);
        var rolls = current.SelectMany(value => value.Farm.GradeRolls).ToArray();
        if (rolls.Length == 0)
        {
            throw new InvalidDataException("Endless reward measurement observed no item-grade rolls.");
        }

        var histogram = Enumerable.Range(0, 5)
            .Select(grade => rolls.Count(value => (int)value.Grade == grade))
            .ToArray();
        var jackpotClusters = BuildDropRatioClusters(
            current,
            observation => observation.JackpotComponentSelected ? 1d : 0d);
        var epicClusters = BuildDropRatioClusters(
            current,
            observation => observation.Grade >= ItemRarityTierValue.Epic ? 1d : 0d);
        var legendaryClusters = BuildDropRatioClusters(
            current,
            observation => observation.Grade == ItemRarityTierValue.Legendary ? 1d : 0d);
        var baselineEpicClusters = BuildDropRatioClusters(
            baseline,
            observation => observation.Grade >= ItemRarityTierValue.Epic ? 1d : 0d);
        var baselineLegendaryClusters = BuildDropRatioClusters(
            baseline,
            observation => observation.Grade == ItemRarityTierValue.Legendary ? 1d : 0d);
        var heat = current[0].Heat;
        var horizon = current[0].HorizonMaps;
        return new EndlessHeatDropAggregate(
            heat,
            horizon,
            SeedClusters: current.Select(value => value.SeedSalt).Distinct().Count(),
            ScenarioClusters: current.Count,
            GradeRolls: rolls.Length,
            OrdinaryComponentSelections: rolls.Count(value => !value.JackpotComponentSelected),
            JackpotComponentSelections: rolls.Count(value => value.JackpotComponentSelected),
            ObservedJackpotFrequency: rolls.Count(value => value.JackpotComponentSelected)
                                      / (double)rolls.Length,
            ExpectedJackpotFrequency: rolls.Average(value => value.EffectiveJackpotWeight),
            GradeHistogram: histogram,
            EpicPlusSharePerDrop: rolls.Count(value => value.Grade >= ItemRarityTierValue.Epic)
                                  / (double)rolls.Length,
            LegendarySharePerDrop: rolls.Count(value => value.Grade == ItemRarityTierValue.Legendary)
                                   / (double)rolls.Length,
            ExpectedEpicPlusProbabilityPerDrop: rolls.Average(value =>
                value.GradeProbabilities[(int)ItemRarityTierValue.Epic]
                + value.GradeProbabilities[(int)ItemRarityTierValue.Legendary]),
            ExpectedLegendaryProbabilityPerDrop: rolls.Average(value =>
                value.GradeProbabilities[(int)ItemRarityTierValue.Legendary]),
            JackpotFrequencyClusteredCi95: EndlessHeatSeedClusteredBootstrap.EstimateRatio(
                jackpotClusters,
                $"drop|jackpot|h={heat}|maps={horizon}"),
            EpicPlusSharePerDropClusteredCi95: EndlessHeatSeedClusteredBootstrap.EstimateRatio(
                epicClusters,
                $"drop|epic|h={heat}|maps={horizon}"),
            LegendarySharePerDropClusteredCi95: EndlessHeatSeedClusteredBootstrap.EstimateRatio(
                legendaryClusters,
                $"drop|legendary|h={heat}|maps={horizon}"),
            EpicPlusSharePerDropDeltaVsHeatZero: EndlessHeatSeedClusteredBootstrap.PairedRatioDelta(
                epicClusters,
                baselineEpicClusters,
                $"drop|epic-delta|h={heat}|maps={horizon}"),
            LegendarySharePerDropDeltaVsHeatZero: EndlessHeatSeedClusteredBootstrap.PairedRatioDelta(
                legendaryClusters,
                baselineLegendaryClusters,
                $"drop|legendary-delta|h={heat}|maps={horizon}"));
    }

    private static IReadOnlyList<EndlessHeatRewardScenarioResult> SelectScenarios(
        IEnumerable<EndlessHeatRewardScenarioResult> scenarios,
        int heat,
        int horizon)
        => scenarios
            .Where(value => value.Heat == heat && value.HorizonMaps == horizon)
            .OrderBy(value => value.ScenarioIndex)
            .ToArray();

    private static IReadOnlyList<EndlessHeatMeanCluster> BuildMeanClusters(
        IEnumerable<EndlessHeatRewardScenarioResult> scenarios,
        Func<EndlessHeatRewardScenarioResult, double> selector)
        => scenarios
            .GroupBy(value => value.SeedSalt)
            .OrderBy(group => group.Key)
            .Select(group => new EndlessHeatMeanCluster(
                group.Key,
                group.Average(selector)))
            .ToArray();

    private static IReadOnlyList<EndlessHeatRatioCluster> BuildDropRatioClusters(
        IEnumerable<EndlessHeatRewardScenarioResult> scenarios,
        Func<DropGradeRollObservation, double> numerator,
        int? denominatorPerScenario = null)
        => scenarios
            .GroupBy(value => value.SeedSalt)
            .OrderBy(group => group.Key)
            .Select(group => new EndlessHeatRatioCluster(
                group.Key,
                group.SelectMany(value => value.Farm.GradeRolls).Sum(numerator),
                denominatorPerScenario.HasValue
                    ? group.Count() * denominatorPerScenario.Value
                    : group.Sum(value => value.Farm.GradeRolls.Count)))
            .ToArray();

    private static string HashEquipmentScenario(
        HeadlessCampaignState state,
        HeadlessEquippedLoadoutObservation loadout)
    {
        var lines = state.Inventory.Select(item =>
                string.Join(
                    "|",
                    "inventory",
                    item.AcquisitionIndex.ToString(CultureInfo.InvariantCulture),
                    item.InstanceId,
                    item.ItemBaseId,
                    ((int)item.RarityTier).ToString(CultureInfo.InvariantCulture),
                    string.Join(",", item.AffixIds),
                    item.EquippedHeroId))
            .Concat(loadout.Slots.Select(slot =>
                string.Join(
                    "|",
                    "equip",
                    slot.HeroId,
                    slot.SlotType,
                    slot.ItemInstanceId,
                    slot.ItemBaseId,
                    slot.Grade.ToString(CultureInfo.InvariantCulture),
                    slot.AffixPowerScoreQ.ToString(CultureInfo.InvariantCulture))));
        return HashStrings(lines);
    }

    private static string HashStrings(IEnumerable<string> values)
    {
        var canonical = string.Join("\n", values);
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)))
            .ToLowerInvariant();
    }

    private static void RequireAligned(
        IReadOnlyList<EndlessHeatRewardScenarioResult> current,
        IReadOnlyList<EndlessHeatRewardScenarioResult> baseline)
    {
        if (current.Count == 0 || current.Count != baseline.Count)
        {
            throw new InvalidDataException(
                $"Reward measurement pairing count mismatch: {current.Count} vs {baseline.Count}.");
        }

        var currentKeys = current.Select(value => (value.SeedSalt, value.SquadId));
        var baselineKeys = baseline.Select(value => (value.SeedSalt, value.SquadId));
        if (!currentKeys.SequenceEqual(baselineKeys))
        {
            throw new InvalidDataException(
                "Reward measurement requires identical ordered seed/squad vectors.");
        }
    }
}
