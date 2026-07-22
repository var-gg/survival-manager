using SM.Combat.Model;
using SM.Core.Contracts;

internal sealed class CounterplayInstrumentationAccumulator
{
    private const string PrimaryPanel = "ranged";
    private readonly List<CounterplayBattleObservation> _observations = new();

    internal void Add(CounterplayBattleObservation observation)
    {
        _observations.Add(observation);
    }

    internal object BuildReport()
    {
        var classified = _observations
            .Where(observation => string.Equals(
                observation.DiveOutcome,
                DiveFailureBattleObserver.NeverSelected,
                StringComparison.Ordinal))
            .Select(observation => new ClassifiedNonselection(observation, ClassifyNonselection(observation)))
            .ToArray();
        var primary = _observations
            .Where(observation => string.Equals(observation.Panel, PrimaryPanel, StringComparison.Ordinal))
            .ToArray();

        return new
        {
            schema_version = "counterplay-stage0-instrumentation-v1",
            scope = "P-ASSASSIN-C credible probe, 128 paired seeds per reference panel; primary aggregate is vs ranged 3R/1T.",
            nonselection_causes = classified
                .GroupBy(value => new { value.Observation.Panel, value.Cause })
                .OrderBy(group => group.Key.Panel, StringComparer.Ordinal)
                .ThenByDescending(group => group.Count())
                .ThenBy(group => group.Key.Cause, StringComparer.Ordinal)
                .Select(group => new
                {
                    cause = group.Key.Cause,
                    count = group.Count(),
                    pct_of_panel_battles = Percent(
                        group.Count(),
                        _observations.Count(value => string.Equals(value.Panel, group.Key.Panel, StringComparison.Ordinal))),
                    pct_of_never_selected = Percent(
                        group.Count(),
                        classified.Count(value => string.Equals(value.Observation.Panel, group.Key.Panel, StringComparison.Ordinal))),
                    panel = group.Key.Panel,
                    raw_evidence = BuildCauseEvidence(group.Select(value => value.Observation).ToArray()),
                })
                .ToArray(),
            backline_formation_check = BuildFormationCheck(),
            charge_lifecycle = BuildChargeLifecycle(),
            knockback_casts = BuildKnockbackReport(_observations),
            ranged_free_fire = BuildRangedFreeFire(primary),
            contact_to_kill = BuildContactToKill(primary),
            healing_quality = BuildHealingQuality(primary),
            damage_share_until_first_death = BuildDamageShare(primary),
            raw_tick_trace = _observations
                .OrderBy(observation => observation.Panel, StringComparer.Ordinal)
                .ThenBy(observation => observation.BattleSeed)
                .Select(observation => new
                {
                    panel = observation.Panel,
                    battle_seed = observation.BattleSeed,
                    dive_outcome = observation.DiveOutcome,
                    classified_nonselection_cause = string.Equals(
                        observation.DiveOutcome,
                        DiveFailureBattleObserver.NeverSelected,
                        StringComparison.Ordinal)
                        ? ClassifyNonselection(observation)
                        : string.Empty,
                    dive_predicate_ticks = observation.DiveIntentEvaluations,
                    dive_hard_aborts = observation.DiveHardAborts,
                    target_selection_calls = observation.TargetSelections,
                    tactic_evaluations = observation.TacticEvaluations,
                    intent_overrides = observation.IntentOverrides,
                })
                .ToArray(),
        };
    }

    private object BuildFormationCheck()
    {
        var rangers = _observations
            .SelectMany(observation => observation.RuntimeFormation.Select(value => new { observation.Panel, Value = value }))
            .Where(value => value.Value.SideIndex == 0
                            && string.Equals(value.Value.ClassId, "ranger", StringComparison.Ordinal))
            .ToArray();
        return new
        {
            rangers_are_backline = rangers.Length > 0
                                   && rangers.All(value => value.Value.FormationLine == FormationLine.Backline),
            runtime_values = rangers
                .Select(value => new
                {
                    panel = value.Panel,
                    value.Value.ArchetypeId,
                    class_id = value.Value.ClassId,
                    formation_line = value.Value.FormationLine.ToString(),
                })
                .Distinct()
                .OrderBy(value => value.panel, StringComparer.Ordinal)
                .ThenBy(value => value.ArchetypeId, StringComparer.Ordinal)
                .ToArray(),
            implication_for_escort_barrier = rangers.Length > 0
                                             && rangers.All(value => value.Value.FormationLine == FormationLine.Backline)
                ? "The runtime FormationLine.Backline half of ApplyBattleBootstrap's ranger/mystic escort-barrier predicate is satisfied; formation labeling does not silently disable it."
                : "At least one runtime ranger is not Backline, so ApplyBattleBootstrap's escort-barrier predicate can be disabled for that unit.",
        };
    }

    private object BuildChargeLifecycle()
    {
        var lifecycle = _observations
            .SelectMany(observation => observation.DisplacementLifecycle.Select(value => new { observation.Panel, observation.BattleSeed, Event = value }))
            .Where(value => string.Equals(value.Event.SkillId, CounterplayInstrumentationObserver.ChargeSkillId, StringComparison.Ordinal))
            .ToArray();
        return new
        {
            equipped_battles = _observations.Count(observation => observation.ChargeEquipped),
            candidate_or_selected_events = lifecycle.Count(value => value.Event.Stage == DisplacementLifecycleStage.Selected),
            cast_starts = lifecycle.Count(value => value.Event.Stage == DisplacementLifecycleStage.CastStarted),
            arrivals = lifecycle.Count(value => value.Event.Stage == DisplacementLifecycleStage.Resolved
                                                 && value.Event.ActorDisplacement > 0f),
            aborts = lifecycle.Count(value => value.Event.Stage == DisplacementLifecycleStage.Aborted),
            events = lifecycle,
        };
    }

    private static object BuildKnockbackReport(IReadOnlyList<CounterplayBattleObservation> observations)
    {
        var casts = observations
            .SelectMany(observation => BuildDisplacementCasts(observation, CounterplayInstrumentationObserver.KnockbackSkillId))
            .ToArray();
        return new
        {
            count = casts.Length,
            mean_cast_distance = MeanOrNull(casts.Select(value => value.CastDistance)),
            median_elapsed = Quantile(casts.Where(value => value.ElapsedSeconds.HasValue).Select(value => value.ElapsedSeconds!.Value), 0.5),
            mean_displacement = MeanOrNull(casts.Select(value => value.ResultingDisplacement)),
            per_panel = casts
                .GroupBy(value => value.Panel, StringComparer.Ordinal)
                .OrderBy(group => group.Key, StringComparer.Ordinal)
                .Select(group => new
                {
                    panel = group.Key,
                    count = group.Count(),
                    mean_cast_distance = MeanOrNull(group.Select(value => value.CastDistance)),
                    median_elapsed = Quantile(group.Where(value => value.ElapsedSeconds.HasValue).Select(value => value.ElapsedSeconds!.Value), 0.5),
                    mean_displacement = MeanOrNull(group.Select(value => value.ResultingDisplacement)),
                })
                .ToArray(),
            casts,
        };
    }

    private static IEnumerable<DisplacementCastReport> BuildDisplacementCasts(
        CounterplayBattleObservation observation,
        string skillId)
    {
        var events = observation.DisplacementLifecycle
            .Where(value => string.Equals(value.SkillId, skillId, StringComparison.Ordinal))
            .ToArray();
        foreach (var started in events.Where(value => value.Stage == DisplacementLifecycleStage.CastStarted))
        {
            var terminal = events.FirstOrDefault(value =>
                value.ActionInstanceId == started.ActionInstanceId
                && value.Stage is DisplacementLifecycleStage.Resolved or DisplacementLifecycleStage.Aborted);
            yield return new DisplacementCastReport(
                observation.Panel,
                observation.BattleSeed,
                started.ActorId,
                started.ActorArchetypeId,
                started.TargetId,
                started.TargetArchetypeId,
                started.ActionInstanceId,
                started.EdgeDistance,
                terminal == null ? null : terminal.TimeSeconds - started.TimeSeconds,
                terminal?.TargetDisplacement ?? 0f,
                terminal?.AbortReason ?? "missing_terminal_event");
        }
    }

    private static object BuildRangedFreeFire(IReadOnlyList<CounterplayBattleObservation> observations)
    {
        var samples = observations.SelectMany(observation => observation.RangedFreeFire).ToArray();
        var observedDamage = samples
            .Where(sample => sample.TimeToFirstDamageSeconds.HasValue)
            .Select(sample => sample.TimeToFirstDamageSeconds!.Value)
            .ToArray();
        return new
        {
            scope = "Player-side runtime ClassId=ranger units in P-ASSASSIN-C_vs_ranged; attacks are positive hostile BasicAttackResolved/SkillCastResolved before that unit's first positive DamageApplied received.",
            attacks_before_first_contact = new
            {
                mean = MeanOrNull(samples.Select(sample => (double)sample.AttacksBeforeFirstDamage)),
                p50 = Quantile(samples.Select(sample => (double)sample.AttacksBeforeFirstDamage), 0.5),
                n = samples.Length,
            },
            time_to_first_damage = new
            {
                mean = MeanOrNull(observedDamage),
                p50 = Quantile(observedDamage, 0.5),
                n = observedDamage.Length,
                censored = samples.Count(sample => sample.DamageCensoredAtBattleEnd),
            },
            per_unit = samples,
        };
    }

    private static object BuildContactToKill(IReadOnlyList<CounterplayBattleObservation> observations)
    {
        var samples = observations
            .SelectMany(observation => observation.ContactToKill)
            .Where(sample => sample.TargetSideIndex == 0)
            .ToArray();
        var killed = samples.Where(sample => sample.ContactToKillSeconds.HasValue).ToArray();
        return new
        {
            definition = "First positive hostile DamageApplied to a player-side runtime Backline target; death is that target's later UnitDied event.",
            n = samples.Length,
            mean_seconds = MeanOrNull(killed.Select(sample => sample.ContactToKillSeconds!.Value)),
            kill_rate = Rate(killed.Length, samples.Length),
            samples,
        };
    }

    private static object BuildHealingQuality(IReadOnlyList<CounterplayBattleObservation> observations)
    {
        var rows = observations
            .SelectMany(observation => observation.HealingApplications.Select(value => new
            {
                observation.Panel,
                observation.BattleSeed,
                Event = value,
            }))
            .ToArray();
        var raw = rows.Sum(value => (double)value.Event.RawAmount);
        var attempted = rows.Sum(value => (double)value.Event.AttemptedAfterModifiers);
        var effective = rows.Sum(value => (double)value.Event.EffectiveAmount);
        var overheal = rows.Sum(value => (double)value.Event.OverhealAmount);
        return new
        {
            raw,
            attempted_after_modifiers = attempted,
            effective,
            overheal_pct = Percent(overheal, attempted),
            definition_source = "UnitSnapshot.HealMeasured reuses UnitSnapshot.Heal's existing healing-taken multiplier and MaxHealth clamp. Raw is the resolved input, attempted_after_modifiers is the post-wound amount, effective is exact HP delta, and overheal is attempted_after_modifiers minus effective.",
            per_healer_per_battle = rows
                .GroupBy(value => new { value.Panel, value.BattleSeed, value.Event.ActorId, value.Event.ActorArchetypeId })
                .OrderBy(group => group.Key.Panel, StringComparer.Ordinal)
                .ThenBy(group => group.Key.BattleSeed)
                .ThenBy(group => group.Key.ActorId, StringComparer.Ordinal)
                .Select(group => new
                {
                    panel = group.Key.Panel,
                    battle_seed = group.Key.BattleSeed,
                    healer_id = group.Key.ActorId,
                    healer_archetype_id = group.Key.ActorArchetypeId,
                    raw = group.Sum(value => (double)value.Event.RawAmount),
                    attempted_after_modifiers = group.Sum(value => (double)value.Event.AttemptedAfterModifiers),
                    effective = group.Sum(value => (double)value.Event.EffectiveAmount),
                    overheal = group.Sum(value => (double)value.Event.OverhealAmount),
                })
                .ToArray(),
        };
    }

    private static object[] BuildDamageShare(IReadOnlyList<CounterplayBattleObservation> observations)
    {
        var samples = observations.SelectMany(observation => observation.DamageShareBeforeFirstDeath).ToArray();
        var total = samples.Sum(sample => sample.DamageTaken);
        return samples
            .GroupBy(sample => sample.Role, StringComparer.Ordinal)
            .OrderBy(group => group.Key, StringComparer.Ordinal)
            .Select(group => (object)new
            {
                role = group.Key,
                share_pct = Percent(group.Sum(sample => sample.DamageTaken), total),
                damage = group.Sum(sample => sample.DamageTaken),
            })
            .ToArray();
    }

    private static string ClassifyNonselection(CounterplayBattleObservation observation)
    {
        var failures = observation.DiveIntentEvaluations
            .Where(value => value.Reason != DiveIntentGateReason.Selected)
            .OrderBy(value => value.StepIndex)
            .ToArray();
        if (failures.Length == 0)
        {
            return "no_dive_predicate_evaluation";
        }

        var scoreCandidates = failures
            .Where(value => value.Reason == DiveIntentGateReason.CandidateScoreBelowEntryThreshold)
            .SelectMany(value => value.Candidates)
            .Where(value => value.PostureEligible && value.TotalScore < value.RequiredScore)
            .ToArray();
        if (scoreCandidates.Any(value =>
                (value.ForwardDepthScore < 0 || value.PathDistanceScore < 0)
                && value.TotalScore - value.ForwardDepthScore - value.PathDistanceScore >= value.RequiredScore))
        {
            return "dive_geometry_hard_veto";
        }

        if (scoreCandidates.Any(value => value.ProtectorScore < 0
                                         && value.TotalScore - value.ProtectorScore >= value.RequiredScore))
        {
            return "frontline_protector_score_penalty";
        }

        if (scoreCandidates.Length > 0)
        {
            return "dive_candidate_score_below_entry_threshold";
        }

        var first = failures[0].Reason;
        return first switch
        {
            DiveIntentGateReason.HoldBruiserTag => "duelist_hold_bruiser_tag",
            DiveIntentGateReason.AttackRangeAboveMeleeThreshold => "actor_not_melee_eligible",
            DiveIntentGateReason.PostureDisallowsDive => "posture_disallows_dive",
            DiveIntentGateReason.HealthBelowEntryThreshold => "actor_health_below_dive_entry_threshold",
            DiveIntentGateReason.SupportProxyMissing => "dive_support_proxy_missing",
            DiveIntentGateReason.TooManyNearbyEnemies => "dive_nearby_enemy_limit_exceeded",
            DiveIntentGateReason.NoRuntimeBacklineCandidate => "no_runtime_backline_candidate",
            DiveIntentGateReason.PostureFilteredAllCandidates => "posture_filtered_all_backline_candidates",
            DiveIntentGateReason.TeamDiveSlotUnavailable => "team_dive_slot_unavailable",
            DiveIntentGateReason.DistinctTargetUnavailable => "distinct_dive_target_unavailable",
            DiveIntentGateReason.ContinueScoreBelowThreshold => "dive_continue_score_below_threshold",
            _ => first.ToString(),
        };
    }

    private static object BuildCauseEvidence(IReadOnlyList<CounterplayBattleObservation> observations)
    {
        var diveTicks = observations.SelectMany(value => value.DiveIntentEvaluations).ToArray();
        var targetCalls = observations.SelectMany(value => value.TargetSelections).ToArray();
        var backlineCandidates = targetCalls
            .SelectMany(value => value.Candidates)
            .Where(value => value.FormationLine == FormationLine.Backline
                            && value.ClassId is "ranger" or "mystic")
            .ToArray();
        var scoreCandidates = diveTicks.SelectMany(value => value.Candidates).Where(value => value.PostureEligible).ToArray();
        return new
        {
            battles = observations.Count,
            dive_predicate_evaluations = diveTicks.Length,
            gate_reason_tick_counts = diveTicks
                .GroupBy(value => value.Reason)
                .OrderByDescending(group => group.Count())
                .ThenBy(group => group.Key)
                .ToDictionary(group => group.Key.ToString(), group => group.Count(), StringComparer.Ordinal),
            runtime_backline_candidate_ticks = scoreCandidates.Length,
            frontline_protector_candidate_ticks = scoreCandidates.Count(value => value.HasFrontlineProtector),
            protector_penalty_flips_eligibility_ticks = scoreCandidates.Count(value =>
                value.ProtectorScore < 0
                && value.TotalScore < value.RequiredScore
                && value.TotalScore - value.ProtectorScore >= value.RequiredScore),
            geometry_penalty_flips_eligibility_ticks = scoreCandidates.Count(value =>
                (value.ForwardDepthScore < 0 || value.PathDistanceScore < 0)
                && value.TotalScore < value.RequiredScore
                && value.TotalScore - value.ForwardDepthScore - value.PathDistanceScore >= value.RequiredScore),
            both_geometry_hard_veto_ticks = scoreCandidates.Count(value =>
                value.ForwardDepthScore < 0 && value.PathDistanceScore < 0),
            forward_depth_only_hard_veto_ticks = scoreCandidates.Count(value =>
                value.ForwardDepthScore < 0 && value.PathDistanceScore == 0),
            path_distance_only_hard_veto_ticks = scoreCandidates.Count(value =>
                value.ForwardDepthScore == 0 && value.PathDistanceScore < 0),
            observed_geometry_limits = diveTicks
                .Select(value => new { value.MaxForwardDepth, value.MaxPathDistance })
                .Distinct()
                .ToArray(),
            observed_forward_depth_range = scoreCandidates.Length == 0
                ? null
                : new { min = scoreCandidates.Min(value => value.ForwardDepth), max = scoreCandidates.Max(value => value.ForwardDepth) },
            observed_path_distance_range = scoreCandidates.Length == 0
                ? null
                : new { min = scoreCandidates.Min(value => value.PathDistance), max = scoreCandidates.Max(value => value.PathDistance) },
            observed_score_range = scoreCandidates.Length == 0
                ? null
                : new { min = scoreCandidates.Min(value => value.TotalScore), max = scoreCandidates.Max(value => value.TotalScore) },
            required_scores = scoreCandidates.Select(value => value.RequiredScore).Distinct().OrderBy(value => value).ToArray(),
            stable_target_holds = observations.SelectMany(value => value.TacticEvaluations).Count(value =>
                value.StableTargetDisposition is StableTargetDisposition.HeldBySwitchLock
                    or StableTargetDisposition.HeldUntilReevaluation
                    or StableTargetDisposition.HeldByDiveIntent),
            backline_filter_rejections = backlineCandidates
                .GroupBy(value => value.InitialRejection)
                .OrderByDescending(group => group.Count())
                .ThenBy(group => group.Key)
                .ToDictionary(group => group.Key.ToString(), group => group.Count(), StringComparer.Ordinal),
            backline_lost_to_frontline_score = targetCalls.Count(call =>
                call.Candidates.Any(candidate => candidate.FormationLine == FormationLine.Backline
                                                 && candidate.InitialRejection == TargetCandidateRejectionReason.None)
                && call.Candidates.Any(candidate => string.Equals(candidate.TargetId, call.FinalSelectedTargetId, StringComparison.Ordinal)
                                                    && candidate.FormationLine != FormationLine.Backline)),
            melee_nearest_overrides = targetCalls.Count(value => value.MeleeNearestOverrideApplied),
            intent_overrides = observations.SelectMany(value => value.IntentOverrides).Count(value => value.OverrideApplied),
            acquire_range_sources = targetCalls
                .GroupBy(value => $"{value.AcquireRangeSource}:{value.ResolvedAcquireRange:0.###}", StringComparer.Ordinal)
                .OrderByDescending(group => group.Count())
                .ThenBy(group => group.Key, StringComparer.Ordinal)
                .ToDictionary(group => group.Key, group => group.Count(), StringComparer.Ordinal),
            representative_score_rows = scoreCandidates
                .Select(value => new
                {
                    target_archetype_id = value.ArchetypeId,
                    formation_line = value.FormationLine.ToString(),
                    value.HasFrontlineProtector,
                    value.FormationLineScore,
                    value.ClassScore,
                    value.LowHealthScore,
                    value.ProtectorScore,
                    value.FocusMarkScore,
                    value.ForwardDepthScore,
                    value.PathDistanceScore,
                    value.TotalScore,
                    value.RequiredScore,
                })
                .Distinct()
                .Take(12)
                .ToArray(),
        };
    }

    private static double Rate(int numerator, int denominator)
        => denominator == 0 ? 0d : numerator / (double)denominator;

    private static double Percent(double numerator, double denominator)
        => denominator <= 0d ? 0d : numerator * 100d / denominator;

    private static double? MeanOrNull(IEnumerable<double> source)
    {
        var values = source.ToArray();
        return values.Length == 0 ? null : values.Average();
    }

    private static double? Quantile(IEnumerable<double> source, double quantile)
    {
        var values = source.OrderBy(value => value).ToArray();
        if (values.Length == 0)
        {
            return null;
        }

        var position = (values.Length - 1) * quantile;
        var lower = (int)Math.Floor(position);
        var upper = (int)Math.Ceiling(position);
        return lower == upper
            ? values[lower]
            : values[lower] + ((values[upper] - values[lower]) * (position - lower));
    }

    private sealed record ClassifiedNonselection(CounterplayBattleObservation Observation, string Cause);

    private sealed record DisplacementCastReport(
        string Panel,
        int BattleSeed,
        string CasterId,
        string CasterArchetypeId,
        string TargetId,
        string TargetArchetypeId,
        long ActionInstanceId,
        double CastDistance,
        double? ElapsedSeconds,
        double ResultingDisplacement,
        string AbortReason);
}
