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
    HeadlessCampaignFlankSurvival FlankSurvival);

internal sealed record HeadlessCampaignNodeObservation(
    CampaignNodeIdentity Identity,
    bool Won,
    bool AnswerTagPresent,
    string FormationHash,
    bool GearCounterUsed,
    HeadlessCampaignFlankSurvival FlankSurvival);

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
    IReadOnlyList<HeadlessCampaignArmExecution> Arms);

internal sealed record HeadlessCampaignArmCount(
    string ArmId,
    string PolicyId,
    int Samples,
    int Wins)
{
    public double WinRate => Samples == 0 ? 0d : Wins / (double)Samples;
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
    HeadlessCampaignFlankSurvival FlankSurvival);

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

internal sealed record HeadlessCampaignSweepReport(
    string SchemaVersion,
    int Cells,
    int Arms,
    int DegreeOfParallelism,
    int LogicalProcessorCount,
    double ElapsedSeconds,
    string CanonicalHash,
    HeadlessCampaignCanonicalResult Result,
    HeadlessCampaignNodeBand TargetBoss,
    HeadlessCampaignSurvivalBand TargetBossSurvival,
    HeadlessCampaignVerification Verification);
