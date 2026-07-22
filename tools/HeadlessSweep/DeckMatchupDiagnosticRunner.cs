using Newtonsoft.Json;
using SM.Combat.Model;
using SM.Combat.Services;
using SM.Core.Contracts;
using SM.Editor.Validation;
using SM.Meta;
using SM.Meta.Model;
using SM.Meta.Serialization;
using SM.Meta.Services;

internal static class DeckMatchupDiagnosticRunner
{
    private const string SnapshotRelativePath = "Assets/Resources/_Game/Content/content-snapshot.json";
    private const string DefaultOutputRelativePath = "Temp/DeckMatchupProbe/deck-matchup-diagnostic.json";
    private const int DefaultSeeds = 128;
    private const int MatchupSeedStart = 62000;
    private const int ThreatSeedStart = 73000;
    private const float ThreatDeathWindowSeconds = 3f;

    private static readonly string[] DiveNodes =
    {
        "passive_duelist_small_02",
        "passive_duelist_small_04",
        "passive_duelist_notable_01",
        "passive_duelist_small_05",
        "passive_duelist_small_07",
        "passive_duelist_notable_04",
        "passive_duelist_keystone_01",
        "passive_duelist_small_06",
    };

    private static readonly string[] SunderNodes =
    {
        "passive_duelist_small_01",
        "passive_duelist_small_03",
        "passive_duelist_notable_02",
        "passive_duelist_small_10",
        "passive_duelist_small_12",
        "passive_duelist_notable_05",
        "passive_duelist_small_09",
        "passive_duelist_small_11",
    };

    private static readonly ProbeSpec[] Probes =
    {
        new(
            "P-SUSTAIN",
            "attrition/sustain",
            new[] { "bulwark", "bastion_penitent", "priest", "shaman" },
            new[]
            {
                DeploymentAnchorId.FrontTop,
                DeploymentAnchorId.FrontBottom,
                DeploymentAnchorId.BackTop,
                DeploymentAnchorId.BackBottom,
            },
            Array.Empty<string>(),
            false,
            "Two durable vanguards screen the canonical priest and shaman healers; no burst specialist."),
        new(
            "P-ASSASSIN",
            "assassin-reach",
            new[] { "slayer", "slayer", "slayer", "slayer" },
            new[]
            {
                DeploymentAnchorId.FrontTop,
                DeploymentAnchorId.FrontCenter,
                DeploymentAnchorId.FrontBottom,
                DeploymentAnchorId.BackCenter,
            },
            DiveNodes,
            true,
            "Four all-melee slayers, each with the existing dive-assassin route and blade; no backline target."),
        new(
            "P-TANKBREAK",
            "tank-break",
            new[] { "slayer", "slayer", "slayer", "slayer" },
            new[]
            {
                DeploymentAnchorId.FrontTop,
                DeploymentAnchorId.FrontCenter,
                DeploymentAnchorId.FrontBottom,
                DeploymentAnchorId.BackCenter,
            },
            SunderNodes,
            true,
            "Four all-melee slayers, each with the existing sunder route and blade, to focus the lone screen."),
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
            var config = CampaignBalanceSweepConfig.Default;
            var playerSquads = config.ReferenceSquads.ToDictionary(
                squad => squad.SquadId,
                squad => CompileSquad(
                    content,
                    $"diagnostic.player.{squad.SquadId}",
                    squad.CoreArchetypeIds,
                    PlayerAnchors(squad.SquadId),
                    Array.Empty<string>(),
                    equipDuelistBlade: false),
                StringComparer.Ordinal);
            var probeSquads = Probes.ToDictionary(
                probe => probe.Id,
                probe => CompileSquad(
                    content,
                    $"diagnostic.enemy.{probe.Id.ToLowerInvariant()}",
                    probe.ArchetypeIds,
                    probe.Anchors,
                    probe.PassiveNodes,
                    probe.EquipDuelistBlade),
                StringComparer.Ordinal);

            var matchupRows = new List<object>();
            var mechanisms = new Dictionary<string, object>(StringComparer.Ordinal);
            foreach (var probe in Probes)
            {
                var cells = new Dictionary<string, MatchupObservation>(StringComparer.Ordinal);
                foreach (var squad in config.ReferenceSquads)
                {
                    cells[squad.SquadId] = ObserveMatchup(
                        playerSquads[squad.SquadId],
                        probeSquads[probe.Id],
                        statusRules,
                        seeds,
                        MatchupSeedStart);
                }

                var ranged = cells["ranged"];
                matchupRows.Add(new
                {
                    probe_deck = probe.Id,
                    vs_ranged_3r1t = CellReport(ranged),
                    vs_frontline = CellReport(cells["frontline"]),
                    vs_mixed = CellReport(cells["mixed"]),
                    seeds,
                });
                mechanisms[probe.Id] = new
                {
                    ranged_deaths_per_battle = ranged.RangerDeaths / (double)seeds,
                    ranged_unit_death_rate = ranged.RangerDeaths / (double)(seeds * 3),
                    ranger_wipe_rate = ranged.RangerWipes / (double)seeds,
                    ranger_death_causes = ranged.RangerDeathCauses
                        .OrderByDescending(pair => pair.Value)
                        .ThenBy(pair => pair.Key, StringComparer.Ordinal)
                        .ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal),
                    tank_fell_first_rate = ranged.TankFellFirst / (double)seeds,
                    tank_death_rate = ranged.TankDeaths / (double)seeds,
                    enemy_backline_contact_rate = ranged.BacklineContactCount / (double)seeds,
                    enemy_backline_contact_mean_seconds = MeanOrNull(ranged.BacklineContactSeconds),
                    enemy_backline_contact_median_seconds = Quantile(ranged.BacklineContactSeconds, 0.5),
                    enemy_effective_healing_mean = ranged.EnemyHealing / seeds,
                    fight_length_mean_seconds = ranged.TotalDurationSeconds / seeds,
                    fight_length_median_seconds = Quantile(ranged.Durations, 0.5),
                };
            }

            var threat = ObserveAuthoredBossThreats(content, config, seeds);
            var report = new
            {
                schema_version = "deck-matchup-diagnostic-v1",
                method = new
                {
                    player_build = "CampaignBalanceSweepConfig reference squads at P20: no equipment and no passive growth",
                    threat_player_build = "naive ranged C0 campaign path, P50 initial build, measured at each actual boss-arrival snapshot",
                    paired_seed_start = MatchupSeedStart,
                    seeds_per_matchup = seeds,
                    boss_threat_seeds_per_boss = seeds,
                    threat_definition = "enemy backline-dive contact or authored enemy area-skill contact on a player back-row unit",
                    threat_hp_fraction_definition = "sum actual backline contact damage / sum contacted units' max HP",
                    threat_death_conversion_definition = $"any contacted backline unit dies within {ThreatDeathWindowSeconds:0.#} seconds",
                },
                probe_decks = Probes.Select(probe => new
                {
                    id = probe.Id,
                    composition = probe.ArchetypeIds,
                    lever = probe.Lever,
                    rationale = probe.Rationale,
                }),
                matchup_matrix = matchupRows,
                mechanism_evidence = mechanisms,
                threat_conversion = threat,
            };
            var outputPath = Resolve(repositoryRoot, outputRelativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
            File.WriteAllText(outputPath, JsonConvert.SerializeObject(report, Formatting.Indented));
            Console.WriteLine($"deck-matchup-diagnostic COMPLETE seeds={seeds} report={outputPath}");
            return 0;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine($"deck-matchup-diagnostic ERROR: {exception}");
            return 2;
        }
    }

    private static object CellReport(MatchupObservation cell) => new
    {
        player_win_rate = cell.PlayerWins / (double)cell.Seeds,
        enemy_win_rate = 1d - (cell.PlayerWins / (double)cell.Seeds),
        mean_duration_seconds = cell.TotalDurationSeconds / cell.Seeds,
    };

    private static MatchupObservation ObserveMatchup(
        BattleLoadoutSnapshot player,
        BattleLoadoutSnapshot enemy,
        CombatStatusRules statusRules,
        int seeds,
        int seedStart)
    {
        var result = new MatchupObservation(seeds);
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
            var battle = BattleResolver.Run(state);
            if (battle.Winner == TeamSide.Ally)
            {
                result.PlayerWins++;
            }

            result.TotalDurationSeconds += battle.DurationSeconds;
            result.Durations.Add(battle.DurationSeconds);
            var alliesByInstance = state.Allies.ToDictionary(unit => unit.Id.Value, StringComparer.Ordinal);
            var allByInstance = state.AllUnits.ToDictionary(unit => unit.Id.Value, StringComparer.Ordinal);
            var playerRangerIds = state.Allies
                .Where(unit => string.Equals(unit.Definition.ClassId, "ranger", StringComparison.Ordinal))
                .Select(unit => unit.Id.Value)
                .ToHashSet(StringComparer.Ordinal);
            var tankIds = state.Allies
                .Where(unit => string.Equals(unit.Definition.ClassId, "vanguard", StringComparison.Ordinal))
                .Select(unit => unit.Id.Value)
                .ToHashSet(StringComparer.Ordinal);
            var telemetry = state.TelemetryEvents;
            var playerDeaths = telemetry
                .Where(value => value.EventKind == TelemetryEventKind.UnitDied
                                && value.Actor is { SideIndex: 0 })
                .OrderBy(value => value.TimeSeconds)
                .ToArray();
            var earliestDeath = playerDeaths.FirstOrDefault()?.TimeSeconds;
            if (earliestDeath.HasValue
                && playerDeaths.Any(value => value.TimeSeconds == earliestDeath.Value
                                             && value.Actor != null
                                             && tankIds.Contains(value.Actor.UnitInstanceId)))
            {
                result.TankFellFirst++;
            }

            if (playerDeaths.Any(value => value.Actor != null && tankIds.Contains(value.Actor.UnitInstanceId)))
            {
                result.TankDeaths++;
            }

            var rangerDeathsThisBattle = playerDeaths
                .Count(value => value.Actor != null && playerRangerIds.Contains(value.Actor.UnitInstanceId));
            result.RangerDeaths += rangerDeathsThisBattle;
            if (playerRangerIds.Count > 0 && rangerDeathsThisBattle == playerRangerIds.Count)
            {
                result.RangerWipes++;
            }

            foreach (var kill in telemetry.Where(value => value.EventKind == TelemetryEventKind.KillCredited
                                                          && value.Target != null
                                                          && playerRangerIds.Contains(value.Target.UnitInstanceId)))
            {
                var actor = kill.Actor == null || !allByInstance.TryGetValue(kill.Actor.UnitInstanceId, out var unit)
                    ? "unknown"
                    : unit.Definition.ArchetypeId;
                var source = !string.IsNullOrWhiteSpace(kill.SkillId)
                    ? kill.SkillId
                    : kill.Explain?.SourceContentId ?? "unknown";
                var key = $"{actor}/{source}";
                result.RangerDeathCauses[key] = result.RangerDeathCauses.TryGetValue(key, out var count)
                    ? count + 1
                    : 1;
            }

            var firstBacklineDamage = telemetry
                .Where(value => value.EventKind == TelemetryEventKind.DamageApplied
                                && value.Actor is { SideIndex: 1 }
                                && value.Target != null
                                && playerRangerIds.Contains(value.Target.UnitInstanceId)
                                && value.ValueA > 0f)
                .Select(value => (double)value.TimeSeconds)
                .DefaultIfEmpty(double.NaN)
                .Min();
            if (!double.IsNaN(firstBacklineDamage))
            {
                result.BacklineContactCount++;
                result.BacklineContactSeconds.Add(firstBacklineDamage);
            }

            result.EnemyHealing += telemetry
                .Where(value => value.EventKind == TelemetryEventKind.HealingApplied
                                && value.Actor is { SideIndex: 1 })
                .Sum(value => value.ValueA);
        }

        return result;
    }

    private static object ObserveAuthoredBossThreats(
        CombatContentSnapshot content,
        CampaignBalanceSweepConfig config,
        int seeds)
    {
        var lookup = new SnapshotSessionContentLookup(content);
        var aggregates = new List<ThreatAggregate>();
        var bossIndex = 0;
        var cell = new CampaignBalanceGridCell(
            config.ReferenceSquads.Single(value => string.Equals(value.SquadId, "ranged", StringComparison.Ordinal)),
            config.BuildPowerQuantiles.Single(value => string.Equals(value.QuantileId, "P50", StringComparison.Ordinal)),
            config.EnemyCompositionVariants.Single(value => value.VariantIndex == 0),
            config.RosterCoverageVariants.Single(value => value.BenchArchetypeCount == 0));
        var arm = config.Arms.Single(value => string.Equals(value.ArmId, "naive", StringComparison.Ordinal));
        _ = HeadlessCampaignPlaythrough.Run(
            lookup,
            config,
            arm,
            cell,
            "site_bone_orchard_boss_1",
            (rangedPlayer, authoredEncounter, identity) =>
            {
                if (identity.ChapterOrder > 3)
                {
                    return;
                }

                var aggregate = new ThreatAggregate(
                    identity.ChapterId,
                    identity.ChapterOrder,
                    identity.SiteId,
                    identity.EncounterId,
                    seeds);
                for (var sample = 0; sample < seeds; sample++)
                {
                    var seed = ThreatSeedStart + (bossIndex * 1000) + sample;
                    var measuredEncounter = authoredEncounter with
                    {
                        Context = authoredEncounter.Context with { BattleSeed = seed },
                    };
                    if (!SessionBattleStateComposer.TryCompose(
                            lookup,
                            rangedPlayer,
                            measuredEncounter,
                            out var state,
                            out var composeError))
                    {
                        throw new InvalidOperationException(composeError);
                    }

                    var observer = new ThreatConversionObserver(state, ThreatDeathWindowSeconds);
                    var battle = BattleResolver.Run(state, BattleSimulator.DefaultMaxSteps, observer.ObserveStep);
                    aggregate.Record(battle, observer.Complete());
                }

                aggregates.Add(aggregate);
                bossIndex++;
            });

        var allFractions = aggregates.SelectMany(value => value.TargetHpFractions).ToArray();
        var allTeamFractions = aggregates.SelectMany(value => value.TeamHpFractions).ToArray();
        var totalLandings = aggregates.Sum(value => value.LandingCount);
        var totalImmediateDeaths = aggregates.Sum(value => value.ImmediateDeathCount);
        var totalWindowDeaths = aggregates.Sum(value => value.WindowDeathCount);
        return new
        {
            landed_threat_pct_of_player_hp = new
            {
                mean_contacted_unit_max_hp_pct = Percent(MeanOrNull(allFractions)),
                median_contacted_unit_max_hp_pct = Percent(Quantile(allFractions, 0.5)),
                mean_team_max_hp_pct = Percent(MeanOrNull(allTeamFractions)),
            },
            landed_threat_to_death_rate = new
            {
                immediate = Rate(totalImmediateDeaths, totalLandings),
                within_3_seconds = Rate(totalWindowDeaths, totalLandings),
                landed_threats = totalLandings,
            },
            per_boss = aggregates.Select(value => value.Report()).ToArray(),
        };
    }

    internal static BattleLoadoutSnapshot CompileSquad(
        CombatContentSnapshot content,
        string blueprintId,
        IReadOnlyList<string> archetypeIds,
        IReadOnlyList<DeploymentAnchorId> anchors,
        IReadOnlyList<string> passiveNodes,
        bool equipDuelistBlade,
        int heroLevel = 1)
    {
        if (archetypeIds.Count != anchors.Count)
        {
            throw new InvalidDataException($"Squad '{blueprintId}' archetype/anchor counts differ.");
        }

        var heroes = new List<HeroRecord>();
        var loadouts = new Dictionary<string, HeroLoadoutState>(StringComparer.Ordinal);
        var progressions = new Dictionary<string, HeroProgressionState>(StringComparer.Ordinal);
        var items = new Dictionary<string, ItemInstanceState>(StringComparer.Ordinal);
        var selections = new Dictionary<string, PassiveBoardSelectionState>(StringComparer.Ordinal);
        var assignments = new Dictionary<DeploymentAnchorId, string>();
        for (var index = 0; index < archetypeIds.Count; index++)
        {
            var archetypeId = archetypeIds[index];
            if (!content.Archetypes.TryGetValue(archetypeId, out var archetype))
            {
                throw new InvalidDataException($"Missing archetype '{archetypeId}'.");
            }

            var heroId = $"{blueprintId}.hero.{index}";
            var appliesDuelistRoute = passiveNodes.Count > 0
                                      && string.Equals(archetype.ClassId, "duelist", StringComparison.Ordinal);
            var equipped = Array.Empty<string>();
            if (appliesDuelistRoute && equipDuelistBlade)
            {
                var itemId = $"{heroId}.blade";
                items[itemId] = new ItemInstanceState(itemId, "item_slayer_blade", Array.Empty<string>(), heroId);
                equipped = new[] { itemId };
            }

            heroes.Add(new HeroRecord(
                heroId,
                archetype.DisplayName,
                archetype.Id,
                archetype.RaceId,
                archetype.ClassId,
                string.Empty,
                string.Empty));
            loadouts[heroId] = new HeroLoadoutState(
                heroId,
                equipped,
                Array.Empty<string>(),
                appliesDuelistRoute ? "board_duelist" : string.Empty,
                appliesDuelistRoute ? passiveNodes : Array.Empty<string>(),
                Array.Empty<string>());
            progressions[heroId] = new HeroProgressionState(
                heroId,
                heroLevel,
                0,
                appliesDuelistRoute ? passiveNodes : Array.Empty<string>(),
                archetype.Skills.Select(skill => skill.Id).Distinct(StringComparer.Ordinal).ToArray());
            if (appliesDuelistRoute)
            {
                selections[heroId] = new PassiveBoardSelectionState(heroId, "board_duelist", passiveNodes);
            }

            assignments[anchors[index]] = heroId;
        }

        return new LoadoutCompiler().Compile(
            heroes,
            loadouts,
            progressions,
            items,
            new Dictionary<string, SkillInstanceState>(StringComparer.Ordinal),
            selections,
            new PermanentAugmentLoadoutState(blueprintId, Array.Empty<string>()),
            new SquadBlueprintState(
                blueprintId,
                blueprintId,
                TeamPostureType.StandardAdvance,
                "team_tactic_standard_advance",
                assignments,
                heroes.Select(hero => hero.Id).ToArray(),
                new Dictionary<string, string>(StringComparer.Ordinal)),
            new RunOverlayState(
                0,
                Array.Empty<string>(),
                Array.Empty<string>(),
                LoadoutCompiler.CurrentCompileVersion,
                string.Empty),
            content);
    }

    internal static IReadOnlyList<DeploymentAnchorId> PlayerAnchors(string squadId) => squadId switch
    {
        "ranged" => new[]
        {
            DeploymentAnchorId.FrontCenter,
            DeploymentAnchorId.BackTop,
            DeploymentAnchorId.BackCenter,
            DeploymentAnchorId.BackBottom,
        },
        "frontline" => new[]
        {
            DeploymentAnchorId.FrontCenter,
            DeploymentAnchorId.FrontTop,
            DeploymentAnchorId.FrontBottom,
            DeploymentAnchorId.BackCenter,
        },
        "mixed" => new[]
        {
            DeploymentAnchorId.FrontCenter,
            DeploymentAnchorId.FrontTop,
            DeploymentAnchorId.BackTop,
            DeploymentAnchorId.BackBottom,
        },
        _ => throw new InvalidDataException($"Unknown reference squad '{squadId}'."),
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
                throw new ArgumentException($"Unknown deck-matchup-diagnostic argument: {arguments[index]}");
            }
        }

        if (seeds < 32)
        {
            throw new ArgumentOutOfRangeException(nameof(arguments), "At least 32 seeds per cell are required.");
        }

        return (seeds, output);
    }

    private static string Resolve(string root, string relative)
        => Path.Combine(root, relative.Replace('/', Path.DirectorySeparatorChar));

    private static double Rate(int numerator, int denominator)
        => denominator == 0 ? 0d : numerator / (double)denominator;

    private static double? MeanOrNull(IEnumerable<double> values)
    {
        var array = values.ToArray();
        return array.Length == 0 ? null : array.Average();
    }

    private static double? Quantile(IEnumerable<double> values, double quantile)
    {
        var ordered = values.OrderBy(value => value).ToArray();
        if (ordered.Length == 0)
        {
            return null;
        }

        var position = (ordered.Length - 1) * quantile;
        var lower = (int)Math.Floor(position);
        var upper = (int)Math.Ceiling(position);
        return lower == upper
            ? ordered[lower]
            : ordered[lower] + ((ordered[upper] - ordered[lower]) * (position - lower));
    }

    private static double? Percent(double? value) => value.HasValue ? value.Value * 100d : null;

    private sealed record ProbeSpec(
        string Id,
        string Lever,
        IReadOnlyList<string> ArchetypeIds,
        IReadOnlyList<DeploymentAnchorId> Anchors,
        IReadOnlyList<string> PassiveNodes,
        bool EquipDuelistBlade,
        string Rationale);

    private sealed class MatchupObservation
    {
        internal MatchupObservation(int seeds) => Seeds = seeds;

        internal int Seeds { get; }
        internal int PlayerWins { get; set; }
        internal double TotalDurationSeconds { get; set; }
        internal List<double> Durations { get; } = new();
        internal int RangerDeaths { get; set; }
        internal int RangerWipes { get; set; }
        internal int TankFellFirst { get; set; }
        internal int TankDeaths { get; set; }
        internal int BacklineContactCount { get; set; }
        internal List<double> BacklineContactSeconds { get; } = new();
        internal double EnemyHealing { get; set; }
        internal Dictionary<string, int> RangerDeathCauses { get; } = new(StringComparer.Ordinal);
    }

    private sealed record ThreatBattleObservation(
        int LandingCount,
        int ImmediateDeathCount,
        int WindowDeathCount,
        IReadOnlyList<double> TargetHpFractions,
        IReadOnlyList<double> TeamHpFractions,
        IReadOnlyDictionary<string, int> Skills,
        bool HadLanding);

    private sealed record ThreatLanding(
        double TimeSeconds,
        IReadOnlyList<string> TargetIds,
        double TargetHpFraction,
        double TeamHpFraction,
        bool ImmediateDeath,
        string SkillId);

    private sealed class ThreatConversionObserver
    {
        private readonly BattleState _state;
        private readonly float _deathWindowSeconds;
        private readonly HashSet<string> _backlineIds;
        private readonly Dictionary<string, double> _maxHpById;
        private readonly Dictionary<string, HashSet<string>> _areaSkillsByActor;
        private readonly double _teamMaxHp;
        private readonly List<ThreatLanding> _landings = new();
        private readonly Dictionary<string, double> _deathTimes = new(StringComparer.Ordinal);

        internal ThreatConversionObserver(BattleState state, float deathWindowSeconds)
        {
            _state = state;
            _deathWindowSeconds = deathWindowSeconds;
            _backlineIds = state.Allies
                .Where(unit => unit.Anchor.IsBackRow() || unit.Behavior.FormationLine == FormationLine.Backline)
                .Select(unit => unit.Id.Value)
                .ToHashSet(StringComparer.Ordinal);
            _maxHpById = state.Allies.ToDictionary(unit => unit.Id.Value, unit => (double)unit.MaxHealth, StringComparer.Ordinal);
            _teamMaxHp = state.Allies.Sum(unit => (double)unit.MaxHealth);
            _areaSkillsByActor = state.AllUnits.ToDictionary(
                unit => unit.Id.Value,
                unit => EnumerateSkills(unit)
                    .Where(skill => skill.AreaEffectFamily != BattleAreaEffectFamily.SingleTarget)
                    .Select(skill => skill.Id)
                    .ToHashSet(StringComparer.Ordinal),
                StringComparer.Ordinal);
        }

        internal void ObserveStep(BattleSimulationStep step)
        {
            var units = step.Units.ToDictionary(unit => unit.Id, StringComparer.Ordinal);
            foreach (var intent in step.CombatEventIntents ?? Array.Empty<BattleCombatEventIntent>())
            {
                if (intent.Status != CombatEventIntentStatus.Contacted
                    || _state.FindUnitById(intent.ActorId.Value)?.Side != TeamSide.Enemy)
                {
                    continue;
                }

                var contacts = (intent.Contacts ?? Array.Empty<BattleContactIntent>())
                    .Where(contact => contact.TargetId is { } targetId
                                      && _backlineIds.Contains(targetId.Value)
                                      && !contact.IsHeal
                                      && contact.Value > 0f)
                    .ToArray();
                if (contacts.Length == 0)
                {
                    continue;
                }

                var dive = units.TryGetValue(intent.ActorId.Value, out var actor)
                           && actor.PositioningIntent == PositioningIntentKind.BacklineDive;
                var area = IsArea(intent);
                if (!dive && !area)
                {
                    continue;
                }

                var targetIds = contacts.Select(contact => contact.TargetId!.Value.Value).Distinct(StringComparer.Ordinal).ToArray();
                var damage = contacts.Sum(contact => (double)contact.Value);
                var targetMaxHp = targetIds.Sum(id => _maxHpById.TryGetValue(id, out var hp) ? hp : 0d);
                _landings.Add(new ThreatLanding(
                    step.TimeSeconds,
                    targetIds,
                    targetMaxHp > 0d ? damage / targetMaxHp : 0d,
                    _teamMaxHp > 0d ? damage / _teamMaxHp : 0d,
                    contacts.Any(contact => contact.Outcome == CombatOutcome.Kill),
                    string.IsNullOrWhiteSpace(intent.SkillId) ? intent.Kind.ToString() : intent.SkillId!));
            }

            foreach (var battleEvent in step.Events.Where(value => value.EventKind == BattleEventKind.Kill))
            {
                var victimId = battleEvent.KillPayload?.ActualVictim.Value ?? battleEvent.TargetId?.Value;
                if (!string.IsNullOrWhiteSpace(victimId) && _backlineIds.Contains(victimId))
                {
                    _deathTimes.TryAdd(victimId, step.TimeSeconds);
                }
            }
        }

        internal ThreatBattleObservation Complete()
        {
            var windowDeaths = _landings.Count(landing => landing.TargetIds.Any(targetId =>
                _deathTimes.TryGetValue(targetId, out var deathTime)
                && deathTime >= landing.TimeSeconds
                && deathTime <= landing.TimeSeconds + _deathWindowSeconds));
            return new ThreatBattleObservation(
                _landings.Count,
                _landings.Count(landing => landing.ImmediateDeath),
                windowDeaths,
                _landings.Select(landing => landing.TargetHpFraction).ToArray(),
                _landings.Select(landing => landing.TeamHpFraction).ToArray(),
                _landings.GroupBy(landing => landing.SkillId, StringComparer.Ordinal)
                    .ToDictionary(group => group.Key, group => group.Count(), StringComparer.Ordinal),
                _landings.Count > 0);
        }

        private bool IsArea(BattleCombatEventIntent intent)
        {
            if (!string.IsNullOrWhiteSpace(intent.SkillId)
                && _areaSkillsByActor.TryGetValue(intent.ActorId.Value, out var skills)
                && skills.Contains(intent.SkillId))
            {
                return true;
            }

            if (intent.DeliveryKind is SkillDelivery.Nova or SkillDelivery.Aura or SkillDelivery.Zone)
            {
                return true;
            }

            return intent.Kind == CombatEventKind.Skill
                   && (intent.Contacts ?? Array.Empty<BattleContactIntent>())
                       .Count(contact => !contact.IsHeal && contact.Value > 0f) > 1;
        }

        private static IEnumerable<BattleSkillSpec> EnumerateSkills(UnitSnapshot unit)
        {
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var skill in unit.Definition.Skills ?? Array.Empty<BattleSkillSpec>())
            {
                if (skill != null && seen.Add(skill.Id))
                {
                    yield return skill;
                }
            }

            foreach (var skill in new[] { unit.Definition.EffectiveSignatureActive, unit.Definition.EffectiveFlexActive })
            {
                if (skill != null && seen.Add(skill.Id))
                {
                    yield return skill;
                }
            }
        }
    }

    private sealed class ThreatAggregate
    {
        private readonly Dictionary<string, int> _skills = new(StringComparer.Ordinal);
        private int _playerWins;
        private int _battlesWithLanding;

        internal ThreatAggregate(string chapterId, int chapterOrder, string siteId, string encounterId, int battles)
        {
            ChapterId = chapterId;
            ChapterOrder = chapterOrder;
            SiteId = siteId;
            EncounterId = encounterId;
            Battles = battles;
        }

        internal string ChapterId { get; }
        internal int ChapterOrder { get; }
        internal string SiteId { get; }
        internal string EncounterId { get; }
        internal int Battles { get; }
        internal int LandingCount { get; private set; }
        internal int ImmediateDeathCount { get; private set; }
        internal int WindowDeathCount { get; private set; }
        internal List<double> TargetHpFractions { get; } = new();
        internal List<double> TeamHpFractions { get; } = new();

        internal void Record(BattleResult battle, ThreatBattleObservation observation)
        {
            if (battle.Winner == TeamSide.Ally)
            {
                _playerWins++;
            }

            if (observation.HadLanding)
            {
                _battlesWithLanding++;
            }

            LandingCount += observation.LandingCount;
            ImmediateDeathCount += observation.ImmediateDeathCount;
            WindowDeathCount += observation.WindowDeathCount;
            TargetHpFractions.AddRange(observation.TargetHpFractions);
            TeamHpFractions.AddRange(observation.TeamHpFractions);
            foreach (var pair in observation.Skills)
            {
                _skills[pair.Key] = _skills.TryGetValue(pair.Key, out var count) ? count + pair.Value : pair.Value;
            }
        }

        internal object Report() => new
        {
            chapter_id = ChapterId,
            chapter_order = ChapterOrder,
            site_id = SiteId,
            encounter_id = EncounterId,
            battles = Battles,
            player_win_rate = _playerWins / (double)Battles,
            battles_with_landing_rate = _battlesWithLanding / (double)Battles,
            landed_threats = LandingCount,
            mean_contacted_unit_max_hp_pct = Percent(MeanOrNull(TargetHpFractions)),
            median_contacted_unit_max_hp_pct = Percent(Quantile(TargetHpFractions, 0.5)),
            mean_team_max_hp_pct = Percent(MeanOrNull(TeamHpFractions)),
            immediate_death_rate = Rate(ImmediateDeathCount, LandingCount),
            death_within_3_seconds_rate = Rate(WindowDeathCount, LandingCount),
            threat_skills = _skills.OrderByDescending(pair => pair.Value)
                .ThenBy(pair => pair.Key, StringComparer.Ordinal)
                .ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal),
        };
    }
}
