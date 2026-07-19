using System;
using System.Collections.Generic;
using System.Linq;
using SM.HeadlessPolicies;

namespace SM.Editor.Validation;

/// <summary>
/// 캠페인 난이도 곡선 Phase A 측정 사양. 런타임 콘텐츠가 아니라 Editor 검증 하네스의 버전된 입력이다.
/// gpt-pro 정량 사양 v1의 480-cell grid, 두 arm binding, 승률 band와 guardrail을 한 곳에 고정한다.
/// </summary>
public sealed record CampaignBalanceSweepConfig(
    string SchemaVersion,
    IReadOnlyList<CampaignBalanceArmSpec> Arms,
    IReadOnlyList<CampaignReferenceSquadSpec> ReferenceSquads,
    IReadOnlyList<CampaignBuildPowerQuantileSpec> BuildPowerQuantiles,
    IReadOnlyList<CampaignEnemyCompositionVariantSpec> EnemyCompositionVariants,
    IReadOnlyList<CampaignRosterCoverageSpec> RosterCoverageVariants,
    IReadOnlyList<CampaignNonBossBandSpec> NonBossBands,
    IReadOnlyList<CampaignBossBandSpec> BossBands,
    CampaignBalanceGuardrails Guardrails,
    int MinimumEffectiveSamplesPerArmPerNode,
    double MaximumWilsonHalfWidth,
    int ProgressionRetrySeedCount,
    int SiteSafety,
    int? SamplingCap = null,
    string SamplingCapReason = "")
{
    public const string CurrentSchemaVersion = "campaign-balance-sweep-v1";

    public static CampaignBalanceSweepConfig Default { get; } = CreateDefault();

    public int FullGridCellCount => ReferenceSquads.Count
                                    * BuildPowerQuantiles.Count
                                    * EnemyCompositionVariants.Count
                                    * RosterCoverageVariants.Count;

    public int EffectiveGridCellCount => SamplingCap is { } cap
        ? Math.Min(cap, FullGridCellCount)
        : FullGridCellCount;

    public IReadOnlyList<CampaignBalanceGridCell> BuildGrid()
    {
        Validate();
        var cells = new List<CampaignBalanceGridCell>(FullGridCellCount);
        foreach (var squad in ReferenceSquads)
        foreach (var build in BuildPowerQuantiles)
        foreach (var enemy in EnemyCompositionVariants)
        foreach (var coverage in RosterCoverageVariants)
        {
            cells.Add(new CampaignBalanceGridCell(squad, build, enemy, coverage));
        }

        return SamplingCap is { } cap ? cells.Take(cap).ToArray() : cells;
    }

    public void Validate()
    {
        if (!string.Equals(SchemaVersion, CurrentSchemaVersion, StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"Unsupported campaign balance sweep schema '{SchemaVersion}'.");
        }

        if (Arms.Count != 2
            || Arms.Select(arm => arm.ArmId).Distinct(StringComparer.Ordinal).Count() != 2
            || Arms.Select(arm => arm.PolicyId).Distinct(StringComparer.Ordinal).Count() != 2)
        {
            throw new InvalidOperationException("Campaign balance sweep requires two distinct arm ids and policy bindings.");
        }

        if (ReferenceSquads.Count != 3
            || BuildPowerQuantiles.Count != 5
            || EnemyCompositionVariants.Count != 8
            || RosterCoverageVariants.Count != 4)
        {
            throw new InvalidOperationException("Campaign balance sweep canonical grid must be 3 squads x 5 builds x 8 enemies x 4 coverage variants.");
        }

        if (FullGridCellCount != 480)
        {
            throw new InvalidOperationException($"Campaign balance sweep canonical grid must contain 480 cells, found {FullGridCellCount}.");
        }

        if (EffectiveGridCellCount < MinimumEffectiveSamplesPerArmPerNode)
        {
            throw new InvalidOperationException(
                $"Sampling cap leaves {EffectiveGridCellCount} cells; at least {MinimumEffectiveSamplesPerArmPerNode} are required per arm/node.");
        }

        if (SamplingCap.HasValue && string.IsNullOrWhiteSpace(SamplingCapReason))
        {
            throw new InvalidOperationException("A sampling cap must be accompanied by a reportable reason.");
        }

        foreach (var arm in Arms)
        {
            _ = HeadlessPolicyFactory.NormalizePolicyId(arm.PolicyId);
        }
    }

    public CampaignNonBossBandSpec NonBossBand(int chapterOrder)
        => NonBossBands.Single(band => band.ChapterOrder == chapterOrder);

    public CampaignBossBandSpec BossBand(int chapterOrder, int siteOrder)
        => BossBands.Single(band => band.ChapterOrder == chapterOrder && band.SiteOrder == siteOrder);

    private static CampaignBalanceSweepConfig CreateDefault()
    {
        var arms = new[]
        {
            new CampaignBalanceArmSpec(
                "naive",
                HeadlessPolicyFactory.GreedyId,
                UsesForcedPreview: false,
                "Fixed greedy deployment; ignores preview and preserves the prior formation after initial setup."),
            new CampaignBalanceArmSpec(
                "informed",
                HeadlessPolicyFactory.PreviewGroundedConceptId,
                UsesForcedPreview: true,
                "Phase A site-entry preview-grounded setup; Phase B mid-site prep transaction is not available."),
        };

        var squads = new[]
        {
            new CampaignReferenceSquadSpec(
                "frontline",
                new[] { "warden", "guardian", "slayer", "raider" },
                new[] { "marksman", "hunter", "priest", "shaman" }),
            new CampaignReferenceSquadSpec(
                "ranged",
                new[] { "warden", "marksman", "hunter", "scout" },
                new[] { "guardian", "slayer", "priest", "shaman" }),
            new CampaignReferenceSquadSpec(
                "mixed",
                new[] { "warden", "guardian", "marksman", "hunter" },
                new[] { "raider", "scout", "priest", "shaman" }),
        };

        // Phase A에는 라이브 EBP 분포가 아직 없다. 기존 3-slot 장비와 passive budget 경로만 사용해
        // 동일 콘텐츠에서 재현 가능한 ordinal cohort를 만든다(수치 stat multiplier는 발명하지 않는다).
        var builds = new[]
        {
            new CampaignBuildPowerQuantileSpec("P20", EquipmentSlotsPerHero: 0, GrowAvailablePassives: false),
            new CampaignBuildPowerQuantileSpec("P35", EquipmentSlotsPerHero: 1, GrowAvailablePassives: false),
            new CampaignBuildPowerQuantileSpec("P50", EquipmentSlotsPerHero: 2, GrowAvailablePassives: false),
            new CampaignBuildPowerQuantileSpec("P65", EquipmentSlotsPerHero: 3, GrowAvailablePassives: false),
            new CampaignBuildPowerQuantileSpec("P80", EquipmentSlotsPerHero: 3, GrowAvailablePassives: true),
        };

        var enemyVariants = new[]
        {
            new CampaignEnemyCompositionVariantSpec(0, "authored"),
            new CampaignEnemyCompositionVariantSpec(1, "mirror-lanes"),
            new CampaignEnemyCompositionVariantSpec(2, "swap-rows"),
            new CampaignEnemyCompositionVariantSpec(3, "mirror-lanes-swap-rows"),
            new CampaignEnemyCompositionVariantSpec(4, "rotate-anchor-forward"),
            new CampaignEnemyCompositionVariantSpec(5, "rotate-anchor-back"),
            new CampaignEnemyCompositionVariantSpec(6, "rotate-member-anchors"),
            new CampaignEnemyCompositionVariantSpec(7, "reverse-member-order"),
        };

        var coverage = new[]
        {
            new CampaignRosterCoverageSpec("C0-core-only", BenchArchetypeCount: 0),
            new CampaignRosterCoverageSpec("C1-one-counter", BenchArchetypeCount: 1),
            new CampaignRosterCoverageSpec("C2-two-counters", BenchArchetypeCount: 2),
            new CampaignRosterCoverageSpec("C3-full-counter-bench", BenchArchetypeCount: 4),
        };

        var nonBoss = new[]
        {
            NonBoss(1, Band(.94, .98, .96), Band(.96, .99, .98), Band(.90, .95, .93), Band(.93, .98, .96), Range(0, .08)),
            NonBoss(2, Band(.84, .90, .87), Band(.88, .94, .91), Band(.76, .84, .80), Band(.83, .90, .86), Range(.03, .12)),
            NonBoss(3, Band(.76, .84, .80), Band(.83, .90, .86), Band(.66, .76, .71), Band(.75, .84, .80), Range(.05, .14)),
            NonBoss(4, Band(.68, .78, .73), Band(.78, .86, .82), Band(.58, .68, .63), Band(.69, .80, .75), Range(.08, .15)),
            NonBoss(5, Band(.60, .72, .66), Band(.71, .82, .77), Band(.50, .62, .56), Band(.63, .75, .69), Range(.10, .15)),
        };

        var bosses = new[]
        {
            Boss(1, 1, Band(.40, .55, .46), Band(.75, .85, .80), .34),
            Boss(1, 2, Band(.20, .40, .30), Band(.70, .85, .76), .46),
            Boss(2, 1, Band(.35, .50, .42), Band(.72, .85, .78), .36),
            Boss(2, 2, Band(.28, .43, .35), Band(.70, .82, .75), .40),
            Boss(3, 1, Band(.30, .44, .37), Band(.72, .84, .77), .40),
            Boss(3, 2, Band(.25, .40, .32), Band(.70, .82, .75), .43),
            Boss(4, 1, Band(.25, .39, .32), Band(.72, .82, .76), .44),
            Boss(4, 2, Band(.20, .35, .28), Band(.70, .80, .74), .46),
            Boss(5, 1, Band(.20, .35, .28), Band(.70, .80, .74), .46),
            Boss(5, 2, Band(.15, .30, .22), Band(.50, .70, .60), .38),
        };

        var guardrails = new CampaignBalanceGuardrails(
            BossGapMinimum: .30,
            NaiveBossAnswerTagConditionalMinimum: .85,
            Chapter1NonBossNaiveEachReferenceSquadMinimum: .90,
            NonBossArmGapMinimum: 0,
            NonBossArmGapMaximum: .15,
            CliffNodeMinimumWinRate: .30,
            MaximumInfoConsecutiveSiteAndDrop: .30,
            LateSaturationWinRate: .95,
            FinalFrontlineSiteAndMinimum: .50,
            FinalFrontlineSiteAndMaximum: .70,
            FinalCounterSiteAndMinimum: .85,
            FinalCounterSiteAndTargetMinimum: .88,
            FinalCounterSiteAndTargetMaximum: .92,
            ThreatEnvelopeHpHardCap: 1.14,
            ThreatEnvelopeAtkHardCap: 1.10,
            ThreatEnvelopePiHardCap: 10.5,
            EnvelopeRampShareTargetMaximum: .15,
            EnvelopeRampShareFailMaximum: .18,
            BossGapEnvelopeRemovalShrinkFail: .05,
            MaximumCountDeltaPerSequentialNode: 1,
            MaximumCountPiDeltaWithinSite: 10,
            NaiveFirstClearMinimum: .20,
            NaiveFirstClearMaximum: .40,
            LessonRetry1ClearMinimum: .65,
            LessonRetry1ClearMaximum: .75,
            LessonRetry2ClearMinimum: .75,
            LessonRetry2ClearMaximum: .85,
            Chapter1CanonicalBossBankruptTargetMaximum: .08,
            Chapter1CanonicalBossBankruptFailAbove: .10,
            Chapter1GreedRouteBossBankruptMaximum: .15,
            UnchangedRetryRateTarget: 0,
            UnchangedRetryRateFailAbove: .05,
            AuthoredDecisionOpportunityRatioMinimum: 1.50,
            AuthoredDecisionOpportunityRatioMaximum: 1.90,
            RealizedStateChangingRatioMinimum: .90,
            RealizedStateChangingRatioMaximum: 1.30,
            ForcedNoOpClickRatioMaximum: .25,
            LossToChangedRetryRateTargetMinimum: .95);

        return new CampaignBalanceSweepConfig(
            CurrentSchemaVersion,
            arms,
            squads,
            builds,
            enemyVariants,
            coverage,
            nonBoss,
            bosses,
            guardrails,
            MinimumEffectiveSamplesPerArmPerNode: 480,
            MaximumWilsonHalfWidth: .045,
            ProgressionRetrySeedCount: 32,
            SiteSafety: 16);
    }

    private static ProbabilityBand Band(double min, double max, double center) => new(min, max, center);
    private static ProbabilityRange Range(double min, double max) => new(min, max);

    private static CampaignNonBossBandSpec NonBoss(
        int chapter,
        ProbabilityBand trashNaive,
        ProbabilityBand trashInfo,
        ProbabilityBand eliteNaive,
        ProbabilityBand eliteInfo,
        ProbabilityRange gap)
        => new(chapter, trashNaive, trashInfo, eliteNaive, eliteInfo, gap);

    private static CampaignBossBandSpec Boss(
        int chapter,
        int site,
        ProbabilityBand naive,
        ProbabilityBand info,
        double centerGap)
        => new(chapter, site, naive, info, centerGap);
}

public sealed record CampaignBalanceArmSpec(
    string ArmId,
    string PolicyId,
    bool UsesForcedPreview,
    string PhaseADescription);

public sealed record CampaignReferenceSquadSpec(
    string SquadId,
    IReadOnlyList<string> CoreArchetypeIds,
    IReadOnlyList<string> BenchArchetypeIds);

public sealed record CampaignBuildPowerQuantileSpec(
    string QuantileId,
    int EquipmentSlotsPerHero,
    bool GrowAvailablePassives);

public sealed record CampaignEnemyCompositionVariantSpec(int VariantIndex, string VariantId);

public sealed record CampaignRosterCoverageSpec(string CoverageId, int BenchArchetypeCount);

public sealed record CampaignBalanceGridCell(
    CampaignReferenceSquadSpec Squad,
    CampaignBuildPowerQuantileSpec BuildPower,
    CampaignEnemyCompositionVariantSpec EnemyComposition,
    CampaignRosterCoverageSpec RosterCoverage)
{
    public string CellId => $"{Squad.SquadId}|{BuildPower.QuantileId}|{EnemyComposition.VariantId}|{RosterCoverage.CoverageId}";

    public IReadOnlyList<string> RosterArchetypeIds => Squad.CoreArchetypeIds
        .Concat(Squad.BenchArchetypeIds.Take(RosterCoverage.BenchArchetypeCount))
        .ToArray();
}

public sealed record ProbabilityBand(double Minimum, double Maximum, double Center)
{
    public bool Contains(double value) => value >= Minimum && value <= Maximum;
}

public sealed record ProbabilityRange(double Minimum, double Maximum)
{
    public bool Contains(double value) => value >= Minimum && value <= Maximum;
}

public sealed record CampaignNonBossBandSpec(
    int ChapterOrder,
    ProbabilityBand TrashNaiveWinBand,
    ProbabilityBand TrashInfoWinBand,
    ProbabilityBand EliteNaiveWinBand,
    ProbabilityBand EliteInfoWinBand,
    ProbabilityRange SameCellArmGapBand);

public sealed record CampaignBossBandSpec(
    int ChapterOrder,
    int SiteOrder,
    ProbabilityBand NaiveWinBand,
    ProbabilityBand InfoWinBand,
    double InitialCenterGap);

public sealed record CampaignBalanceGuardrails(
    double BossGapMinimum,
    double NaiveBossAnswerTagConditionalMinimum,
    double Chapter1NonBossNaiveEachReferenceSquadMinimum,
    double NonBossArmGapMinimum,
    double NonBossArmGapMaximum,
    double CliffNodeMinimumWinRate,
    double MaximumInfoConsecutiveSiteAndDrop,
    double LateSaturationWinRate,
    double FinalFrontlineSiteAndMinimum,
    double FinalFrontlineSiteAndMaximum,
    double FinalCounterSiteAndMinimum,
    double FinalCounterSiteAndTargetMinimum,
    double FinalCounterSiteAndTargetMaximum,
    double ThreatEnvelopeHpHardCap,
    double ThreatEnvelopeAtkHardCap,
    double ThreatEnvelopePiHardCap,
    double EnvelopeRampShareTargetMaximum,
    double EnvelopeRampShareFailMaximum,
    double BossGapEnvelopeRemovalShrinkFail,
    int MaximumCountDeltaPerSequentialNode,
    double MaximumCountPiDeltaWithinSite,
    double NaiveFirstClearMinimum,
    double NaiveFirstClearMaximum,
    double LessonRetry1ClearMinimum,
    double LessonRetry1ClearMaximum,
    double LessonRetry2ClearMinimum,
    double LessonRetry2ClearMaximum,
    double Chapter1CanonicalBossBankruptTargetMaximum,
    double Chapter1CanonicalBossBankruptFailAbove,
    double Chapter1GreedRouteBossBankruptMaximum,
    double UnchangedRetryRateTarget,
    double UnchangedRetryRateFailAbove,
    double AuthoredDecisionOpportunityRatioMinimum,
    double AuthoredDecisionOpportunityRatioMaximum,
    double RealizedStateChangingRatioMinimum,
    double RealizedStateChangingRatioMaximum,
    double ForcedNoOpClickRatioMaximum,
    double LossToChangedRetryRateTargetMinimum);
