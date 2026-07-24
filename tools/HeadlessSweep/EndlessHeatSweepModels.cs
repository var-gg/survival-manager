using SM.Combat.Model;

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
    double MeasuredSecondaryPressureFraction,
    bool ProductionPackagePresent,
    bool RulePackagePresent);

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
    IReadOnlyList<int> Histogram,
    string OrderedInventoryAndEquipHash,
    EndlessHeatConfidenceInterval MeanEquippedGradeClusteredCi95,
    EndlessHeatConfidenceInterval EpicPlusShareClusteredCi95,
    EndlessHeatConfidenceInterval LegendaryShareClusteredCi95,
    EndlessHeatPairedDelta EpicPlusShareDeltaVsHeatZero,
    EndlessHeatPairedDelta LegendaryShareDeltaVsHeatZero);

internal sealed record EndlessHeatDropAggregate(
    int Heat,
    int HorizonMaps,
    int SeedClusters,
    int ScenarioClusters,
    int GradeRolls,
    int OrdinaryComponentSelections,
    int JackpotComponentSelections,
    double ObservedJackpotFrequency,
    double ExpectedJackpotFrequency,
    IReadOnlyList<int> GradeHistogram,
    double EpicPlusSharePerDrop,
    double LegendarySharePerDrop,
    double ExpectedEpicPlusProbabilityPerDrop,
    double ExpectedLegendaryProbabilityPerDrop,
    EndlessHeatConfidenceInterval JackpotFrequencyClusteredCi95,
    EndlessHeatConfidenceInterval EpicPlusSharePerDropClusteredCi95,
    EndlessHeatConfidenceInterval LegendarySharePerDropClusteredCi95,
    EndlessHeatPairedDelta EpicPlusSharePerDropDeltaVsHeatZero,
    EndlessHeatPairedDelta LegendarySharePerDropDeltaVsHeatZero);

internal sealed record EndlessHeatAcquisitionAggregate(
    int Heat,
    int HorizonMaps,
    double PairedClearRate,
    double EpicPlusPerSuccessfulMap,
    double LegendaryPerSuccessfulMap,
    double EpicPlusPerAttemptedMap,
    double LegendaryPerAttemptedMap,
    EndlessHeatConfidenceInterval EpicPlusPerSuccessfulMapClusteredCi95,
    EndlessHeatConfidenceInterval LegendaryPerSuccessfulMapClusteredCi95,
    EndlessHeatConfidenceInterval PairedClearRateClusteredCi95);

internal sealed record EndlessHeatConfidenceInterval(
    double Lower,
    double Upper);

internal sealed record EndlessHeatPairedDelta(
    double Estimate,
    EndlessHeatConfidenceInterval ClusteredCi95);

internal sealed record EndlessHeatClearRateAggregate(
    int Heat,
    int GearHorizonMaps,
    int Wins,
    int Samples,
    int SeedsPerCell,
    double WinRate,
    IReadOnlyList<EndlessHeatCellClearRate> Cells,
    EndlessHeatConfidenceInterval SeedClusteredCi95);

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

internal sealed record EndlessHeatTargetPressureAggregate(
    string TargetId,
    string Anchor,
    int BattlesPresent,
    int Deaths,
    long PrimaryDamageAfterMitigationRaw,
    long SecondaryRawAllocated,
    long SecondaryDamageAfterMitigationRaw);

internal sealed record EndlessHeatMeasuredBattle(
    bool Won,
    int BattleSeed,
    IReadOnlyList<string> EntityIds,
    BattleResult Result,
    SecondaryPressureTelemetrySnapshot SecondaryPressureTelemetry);

internal sealed record EndlessHeatDifficultyAggregate(
    int Heat,
    int GearHorizonMaps,
    int Wins,
    int Samples,
    double WinRate,
    double MeanDurationSeconds,
    IReadOnlyList<EndlessHeatCellClearRate> Cells,
    long NormalizedDamageBudgetRaw,
    long PrimaryRawBudgetRaw,
    long PrimaryDamageAfterMitigationRaw,
    long SecondaryRawAllocated,
    long SecondaryDamageAfterMitigationRaw,
    double PrimaryRawBudgetRatio,
    double SecondaryRawBudgetRatio,
    double SecondaryShareOfRawOutput,
    IReadOnlyList<EndlessHeatTargetPressureAggregate> TargetPressure,
    string EnemyModifierPackageHash,
    string SecondaryAllocationHash,
    string FinalBattleOutcomeHash);

internal sealed record EndlessHeatCompositionGamma(
    string SquadId,
    double Gamma);

internal sealed record EndlessHeatNeutralityFit(
    string Method,
    bool Converged,
    int Iterations,
    IReadOnlyList<EndlessHeatCompositionGamma> GammaByComposition,
    double MaxGammaSpread,
    double H0ToH3LossSpreadPoints);

internal sealed record EndlessHeatValidationCell(
    string Description,
    string FrontlineLoadout,
    string MixedLoadout,
    string RangedLoadout,
    bool ProductionHeatZeroUnchanged,
    bool AllHeatZeroRatesWithinTwentyToNinety);

internal sealed record EndlessHeatDifficultyReport(
    EndlessHeatValidationCell ValidationCell,
    IReadOnlyList<EndlessHeatDifficultyAggregate> RepresentativeFixedGear,
    IReadOnlyList<EndlessHeatDifficultyAggregate> NonCeilingFixedGear,
    EndlessHeatNeutralityFit NeutralityFit);

internal sealed record EndlessHeatSweepReport(
    string SchemaVersion,
    string TargetEncounterId,
    string TargetSiteId,
    IReadOnlyList<string> ReferenceSquads,
    int SeedsPerCell,
    int AggregateSamplesPerHeat,
    double MinimumResolvableRatePerCell,
    double MinimumResolvableAggregateRate,
    double MinimumResolvableEquippedShare,
    int BootstrapSeedClusters,
    int BootstrapReplicates,
    string BootstrapMethod,
    IReadOnlyList<int> EquipmentHeats,
    IReadOnlyList<int> ClearRateHeats,
    IReadOnlyList<int> EquipmentHorizonsMaps,
    int PairedClearRateHorizonMaps,
    int BattleRewardNodesPerFarmMap,
    string EquipmentPowerPolicy,
    IReadOnlyList<EndlessHeatEnemyScalingObservation> EnemyScaling,
    IReadOnlyList<EndlessHeatEquippedAggregate> EquippedByHeat,
    IReadOnlyList<EndlessHeatDropAggregate> DropsByHeat,
    IReadOnlyList<EndlessHeatAcquisitionAggregate> AcquisitionByHeat,
    IReadOnlyList<EndlessHeatClearRateAggregate> ClearRateFixedGear,
    IReadOnlyList<EndlessHeatClearRateAggregate> ClearRatePairedGear,
    EndlessHeatPairingVerification Pairing,
    string ClearRateCodePath,
    EndlessHeatDifficultyReport Difficulty,
    string CanonicalHash);
