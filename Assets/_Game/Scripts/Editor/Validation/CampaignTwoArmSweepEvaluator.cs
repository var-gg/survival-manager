using System;
using System.Collections.Generic;
using System.Linq;

namespace SM.Editor.Validation;

/// <summary>두 arm raw observation을 node/site/DDR aggregate로 모은다. 전투 실행이나 파일 I/O는 소유하지 않는다.</summary>
internal sealed class CampaignTwoArmSweepAccumulator
{
    private readonly CampaignBalanceSweepConfig _config;
    private readonly Dictionary<string, NodeBucket> _nodes = new(StringComparer.Ordinal);
    private readonly Dictionary<string, SiteBucket> _sites = new(StringComparer.Ordinal);
    private readonly Dictionary<string, DecisionBucket> _decisions = new(StringComparer.Ordinal);
    private readonly Dictionary<string, PairedNodeObservation> _pairedNodes = new(StringComparer.Ordinal);
    private int _equipmentAssignmentCount;

    public CampaignTwoArmSweepAccumulator(CampaignBalanceSweepConfig config)
    {
        _config = config;
        foreach (var arm in config.Arms)
        {
            _decisions[arm.ArmId] = new DecisionBucket(arm);
        }
    }

    public void RecordNode(
        CampaignBalanceArmSpec arm,
        string cellId,
        string squadId,
        CampaignNodeIdentity node,
        bool won,
        bool answerTagPresent,
        string formationHash,
        bool gearCounterUsed)
    {
        var key = $"{node.ChapterOrder:D2}|{node.SiteOrder:D2}|{node.NodeOrder:D2}|{node.NodeId}";
        if (!_nodes.TryGetValue(key, out var bucket))
        {
            bucket = new NodeBucket(node, _config.Arms, _config.ReferenceSquads);
            _nodes.Add(key, bucket);
        }

        bucket.Record(arm.ArmId, squadId, won, answerTagPresent && won && node.IsBoss);
        var decision = _decisions[arm.ArmId];
        decision.UniqueBattlesEntered++;
        if (!won)
        {
            decision.LossesObserved++;
        }

        var pairKey = $"{cellId}|{key}";
        if (!_pairedNodes.TryGetValue(pairKey, out var paired))
        {
            paired = new PairedNodeObservation();
            _pairedNodes.Add(pairKey, paired);
        }

        paired.Record(arm.ArmId, won, formationHash, gearCounterUsed);
    }

    public void RecordSite(
        CampaignBalanceArmSpec arm,
        string squadId,
        CampaignSiteIdentity site,
        bool firstVisitAndClear)
    {
        var key = $"{site.ChapterOrder:D2}|{site.SiteOrder:D2}|{site.SiteId}";
        if (!_sites.TryGetValue(key, out var bucket))
        {
            bucket = new SiteBucket(site, _config.Arms, _config.ReferenceSquads);
            _sites.Add(key, bucket);
        }

        bucket.Record(arm.ArmId, squadId, firstVisitAndClear);
    }

    public void RecordSiteEntryDecision(CampaignBalanceArmSpec arm, bool previewAvailable, bool setupChanged)
    {
        if (!previewAvailable)
        {
            return;
        }

        var bucket = _decisions[arm.ArmId];
        bucket.PreviewOpportunities++;
        if (setupChanged)
        {
            bucket.PreviewStateChanges++;
        }
        else
        {
            bucket.ForcedNoOpClicks++;
        }
    }

    public void RecordPrepDecision(
        CampaignBalanceArmSpec arm,
        bool opportunity,
        bool setupChanged,
        int equipmentAssignmentCount)
    {
        if (!opportunity)
        {
            return;
        }

        var bucket = _decisions[arm.ArmId];
        _equipmentAssignmentCount += Math.Max(0, equipmentAssignmentCount);
        bucket.PrepOpportunities++;
        if (setupChanged)
        {
            bucket.PrepStateChanges++;
        }
        else
        {
            bucket.ForcedNoOpClicks++;
        }
    }

    public IReadOnlyList<CampaignTwoArmNodeAggregate> BuildNodeAggregates()
        => _nodes.Values
            .OrderBy(bucket => bucket.Identity.ChapterOrder)
            .ThenBy(bucket => bucket.Identity.SiteOrder)
            .ThenBy(bucket => bucket.Identity.NodeOrder)
            .ThenBy(bucket => bucket.Identity.NodeId, StringComparer.Ordinal)
            .Select(bucket => bucket.Build(_config))
            .ToArray();

    public IReadOnlyList<CampaignTwoArmSiteAggregate> BuildSiteAggregates()
        => _sites.Values
            .OrderBy(bucket => bucket.Identity.ChapterOrder)
            .ThenBy(bucket => bucket.Identity.SiteOrder)
            .ThenBy(bucket => bucket.Identity.SiteId, StringComparer.Ordinal)
            .Select(bucket => bucket.Build(_config))
            .ToArray();

    public IReadOnlyList<CampaignDecisionDensityRaw> BuildDecisionAggregates()
        => _config.Arms.Select(arm => _decisions[arm.ArmId].Build()).ToArray();

    public CampaignPrepMechanismSummary BuildPrepMechanismSummary()
    {
        var complete = _pairedNodes.Values.Where(value => value.IsComplete).ToArray();
        var gearCounter = complete.Where(value => value.InformedGearCounterUsed).ToArray();
        return new CampaignPrepMechanismSummary(
            complete.Count(value => !string.Equals(value.NaiveFormationHash, value.InformedFormationHash, StringComparison.Ordinal)),
            complete.Count(value => value.NaiveWon.Value != value.InformedWon.Value),
            complete.Count(value => !value.NaiveWon.Value && value.InformedWon.Value),
            complete.Count(value => value.NaiveWon.Value && !value.InformedWon.Value),
            _equipmentAssignmentCount,
            gearCounter.Length,
            gearCounter.Count(value => value.NaiveWon.Value),
            gearCounter.Count(value => value.InformedWon.Value));
    }

    private sealed class NodeBucket
    {
        private readonly Dictionary<string, ArmCounter> _arms;
        private readonly Dictionary<string, Dictionary<string, ArmCounter>> _squads;

        public NodeBucket(
            CampaignNodeIdentity identity,
            IEnumerable<CampaignBalanceArmSpec> arms,
            IEnumerable<CampaignReferenceSquadSpec> squads)
        {
            Identity = identity;
            _arms = arms.ToDictionary(arm => arm.ArmId, _ => new ArmCounter(), StringComparer.Ordinal);
            _squads = squads.ToDictionary(
                squad => squad.SquadId,
                _ => arms.ToDictionary(arm => arm.ArmId, _ => new ArmCounter(), StringComparer.Ordinal),
                StringComparer.Ordinal);
        }

        public CampaignNodeIdentity Identity { get; }

        public void Record(string armId, string squadId, bool won, bool answerTagPresent)
        {
            _arms[armId].Record(won, answerTagPresent);
            _squads[squadId][armId].Record(won, answerTagPresent);
        }

        public CampaignTwoArmNodeAggregate Build(CampaignBalanceSweepConfig config)
        {
            var naiveSpec = config.Arms.Single(arm => arm.ArmId == "naive");
            var infoSpec = config.Arms.Single(arm => arm.ArmId == "informed");
            return new CampaignTwoArmNodeAggregate(
                Identity.ChapterId,
                Identity.ChapterOrder,
                Identity.SiteId,
                Identity.SiteOrder,
                Identity.NodeId,
                Identity.NodeOrder,
                Identity.EncounterId,
                Identity.IsElite,
                Identity.IsBoss,
                _arms[naiveSpec.ArmId].Build(naiveSpec),
                _arms[infoSpec.ArmId].Build(infoSpec),
                config.ReferenceSquads.Select(squad => new CampaignSquadArmSampleAggregate(
                    squad.SquadId,
                    _squads[squad.SquadId][naiveSpec.ArmId].Build(naiveSpec),
                    _squads[squad.SquadId][infoSpec.ArmId].Build(infoSpec))).ToArray());
        }
    }

    private sealed class SiteBucket
    {
        private readonly Dictionary<string, ArmCounter> _arms;
        private readonly Dictionary<string, Dictionary<string, ArmCounter>> _squads;

        public SiteBucket(
            CampaignSiteIdentity identity,
            IEnumerable<CampaignBalanceArmSpec> arms,
            IEnumerable<CampaignReferenceSquadSpec> squads)
        {
            Identity = identity;
            _arms = arms.ToDictionary(arm => arm.ArmId, _ => new ArmCounter(), StringComparer.Ordinal);
            _squads = squads.ToDictionary(
                squad => squad.SquadId,
                _ => arms.ToDictionary(arm => arm.ArmId, _ => new ArmCounter(), StringComparer.Ordinal),
                StringComparer.Ordinal);
        }

        public CampaignSiteIdentity Identity { get; }

        public void Record(string armId, string squadId, bool clear)
        {
            _arms[armId].Record(clear, false);
            _squads[squadId][armId].Record(clear, false);
        }

        public CampaignTwoArmSiteAggregate Build(CampaignBalanceSweepConfig config)
        {
            var naiveSpec = config.Arms.Single(arm => arm.ArmId == "naive");
            var infoSpec = config.Arms.Single(arm => arm.ArmId == "informed");
            return new CampaignTwoArmSiteAggregate(
                Identity,
                _arms[naiveSpec.ArmId].Build(naiveSpec),
                _arms[infoSpec.ArmId].Build(infoSpec),
                config.ReferenceSquads.Select(squad => new CampaignSquadArmSampleAggregate(
                    squad.SquadId,
                    _squads[squad.SquadId][naiveSpec.ArmId].Build(naiveSpec),
                    _squads[squad.SquadId][infoSpec.ArmId].Build(infoSpec))).ToArray());
        }
    }

    private sealed class ArmCounter
    {
        public int Samples { get; private set; }
        public int Wins { get; private set; }
        public int BossWinsWithAnswerTag { get; private set; }

        public void Record(bool won, bool answerTagPresent)
        {
            Samples++;
            Wins += won ? 1 : 0;
            BossWinsWithAnswerTag += answerTagPresent ? 1 : 0;
        }

        public CampaignArmSampleAggregate Build(CampaignBalanceArmSpec arm)
            => new(arm.ArmId, arm.PolicyId, Samples, Wins, BossWinsWithAnswerTag);
    }

    private sealed class DecisionBucket
    {
        public DecisionBucket(CampaignBalanceArmSpec arm) => Arm = arm;

        public CampaignBalanceArmSpec Arm { get; }
        public long UniqueBattlesEntered { get; set; }
        public long PreviewOpportunities { get; set; }
        public long PreviewStateChanges { get; set; }
        public long PrepOpportunities { get; set; }
        public long PrepStateChanges { get; set; }
        public long ForcedNoOpClicks { get; set; }
        public long LossesObserved { get; set; }
        public long LossesFollowedByChangedSetup { get; set; }

        public CampaignDecisionDensityRaw Build()
            => new(
                Arm,
                UniqueBattlesEntered,
                new CampaignDecisionDensityCounts(0, PreviewOpportunities, PrepOpportunities, 0),
                new CampaignDecisionDensityCounts(0, PreviewStateChanges, PrepStateChanges, 0),
                ForcedNoOpClicks,
                LossesObserved,
                LossesFollowedByChangedSetup);
    }

    private sealed class PairedNodeObservation
    {
        public bool? NaiveWon { get; private set; }
        public bool? InformedWon { get; private set; }
        public string NaiveFormationHash { get; private set; }
        public string InformedFormationHash { get; private set; }
        public bool InformedGearCounterUsed { get; private set; }
        public bool IsComplete => NaiveWon.HasValue && InformedWon.HasValue;

        public void Record(string armId, bool won, string formationHash, bool gearCounterUsed)
        {
            if (string.Equals(armId, "naive", StringComparison.Ordinal))
            {
                NaiveWon = won;
                NaiveFormationHash = formationHash;
            }
            else if (string.Equals(armId, "informed", StringComparison.Ordinal))
            {
                InformedWon = won;
                InformedFormationHash = formationHash;
                InformedGearCounterUsed = gearCounterUsed;
            }
            else
            {
                throw new InvalidOperationException($"Unknown campaign arm '{armId}'.");
            }
        }
    }
}

internal sealed record CampaignPrepMechanismSummary(
    int FormationDivergenceCount,
    int OutcomeDivergenceCount,
    int InformedOnlyWinCount,
    int NaiveOnlyWinCount,
    int EquipmentAssignmentCount,
    int GearCounterSampleCount,
    int GearCounterNaiveWinCount,
    int GearCounterInformedWinCount)
{
    public double GearCounterNaiveWinRate => GearCounterSampleCount == 0
        ? 0d
        : GearCounterNaiveWinCount / (double)GearCounterSampleCount;

    public double GearCounterInformedWinRate => GearCounterSampleCount == 0
        ? 0d
        : GearCounterInformedWinCount / (double)GearCounterSampleCount;

    public double GearCounterGap => GearCounterInformedWinRate - GearCounterNaiveWinRate;
}

internal sealed record CampaignNodeIdentity(
    string ChapterId,
    int ChapterOrder,
    string SiteId,
    int SiteOrder,
    string NodeId,
    int NodeOrder,
    string EncounterId,
    bool IsElite,
    bool IsBoss);

internal sealed record CampaignSiteIdentity(string ChapterId, int ChapterOrder, string SiteId, int SiteOrder);

internal sealed record CampaignTwoArmSiteAggregate(
    CampaignSiteIdentity Identity,
    CampaignArmSampleAggregate Naive,
    CampaignArmSampleAggregate Informed,
    IReadOnlyList<CampaignSquadArmSampleAggregate> ByReferenceSquad);

internal sealed record CampaignDecisionDensityRaw(
    CampaignBalanceArmSpec Arm,
    long UniqueBattlesEntered,
    CampaignDecisionDensityCounts AuthoredOpportunities,
    CampaignDecisionDensityCounts RealizedStateChanges,
    long ForcedNoOpClicks,
    long LossesObserved,
    long LossesFollowedByChangedSetup);

/// <summary>측정값을 gpt-pro v1 band/guardrail에 대조한다. fixture test가 같은 판정 함수를 직접 사용한다.</summary>
public static class CampaignTwoArmBandEvaluator
{
    public static CampaignTwoArmNodeReport EvaluateNode(
        CampaignBalanceSweepConfig config,
        CampaignTwoArmNodeAggregate aggregate)
    {
        var target = ResolveTarget(config, aggregate);
        var naiveBandPass = target.NaiveWinBand.Contains(aggregate.Naive.WinRate);
        var infoBandPass = target.InfoWinBand.Contains(aggregate.Informed.WinRate);
        var gap = aggregate.Informed.WinRate - aggregate.Naive.WinRate;
        var gapPass = target.ArmGapBand.Contains(gap);
        var answerTagPass = !aggregate.IsBoss
                            || aggregate.Naive.AnswerTagGivenWinRate is { } answerRate
                            && answerRate >= config.Guardrails.NaiveBossAnswerTagConditionalMinimum;
        var chapter1EachSquadPass = aggregate.IsBoss
                                    || aggregate.ChapterOrder != 1
                                    || aggregate.ByReferenceSquad.All(value =>
                                        value.Naive.WinRate >= config.Guardrails.Chapter1NonBossNaiveEachReferenceSquadMinimum);
        var samplingPass = aggregate.Naive.SampleCount >= config.MinimumEffectiveSamplesPerArmPerNode
                           && aggregate.Informed.SampleCount >= config.MinimumEffectiveSamplesPerArmPerNode
                           && WilsonHalfWidth(aggregate.Naive.WinRate, aggregate.Naive.SampleCount) <= config.MaximumWilsonHalfWidth
                           && WilsonHalfWidth(aggregate.Informed.WinRate, aggregate.Informed.SampleCount) <= config.MaximumWilsonHalfWidth;

        var findings = new List<string>();
        if (!samplingPass) findings.Add("effective sample/Wilson contract failed");
        if (!naiveBandPass) findings.Add($"naive {aggregate.Naive.WinRate:0.000} outside {target.NaiveWinBand.Minimum:0.00}-{target.NaiveWinBand.Maximum:0.00}");
        if (!infoBandPass) findings.Add($"info {aggregate.Informed.WinRate:0.000} outside {target.InfoWinBand.Minimum:0.00}-{target.InfoWinBand.Maximum:0.00}");
        if (!gapPass) findings.Add($"gap {gap:0.000} outside {target.ArmGapBand.Minimum:0.00}-{target.ArmGapBand.Maximum:0.00}");
        if (!answerTagPass) findings.Add("naive boss answer-tag conditional below 0.85 or undefined");
        if (!chapter1EachSquadPass) findings.Add("chapter-1 nonboss naive floor failed for at least one reference squad");

        var allPass = samplingPass && naiveBandPass && infoBandPass && gapPass && answerTagPass && chapter1EachSquadPass;
        var status = allPass
            ? "PASS"
            : samplingPass && gap < target.ArmGapBand.Minimum
                ? "BASELINE-GAP"
                : "FAIL";

        return new CampaignTwoArmNodeReport(
            aggregate.ChapterId,
            aggregate.ChapterOrder,
            aggregate.SiteId,
            aggregate.SiteOrder,
            aggregate.NodeId,
            aggregate.NodeOrder,
            aggregate.EncounterId,
            aggregate.IsElite,
            aggregate.IsBoss,
            aggregate.Naive,
            aggregate.Informed,
            gap,
            WilsonHalfWidth(aggregate.Naive.WinRate, aggregate.Naive.SampleCount),
            WilsonHalfWidth(aggregate.Informed.WinRate, aggregate.Informed.SampleCount),
            target,
            naiveBandPass,
            infoBandPass,
            gapPass,
            answerTagPass,
            chapter1EachSquadPass,
            status,
            findings,
            aggregate.ByReferenceSquad);
    }

    internal static CampaignTwoArmSweepReport BuildReport(
        CampaignBalanceSweepConfig config,
        CampaignTwoArmSweepAccumulator accumulator)
    {
        var nodes = accumulator.BuildNodeAggregates().Select(node => EvaluateNode(config, node)).ToArray();
        var sites = accumulator.BuildSiteAggregates().Select(site => new CampaignTwoArmSiteReport(
            site.Identity.ChapterId,
            site.Identity.ChapterOrder,
            site.Identity.SiteId,
            site.Identity.SiteOrder,
            site.Naive,
            site.Informed,
            site.Informed.WinRate - site.Naive.WinRate,
            site.ByReferenceSquad)).ToArray();
        var chapters = BuildChapters(nodes);
        var decisionDensity = accumulator.BuildDecisionAggregates()
            .Select(raw => EvaluateDecisionDensity(config, raw))
            .ToArray();
        var maximumWilson = nodes
            .SelectMany(node => new[] { node.NaiveWilsonHalfWidth, node.InfoWilsonHalfWidth })
            .DefaultIfEmpty(0)
            .Max();
        var samplingPass = nodes.Length > 0
                           && nodes.All(node => node.Naive.SampleCount >= config.MinimumEffectiveSamplesPerArmPerNode
                                                && node.Informed.SampleCount >= config.MinimumEffectiveSamplesPerArmPerNode)
                           && maximumWilson <= config.MaximumWilsonHalfWidth;

        return new CampaignTwoArmSweepReport
        {
            Config = config,
            Arms = config.Arms,
            Grid = new CampaignGridExecutionReport
            {
                ReferenceSquadCount = config.ReferenceSquads.Count,
                BuildPowerQuantileCount = config.BuildPowerQuantiles.Count,
                EnemyCompositionVariantCount = config.EnemyCompositionVariants.Count,
                RosterCoverageVariantCount = config.RosterCoverageVariants.Count,
                FullCellCountPerArmPerNode = config.FullGridCellCount,
                ExecutedCellCountPerArmPerNode = config.EffectiveGridCellCount,
                MinimumRequiredEffectiveSamples = config.MinimumEffectiveSamplesPerArmPerNode,
                MaximumAllowedWilsonHalfWidth = config.MaximumWilsonHalfWidth,
                MaximumObservedWilsonHalfWidth = maximumWilson,
                SamplingCap = config.SamplingCap,
                SamplingCapLog = config.SamplingCap.HasValue
                    ? $"cap={config.SamplingCap}: {config.SamplingCapReason}"
                    : "none; canonical 480-cell grid executed in full",
                MeetsSamplingContract = samplingPass,
            },
            Nodes = nodes,
            Sites = sites,
            Chapters = chapters,
            DecisionDensity = decisionDensity,
            Summary = BuildSummary(config, nodes, sites, samplingPass, accumulator.BuildPrepMechanismSummary()),
            PhaseAApproximations = new[]
            {
                "both arms share greedy site-entry deployment; informed alone receives the elite/boss <=2 formation edits + <=1 bench swap + <=1 owned-equipment assignment prep transaction",
                "build-power quantiles are ordinal cohorts made only from existing 0/1/2/3 equipment-slot and available-passive paths; no stat multiplier is injected",
                "enemy composition variants preserve authored units/mechanics and vary only deterministic member order/anchor placement inside the measurement input",
                "paid Scout, recruit, and Refit are never invoked by either arm",
            },
        };
    }

    public static double WilsonHalfWidth(double rate, int sampleCount)
    {
        if (sampleCount <= 0)
        {
            return 1;
        }

        const double z = 1.96;
        var n = sampleCount;
        var denominator = 1 + (z * z / n);
        return z * Math.Sqrt((rate * (1 - rate) / n) + (z * z / (4 * n * n))) / denominator;
    }

    private static CampaignNodeBandTarget ResolveTarget(
        CampaignBalanceSweepConfig config,
        CampaignTwoArmNodeAggregate aggregate)
    {
        if (aggregate.IsBoss)
        {
            var boss = config.BossBand(aggregate.ChapterOrder, aggregate.SiteOrder);
            return new CampaignNodeBandTarget(
                "boss",
                boss.NaiveWinBand,
                boss.InfoWinBand,
                new ProbabilityRange(config.Guardrails.BossGapMinimum, 1),
                NaiveBossCliffExemptWhenGapPasses: true,
                config.Guardrails.NaiveBossAnswerTagConditionalMinimum);
        }

        var nonBoss = config.NonBossBand(aggregate.ChapterOrder);
        return new CampaignNodeBandTarget(
            aggregate.IsElite ? "elite" : "trash",
            aggregate.IsElite ? nonBoss.EliteNaiveWinBand : nonBoss.TrashNaiveWinBand,
            aggregate.IsElite ? nonBoss.EliteInfoWinBand : nonBoss.TrashInfoWinBand,
            nonBoss.SameCellArmGapBand,
            NaiveBossCliffExemptWhenGapPasses: false,
            AnswerTagGivenNaiveWinMinimum: null);
    }

    private static IReadOnlyList<CampaignTwoArmChapterReport> BuildChapters(
        IEnumerable<CampaignTwoArmNodeReport> nodes)
        => nodes.GroupBy(node => (node.ChapterOrder, node.ChapterId))
            .OrderBy(group => group.Key.ChapterOrder)
            .Select(group =>
            {
                var chapterNodes = group.ToArray();
                var bosses = chapterNodes.Where(node => node.IsBoss).ToArray();
                return new CampaignTwoArmChapterReport(
                    group.Key.ChapterId,
                    group.Key.ChapterOrder,
                    WeightedRate(chapterNodes.Select(node => node.Naive)),
                    WeightedRate(chapterNodes.Select(node => node.Informed)),
                    WeightedRate(chapterNodes.Select(node => node.Informed)) - WeightedRate(chapterNodes.Select(node => node.Naive)),
                    WeightedRate(bosses.Select(node => node.Naive)),
                    WeightedRate(bosses.Select(node => node.Informed)),
                    WeightedRate(bosses.Select(node => node.Informed)) - WeightedRate(bosses.Select(node => node.Naive)),
                    chapterNodes.Sum(node => node.Naive.SampleCount),
                    bosses.Sum(node => node.Naive.SampleCount));
            }).ToArray();

    private static CampaignDecisionDensityReport EvaluateDecisionDensity(
        CampaignBalanceSweepConfig config,
        CampaignDecisionDensityRaw raw)
    {
        var battles = Math.Max(1, raw.UniqueBattlesEntered);
        var opportunities = raw.AuthoredOpportunities.Total / (double)battles;
        var realized = raw.RealizedStateChanges.Total / (double)battles;
        var noOp = raw.AuthoredOpportunities.Total == 0
            ? 0
            : raw.ForcedNoOpClicks / (double)raw.AuthoredOpportunities.Total;
        var changedRetry = raw.LossesObserved == 0
            ? 1
            : raw.LossesFollowedByChangedSetup / (double)raw.LossesObserved;
        var findings = new List<string>();
        if (opportunities < config.Guardrails.AuthoredDecisionOpportunityRatioMinimum
            || opportunities > config.Guardrails.AuthoredDecisionOpportunityRatioMaximum)
        {
            findings.Add($"authored DDR {opportunities:0.000} outside 1.50-1.90");
        }

        if (realized < config.Guardrails.RealizedStateChangingRatioMinimum
            || realized > config.Guardrails.RealizedStateChangingRatioMaximum)
        {
            findings.Add($"realized state-changing ratio {realized:0.000} outside 0.90-1.30");
        }

        if (noOp > config.Guardrails.ForcedNoOpClickRatioMaximum)
        {
            findings.Add($"forced no-op click ratio {noOp:0.000} above 0.25");
        }

        if (raw.LossesObserved > 0 && changedRetry < config.Guardrails.LossToChangedRetryRateTargetMinimum)
        {
            findings.Add($"loss-to-changed-retry rate {changedRetry:0.000} below 0.95");
        }

        return new CampaignDecisionDensityReport(
            raw.Arm.ArmId,
            raw.Arm.PolicyId,
            raw.UniqueBattlesEntered,
            raw.AuthoredOpportunities,
            raw.RealizedStateChanges,
            raw.ForcedNoOpClicks,
            raw.LossesObserved,
            raw.LossesFollowedByChangedSetup,
            opportunities,
            realized,
            noOp,
            changedRetry,
            findings.Count == 0 ? "PASS" : "BASELINE-GAP",
            findings);
    }

    private static CampaignTwoArmSweepSummary BuildSummary(
        CampaignBalanceSweepConfig config,
        IReadOnlyList<CampaignTwoArmNodeReport> nodes,
        IReadOnlyList<CampaignTwoArmSiteReport> sites,
        bool samplingPass,
        CampaignPrepMechanismSummary mechanism)
    {
        var cliffs = nodes.Where(node =>
                node.Informed.WinRate < config.Guardrails.CliffNodeMinimumWinRate
                || (!node.IsBoss && node.Naive.WinRate < config.Guardrails.CliffNodeMinimumWinRate)
                || (node.IsBoss && !node.GapBandPass && node.Naive.WinRate < config.Guardrails.CliffNodeMinimumWinRate))
            .Select(node => $"{node.ChapterId}/{node.SiteId}/{node.NodeId}: naive={node.Naive.WinRate:0.000}, info={node.Informed.WinRate:0.000}")
            .ToArray();

        var drops = new List<string>();
        CampaignTwoArmSiteReport previous = null;
        foreach (var site in sites.OrderBy(site => site.ChapterOrder).ThenBy(site => site.SiteOrder))
        {
            if (previous != null)
            {
                var drop = previous.InformedSiteAnd.WinRate - site.InformedSiteAnd.WinRate;
                if (drop > config.Guardrails.MaximumInfoConsecutiveSiteAndDrop)
                {
                    drops.Add($"{previous.SiteId}->{site.SiteId}: info SiteAND1 drop={drop:0.000}");
                }
            }

            previous = site;
        }

        var saturation = new List<string>();
        foreach (var squad in config.ReferenceSquads)
        foreach (var armId in new[] { "naive", "informed" })
        {
            var rates = nodes.Where(node => node.ChapterOrder >= 2)
                .Select(node => node.ByReferenceSquad.Single(value => value.SquadId == squad.SquadId))
                .Select(value => armId == "naive" ? value.Naive.WinRate : value.Informed.WinRate)
                .ToArray();
            var saturated = rates.Count(rate => rate > config.Guardrails.LateSaturationWinRate);
            if (saturated > 0)
            {
                saturation.Add($"{armId}/{squad.SquadId}: {saturated}/{rates.Length} Ch2-5 node rates >0.95");
            }
        }

        var finalFindings = new List<string>();
        var final = sites.OrderBy(site => site.ChapterOrder).ThenBy(site => site.SiteOrder).LastOrDefault();
        if (final != null)
        {
            foreach (var squad in final.ByReferenceSquad)
            {
                var rate = squad.Informed.WinRate;
                if (squad.SquadId == "frontline")
                {
                    if (rate < config.Guardrails.FinalFrontlineSiteAndMinimum
                        || rate > config.Guardrails.FinalFrontlineSiteAndMaximum)
                    {
                        finalFindings.Add($"frontline final info SiteAND1={rate:0.000} outside 0.50-0.70");
                    }
                }
                else if (rate < config.Guardrails.FinalCounterSiteAndMinimum)
                {
                    finalFindings.Add($"{squad.SquadId} final info SiteAND1={rate:0.000} below 0.85");
                }
            }
        }

        var bosses = nodes.Where(node => node.IsBoss).ToArray();
        var hasTargetMiss = nodes.Any(node => node.Status != "PASS");
        return new CampaignTwoArmSweepSummary
        {
            Status = !samplingPass ? "FAIL" : hasTargetMiss ? "BASELINE-GAP" : "PASS",
            NodeCount = nodes.Count,
            PassNodeCount = nodes.Count(node => node.Status == "PASS"),
            FailNodeCount = nodes.Count(node => node.Status == "FAIL"),
            BaselineGapNodeCount = nodes.Count(node => node.Status == "BASELINE-GAP"),
            MeanNaiveWinRate = nodes.Count == 0 ? 0 : nodes.Average(node => node.Naive.WinRate),
            MeanInfoWinRate = nodes.Count == 0 ? 0 : nodes.Average(node => node.Informed.WinRate),
            MeanGap = nodes.Count == 0 ? 0 : nodes.Average(node => node.Gap),
            MeanBossNaiveWinRate = bosses.Length == 0 ? 0 : bosses.Average(node => node.Naive.WinRate),
            MeanBossInfoWinRate = bosses.Length == 0 ? 0 : bosses.Average(node => node.Informed.WinRate),
            MeanBossGap = bosses.Length == 0 ? 0 : bosses.Average(node => node.Gap),
            PrepFormationDivergenceCount = mechanism.FormationDivergenceCount,
            OutcomeDivergenceCount = mechanism.OutcomeDivergenceCount,
            InformedOnlyWinCount = mechanism.InformedOnlyWinCount,
            NaiveOnlyWinCount = mechanism.NaiveOnlyWinCount,
            PrepEquipmentAssignmentCount = mechanism.EquipmentAssignmentCount,
            GearCounterSampleCount = mechanism.GearCounterSampleCount,
            GearCounterNaiveWinRate = mechanism.GearCounterNaiveWinRate,
            GearCounterInformedWinRate = mechanism.GearCounterInformedWinRate,
            GearCounterGap = mechanism.GearCounterGap,
            CliffFindings = cliffs,
            ConsecutiveInfoSiteAndDropFindings = drops,
            LateSaturationFindings = saturation,
            FinalSiteAndFindings = finalFindings,
            SamplingFindings = samplingPass
                ? Array.Empty<string>()
                : new[] { "480 effective samples / Wilson half-width <=0.045 contract failed" },
        };
    }

    private static double WeightedRate(IEnumerable<CampaignArmSampleAggregate> samples)
    {
        var values = samples.ToArray();
        var count = values.Sum(value => value.SampleCount);
        return count == 0 ? 0 : values.Sum(value => value.WinCount) / (double)count;
    }
}
