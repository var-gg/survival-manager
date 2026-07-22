using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using SM.Combat.Model;
using SM.Combat.Services;
using SM.Core.Contracts;
using SM.Meta.Model;
using SM.Meta.Serialization;
using SM.Meta.Services;

internal static class CredibleDeckMatchupRunner
{
    private const string SnapshotRelativePath = "Assets/Resources/_Game/Content/content-snapshot.json";
    private const string DefaultOutputRelativePath = "Temp/G1Rebaseline/part-b-credible.json";
    private const int DefaultSeeds = 128;
    private const int MatchupSeedStart = 62000;
    private const int CredibleHeroLevel = 5;
    private const string AssassinControlProbeId = "P-ASSASSIN-C";
    private const string AssassinBlinkProbeId = "P-ASSASSIN-BLINK-C";

    private static readonly string[] CredibleDiveNodes =
    {
        "passive_duelist_small_02",
        "passive_duelist_small_04",
        "passive_duelist_notable_01",
        "passive_duelist_small_05",
        "passive_duelist_small_07",
        "passive_duelist_notable_04",
    };

    private static readonly string[] CredibleSunderNodes =
    {
        "passive_duelist_small_01",
        "passive_duelist_small_03",
        "passive_duelist_notable_02",
        "passive_duelist_small_10",
        "passive_duelist_small_12",
        "passive_duelist_notable_05",
    };

    private static readonly DeploymentAnchorId[] CredibleAnchors =
    {
        DeploymentAnchorId.FrontCenter,
        DeploymentAnchorId.FrontTop,
        DeploymentAnchorId.BackBottom,
        DeploymentAnchorId.BackCenter,
    };

    private static readonly CredibleProbeSpec[] CredibleProbes =
    {
        new(
            AssassinControlProbeId,
            "assassin-reach",
            new[] { "warden", "slayer", "scout", "priest" },
            CredibleDiveNodes,
            "A vanguard opens space for one committed slayer while a ranger and healer supply credible follow-up."),
        new(
            "P-TANKBREAK-C",
            "tank-break",
            new[] { "warden", "slayer", "marksman", "priest" },
            CredibleSunderNodes,
            "One sunder slayer collapses the screen while a marksman exploits the exposed target behind a vanguard/healer shell."),
    };

    internal static int Run(string repositoryRoot, IReadOnlyList<string> arguments)
    {
        try
        {
            var (seeds, outputRelativePath, openingLockSecondsOverride) = Parse(arguments);
            ContentSnapshotFreshnessGuard.EnsureFresh(repositoryRoot);
            var snapshotPath = Resolve(repositoryRoot, SnapshotRelativePath);
            var content = ContentSnapshotJsonSerializer.Deserialize(File.ReadAllText(snapshotPath));
            if (!content.SkillCatalog.TryGetValue(CounterplayInstrumentationObserver.VeilBreachSkillId, out var authoredVeilBreach))
            {
                throw new InvalidDataException("The exported content snapshot does not contain skill_veil_breach.");
            }

            var openingLockSeconds = openingLockSecondsOverride ?? authoredVeilBreach.OpeningLockSeconds;
            var repeatCooldownSeconds = authoredVeilBreach.BaseCooldownSeconds;
            content = ApplyVeilBreachOpeningLock(content, openingLockSeconds);
            if (BitConverter.SingleToInt32Bits(content.SkillCatalog[CounterplayInstrumentationObserver.VeilBreachSkillId].BaseCooldownSeconds)
                != BitConverter.SingleToInt32Bits(repeatCooldownSeconds))
            {
                throw new InvalidDataException("The opening-lock override changed Veil Breach's repeat cooldown.");
            }
            var statusRules = CombatStatusRuleCompiler.Compile(content);
            var config = SM.Editor.Validation.CampaignBalanceSweepConfig.Default;
            var nodeBudget = PassiveBoardSelectionValidator.ResolveMaxActiveNodeCount(CredibleHeroLevel);
            if (nodeBudget != 6)
            {
                throw new InvalidDataException($"Expected Level {CredibleHeroLevel} passive budget 6, observed {nodeBudget}.");
            }

            foreach (var probe in CredibleProbes)
            {
                ValidatePassiveRoute(content, probe, nodeBudget);
            }

            var playerSquads = config.ReferenceSquads.ToDictionary(
                squad => squad.SquadId,
                squad => DeckMatchupDiagnosticRunner.CompileSquad(
                    content,
                    $"credible.player.{squad.SquadId}",
                    squad.CoreArchetypeIds,
                    DeckMatchupDiagnosticRunner.PlayerAnchors(squad.SquadId),
                    Array.Empty<string>(),
                    equipDuelistBlade: false),
                StringComparer.Ordinal);
            var probeSquads = CredibleProbes.ToDictionary(
                probe => probe.Id,
                probe => DeckMatchupDiagnosticRunner.CompileSquad(
                    content,
                    $"credible.enemy.{probe.Id.ToLowerInvariant()}",
                    probe.ArchetypeIds,
                    CredibleAnchors,
                    probe.PassiveNodes,
                    equipDuelistBlade: true,
                    heroLevel: CredibleHeroLevel),
                StringComparer.Ordinal);

            var assassinProbe = CredibleProbes.Single(probe =>
                string.Equals(probe.Id, AssassinControlProbeId, StringComparison.Ordinal));
            var blinkAssassinSquad = DeckMatchupDiagnosticRunner.CompileSquad(
                content,
                "credible.enemy.p-assassin-c",
                assassinProbe.ArchetypeIds,
                CredibleAnchors,
                assassinProbe.PassiveNodes,
                equipDuelistBlade: true,
                heroLevel: CredibleHeroLevel,
                duelistFlexActiveSkillId: CounterplayInstrumentationObserver.VeilBreachSkillId);
            ValidateBlinkArmSlots(probeSquads[AssassinControlProbeId], blinkAssassinSquad);

            var rows = new List<object>();
            var firstDeathRows = new List<object>();
            var controlDiveObservations = new List<DiveFailureObservation>();
            var blinkDiveObservations = new List<DiveFailureObservation>();
            var counterplayInstrumentation = new CounterplayInstrumentationAccumulator();
            var veilBreachMeasurements = new List<VeilBreachBattleMeasurement>();
            Dictionary<string, CredibleMatchupObservation>? controlCells = null;
            foreach (var probe in CredibleProbes)
            {
                var compiled = probeSquads[probe.Id];
                ValidateCompiledLever(probe, compiled);
                var cells = new Dictionary<string, CredibleMatchupObservation>(StringComparer.Ordinal);
                var observeDive = string.Equals(probe.Id, AssassinControlProbeId, StringComparison.Ordinal);
                foreach (var reference in config.ReferenceSquads)
                {
                    cells[reference.SquadId] = ObserveMatchup(
                        playerSquads[reference.SquadId],
                        compiled,
                        statusRules,
                        seeds,
                        MatchupSeedStart,
                        reference.SquadId,
                        observeDive ? controlDiveObservations : null,
                        counterplayInstrumentation: null,
                        veilBreachMeasurements: null);
                }

                if (observeDive)
                {
                    controlCells = cells;
                }

                rows.Add(new
                {
                    probe_deck = probe.Id,
                    vs_ranged_3r1t = CellReport(cells["ranged"]),
                    vs_frontline = CellReport(cells["frontline"]),
                    vs_mixed = CellReport(cells["mixed"]),
                    seeds,
                });
                firstDeathRows.AddRange(config.ReferenceSquads.Select(reference => new
                {
                    panel = $"{probe.Id}_vs_{reference.SquadId}",
                    vanguard_first_death_rate = cells[reference.SquadId].VanguardFellFirst / (double)seeds,
                    battles_with_player_vanguard = cells[reference.SquadId].BattlesWithVanguard,
                }));
            }

            var blinkCells = new Dictionary<string, CredibleMatchupObservation>(StringComparer.Ordinal);
            foreach (var reference in config.ReferenceSquads)
            {
                blinkCells[reference.SquadId] = ObserveMatchup(
                    playerSquads[reference.SquadId],
                    blinkAssassinSquad,
                    statusRules,
                    seeds,
                    MatchupSeedStart,
                    reference.SquadId,
                    blinkDiveObservations,
                    counterplayInstrumentation,
                    veilBreachMeasurements);
            }

            rows.Add(new
            {
                probe_deck = AssassinBlinkProbeId,
                vs_ranged_3r1t = CellReport(blinkCells["ranged"]),
                vs_frontline = CellReport(blinkCells["frontline"]),
                vs_mixed = CellReport(blinkCells["mixed"]),
                seeds,
            });
            firstDeathRows.AddRange(config.ReferenceSquads.Select(reference => new
            {
                panel = $"{AssassinBlinkProbeId}_vs_{reference.SquadId}",
                vanguard_first_death_rate = blinkCells[reference.SquadId].VanguardFellFirst / (double)seeds,
                battles_with_player_vanguard = blinkCells[reference.SquadId].BattlesWithVanguard,
            }));

            if (controlCells == null)
            {
                throw new InvalidOperationException("The P-ASSASSIN-C control arm was not measured.");
            }

            var observerDeterminism = VerifyObserverDeterminism(
                playerSquads["ranged"],
                blinkAssassinSquad,
                statusRules);
            if (!observerDeterminism.Identical)
            {
                throw new InvalidOperationException("Dive observation changed canonical battle output.");
            }

            var report = new
            {
                schema_version = "credible-deck-matchup-v1",
                method = new
                {
                    reference_build = "Unchanged CampaignBalanceSweepConfig P20 reference compositions: four base-grade heroes, zero equipment, zero passive growth.",
                    credible_build = "Both assassin arms use the same four base-grade shipped heroes, anchors, common tier-0 slayer blade, and Level-5 six-node legal duelist route. The blink arm differs only by an explicit player-style FlexActive loadout selection.",
                    credible_level = CredibleHeroLevel,
                    credible_node_budget = nodeBudget,
                    level_basis = "Level 5 is reachable after 18 wins at 50 XP per win and is available during chapter 3; ResolveMaxActiveNodeCount(5) is 6.",
                    anchor_rule = "One hero per unique legal anchor; FrontCenter, FrontTop, BackBottom, BackCenter; no overlap.",
                    paired_seed_start = MatchupSeedStart,
                    seeds_per_matchup = seeds,
                    veil_breach_opening_lock_seconds = openingLockSeconds,
                    veil_breach_repeat_cooldown_seconds = repeatCooldownSeconds,
                    contact_definition = "First positive DamageApplied telemetry from the authored diver to an eligible player backline target (FormationLine.Backline and class ranger or mystic).",
                    in_range_definition = "MovementResolver.IsInActionRange against the diver basic-attack range; gate opened only when an attack/skill start targets that eligible backline unit.",
                },
                credible_probes = CredibleProbes.Select(probe => new
                {
                    id = probe.Id,
                    composition = probe.ArchetypeIds,
                    anchors = CredibleAnchors.Select(value => value.ToString()).ToArray(),
                    lever = probe.Lever,
                    level_used = CredibleHeroLevel,
                    node_budget_used = nodeBudget,
                    passive_nodes = probe.PassiveNodes,
                    equipment = new[] { "item_slayer_blade (rarity tier 0, sole slayer only)" },
                    rationale = probe.Rationale,
                    compiled_lever_evidence = BuildCompiledLeverEvidence(probe, probeSquads[probe.Id]),
                }).ToArray(),
                blink_probe = new
                {
                    id = AssassinBlinkProbeId,
                    control_probe = AssassinControlProbeId,
                    equip_method = "A SkillInstanceState for skill_veil_breach is placed in the slayer HeroLoadoutState.EquippedSkillInstanceIds with ResolvedSlotKind=FlexActive, matching the player loadout compile path.",
                    matched_properties = new
                    {
                        composition = assassinProbe.ArchetypeIds,
                        anchors = CredibleAnchors.Select(value => value.ToString()).ToArray(),
                        grade = "base",
                        hero_level = CredibleHeroLevel,
                        node_budget = nodeBudget,
                        passive_nodes = assassinProbe.PassiveNodes,
                        equipment = new[] { "item_slayer_blade (rarity tier 0, sole slayer only)" },
                    },
                    compiled_slot_evidence = BuildCompiledLeverEvidence(assassinProbe, blinkAssassinSquad),
                },
                matchup_matrix = rows,
                first_death_rates = firstDeathRows,
                control_arm = new
                {
                    probe = AssassinControlProbeId,
                    compiled_slot_evidence = BuildCompiledLeverEvidence(assassinProbe, probeSquads[AssassinControlProbeId]),
                    matchup = new
                    {
                        vs_ranged_3r1t = CellReport(controlCells["ranged"]),
                        vs_frontline = CellReport(controlCells["frontline"]),
                        vs_mixed = CellReport(controlCells["mixed"]),
                    },
                    dive_failure_witness = BuildDiveWitnessReport(controlDiveObservations),
                },
                dive_failure_witness = BuildDiveWitnessReport(blinkDiveObservations),
                veil_breach = BuildVeilBreachMeasurementReport(veilBreachMeasurements, openingLockSeconds),
                counterplay_stage0 = counterplayInstrumentation.BuildReport(),
                observer_determinism = observerDeterminism,
            };

            var outputPath = Resolve(repositoryRoot, outputRelativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
            File.WriteAllText(
                outputPath,
                JsonConvert.SerializeObject(
                    report,
                    Formatting.Indented,
                    new StringEnumConverter()));
            Console.WriteLine($"credible-deck-matchup COMPLETE seeds={seeds} report={outputPath}");
            return 0;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine($"credible-deck-matchup ERROR: {exception}");
            return 2;
        }
    }

    internal static DiveFailureObservation RunWitnessRegressionFixture(string repositoryRoot)
    {
        ContentSnapshotFreshnessGuard.EnsureFresh(repositoryRoot);
        var snapshotPath = Resolve(repositoryRoot, SnapshotRelativePath);
        var content = ContentSnapshotJsonSerializer.Deserialize(File.ReadAllText(snapshotPath));
        var statusRules = CombatStatusRuleCompiler.Compile(content);
        var config = SM.Editor.Validation.CampaignBalanceSweepConfig.Default;
        var reference = config.ReferenceSquads.Single(value =>
            string.Equals(value.SquadId, "ranged", StringComparison.Ordinal));
        var probe = CredibleProbes.Single(value =>
            string.Equals(value.Id, "P-ASSASSIN-C", StringComparison.Ordinal));
        var nodeBudget = PassiveBoardSelectionValidator.ResolveMaxActiveNodeCount(CredibleHeroLevel);
        ValidatePassiveRoute(content, probe, nodeBudget);

        var player = DeckMatchupDiagnosticRunner.CompileSquad(
            content,
            "credible.player.ranged",
            reference.CoreArchetypeIds,
            DeckMatchupDiagnosticRunner.PlayerAnchors(reference.SquadId),
            Array.Empty<string>(),
            equipDuelistBlade: false);
        var enemy = DeckMatchupDiagnosticRunner.CompileSquad(
            content,
            "credible.enemy.p-assassin-c",
            probe.ArchetypeIds,
            CredibleAnchors,
            probe.PassiveNodes,
            equipDuelistBlade: true,
            heroLevel: CredibleHeroLevel);
        ValidateCompiledLever(probe, enemy);

        var state = CreateState(player, enemy, statusRules, MatchupSeedStart);
        var diver = state.Enemies.Single(value =>
            string.Equals(value.Definition.ArchetypeId, "slayer", StringComparison.Ordinal));
        var diagnostics = new CounterplayInstrumentationObserver(state, reference.SquadId, diver.Id.Value);
        var witness = new DiveFailureBattleObserver(state, diagnostics, reference.SquadId, diver.Id.Value);
        var result = new BattleSimulator(state, BattleSimulator.DefaultMaxSteps, diagnostics)
            .RunToEnd(witness.ObserveStep);
        var observation = witness.Complete();
        _ = diagnostics.Complete(result, observation);
        return observation;
    }

    private static CredibleMatchupObservation ObserveMatchup(
        BattleLoadoutSnapshot player,
        BattleLoadoutSnapshot enemy,
        CombatStatusRules statusRules,
        int seeds,
        int seedStart,
        string referenceSquadId,
        ICollection<DiveFailureObservation>? diveObservations,
        CounterplayInstrumentationAccumulator? counterplayInstrumentation,
        ICollection<VeilBreachBattleMeasurement>? veilBreachMeasurements)
    {
        var observation = new CredibleMatchupObservation(seeds);
        for (var sample = 0; sample < seeds; sample++)
        {
            var state = BattleFactory.Create(
                player.Allies,
                enemy.Allies,
                player.TeamTactic.Posture,
                enemy.TeamTactic.Posture,
                BattleSimulator.DefaultFixedStepSeconds,
                seedStart + sample,
                statusRules: statusRules);
            var vanguardIds = state.Allies
                .Where(unit => string.Equals(unit.Definition.ClassId, "vanguard", StringComparison.Ordinal))
                .Select(unit => unit.Id.Value)
                .ToHashSet(StringComparer.Ordinal);
            if (vanguardIds.Count > 0)
            {
                observation.BattlesWithVanguard++;
            }

            DiveFailureBattleObserver? diveObserver = null;
            CounterplayInstrumentationObserver? diagnosticObserver = null;
            if (diveObservations != null)
            {
                var diver = state.Enemies.Single(unit => string.Equals(unit.Definition.ArchetypeId, "slayer", StringComparison.Ordinal));
                diagnosticObserver = new CounterplayInstrumentationObserver(state, referenceSquadId, diver.Id.Value);
                diveObserver = new DiveFailureBattleObserver(state, diagnosticObserver, referenceSquadId, diver.Id.Value);
            }

            Action<BattleSimulationStep>? stepObserver = diveObserver == null
                ? null
                : diveObserver.ObserveStep;
            var result = diagnosticObserver == null
                ? BattleResolver.Run(state, BattleSimulator.DefaultMaxSteps, stepObserver)
                : new BattleSimulator(state, BattleSimulator.DefaultMaxSteps, diagnosticObserver).RunToEnd(stepObserver);
            if (result.Winner == TeamSide.Enemy)
            {
                observation.ProbeWins++;
            }

            observation.TotalDurationSeconds += result.DurationSeconds;
            var playerDeaths = state.TelemetryEvents
                .Where(value => value.EventKind == TelemetryEventKind.UnitDied && value.Actor is { SideIndex: 0 })
                .OrderBy(value => value.TimeSeconds)
                .ToArray();
            var firstDeathTime = playerDeaths.FirstOrDefault()?.TimeSeconds;
            if (firstDeathTime.HasValue
                && playerDeaths.Any(value => value.TimeSeconds == firstDeathTime.Value
                                             && value.Actor != null
                                             && vanguardIds.Contains(value.Actor.UnitInstanceId)))
            {
                observation.VanguardFellFirst++;
            }

            if (diveObserver != null)
            {
                var diveObservation = diveObserver.Complete();
                diveObservations!.Add(diveObservation);
                var diagnosticObservation = diagnosticObserver!.Complete(result, diveObservation);
                counterplayInstrumentation?.Add(diagnosticObservation);
                if (veilBreachMeasurements != null)
                {
                    veilBreachMeasurements.Add(BuildVeilBreachBattleMeasurement(
                        result,
                        diveObservation,
                        diagnosticObserver));
                }
            }
        }

        return observation;
    }

    private static object BuildDiveWitnessReport(IReadOnlyList<DiveFailureObservation> observations)
    {
        var eligible = observations.Where(value => value.HasEligibleBackline).ToArray();
        var contacts = eligible
            .Where(value => value.TimeToFirstBacklineContactSeconds.HasValue)
            .Select(value => value.TimeToFirstBacklineContactSeconds!.Value)
            .OrderBy(value => value)
            .ToArray();
        return new
        {
            schema_version = "dive-failure-witness-v1",
            observations = observations.Count,
            eligible_backline_observations = eligible.Length,
            no_eligible_backline_observations = observations.Count - eligible.Length,
            outcome_distribution = BuildOutcomeDistribution(eligible),
            all_panel_outcome_distribution = BuildOutcomeDistribution(observations),
            time_to_first_backline_contact = new
            {
                mean = MeanOrNull(contacts),
                p50 = Quantile(contacts, 0.50),
                p90 = Quantile(contacts, 0.90),
                n = contacts.Length,
            },
            selector_ever_produced_backline_rate = Rate(eligible.Count(value => value.SelectorEverProducedBackline), eligible.Length),
            dive_intent_ever_selected_backline_rate = Rate(eligible.Count(value => value.DiveIntentEverSelectedBackline), eligible.Length),
            reached_action_range_rate = Rate(eligible.Count(value => value.ReachedActionRange), eligible.Length),
            in_range_gate_opened_rate = Rate(eligible.Count(value => value.InRangeGateOpened), eligible.Length),
            per_battle_per_diver = observations
                .OrderBy(value => value.ReferenceSquadId, StringComparer.Ordinal)
                .ThenBy(value => value.BattleSeed)
                .ThenBy(value => value.DiverId, StringComparer.Ordinal)
                .ToArray(),
        };
    }

    private static VeilBreachBattleMeasurement BuildVeilBreachBattleMeasurement(
        BattleResult result,
        DiveFailureObservation diveObservation,
        CounterplayInstrumentationObserver diagnosticObserver)
    {
        var lifecycle = diagnosticObserver.DisplacementLifecycle
            .Where(value => string.Equals(
                value.SkillId,
                CounterplayInstrumentationObserver.VeilBreachSkillId,
                StringComparison.Ordinal))
            .OrderBy(value => value.StepIndex)
            .ThenBy(value => value.Stage)
            .ToArray();
        var landing = lifecycle.FirstOrDefault(value =>
            value.Stage == DisplacementLifecycleStage.Resolved
            && value.ActorDisplacement > 0f);
        var postLandingVeto = false;
        if (landing != null)
        {
            var windowEnd = landing.TimeSeconds + 1f + 0.0001f;
            postLandingVeto = diagnosticObserver.DiveIntentEvaluations.Any(value =>
                                  value.TimeSeconds > landing.TimeSeconds
                                  && value.TimeSeconds <= windowEnd
                                  && value.ContinuingExistingDive
                                  && value.Reason == DiveIntentGateReason.ContinueScoreBelowThreshold)
                              || diagnosticObserver.DiveHardAborts.Any(value =>
                                  value.TimeSeconds > landing.TimeSeconds
                                  && value.TimeSeconds <= windowEnd)
                              || diveObservation.Switch is { } targetSwitch
                              && targetSwitch.ElapsedSeconds > landing.TimeSeconds
                              && targetSwitch.ElapsedSeconds <= windowEnd;
        }

        return new VeilBreachBattleMeasurement(
            diveObservation.ReferenceSquadId,
            diveObservation.BattleSeed,
            diveObservation,
            result.Winner == TeamSide.Enemy,
            landing != null,
            lifecycle.Any(value => value.Stage == DisplacementLifecycleStage.Aborted),
            landing?.TimeSeconds,
            postLandingVeto);
    }

    private static object BuildVeilBreachMeasurementReport(
        IReadOnlyList<VeilBreachBattleMeasurement> measurements,
        float openingLockSeconds)
    {
        var eligible = measurements.Where(value => value.Dive.HasEligibleBackline).ToArray();
        var contacts = eligible.Where(value => value.Dive.TimeToFirstBacklineContactSeconds.HasValue).ToArray();
        var noncontacts = eligible.Where(value => !value.Dive.TimeToFirstBacklineContactSeconds.HasValue).ToArray();
        var contactTimes = contacts
            .Select(value => value.Dive.TimeToFirstBacklineContactSeconds!.Value)
            .OrderBy(value => value)
            .ToArray();
        var landings = eligible.Where(value => value.BlinkLanded).ToArray();
        var contactRate = Rate(contacts.Length, eligible.Length);
        var observedWinRate = Rate(eligible.Count(value => value.ProbeWon), eligible.Length);
        var winOnContact = RateOrNull(contacts.Count(value => value.ProbeWon), contacts.Length);
        var winWithoutContact = RateOrNull(noncontacts.Count(value => value.ProbeWon), noncontacts.Length);
        var assumedBandArithmetic = (contactRate * 0.90d) + ((1d - contactRate) * 0.55d);
        var measuredMixture = winOnContact.HasValue && winWithoutContact.HasValue
            ? (double?)((contactRate * winOnContact.Value) + ((1d - contactRate) * winWithoutContact.Value))
            : null;

        return new
        {
            opening_lock_seconds = openingLockSeconds,
            eligible_backline_battles = eligible.Length,
            selected_battles = eligible.Count(value => value.Dive.DiveIntentEverSelectedBackline),
            selection_rate = Rate(eligible.Count(value => value.Dive.DiveIntentEverSelectedBackline), eligible.Length),
            contact_battles = contacts.Length,
            contact_rate = contactRate,
            time_to_first_backline_contact = new
            {
                mean = MeanOrNull(contactTimes),
                p50 = Quantile(contactTimes, 0.50),
                p90 = Quantile(contactTimes, 0.90),
                n = contactTimes.Length,
            },
            win_on_contact = winOnContact,
            win_without_contact = winWithoutContact,
            observed_probe_win_rate = observedWinRate,
            band_arithmetic = new
            {
                assumed_win_on_contact = 0.90,
                assumed_win_without_contact = 0.55,
                assumed_mixture = assumedBandArithmetic,
                measured_mixture = measuredMixture,
                measured_minus_assumed = measuredMixture - assumedBandArithmetic,
                observed_minus_assumed = observedWinRate - assumedBandArithmetic,
            },
            failure_histogram = eligible
                .Where(value => !value.Dive.TimeToFirstBacklineContactSeconds.HasValue)
                .GroupBy(value => value.Dive.Outcome, StringComparer.Ordinal)
                .OrderByDescending(group => group.Count())
                .ThenBy(group => group.Key, StringComparer.Ordinal)
                .Select(group => new
                {
                    outcome = group.Key,
                    count = group.Count(),
                    rate = Rate(group.Count(), eligible.Length),
                })
                .ToArray(),
            blink_landings = landings.Length,
            windup_fizzle_battles = eligible.Count(value => value.BlinkAborted),
            post_landing_veto_count = landings.Count(value => value.PostLandingVeto),
            post_landing_veto_rate = RateOrNull(landings.Count(value => value.PostLandingVeto), landings.Length),
            post_landing_veto_definition = "A continuing-dive score veto, hard abort, or observed target drop within 1.0 seconds after a resolved blink landing.",
            per_panel = measurements
                .GroupBy(value => value.Panel, StringComparer.Ordinal)
                .OrderBy(group => group.Key, StringComparer.Ordinal)
                .Select(group =>
                {
                    var panelEligible = group.Where(value => value.Dive.HasEligibleBackline).ToArray();
                    var panelContacts = panelEligible.Count(value => value.Dive.TimeToFirstBacklineContactSeconds.HasValue);
                    var panelLandings = panelEligible.Where(value => value.BlinkLanded).ToArray();
                    return new
                    {
                        panel = group.Key,
                        battles = group.Count(),
                        eligible_backline_battles = panelEligible.Length,
                        selection_rate = Rate(panelEligible.Count(value => value.Dive.DiveIntentEverSelectedBackline), panelEligible.Length),
                        contact_rate = Rate(panelContacts, panelEligible.Length),
                        probe_win_rate = Rate(group.Count(value => value.ProbeWon), group.Count()),
                        blink_landings = panelLandings.Length,
                        post_landing_veto_rate = RateOrNull(panelLandings.Count(value => value.PostLandingVeto), panelLandings.Length),
                    };
                })
                .ToArray(),
        };
    }

    private static object[] BuildOutcomeDistribution(IReadOnlyList<DiveFailureObservation> observations)
    {
        var orderedOutcomes = new[]
        {
            DiveFailureBattleObserver.DiedEnRoute,
            DiveFailureBattleObserver.RetargetedAway,
            DiveFailureBattleObserver.NeverSelected,
            DiveFailureBattleObserver.InRangeNeverOpened,
            DiveFailureBattleObserver.BattleEndedFirst,
            DiveFailureBattleObserver.Success,
        };
        return orderedOutcomes.Select(outcome =>
        {
            var matching = observations.Where(value => string.Equals(value.Outcome, outcome, StringComparison.Ordinal)).ToArray();
            return (object)new
            {
                outcome,
                count = matching.Length,
                rate = Rate(matching.Length, observations.Count),
                retarget_causes = matching
                    .Where(value => !string.IsNullOrWhiteSpace(value.RetargetCause))
                    .GroupBy(value => value.RetargetCause, StringComparer.Ordinal)
                    .OrderByDescending(group => group.Count())
                    .ThenBy(group => group.Key, StringComparer.Ordinal)
                    .ToDictionary(group => group.Key, group => group.Count(), StringComparer.Ordinal),
                killer_roles = matching
                    .Where(value => !string.IsNullOrWhiteSpace(value.KillerRole))
                    .GroupBy(value => $"{value.KillerArchetypeId}/{value.KillerRole}", StringComparer.Ordinal)
                    .OrderByDescending(group => group.Count())
                    .ThenBy(group => group.Key, StringComparer.Ordinal)
                    .ToDictionary(group => group.Key, group => group.Count(), StringComparer.Ordinal),
                mean_elapsed_seconds = MeanOrNull(matching.Where(value => value.ElapsedSeconds.HasValue).Select(value => value.ElapsedSeconds!.Value)),
                mean_remaining_distance = MeanOrNull(matching.Where(value => value.RemainingDistance.HasValue).Select(value => value.RemainingDistance!.Value)),
                mean_remaining_center_path = MeanOrNull(matching.Where(value => value.RemainingCenterPath.HasValue).Select(value => value.RemainingCenterPath!.Value)),
                mean_remaining_time_budget_seconds = MeanOrNull(matching.Where(value => value.RemainingTimeBudgetSeconds.HasValue).Select(value => value.RemainingTimeBudgetSeconds!.Value)),
            };
        }).ToArray();
    }

    private static ObserverDeterminismReport VerifyObserverDeterminism(
        BattleLoadoutSnapshot player,
        BattleLoadoutSnapshot enemy,
        CombatStatusRules statusRules)
    {
        var baselineState = CreateState(player, enemy, statusRules, MatchupSeedStart);
        var observedState = CreateState(player, enemy, statusRules, MatchupSeedStart);
        var baselineHashes = new List<string>();
        var observedHashes = new List<string>();
        var diver = observedState.Enemies.Single(unit => string.Equals(unit.Definition.ArchetypeId, "slayer", StringComparison.Ordinal));
        var diagnosticObserver = new CounterplayInstrumentationObserver(observedState, "ranged", diver.Id.Value);
        var witness = new DiveFailureBattleObserver(observedState, diagnosticObserver, "ranged", diver.Id.Value);
        var baselineResult = BattleResolver.Run(
            baselineState,
            BattleSimulator.DefaultMaxSteps,
            _ => baselineHashes.Add(BattleStateCanonicalHash.Compute(baselineState)));
        var observedResult = new BattleSimulator(
                observedState,
                BattleSimulator.DefaultMaxSteps,
                diagnosticObserver)
            .RunToEnd(step =>
            {
                witness.ObserveStep(step);
                observedHashes.Add(BattleStateCanonicalHash.Compute(observedState));
            });
        var diveObservation = witness.Complete();
        _ = diagnosticObserver.Complete(observedResult, diveObservation);
        var eventBytesIdentical = string.Equals(
            JsonConvert.SerializeObject(baselineResult.Events),
            JsonConvert.SerializeObject(observedResult.Events),
            StringComparison.Ordinal);
        var identical = baselineHashes.SequenceEqual(observedHashes, StringComparer.Ordinal)
                        && baselineResult.Winner == observedResult.Winner
                        && baselineResult.StepCount == observedResult.StepCount
                        && BitConverter.SingleToInt32Bits(baselineResult.DurationSeconds)
                        == BitConverter.SingleToInt32Bits(observedResult.DurationSeconds)
                        && eventBytesIdentical;
        return new ObserverDeterminismReport(
            identical,
            baselineHashes.Count,
            eventBytesIdentical,
            baselineHashes.SequenceEqual(observedHashes, StringComparer.Ordinal));
    }

    private static BattleState CreateState(
        BattleLoadoutSnapshot player,
        BattleLoadoutSnapshot enemy,
        CombatStatusRules statusRules,
        int seed)
        => BattleFactory.Create(
            player.Allies,
            enemy.Allies,
            player.TeamTactic.Posture,
            enemy.TeamTactic.Posture,
            BattleSimulator.DefaultFixedStepSeconds,
            seed,
            statusRules: statusRules);

    private static CombatContentSnapshot ApplyVeilBreachOpeningLock(
        CombatContentSnapshot source,
        float openingLockSeconds)
    {
        if (openingLockSeconds < 0f)
        {
            throw new ArgumentOutOfRangeException(nameof(openingLockSeconds));
        }

        if (!source.SkillCatalog.TryGetValue(CounterplayInstrumentationObserver.VeilBreachSkillId, out var skill))
        {
            throw new InvalidDataException("Cannot tune the opening lock because skill_veil_breach is absent.");
        }

        var catalog = source.SkillCatalog.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);
        catalog[skill.Id] = skill with
        {
            OpeningLockSeconds = openingLockSeconds,
            StartsOnCooldown = true,
        };
        return source with { SkillCatalog = catalog };
    }

    private static void ValidatePassiveRoute(CombatContentSnapshot content, CredibleProbeSpec probe, int nodeBudget)
    {
        if (probe.PassiveNodes.Count != nodeBudget)
        {
            throw new InvalidDataException($"{probe.Id} selected {probe.PassiveNodes.Count} nodes for budget {nodeBudget}.");
        }

        var normalized = PassiveBoardSelectionValidator.Normalize(
            "board_duelist",
            probe.PassiveNodes,
            content.PassiveNodes,
            nodeBudget);
        if (!normalized.IsValid || !normalized.NormalizedNodeIds.SequenceEqual(probe.PassiveNodes, StringComparer.Ordinal))
        {
            throw new InvalidDataException($"{probe.Id} passive route is not a legal six-node selection: {normalized.Error}");
        }
    }

    private static void ValidateCompiledLever(CredibleProbeSpec probe, BattleLoadoutSnapshot compiled)
    {
        var slayer = compiled.Allies.Single(value => string.Equals(value.ArchetypeId, "slayer", StringComparison.Ordinal));
        if (string.Equals(probe.Id, AssassinControlProbeId, StringComparison.Ordinal)
            && !CombatBehaviorTags.Contains(slayer.RulePackages, CombatBehaviorTags.DuelistDiveCommit))
        {
            throw new InvalidDataException("P-ASSASSIN-C did not compile duelist_dive_commit.");
        }

        if (string.Equals(probe.Id, "P-TANKBREAK-C", StringComparison.Ordinal)
            && !HasAppliedSunder(compiled, slayer.Id))
        {
            throw new InvalidDataException("P-TANKBREAK-C did not compile skill_sunder_rhythm.");
        }
    }

    private static void ValidateBlinkArmSlots(
        BattleLoadoutSnapshot control,
        BattleLoadoutSnapshot blink)
    {
        var controlSlayer = control.Allies.Single(value =>
            string.Equals(value.ArchetypeId, "slayer", StringComparison.Ordinal));
        var blinkSlayer = blink.Allies.Single(value =>
            string.Equals(value.ArchetypeId, "slayer", StringComparison.Ordinal));
        var blinkFlexActive = blinkSlayer.FlexActive;
        if (string.Equals(
                controlSlayer.FlexActive?.Id,
                CounterplayInstrumentationObserver.VeilBreachSkillId,
                StringComparison.Ordinal))
        {
            throw new InvalidDataException("The P-ASSASSIN-C control unexpectedly equipped skill_veil_breach.");
        }

        if (blinkFlexActive == null
            || !string.Equals(
                blinkFlexActive.Id,
                CounterplayInstrumentationObserver.VeilBreachSkillId,
                StringComparison.Ordinal)
            || blinkFlexActive.EffectiveSlotKind != ActionSlotKind.FlexActive)
        {
            throw new InvalidDataException("The blink arm did not compile skill_veil_breach into FlexActive.");
        }

        var loadoutProvenance = (blink.Provenance ?? Array.Empty<CompileProvenanceEntry>()).Any(entry =>
            string.Equals(entry.SubjectId, blinkSlayer.Id, StringComparison.Ordinal)
            && string.Equals(entry.SourceId, CounterplayInstrumentationObserver.VeilBreachSkillId, StringComparison.Ordinal)
            && string.Equals(entry.ArtifactKind, "skill_slot", StringComparison.Ordinal)
            && entry.Details.Contains("source:loadout_skill", StringComparer.Ordinal)
            && entry.Details.Contains("slot:utility_active", StringComparer.Ordinal));
        if (!loadoutProvenance)
        {
            throw new InvalidDataException("The blink arm lacks compiled loadout-skill provenance for FlexActive.");
        }
    }

    private static object BuildCompiledLeverEvidence(CredibleProbeSpec probe, BattleLoadoutSnapshot compiled)
    {
        var slayer = compiled.Allies.Single(value => string.Equals(value.ArchetypeId, "slayer", StringComparison.Ordinal));
        var flexActive = slayer.FlexActive;
        var flexProvenance = (compiled.Provenance ?? Array.Empty<CompileProvenanceEntry>())
            .Where(entry => string.Equals(entry.SubjectId, slayer.Id, StringComparison.Ordinal)
                            && string.Equals(entry.ArtifactKind, "skill_slot", StringComparison.Ordinal)
                            && string.Equals(entry.SourceId, flexActive?.Id, StringComparison.Ordinal))
            .Select(entry => new
            {
                entry.SourceId,
                entry.ArtifactKind,
                entry.Details,
            })
            .ToArray();
        return new
        {
            unique_slayer_count = compiled.Allies.Count(value => string.Equals(value.ArchetypeId, "slayer", StringComparison.Ordinal)),
            dive_commit_compiled = CombatBehaviorTags.Contains(slayer.RulePackages, CombatBehaviorTags.DuelistDiveCommit),
            sunder_rhythm_compiled = HasAppliedSunder(compiled, slayer.Id),
            flex_active_skill_id = flexActive?.Id ?? string.Empty,
            flex_active_compiled_slot = flexActive?.SlotKind ?? string.Empty,
            flex_active_resolved_slot = flexActive?.EffectiveSlotKind.ToString() ?? string.Empty,
            flex_active_provenance = flexProvenance,
            expected_lever = probe.Lever,
        };
    }

    private static bool HasAppliedSunder(BattleLoadoutSnapshot compiled, string slayerId)
        => (compiled.Provenance ?? Array.Empty<CompileProvenanceEntry>()).Any(entry =>
            string.Equals(entry.SubjectId, slayerId, StringComparison.Ordinal)
            && string.Equals(entry.SourceId, "skill_sunder_rhythm", StringComparison.Ordinal)
            && string.Equals(entry.ArtifactKind, "support_modifier", StringComparison.Ordinal));

    private static object CellReport(CredibleMatchupObservation observation) => new
    {
        probe_wins = observation.ProbeWins,
        probe_win_rate = observation.ProbeWins / (double)observation.Seeds,
        player_win_rate = 1d - (observation.ProbeWins / (double)observation.Seeds),
        mean_duration_seconds = observation.TotalDurationSeconds / observation.Seeds,
        vanguard_first_death_rate = observation.VanguardFellFirst / (double)observation.Seeds,
    };

    private static (int Seeds, string Output, float? OpeningLockSeconds) Parse(IReadOnlyList<string> arguments)
    {
        var seeds = DefaultSeeds;
        var output = DefaultOutputRelativePath;
        float? openingLockSeconds = null;
        for (var index = 0; index < arguments.Count; index++)
        {
            if (arguments[index] == "--seeds" && index + 1 < arguments.Count)
            {
                seeds = int.Parse(arguments[++index], System.Globalization.CultureInfo.InvariantCulture);
            }
            else if (arguments[index] == "--output" && index + 1 < arguments.Count)
            {
                output = arguments[++index];
            }
            else if (arguments[index] == "--opening-lock-seconds" && index + 1 < arguments.Count)
            {
                openingLockSeconds = float.Parse(
                    arguments[++index],
                    System.Globalization.CultureInfo.InvariantCulture);
            }
            else
            {
                throw new ArgumentException($"Unknown credible-deck-matchup argument: {arguments[index]}");
            }
        }

        if (seeds < 32)
        {
            throw new ArgumentOutOfRangeException(nameof(arguments), "At least 32 seeds per cell are required.");
        }

        if (openingLockSeconds is < 0f)
        {
            throw new ArgumentOutOfRangeException(nameof(arguments), "Opening lock seconds must be non-negative.");
        }

        return (seeds, output, openingLockSeconds);
    }

    private static double Rate(int numerator, int denominator)
        => denominator == 0 ? 0d : numerator / (double)denominator;

    private static double? RateOrNull(int numerator, int denominator)
        => denominator == 0 ? null : numerator / (double)denominator;

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

    private static string Resolve(string root, string relative)
        => Path.Combine(root, relative.Replace('/', Path.DirectorySeparatorChar));

    private sealed record CredibleProbeSpec(
        string Id,
        string Lever,
        IReadOnlyList<string> ArchetypeIds,
        IReadOnlyList<string> PassiveNodes,
        string Rationale);

    private sealed class CredibleMatchupObservation
    {
        internal CredibleMatchupObservation(int seeds) => Seeds = seeds;

        internal int Seeds { get; }
        internal int ProbeWins { get; set; }
        internal int VanguardFellFirst { get; set; }
        internal int BattlesWithVanguard { get; set; }
        internal double TotalDurationSeconds { get; set; }
    }

    private sealed record ObserverDeterminismReport(
        bool Identical,
        int ComparedSteps,
        bool EventBytesIdentical,
        bool CanonicalStateHashesIdentical);

    private sealed record VeilBreachBattleMeasurement(
        string Panel,
        int BattleSeed,
        DiveFailureObservation Dive,
        bool ProbeWon,
        bool BlinkLanded,
        bool BlinkAborted,
        double? LandingSeconds,
        bool PostLandingVeto);
}
