internal sealed record RefitFarmPowerObservation(
    double Health,
    double Offense,
    double EffectivePower,
    double LogPower);

internal sealed record RefitFarmPurchaseObservation(
    int MapIndex,
    string ItemInstanceId,
    int TargetRefitLevel,
    int EchoCost,
    int ScoreBeforeQ,
    int ScoreAfterQ,
    double PowerDeltaLog,
    double CdfOvershoot);

internal sealed record RefitFarmPreviewObservation(
    string TransitionKey,
    double PowerDeltaLog,
    bool BudgetScoreIncreased,
    bool ReducedActualPower);

internal sealed record RefitFarmFinalItemObservation(
    string ItemInstanceId,
    int FinalRefitLevel,
    bool ReachedMaximumFloor);

internal sealed record RefitFarmScenarioResult(
    int HorizonMaps,
    int Heat,
    int SeedSalt,
    string SquadId,
    double InitialPower,
    double DropsOnlyPower,
    double DropsAndRefitPower,
    int TotalItems,
    int EligibleItems,
    int Top20NaturalItems,
    int Top20NaturalItemsChanged,
    int EchoEarned,
    IReadOnlyList<RefitFarmPurchaseObservation> Purchases,
    IReadOnlyList<RefitFarmPreviewObservation> PreviewDiagnostics,
    IReadOnlyList<RefitFarmFinalItemObservation> FinalRefittedItems,
    int PairingChecks);

internal sealed record RefitFarmActivationResult(
    int Horizon,
    double RefitPurchasesPerSeed,
    double ItemsEligiblePct,
    bool EnoughActivityForTailMetrics);

internal sealed record RefitFarmTailMetricResult(
    string Slice,
    double? ALow,
    IReadOnlyList<double?> ALowCi95,
    bool InBand050065,
    double? TTop,
    IReadOnlyList<double?> TTopCi95,
    bool Meets080,
    bool VerdictResolved);

internal sealed record RefitFarmChannelShareResult(
    double GD,
    double GR,
    double SR,
    bool InBand025035,
    string Note);

internal sealed record RefitFarmLevelDistribution(
    int Level,
    int ItemCount,
    double Pct);

internal sealed record RefitFarmEchoHorizon(
    int Horizon,
    int TotalEchoSpent,
    double MeanEchoSpentPerSeed,
    double MeanEchoSpentPerRefittedItem);

internal sealed record RefitFarmCdfOvershootLevel(
    int Level,
    int Operations,
    double Mean,
    double Maximum);

internal sealed record RefitFarmCdfOvershootSummary(
    int Operations,
    double Mean,
    double Maximum,
    IReadOnlyList<RefitFarmCdfOvershootLevel> ByLevel);

internal sealed record RefitFarmDiagnosticsResult(
    IReadOnlyList<RefitFarmLevelDistribution> RefitLevelDistribution,
    double EchoPerItem,
    IReadOnlyList<RefitFarmEchoHorizon> EchoPerHorizon,
    double PctReaching70thCap,
    double PctTop20NaturalItemsChanged,
    RefitFarmCdfOvershootSummary CdfOvershoot,
    double PctRefitsReducingRealPower,
    int DistinctPreviewOperations,
    double PctPurchasedRefitsReducingRealPower);

internal sealed record RefitFarmPairingResult(
    bool Verified,
    int InitialSaveChecks,
    int SeedChecks,
    int DropChecks,
    int EchoRewardChecks,
    int EntityIdChecks,
    string HowVerified);

internal sealed record RefitFarmProfileReport(
    string SchemaVersion,
    int SeedsPerCell,
    IReadOnlyList<string> Squads,
    IReadOnlyList<int> Horizons,
    IReadOnlyDictionary<int, int> HeatByHorizon,
    int BootstrapReplicates,
    string SpendingPolicy,
    RefitFarmPairingResult Pairing,
    IReadOnlyList<RefitFarmActivationResult> Activation,
    IReadOnlyList<RefitFarmTailMetricResult> TailMetrics,
    RefitFarmChannelShareResult ChannelShare,
    RefitFarmDiagnosticsResult Diagnostics,
    IReadOnlyList<string> ResolutionNotes,
    string CanonicalHash);
