using SM.Combat.Model;
using SM.Editor.Validation;
using SM.Meta.Model;

internal sealed class HeadlessCampaignHero
{
    internal HeadlessCampaignHero(HeroRecord record) => Record = record;

    internal HeroRecord Record { get; }
    internal string Id => Record.Id;
    internal string ArchetypeId => Record.ArchetypeId;
    internal string RaceId => Record.RaceId;
    internal string ClassId => Record.ClassId;
    internal string FlexActiveId => Record.FlexActiveId;
    internal string FlexPassiveId => Record.FlexPassiveId;
    internal int Level { get; set; } = 1;
    internal int Experience { get; set; }
    internal int CurrentHp { get; set; }
    internal int MaxHp { get; set; }
    internal List<string> EquippedItemIds { get; } = new();
    internal string PassiveBoardId { get; set; } = string.Empty;
    internal List<string> SelectedPassiveNodeIds { get; } = new();
}

internal sealed class HeadlessCampaignItem
{
    internal HeadlessCampaignItem(
        string instanceId,
        string itemBaseId,
        IReadOnlyList<string> affixIds,
        string equippedHeroId,
        int acquisitionIndex)
    {
        InstanceId = instanceId;
        ItemBaseId = itemBaseId;
        AffixIds = affixIds;
        EquippedHeroId = equippedHeroId;
        AcquisitionIndex = acquisitionIndex;
    }

    internal string InstanceId { get; }
    internal string ItemBaseId { get; }
    internal IReadOnlyList<string> AffixIds { get; }
    internal string EquippedHeroId { get; set; }
    internal int AcquisitionIndex { get; }
}

internal sealed record HeadlessCampaignBattleSetup(
    BattleLoadoutSnapshot AllySnapshot,
    ResolvedEncounterContext AuthoredEncounter);

internal sealed record HeadlessCampaignFlankSurvival(
    int TargetedShooterCount,
    int SurvivingTargetedShooterCount,
    int BacklineDiveKillCount)
{
    public double SurvivalRate => TargetedShooterCount == 0
        ? 0d
        : SurvivingTargetedShooterCount / (double)TargetedShooterCount;
}

internal sealed record HeadlessCampaignBattleOutcome(
    BattleResult Result,
    HeadlessCampaignFlankSurvival FlankSurvival,
    HeadlessCampaignAoeSurvival AntiClusterAoeSurvival,
    CampaignThreatLandingObservation? ThreatLanding,
    HeadlessCampaignBossKillDynamicsSample? BossKillDynamics);

internal sealed record HeadlessCampaignBossKillDynamicsSample(
    double DurationSeconds,
    double? BossDeathTimeSeconds,
    double BossDamageDealt,
    int PlayerDeaths,
    double? TimeTo75PercentSeconds,
    double? TimeTo50PercentSeconds,
    double? TimeTo25PercentSeconds);

internal sealed record HeadlessCampaignBossKillDynamicsArm(
    string ArmId,
    string PolicyId,
    int Samples,
    double MedianDurationSeconds,
    double? MedianBossDeathTimeSeconds,
    double MedianBossDamageDealt,
    int TotalPlayerDeaths,
    int SamplesWithPlayerDeath,
    double? MedianTimeTo75PercentSeconds,
    double? MedianTimeTo50PercentSeconds,
    double? MedianTimeTo25PercentSeconds);

internal sealed record HeadlessCampaignBossKillDynamicsBand(
    string EncounterId,
    HeadlessCampaignBossKillDynamicsArm Naive,
    HeadlessCampaignBossKillDynamicsArm Informed);

internal sealed record HeadlessCampaignAoeSurvival(
    int BossAoeCastCount,
    int AoeCatchCount,
    int ShooterAoeCatchCount,
    int ShooterCount,
    int SurvivingShooterCount)
{
    public double ShooterSurvivalRate => ShooterCount == 0
        ? 0d
        : SurvivingShooterCount / (double)ShooterCount;
}

internal sealed record HeadlessCampaignNodeObservation(
    CampaignNodeIdentity Identity,
    bool Won,
    bool AnswerTagPresent,
    string FormationHash,
    bool GearCounterUsed,
    HeadlessCampaignFlankSurvival FlankSurvival,
    HeadlessCampaignAoeSurvival AntiClusterAoeSurvival,
    CampaignThreatLandingObservation? ThreatLanding,
    int WoundsApplied,
    HeadlessCampaignBossKillDynamicsSample? BossKillDynamics);

internal sealed record HeadlessCampaignSiteObservation(
    CampaignSiteIdentity Identity,
    bool FirstVisitAndClear);

internal sealed record HeadlessCampaignSiteEntryDecisionObservation(
    bool PreviewAvailable,
    bool SetupChanged);

internal sealed record HeadlessCampaignPrepDecisionObservation(
    bool Opportunity,
    bool SetupChanged,
    int EquipmentAssignmentCount);

internal sealed record HeadlessCampaignArmExecution(
    CampaignBalanceArmSpec Arm,
    string CellId,
    string SquadId,
    IReadOnlyList<HeadlessCampaignNodeObservation> Nodes,
    IReadOnlyList<HeadlessCampaignSiteObservation> Sites,
    IReadOnlyList<HeadlessCampaignSiteEntryDecisionObservation> SiteEntryDecisions,
    IReadOnlyList<HeadlessCampaignPrepDecisionObservation> PrepDecisions,
    bool StoppedAtTarget);

internal sealed record HeadlessCampaignCellExecution(
    int CellIndex,
    string CellId,
    string SquadId,
    IReadOnlyList<HeadlessCampaignArmExecution> Arms,
    CampaignBalanceGridCell Cell);

internal sealed record HeadlessCampaignArmCount(
    string ArmId,
    string PolicyId,
    int Samples,
    int Wins,
    int AnswerTaggedWins)
{
    public double WinRate => Samples == 0 ? 0d : Wins / (double)Samples;
    public double? AnswerTagGivenWinRate => Wins == 0 ? null : AnswerTaggedWins / (double)Wins;
}

internal sealed record HeadlessCampaignSquadBand(
    string SquadId,
    HeadlessCampaignArmCount Naive,
    HeadlessCampaignArmCount Informed)
{
    public double Gap => Informed.WinRate - Naive.WinRate;
}

internal sealed record HeadlessCampaignNodeBand(
    string ChapterId,
    int ChapterOrder,
    string SiteId,
    int SiteOrder,
    string NodeId,
    int NodeOrder,
    string EncounterId,
    bool IsElite,
    bool IsBoss,
    HeadlessCampaignArmCount Naive,
    HeadlessCampaignArmCount Informed,
    IReadOnlyList<HeadlessCampaignSquadBand> ByReferenceSquad)
{
    public double Gap => Informed.WinRate - Naive.WinRate;
}

internal sealed record HeadlessCampaignCellNodeResult(
    string ArmId,
    string EncounterId,
    bool Won,
    string FormationHash,
    bool GearCounterUsed,
    HeadlessCampaignFlankSurvival FlankSurvival,
    HeadlessCampaignAoeSurvival AntiClusterAoeSurvival);

internal sealed record HeadlessCampaignSurvivalArmCount(
    string ArmId,
    string PolicyId,
    int TargetedShooters,
    int SurvivingTargetedShooters,
    int BacklineDiveKills)
{
    public double SurvivalRate => TargetedShooters == 0
        ? 0d
        : SurvivingTargetedShooters / (double)TargetedShooters;
}

internal sealed record HeadlessCampaignSquadSurvivalBand(
    string SquadId,
    HeadlessCampaignSurvivalArmCount Naive,
    HeadlessCampaignSurvivalArmCount Informed);

internal sealed record HeadlessCampaignSurvivalBand(
    string EncounterId,
    HeadlessCampaignSurvivalArmCount Naive,
    HeadlessCampaignSurvivalArmCount Informed,
    IReadOnlyList<HeadlessCampaignSquadSurvivalBand> ByReferenceSquad);

internal sealed record HeadlessCampaignAoeArmCount(
    string ArmId,
    string PolicyId,
    int Samples,
    int BossAoeCasts,
    int AoeCatches,
    int ShooterAoeCatches,
    int Shooters,
    int SurvivingShooters)
{
    public double MeanAoeCatches => Samples == 0 ? 0d : AoeCatches / (double)Samples;
    public double MeanShooterAoeCatches => Samples == 0 ? 0d : ShooterAoeCatches / (double)Samples;
    public double ShooterSurvivalRate => Shooters == 0 ? 0d : SurvivingShooters / (double)Shooters;
}

internal sealed record HeadlessCampaignSquadAoeBand(
    string SquadId,
    HeadlessCampaignAoeArmCount Naive,
    HeadlessCampaignAoeArmCount Informed);

internal sealed record HeadlessCampaignAoeBand(
    string EncounterId,
    HeadlessCampaignAoeArmCount Naive,
    HeadlessCampaignAoeArmCount Informed,
    IReadOnlyList<HeadlessCampaignSquadAoeBand> ByReferenceSquad);

internal sealed record HeadlessCampaignCellResult(
    int CellIndex,
    string CellId,
    string SquadId,
    IReadOnlyList<HeadlessCampaignCellNodeResult> Nodes);

internal sealed record HeadlessCampaignCanonicalResult(
    string SchemaVersion,
    string StopAfterEncounterId,
    IReadOnlyList<string> CellIds,
    IReadOnlyList<string> Arms,
    IReadOnlyList<HeadlessCampaignNodeBand> Nodes,
    IReadOnlyList<HeadlessCampaignCellResult> Cells);

internal sealed record HeadlessCampaignVerification(
    bool Requested,
    bool ReproducibleAcrossParallelRuns,
    bool MatchesSingleThread,
    string RepeatHash,
    string SingleThreadHash,
    double RepeatElapsedSeconds,
    double SingleThreadElapsedSeconds);

internal sealed record HeadlessCampaignWoundMeasure(
    int WonCells,
    int CellsWithWound,
    int AppliedWounds)
{
    public double OccurrenceRate => WonCells == 0 ? 0d : CellsWithWound / (double)WonCells;
}

internal sealed record HeadlessCampaignSweepReport(
    string SchemaVersion,
    int Cells,
    int BaseCells,
    IReadOnlyList<int> CampaignSeedSalts,
    int Arms,
    int DegreeOfParallelism,
    int LogicalProcessorCount,
    double ElapsedSeconds,
    string CanonicalHash,
    HeadlessCampaignCanonicalResult Result,
    HeadlessCampaignNodeBand TargetBoss,
    HeadlessCampaignSurvivalBand TargetBossSurvival,
    HeadlessCampaignAoeBand TargetBossAoeSurvival,
    IReadOnlyList<HeadlessCampaignBossKillDynamicsBand> BossKillDynamics,
    CampaignThreatLandingWitnessReport ThreatLandingWitness,
    HeadlessCampaignWoundMeasure WoundMeasure,
    HeadlessCampaignVerification Verification);
