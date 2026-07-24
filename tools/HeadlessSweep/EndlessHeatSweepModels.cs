internal sealed record HeadlessEquippedSlotObservation(
    string HeroId,
    string SlotType,
    string ItemInstanceId,
    string ItemBaseId,
    int Grade,
    int AffixPowerScoreQ);

internal sealed record HeadlessEquippedLoadoutObservation(
    IReadOnlyList<HeadlessEquippedSlotObservation> Slots,
    double MeanGrade,
    double EpicPlusShare,
    double LegendaryShare,
    IReadOnlyList<int> Histogram);

internal sealed record EndlessHeatEnemyScalingObservation(
    int Heat,
    double MeasuredHpMultiplier,
    double MeasuredPhysPowerMultiplier,
    double MeasuredMagPowerMultiplier,
    bool ProductionPackagePresent);

internal sealed record EndlessHeatEquippedAggregate(
    int Heat,
    int HorizonMaps,
    int SeedsPerCell,
    int Cells,
    int EquippedSlots,
    int ItemDrops,
    double MeanEquippedGrade,
    double EpicPlusShare,
    double LegendaryShare,
    IReadOnlyList<int> Histogram);

internal sealed record EndlessHeatClearRateAggregate(
    int Heat,
    int Wins,
    int Samples,
    int SeedsPerCell,
    double WinRate,
    IReadOnlyList<EndlessHeatCellClearRate> Cells);

internal sealed record EndlessHeatCellClearRate(
    string SquadId,
    int Wins,
    int Samples,
    double WinRate);

internal sealed record EndlessHeatPairingVerification(
    bool SeedsShared,
    bool EntityIdsShared,
    int PairsChecked,
    string Method);

internal sealed record EndlessHeatSweepReport(
    string SchemaVersion,
    string TargetEncounterId,
    string TargetSiteId,
    IReadOnlyList<string> ReferenceSquads,
    int SeedsPerCell,
    int AggregateSamplesPerHeat,
    double MinimumResolvableRatePerCell,
    double MinimumResolvableAggregateRate,
    IReadOnlyList<int> EquipmentHeats,
    IReadOnlyList<int> ClearRateHeats,
    IReadOnlyList<int> EquipmentHorizonsMaps,
    int PairedClearRateHorizonMaps,
    int BattleRewardNodesPerFarmMap,
    string EquipmentPowerPolicy,
    IReadOnlyList<EndlessHeatEnemyScalingObservation> EnemyScaling,
    IReadOnlyList<EndlessHeatEquippedAggregate> EquippedByHeat,
    IReadOnlyList<EndlessHeatClearRateAggregate> ClearRateFixedGear,
    IReadOnlyList<EndlessHeatClearRateAggregate> ClearRatePairedGear,
    EndlessHeatPairingVerification Pairing,
    string ClearRateCodePath,
    string CanonicalHash);
