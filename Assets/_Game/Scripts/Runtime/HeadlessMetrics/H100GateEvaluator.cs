using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace SM.HeadlessMetrics;

/// <summary>Battle/Campaign records와 외부 blinded observation을 H100 AND-gate 관측치로 집계한다.</summary>
public static class H100GateEvaluator
{
    public sealed record ExternalObservation(string MetricId, double Value, int SampleCount, string Evidence);

    private sealed record MetricValue(double Value, int SampleCount, string Evidence);

    public static GateReport Generate(
        H100GateSpec spec,
        IReadOnlyList<BattleMetricRecord> battleRecords,
        IReadOnlyList<CampaignMetricRecord> campaignRecords,
        IReadOnlyList<ExternalObservation>? externalObservations = null)
    {
        spec.Validate();
        var metrics = BuildMetrics(spec, battleRecords, campaignRecords);
        foreach (var observation in externalObservations ?? Array.Empty<ExternalObservation>())
        {
            if (string.IsNullOrWhiteSpace(observation.MetricId)
                || observation.SampleCount < 0
                || double.IsNaN(observation.Value)
                || double.IsInfinity(observation.Value))
            {
                throw new ArgumentException("external H100 observation이 유효하지 않다.", nameof(externalObservations));
            }

            if (metrics.ContainsKey(observation.MetricId))
            {
                throw new InvalidOperationException($"computed metric을 external observation으로 덮어쓸 수 없다: {observation.MetricId}");
            }

            metrics.Add(observation.MetricId, new MetricValue(observation.Value, observation.SampleCount, observation.Evidence));
        }

        var gates = new List<GateReport.GateResult>(spec.Gates.Count);
        foreach (var gate in spec.Gates)
        {
            var thresholdResults = gate.Thresholds.Select(threshold => Evaluate(threshold, metrics)).ToArray();
            gates.Add(new GateReport.GateResult(gate.Id, gate.NameKo, thresholdResults.All(result => result.Pass), thresholdResults));
        }

        return new GateReport
        {
            SpecVersion = spec.SpecVersion,
            OverallPass = gates.All(gate => gate.Pass),
            BattleRecordCount = battleRecords.Count,
            CampaignRecordCount = campaignRecords.Count,
            Gates = gates,
        };
    }

    private static Dictionary<string, MetricValue> BuildMetrics(
        H100GateSpec spec,
        IReadOnlyList<BattleMetricRecord> battles,
        IReadOnlyList<CampaignMetricRecord> campaigns)
    {
        var metrics = new Dictionary<string, MetricValue>(StringComparer.Ordinal);
        AddIntegrityMetrics(metrics, battles, campaigns);
        AddCampaignMetrics(metrics, campaigns);
        AddBuildMetrics(metrics, battles, campaigns);
        AddFormationAndSpectatorMetrics(metrics, spec, battles);
        AddDepthMetrics(metrics, battles);
        return metrics;
    }

    private static void AddIntegrityMetrics(
        IDictionary<string, MetricValue> metrics,
        IReadOnlyList<BattleMetricRecord> battles,
        IReadOnlyList<CampaignMetricRecord> campaigns)
    {
        var replayGroups = battles
            .Where(record => !string.IsNullOrWhiteSpace(record.ReplayGroupId))
            .GroupBy(record => record.ReplayGroupId, StringComparer.Ordinal)
            .Where(group => group.Count() >= 2)
            .ToArray();
        Add(metrics, "battle_replay_group_count", replayGroups.Length, replayGroups.Length, "replay groups with at least two copies");
        if (replayGroups.Length > 0)
        {
            var matching = replayGroups.Count(group => group.All(record => !string.IsNullOrWhiteSpace(record.ReplayHash))
                                                       && group.Select(record => record.ReplayHash).Distinct(StringComparer.Ordinal).Count() == 1);
            Add(metrics, "replay_hash_match_rate", (double)matching / replayGroups.Length, replayGroups.Length, "same-input replay group hash equality");
        }

        var completedFuzzRuns = campaigns.Count(record => !record.Truncated);
        Add(metrics, "campaign_fuzz_count", completedFuzzRuns, campaigns.Count, "non-truncated campaign records");
        var battleCrashCount = battles.Count(record => record.Crashed);
        var battleCrashesByCampaign = battles
            .Where(record => record.Crashed && !string.IsNullOrWhiteSpace(record.CampaignId))
            .GroupBy(record => record.CampaignId, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.Count(), StringComparer.Ordinal);
        var unrepresentedCampaignCrashCount = campaigns.Sum(campaign => Math.Max(
            0,
            campaign.CrashCount - (battleCrashesByCampaign.TryGetValue(campaign.CampaignId, out var represented)
                ? represented
                : 0)));
        Add(
            metrics,
            "crash_count",
            battleCrashCount + unrepresentedCampaignCrashCount,
            battles.Count + campaigns.Count,
            "deduplicated battle and campaign crash flags");
        Add(metrics, "softlock_count", battles.Count(record => record.Softlocked) + campaigns.Sum(record => record.SoftlockCount), campaigns.Count, "battle/campaign softlock flags");
        Add(metrics, "nan_or_infinite_state_count", battles.Count(record => record.ContainsNonFinite), battles.Count, "non-finite state flags");
        Add(metrics, "illegal_negative_state_count", battles.Count(record => record.IllegalNegativeState), battles.Count, "illegal negative state flags");
        Add(metrics, "non_terminating_state_count", battles.Count(record => record.NonTerminating), battles.Count, "max-step forced termination flags");
    }

    private static void AddCampaignMetrics(
        IDictionary<string, MetricValue> metrics,
        IReadOnlyList<CampaignMetricRecord> campaigns)
    {
        var eligibleRuns = campaigns
            .Where(record => !record.Truncated && record.CrashCount == 0 && record.SoftlockCount == 0)
            .ToArray();
        if (eligibleRuns.Length == 0)
        {
            return;
        }

        var seedCount = eligibleRuns.Select(record => record.Seed).Distinct().Count();
        Add(metrics, "campaign_seed_count", seedCount, eligibleRuns.Length, "distinct non-truncated campaign seeds");
        var competent = eligibleRuns.Where(record => record.PolicyId.StartsWith("competent", StringComparison.Ordinal)).ToArray();
        var greedy = eligibleRuns.Where(record => record.PolicyId.StartsWith("greedy", StringComparison.Ordinal)).ToArray();
        if (competent.Length > 0)
        {
            Add(metrics, "competent_completion_rate", CompletionRate(competent), competent.Length, "competent policy campaigns");
        }

        if (greedy.Length > 0)
        {
            Add(metrics, "greedy_completion_rate", CompletionRate(greedy), greedy.Length, "greedy policy campaigns");
        }

        if (competent.Length > 0 && greedy.Length > 0)
        {
            var greedyBySeed = greedy.GroupBy(record => record.Seed)
                .ToDictionary(group => group.Key, group => group.Average(record => record.Completed ? 1d : 0d));
            var pairedDifferences = competent.GroupBy(record => record.Seed)
                .Where(group => greedyBySeed.ContainsKey(group.Key))
                .Select(group => group.Average(record => record.Completed ? 1d : 0d) - greedyBySeed[group.Key])
                .ToArray();
            if (pairedDifferences.Length > 0)
            {
                var gap = pairedDifferences.Average();
                Add(metrics, "competent_minus_greedy_completion_rate", gap, pairedDifferences.Length, "paired seed completion-rate difference");
                if (pairedDifferences.Length >= 2)
                {
                    var sampleVariance = pairedDifferences.Sum(value => (value - gap) * (value - gap)) / (pairedDifferences.Length - 1);
                    var standardError = Math.Sqrt(sampleVariance / pairedDifferences.Length);
                    Add(metrics, "competent_minus_greedy_ci95_lower", gap - 1.96d * standardError, pairedDifferences.Length, "paired normal-approximation difference lower bound");
                }
            }
        }

        var competentPolicyCount = competent.Select(record => record.PolicyId).Distinct(StringComparer.Ordinal).Count();
        if (competentPolicyCount >= 2)
        {
            var seedGroups = competent.GroupBy(record => record.Seed).ToArray();
            Add(metrics, "ensemble_solvability_rate", (double)seedGroups.Count(group => group.Any(record => record.Completed)) / seedGroups.Length, seedGroups.Length, "any competent policy completes per seed");
        }

        var campaignBattleCount = eligibleRuns.Sum(record => record.BattleCount);
        if (campaignBattleCount > 0)
        {
            Add(metrics, "forced_timeout_rate", (double)eligibleRuns.Sum(record => record.ForcedTimeoutCount) / campaignBattleCount, campaignBattleCount, "forced timeouts per campaign battle");
        }

        var decisionCampaigns = eligibleRuns.Where(record => record.DecisionMetricsAvailable).ToArray();
        var importantChoices = decisionCampaigns.Sum(record => record.ImportantDecisionCount);
        if (importantChoices > 0)
        {
            Add(metrics, "important_choice_near_best_alternative_rate", (double)decisionCampaigns.Sum(record => record.NearBestAlternativeDecisionCount) / importantChoices, importantChoices, "paired decision rollouts");
            Add(metrics, "important_choice_high_leverage_rate", (double)decisionCampaigns.Sum(record => record.HighLeverageDecisionCount) / importantChoices, importantChoices, "paired decision rollouts");
        }
    }

    private static void AddBuildMetrics(
        IDictionary<string, MetricValue> metrics,
        IReadOnlyList<BattleMetricRecord> battles,
        IReadOnlyList<CampaignMetricRecord> campaigns)
    {
        var valid = battles.Where(record => !record.Crashed && !record.Softlocked && !string.IsNullOrWhiteSpace(record.BuildFamilyId)).ToArray();
        var familyRates = valid.GroupBy(record => record.BuildFamilyId, StringComparer.Ordinal)
            .Select(group => new
            {
                Id = group.Key,
                Rate = group.GroupBy(record => record.OpponentFamilyId, StringComparer.Ordinal)
                    .Select(cell => cell.Count(record => record.WinnerSide == "ally") / (double)cell.Count())
                    .Average(),
                Count = group.Count(),
            })
            .OrderBy(value => value.Id, StringComparer.Ordinal)
            .ToArray();
        if (familyRates.Length > 0)
        {
            var viable = familyRates.Where(value => value.Rate is >= 0.42d and <= 0.58d).ToArray();
            Add(metrics, "viable_doctrine_family_count", viable.Count(value => value.Id.StartsWith("doctrine:", StringComparison.Ordinal)), valid.Length, "families within 42-58 percent");
            Add(metrics, "viable_non_doctrine_or_hybrid_family_count", viable.Count(value => !value.Id.StartsWith("doctrine:", StringComparison.Ordinal)), valid.Length, "families within 42-58 percent");
            Add(metrics, "family_adjusted_win_rate_min", familyRates.Min(value => value.Rate), valid.Length, "observed family minimum");
            Add(metrics, "family_adjusted_win_rate_max", familyRates.Max(value => value.Rate), valid.Length, "observed family maximum");
        }

        var doctrineSubbuildCounts = valid
            .Where(record => record.BuildFamilyId.StartsWith("doctrine:", StringComparison.Ordinal))
            .GroupBy(record => record.BuildFamilyId, StringComparer.Ordinal)
            .Select(doctrine => doctrine
                .GroupBy(SubbuildKey, StringComparer.Ordinal)
                .Count(subbuild =>
                {
                    var adjustedRate = subbuild
                        .GroupBy(record => record.OpponentFamilyId, StringComparer.Ordinal)
                        .Select(cell => cell.Count(record => record.WinnerSide == "ally") / (double)cell.Count())
                        .Average();
                    return adjustedRate is >= 0.42d and <= 0.58d;
                }))
            .ToArray();
        if (doctrineSubbuildCounts.Length > 0)
        {
            Add(
                metrics,
                "min_viable_subbuild_count_per_doctrine",
                doctrineSubbuildCounts.Min(),
                valid.Length,
                "distinct viable build-component and formation subbuilds");
        }

        var matchups = valid.Where(record => !string.IsNullOrWhiteSpace(record.OpponentFamilyId))
            .GroupBy(record => $"{record.BuildFamilyId}|{record.OpponentFamilyId}|{record.IntentionalHardCounter}", StringComparer.Ordinal)
            .Select(group => new
            {
                Hard = group.First().IntentionalHardCounter,
                Rate = group.Count(record => record.WinnerSide == "ally") / (double)group.Count(),
            })
            .ToArray();
        AddMatchupRange(metrics, matchups.Where(value => !value.Hard).Select(value => value.Rate).ToArray(), "non_hard_matchup_win_rate");
        AddMatchupRange(metrics, matchups.Where(value => value.Hard).Select(value => value.Rate).ToArray(), "hard_counter_matchup_win_rate");

        var successfulFamilies = campaigns
            .Where(record => record.Completed && !string.IsNullOrWhiteSpace(record.MacroFamilyId))
            .GroupBy(record => record.MacroFamilyId, StringComparer.Ordinal)
            .Select(group => group.Count())
            .OrderByDescending(count => count)
            .ToArray();
        if (successfulFamilies.Length > 0)
        {
            var total = successfulFamilies.Sum();
            var shares = successfulFamilies.Select(count => count / (double)total).ToArray();
            var entropy = -shares.Sum(share => share * Math.Log(share));
            Add(metrics, "effective_build_diversity_neff", Math.Exp(entropy), total, "successful campaign macro-family entropy");
            Add(metrics, "largest_family_share", shares[0], total, "successful campaign macro-family share");
            Add(metrics, "top_three_family_share", shares.Take(3).Sum(), total, "successful campaign macro-family share");
        }
    }

    private static void AddFormationAndSpectatorMetrics(
        IDictionary<string, MetricValue> metrics,
        H100GateSpec spec,
        IReadOnlyList<BattleMetricRecord> battles)
    {
        var formation = battles.Where(record => record.FormationWinRateLeverage.HasValue)
            .Select(record => (double)record.FormationWinRateLeverage!.Value)
            .OrderBy(value => value)
            .ToArray();
        if (formation.Length > 0)
        {
            Add(metrics, "formation_leverage_median", Percentile(formation, 0.5d), formation.Length, "paired formation rollouts");
            Add(metrics, "formation_leverage_p90", Percentile(formation, 0.9d), formation.Length, "paired formation rollouts");
        }

        var sensitive = battles.Where(record => record.FormationSensitive == true && record.FormationWinRateLeverage.HasValue)
            .Select(record => (double)record.FormationWinRateLeverage!.Value)
            .OrderBy(value => value)
            .ToArray();
        if (sensitive.Length > 0)
        {
            Add(metrics, "formation_sensitive_leverage_median", Percentile(sensitive, 0.5d), sensitive.Length, "formation-sensitive paired rollouts");
        }

        var defaultOptimal = battles.Where(record => record.DefaultFormationWasOptimal.HasValue).ToArray();
        if (defaultOptimal.Length > 0)
        {
            Add(metrics, "default_formation_optimal_rate", defaultOptimal.Count(record => record.DefaultFormationWasOptimal == true) / (double)defaultOptimal.Length, defaultOptimal.Length, "paired formation rollouts");
        }

        var valid = battles.Where(record => !record.Crashed && !record.Softlocked && record.DurationSeconds >= 0f).ToArray();
        if (valid.Length == 0)
        {
            return;
        }

        var target = spec.TargetBattleSeconds;
        var durations = valid.Select(record => (double)record.DurationSeconds).OrderBy(value => value).ToArray();
        Add(metrics, "battle_duration_median_target_ratio", Percentile(durations, 0.5d) / target, valid.Length, "battle duration median divided by T");
        Add(metrics, "battle_duration_within_0_5_1_6_target_rate", valid.Count(record => record.DurationSeconds >= 0.5f * target && record.DurationSeconds <= 1.6f * target) / (double)valid.Length, valid.Length, "battle duration band");
        Add(metrics, "battle_duration_over_2_target_rate", valid.Count(record => record.DurationSeconds > 2f * target) / (double)valid.Length, valid.Length, "battle duration tail");
        Add(metrics, "battle_timeout_rate", valid.Count(record => record.Timeout) / (double)valid.Length, valid.Length, "battle timeout flags");
        Add(metrics, "battle_stomp_rate", valid.Count(record => record.Stomp) / (double)valid.Length, valid.Length, "duration below 0.35T");
    }

    private static void AddDepthMetrics(IDictionary<string, MetricValue> metrics, IReadOnlyList<BattleMetricRecord> battles)
    {
        var valid = battles.Where(record => !record.Crashed && !record.Softlocked).ToArray();
        var eligibleCount = valid.Sum(record => record.EligibleDepthRuleIds.Count);
        if (eligibleCount > 0)
        {
            var firedEligible = valid.Sum(record => record.FiredDepthRuleIds.Intersect(record.EligibleDepthRuleIds, StringComparer.Ordinal).Count());
            var causalEligible = valid.Sum(record => record.CausalDepthRuleIds.Intersect(record.EligibleDepthRuleIds, StringComparer.Ordinal).Count());
            Add(metrics, "representative_grammar_fire_rate", firedEligible / (double)eligibleCount, eligibleCount, "fired eligible rule ids");
            Add(metrics, "representative_grammar_causal_impact_rate", causalEligible / (double)eligibleCount, eligibleCount, "causal ablation rule ids");
        }

        var nontrivial = valid.Where(record => !record.Stomp && !record.Timeout).ToArray();
        if (nontrivial.Length > 0)
        {
            Add(metrics, "nontrivial_battle_causal_salient_event_rate", nontrivial.Count(record => record.CausalSalientEventCount > 0) / (double)nontrivial.Length, nontrivial.Length, "causal salient events");
            Add(metrics, "nontrivial_battle_two_grammar_rate", nontrivial.Count(record => record.FiredDepthRuleIds.Count >= 2) / (double)nontrivial.Length, nontrivial.Length, "distinct fired rule ids");
            Add(metrics, "nontrivial_battle_three_grammar_rate", nontrivial.Count(record => record.FiredDepthRuleIds.Count >= 3) / (double)nontrivial.Length, nontrivial.Length, "distinct fired rule ids");
        }
    }

    private static GateReport.ThresholdResult Evaluate(
        H100GateSpec.ThresholdDefinition threshold,
        IReadOnlyDictionary<string, MetricValue> metrics)
    {
        if (!metrics.TryGetValue(threshold.MetricId, out var observed))
        {
            return new GateReport.ThresholdResult(
                threshold.MetricId, threshold.Operator, threshold.Value, threshold.MinValue, threshold.MaxValue,
                threshold.Unit, false, null, 0, false, "metric unavailable", threshold.Note);
        }

        var pass = threshold.Operator switch
        {
            "eq" => Math.Abs(observed.Value - threshold.Value!.Value) <= 1e-9d,
            "gte" => observed.Value >= threshold.Value!.Value,
            "lte" => observed.Value <= threshold.Value!.Value,
            "lt" => observed.Value < threshold.Value!.Value,
            "range_inclusive" => observed.Value >= threshold.MinValue!.Value && observed.Value <= threshold.MaxValue!.Value,
            _ => false,
        };
        return new GateReport.ThresholdResult(
            threshold.MetricId, threshold.Operator, threshold.Value, threshold.MinValue, threshold.MaxValue,
            threshold.Unit, true, observed.Value, observed.SampleCount, pass, observed.Evidence, threshold.Note);
    }

    private static void AddMatchupRange(IDictionary<string, MetricValue> metrics, IReadOnlyList<double> rates, string prefix)
    {
        if (rates.Count == 0)
        {
            return;
        }

        Add(metrics, $"{prefix}_min", rates.Min(), rates.Count, "observed matchup cells");
        Add(metrics, $"{prefix}_max", rates.Max(), rates.Count, "observed matchup cells");
    }

    private static string SubbuildKey(BattleMetricRecord record)
    {
        var components = string.Join(
            ";",
            MetricCount.Normalize(record.AllyBuildComponentCounts)
                .Select(component => $"{component.Id}={component.Count.ToString(CultureInfo.InvariantCulture)}"));
        return $"{record.AllyFormationId}|{components}";
    }

    private static double CompletionRate(IReadOnlyCollection<CampaignMetricRecord> records)
        => records.Count(record => record.Completed) / (double)records.Count;

    private static double Percentile(IReadOnlyList<double> sorted, double p)
    {
        if (sorted.Count == 0)
        {
            return 0d;
        }

        var position = Math.Clamp(p, 0d, 1d) * (sorted.Count - 1);
        var lower = (int)Math.Floor(position);
        var upper = (int)Math.Ceiling(position);
        if (lower == upper)
        {
            return sorted[lower];
        }

        return sorted[lower] + (sorted[upper] - sorted[lower]) * (position - lower);
    }

    private static void Add(IDictionary<string, MetricValue> metrics, string id, double value, int samples, string evidence)
    {
        metrics[id] = new MetricValue(value, samples, evidence);
    }
}
