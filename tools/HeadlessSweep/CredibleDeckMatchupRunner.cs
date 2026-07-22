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
            "P-ASSASSIN-C",
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
            var (seeds, outputRelativePath) = Parse(arguments);
            ContentSnapshotFreshnessGuard.EnsureFresh(repositoryRoot);
            var snapshotPath = Resolve(repositoryRoot, SnapshotRelativePath);
            var content = ContentSnapshotJsonSerializer.Deserialize(File.ReadAllText(snapshotPath));
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

            var rows = new List<object>();
            var firstDeathRows = new List<object>();
            var diveObservations = new List<DiveFailureObservation>();
            var counterplayInstrumentation = new CounterplayInstrumentationAccumulator();
            foreach (var probe in CredibleProbes)
            {
                var compiled = probeSquads[probe.Id];
                ValidateCompiledLever(probe, compiled);
                var cells = new Dictionary<string, CredibleMatchupObservation>(StringComparer.Ordinal);
                foreach (var reference in config.ReferenceSquads)
                {
                    var observeDive = string.Equals(probe.Id, "P-ASSASSIN-C", StringComparison.Ordinal);
                    cells[reference.SquadId] = ObserveMatchup(
                        playerSquads[reference.SquadId],
                        compiled,
                        statusRules,
                        seeds,
                        MatchupSeedStart,
                        reference.SquadId,
                        observeDive ? diveObservations : null,
                        observeDive ? counterplayInstrumentation : null);
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

            var observerDeterminism = VerifyObserverDeterminism(
                playerSquads["ranged"],
                probeSquads["P-ASSASSIN-C"],
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
                    credible_build = "Four base-grade shipped heroes; one common tier-0 blade on the sole slayer; one Level-5 six-node legal duelist route; no affixes, augments, traits, rarity boosts, or stat scalars.",
                    credible_level = CredibleHeroLevel,
                    credible_node_budget = nodeBudget,
                    level_basis = "Level 5 is reachable after 18 wins at 50 XP per win and is available during chapter 3; ResolveMaxActiveNodeCount(5) is 6.",
                    anchor_rule = "One hero per unique legal anchor; FrontCenter, FrontTop, BackBottom, BackCenter; no overlap.",
                    paired_seed_start = MatchupSeedStart,
                    seeds_per_matchup = seeds,
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
                matchup_matrix = rows,
                first_death_rates = firstDeathRows,
                dive_failure_witness = BuildDiveWitnessReport(diveObservations),
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

    private static CredibleMatchupObservation ObserveMatchup(
        BattleLoadoutSnapshot player,
        BattleLoadoutSnapshot enemy,
        CombatStatusRules statusRules,
        int seeds,
        int seedStart,
        string referenceSquadId,
        ICollection<DiveFailureObservation>? diveObservations,
        CounterplayInstrumentationAccumulator? counterplayInstrumentation)
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
                diveObserver = new DiveFailureBattleObserver(state, referenceSquadId, diver.Id.Value);
                diagnosticObserver = new CounterplayInstrumentationObserver(state, referenceSquadId, diver.Id.Value);
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
                counterplayInstrumentation!.Add(diagnosticObserver!.Complete(result, diveObservation));
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
        var witness = new DiveFailureBattleObserver(observedState, "ranged", diver.Id.Value);
        var diagnosticObserver = new CounterplayInstrumentationObserver(observedState, "ranged", diver.Id.Value);
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
        if (string.Equals(probe.Id, "P-ASSASSIN-C", StringComparison.Ordinal)
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

    private static object BuildCompiledLeverEvidence(CredibleProbeSpec probe, BattleLoadoutSnapshot compiled)
    {
        var slayer = compiled.Allies.Single(value => string.Equals(value.ArchetypeId, "slayer", StringComparison.Ordinal));
        return new
        {
            unique_slayer_count = compiled.Allies.Count(value => string.Equals(value.ArchetypeId, "slayer", StringComparison.Ordinal)),
            dive_commit_compiled = CombatBehaviorTags.Contains(slayer.RulePackages, CombatBehaviorTags.DuelistDiveCommit),
            sunder_rhythm_compiled = HasAppliedSunder(compiled, slayer.Id),
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

    private static (int Seeds, string Output) Parse(IReadOnlyList<string> arguments)
    {
        var seeds = DefaultSeeds;
        var output = DefaultOutputRelativePath;
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
            else
            {
                throw new ArgumentException($"Unknown credible-deck-matchup argument: {arguments[index]}");
            }
        }

        if (seeds < 32)
        {
            throw new ArgumentOutOfRangeException(nameof(arguments), "At least 32 seeds per cell are required.");
        }

        return (seeds, output);
    }

    private static double Rate(int numerator, int denominator)
        => denominator == 0 ? 0d : numerator / (double)denominator;

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
}
