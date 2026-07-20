using Newtonsoft.Json;
using SM.Combat.Model;
using SM.Combat.Services;
using SM.Core.Contracts;
using SM.Core.Stats;
using SM.Meta.Model;
using SM.Meta.Serialization;
using SM.Meta.Services;

internal static class BalanceFrameworkGateRunner
{
    private const string SnapshotRelativePath = "Assets/Resources/_Game/Content/content-snapshot.json";
    private const int DefaultSamples = 2048;
    private const int ObservationSteps = 120;
    private const float DefaultNeutralHealthMore = 0f;
    private const float DefaultNeutralPowerMore = 0f;
    private static readonly BattlefieldLayout RawDamageLayout = new(0.8f, 0.8f, 0f, 0f, 0f, 0f);

    private static readonly string[] RangerIds = { "hunter", "scout", "marksman" };
    private static readonly string[] DuelistIds = { "slayer", "raider", "reaver" };

    public static int Run(string repositoryRoot, IReadOnlyList<string> args)
    {
        try
        {
            var samples = DefaultSamples;
            var neutralHealthMore = DefaultNeutralHealthMore;
            var neutralPowerMore = DefaultNeutralPowerMore;
            float? precisionPowerOverride = null;
            var duelistCorePowerScale = 1f;
            var lowGrowthRangerAttackDelta = 0f;
            string? snapshotOverride = null;
            string? neutralSnapshotOverride = null;
            var emulateBefore = false;
            var neutralEmulateBefore = false;
            string? outputPath = null;
            for (var index = 0; index < args.Count; index++)
            {
                if (string.Equals(args[index], "--samples", StringComparison.Ordinal) && index + 1 < args.Count)
                {
                    if (!int.TryParse(args[++index], out samples) || samples <= 0)
                    {
                        throw new ArgumentException("--samples must be a positive integer.");
                    }
                }
                else if (string.Equals(args[index], "--output", StringComparison.Ordinal) && index + 1 < args.Count)
                {
                    outputPath = args[++index];
                }
                else if (string.Equals(args[index], "--neutral-health-more", StringComparison.Ordinal) && index + 1 < args.Count)
                {
                    neutralHealthMore = ParseFloat(args[++index], "--neutral-health-more");
                }
                else if (string.Equals(args[index], "--neutral-power-more", StringComparison.Ordinal) && index + 1 < args.Count)
                {
                    neutralPowerMore = ParseFloat(args[++index], "--neutral-power-more");
                }
                else if (string.Equals(args[index], "--precision-power", StringComparison.Ordinal) && index + 1 < args.Count)
                {
                    precisionPowerOverride = ParseFloat(args[++index], "--precision-power");
                }
                else if (string.Equals(args[index], "--duelist-core-power-scale", StringComparison.Ordinal) && index + 1 < args.Count)
                {
                    duelistCorePowerScale = ParseFloat(args[++index], "--duelist-core-power-scale");
                }
                else if (string.Equals(args[index], "--low-growth-ranger-atk-delta", StringComparison.Ordinal) && index + 1 < args.Count)
                {
                    lowGrowthRangerAttackDelta = ParseFloat(args[++index], "--low-growth-ranger-atk-delta");
                }
                else if (string.Equals(args[index], "--snapshot", StringComparison.Ordinal) && index + 1 < args.Count)
                {
                    snapshotOverride = args[++index];
                }
                else if (string.Equals(args[index], "--neutral-snapshot", StringComparison.Ordinal) && index + 1 < args.Count)
                {
                    neutralSnapshotOverride = args[++index];
                }
                else if (string.Equals(args[index], "--emulate-before", StringComparison.Ordinal))
                {
                    emulateBefore = true;
                }
                else if (string.Equals(args[index], "--neutral-emulate-before", StringComparison.Ordinal))
                {
                    neutralEmulateBefore = true;
                }
                else
                {
                    throw new ArgumentException($"Unknown balance-framework argument: {args[index]}");
                }
            }

            var snapshotPath = string.IsNullOrWhiteSpace(snapshotOverride)
                ? Path.Combine(repositoryRoot, SnapshotRelativePath.Replace('/', Path.DirectorySeparatorChar))
                : Path.IsPathRooted(snapshotOverride)
                    ? snapshotOverride
                    : Path.Combine(repositoryRoot, snapshotOverride);
            var content = ContentSnapshotJsonSerializer.Deserialize(File.ReadAllText(snapshotPath));
            if (emulateBefore)
            {
                content = EmulateBeforeBalance(content);
            }
            if (precisionPowerOverride.HasValue
                || Math.Abs(duelistCorePowerScale - 1f) > 0.0001f
                || Math.Abs(lowGrowthRangerAttackDelta) > 0.0001f)
            {
                content = ApplyTuning(content, precisionPowerOverride, duelistCorePowerScale, lowGrowthRangerAttackDelta);
            }
            var neutralSnapshotPath = string.IsNullOrWhiteSpace(neutralSnapshotOverride)
                ? snapshotPath
                : Path.IsPathRooted(neutralSnapshotOverride)
                    ? neutralSnapshotOverride
                    : Path.Combine(repositoryRoot, neutralSnapshotOverride);
            var neutralContent = neutralEmulateBefore
                ? ContentSnapshotJsonSerializer.Deserialize(File.ReadAllText(neutralSnapshotPath))
                : string.Equals(snapshotPath, neutralSnapshotPath, StringComparison.OrdinalIgnoreCase)
                    ? content
                    : ContentSnapshotJsonSerializer.Deserialize(File.ReadAllText(neutralSnapshotPath));
            if (neutralEmulateBefore)
            {
                neutralContent = EmulateBeforeBalance(neutralContent);
            }
            var ranger = CompileSquad(content, "balance.gate.3r", RangerIds, TeamPostureType.ProtectCarry);
            var lowGrowthRanger = CompileSquad(
                content,
                "balance.gate.3r.low_growth",
                new[] { "hunter", "scout", "hunter" },
                TeamPostureType.ProtectCarry);
            var duelist = CompileSquad(content, "balance.gate.3d", DuelistIds, TeamPostureType.StandardAdvance);
            var neutral = ScaleNeutral(CompileNeutral(neutralContent).Allies, neutralHealthMore, neutralPowerMore);

            var raw = RangerIds.Concat(DuelistIds)
                .ToDictionary(
                    id => id,
                    id => MeasureRawDamage(FindByArchetype(id.StartsWith("h", StringComparison.Ordinal) || id is "scout" or "marksman" ? ranger.Allies : duelist.Allies, id)),
                    StringComparer.Ordinal);
            var rangerObservation = ObserveArm(ranger, neutral, RangerIds, samples, 41000);
            var duelistObservation = ObserveArm(duelist, neutral, DuelistIds, samples, 41000);
            var lowGrowthRangerWinRate = ObserveWinRate(lowGrowthRanger, neutral, samples, 41000);
            var rangerEffective = EffectiveFirepower(RangerIds, raw, rangerObservation);
            var duelistEffective = EffectiveFirepower(DuelistIds, raw, duelistObservation);
            var ratio = duelistEffective <= 0f ? 0f : rangerEffective / duelistEffective;

            var crit = BuildCritDump(content);
            var peak = BuildPeakReport(raw, crit);
            var report = new
            {
                schema = "balance-framework-gate-v1",
                samples,
                tuple = new
                {
                    boss_deck = new[] { "guardian", "raider", "scout", "priest" },
                    formations = "fixed-informed",
                    build_roll = "level-1-core-no-gear",
                    equipment_roll = "none",
                    role_variant = "authored-default",
                    seed_start = 41000,
                },
                neutral_calibration = new { health_more = neutralHealthMore, power_more = neutralPowerMore },
                neutral_snapshot = Path.GetFileName(neutralSnapshotPath),
                projection = new { player_before = emulateBefore, neutral_before = neutralEmulateBefore || emulateBefore },
                tuning_probe = new
                {
                    precision_power = precisionPowerOverride,
                    duelist_core_power_scale = duelistCorePowerScale,
                    low_growth_ranger_atk_delta = lowGrowthRangerAttackDelta,
                },
                crit,
                raw_damage = raw,
                arms = new
                {
                    ranger_3r = rangerObservation,
                    duelist_3d = duelistObservation,
                    low_growth_ranger_3r_win_rate = lowGrowthRangerWinRate,
                },
                effective_firepower = new
                {
                    ranger_3r = rangerEffective,
                    duelist_3d = duelistEffective,
                    ratio_3r_3d = ratio,
                    ratio_in_band = ratio is >= 0.92f and <= 0.98f,
                },
                peak,
            };

            var json = JsonConvert.SerializeObject(report, Formatting.Indented);
            if (!string.IsNullOrWhiteSpace(outputPath))
            {
                var resolvedOutput = Path.IsPathRooted(outputPath) ? outputPath : Path.Combine(repositoryRoot, outputPath);
                Directory.CreateDirectory(Path.GetDirectoryName(resolvedOutput)!);
                File.WriteAllText(resolvedOutput, json + Environment.NewLine);
            }

            Console.WriteLine(json);
            return 0;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine($"balance-framework ERROR: {exception}");
            return 2;
        }
    }

    private static BattleLoadoutSnapshot CompileSquad(
        CombatContentSnapshot content,
        string blueprintId,
        IReadOnlyList<string> dpsArchetypeIds,
        TeamPostureType posture)
    {
        var ids = new[] { "warden" }.Concat(dpsArchetypeIds).ToArray();
        var anchors = new[]
        {
            DeploymentAnchorId.FrontCenter,
            DeploymentAnchorId.FrontTop,
            DeploymentAnchorId.FrontBottom,
            DeploymentAnchorId.BackCenter,
        };
        return Compile(content, blueprintId, ids, anchors, posture);
    }

    private static CombatContentSnapshot EmulateBeforeBalance(CombatContentSnapshot source)
    {
        var archetypes = source.Archetypes.ToDictionary(
            pair => pair.Key,
            pair => RevertArchetype(pair.Value),
            StringComparer.Ordinal);
        var skillCatalog = source.SkillCatalog.ToDictionary(
            pair => pair.Key,
            pair => RevertSkill(pair.Value),
            StringComparer.Ordinal);
        var itemGrantedSkills = source.ItemGrantedSkills?.ToDictionary(
            pair => pair.Key,
            pair => (IReadOnlyList<BattleSkillSpec>)pair.Value.Select(RevertSkill).ToArray(),
            StringComparer.Ordinal);
        return source with
        {
            Archetypes = archetypes,
            SkillCatalog = skillCatalog,
            ItemGrantedSkills = itemGrantedSkills,
        };
    }

    private static CombatContentSnapshot ApplyTuning(
        CombatContentSnapshot source,
        float? precisionPower,
        float duelistCorePowerScale,
        float lowGrowthRangerAttackDelta)
    {
        BattleSkillSpec Tune(BattleSkillSpec skill)
        {
            if (precisionPower.HasValue && skill.Id == "skill_precision_shot")
            {
                return skill with { Power = precisionPower.Value, PowerFlat = precisionPower.Value };
            }

            if (skill.Id is "skill_slayer_core" or "skill_raider_core" or "skill_reaver_core")
            {
                return skill with
                {
                    Power = skill.Power * duelistCorePowerScale,
                    PowerFlat = skill.PowerFlat * duelistCorePowerScale,
                };
            }

            return skill;
        }

        CombatArchetypeTemplate TuneArchetype(CombatArchetypeTemplate archetype)
        {
            var stats = new Dictionary<StatKey, float>(archetype.BaseStats);
            if (archetype.Id is "hunter" or "scout")
            {
                stats[StatKey.PhysPower] = stats.GetValueOrDefault(StatKey.PhysPower) + lowGrowthRangerAttackDelta;
            }

            return archetype with
            {
                BaseStats = stats,
                Skills = archetype.Skills.Select(Tune).ToArray(),
                RecruitFlexActivePool = archetype.RecruitFlexActivePool?.Select(Tune).ToArray(),
                RecruitFlexPassivePool = archetype.RecruitFlexPassivePool?.Select(Tune).ToArray(),
                SignatureActive = archetype.SignatureActive == null ? null : Tune(archetype.SignatureActive),
                FlexActive = archetype.FlexActive == null ? null : Tune(archetype.FlexActive),
            };
        }

        return source with
        {
            Archetypes = source.Archetypes.ToDictionary(pair => pair.Key, pair => TuneArchetype(pair.Value), StringComparer.Ordinal),
            SkillCatalog = source.SkillCatalog.ToDictionary(pair => pair.Key, pair => Tune(pair.Value), StringComparer.Ordinal),
            ItemGrantedSkills = source.ItemGrantedSkills?.ToDictionary(
                pair => pair.Key,
                pair => (IReadOnlyList<BattleSkillSpec>)pair.Value.Select(Tune).ToArray(),
                StringComparer.Ordinal),
        };
    }

    private static CombatArchetypeTemplate RevertArchetype(CombatArchetypeTemplate archetype)
    {
        var stats = new Dictionary<StatKey, float>(archetype.BaseStats)
        {
            [StatKey.CritChance] = 0f,
            [StatKey.CritMultiplier] = 0f,
        };
        switch (archetype.Id)
        {
            case "slayer":
                stats[StatKey.PhysPower] = 7f;
                break;
            case "raider":
                stats[StatKey.PhysPower] = 8f;
                break;
            case "marksman":
                stats[StatKey.MaxHealth] = 18f;
                stats[StatKey.Armor] = 2f;
                stats[StatKey.AttackSpeed] = 5f;
                stats[StatKey.CritChance] = 0.03f;
                stats[StatKey.CritMultiplier] = 1.35f;
                break;
            case "hunter":
                stats[StatKey.PhysPower] = 6f;
                break;
            case "scout":
                stats[StatKey.PhysPower] = 5f;
                break;
            case "reaver":
                stats[StatKey.CritChance] = 0.03f;
                stats[StatKey.CritMultiplier] = 1.35f;
                break;
            case "bulwark" or "shaman":
                stats[StatKey.CritChance] = 0.01f;
                stats[StatKey.CritMultiplier] = 1.20f;
                break;
        }

        return archetype with
        {
            BaseStats = stats,
            Skills = archetype.Skills.Select(RevertSkill).ToArray(),
            RecruitFlexActivePool = archetype.RecruitFlexActivePool?.Select(RevertSkill).ToArray(),
            RecruitFlexPassivePool = archetype.RecruitFlexPassivePool?.Select(RevertSkill).ToArray(),
            SignatureActive = archetype.SignatureActive == null ? null : RevertSkill(archetype.SignatureActive),
            FlexActive = archetype.FlexActive == null ? null : RevertSkill(archetype.FlexActive),
            ClassStatPackage = null,
        };
    }

    private static BattleSkillSpec RevertSkill(BattleSkillSpec skill)
    {
        if (skill.Id == "skill_precision_shot")
        {
            return skill with { Power = 2f, PowerFlat = 2f, Range = 5.6f };
        }

        if (skill.Id == "skill_marksman_core")
        {
            return skill with { Power = 4.1f, PowerFlat = 4.1f, Range = 5.8f, CastWindupSeconds = 0.17f };
        }

        if (skill.Id == "skill_slayer_core")
        {
            return skill with { Power = 4.4f, PowerFlat = 4.4f };
        }

        if (skill.Id == "skill_raider_core")
        {
            return skill with { Power = 4.2f, PowerFlat = 4.2f };
        }

        if (skill.Id == "skill_reaver_core")
        {
            return skill with { Power = 4f, PowerFlat = 4f };
        }

        return skill.Id is "skill_power_strike"
            or "skill_hexer_core"
            or "skill_priest_core"
            or "skill_shaman_core"
            or "skill_echo_resonance"
            ? skill with { CanCrit = false }
            : skill;
    }

    private static BattleLoadoutSnapshot CompileNeutral(CombatContentSnapshot content)
        => Compile(
            content,
            "balance.gate.neutral",
            new[] { "guardian", "raider", "scout", "priest" },
            new[]
            {
                DeploymentAnchorId.FrontCenter,
                DeploymentAnchorId.FrontBottom,
                DeploymentAnchorId.BackBottom,
                DeploymentAnchorId.BackCenter,
            },
            TeamPostureType.StandardAdvance);

    private static BattleLoadoutSnapshot Compile(
        CombatContentSnapshot content,
        string blueprintId,
        IReadOnlyList<string> archetypeIds,
        IReadOnlyList<DeploymentAnchorId> anchors,
        TeamPostureType posture)
    {
        var heroes = new List<HeroRecord>();
        var loadouts = new Dictionary<string, HeroLoadoutState>(StringComparer.Ordinal);
        var progressions = new Dictionary<string, HeroProgressionState>(StringComparer.Ordinal);
        var assignments = new Dictionary<DeploymentAnchorId, string>();
        for (var index = 0; index < archetypeIds.Count; index++)
        {
            if (!content.Archetypes.TryGetValue(archetypeIds[index], out var archetype))
            {
                throw new InvalidDataException($"Balance gate missing archetype '{archetypeIds[index]}'.");
            }

            var heroId = $"{blueprintId}.hero.{index}";
            heroes.Add(new HeroRecord(heroId, archetype.DisplayName, archetype.Id, archetype.RaceId, archetype.ClassId, string.Empty, string.Empty));
            loadouts[heroId] = new HeroLoadoutState(heroId, Array.Empty<string>(), Array.Empty<string>(), string.Empty, Array.Empty<string>(), Array.Empty<string>());
            progressions[heroId] = new HeroProgressionState(heroId, 1, 0, Array.Empty<string>(), archetype.Skills.Select(skill => skill.Id).Distinct(StringComparer.Ordinal).ToList());
            assignments[anchors[index]] = heroId;
        }

        return new LoadoutCompiler().Compile(
            heroes,
            loadouts,
            progressions,
            new Dictionary<string, ItemInstanceState>(StringComparer.Ordinal),
            new Dictionary<string, SkillInstanceState>(StringComparer.Ordinal),
            new Dictionary<string, PassiveBoardSelectionState>(StringComparer.Ordinal),
            new PermanentAugmentLoadoutState(blueprintId, Array.Empty<string>()),
            new SquadBlueprintState(
                blueprintId,
                blueprintId,
                posture,
                "team_tactic_standard_advance",
                assignments,
                heroes.Select(hero => hero.Id).ToList(),
                new Dictionary<string, string>(StringComparer.Ordinal)),
            new RunOverlayState(0, Array.Empty<string>(), Array.Empty<string>(), LoadoutCompiler.CurrentCompileVersion, string.Empty),
            content);
    }

    private static IReadOnlyList<BattleUnitLoadout> ScaleNeutral(
        IReadOnlyList<BattleUnitLoadout> units,
        float healthMore,
        float powerMore)
    {
        if (healthMore == 0f && powerMore == 0f)
        {
            return units;
        }

        const string sourceId = "balance_gate:neutral_calibration";
        return units.Select(unit =>
        {
            var modifiers = new List<StatModifier>();
            if (healthMore != 0f)
            {
                modifiers.Add(new StatModifier(StatKey.MaxHealth, ModifierOp.More, healthMore, ModifierSource.Other, sourceId));
            }

            if (powerMore != 0f)
            {
                modifiers.Add(new StatModifier(StatKey.PhysPower, ModifierOp.More, powerMore, ModifierSource.Other, sourceId));
                modifiers.Add(new StatModifier(StatKey.MagPower, ModifierOp.More, powerMore, ModifierSource.Other, sourceId));
            }

            var package = new CombatModifierPackage(sourceId, ModifierSource.Other, modifiers);
            return unit with { Packages = unit.NumericPackages.Append(package).ToArray() };
        }).ToArray();
    }

    private static RawDamageReport MeasureRawDamage(BattleUnitLoadout unit)
    {
        var noCrit = DisableCrit(unit) with { PreferredAnchor = DeploymentAnchorId.FrontCenter };
        var def2 = RunRawDamage(noCrit, 2f);
        var def5 = RunRawDamage(noCrit, 5f);
        var stats = BuildStats(unit);
        var chance = stats.Get(StatKey.CritChance);
        var multiplierBonus = stats.Get(StatKey.CritMultiplier);
        var critExpectation = 1f + (chance * multiplierBonus);
        return new RawDamageReport(
            (def2.Dps + def5.Dps) * 0.5f * critExpectation,
            def2.Dps,
            def5.Dps,
            (def2.CoreHit + def5.CoreHit) * 0.5f,
            chance,
            1f + multiplierBonus,
            critExpectation);
    }

    private static RawTrial RunRawDamage(BattleUnitLoadout attacker, float armor)
    {
        var dummy = BuildDummy(armor);
        var state = BattleFactory.Create(
            new[] { attacker },
            new[] { dummy },
            TeamPostureType.StandardAdvance,
            TeamPostureType.HoldLine,
            BattleSimulator.DefaultFixedStepSeconds,
            7301,
            RawDamageLayout);
        var result = BattleResolver.Run(state, ObservationSteps);
        var actorId = state.Allies[0].Id;
        var damage = result.Events
            .Where(evt => evt.ActorId == actorId && IsDamage(evt))
            .Sum(evt => Math.Max(0f, evt.Value));
        var coreHit = result.Events
            .Where(evt => evt.ActorId == actorId && evt.LogCode == BattleLogCode.ActiveSkillDamage)
            .Select(evt => Math.Max(0f, evt.Value))
            .DefaultIfEmpty(0f)
            .Max();
        return new RawTrial(damage / (ObservationSteps * BattleSimulator.DefaultFixedStepSeconds), coreHit);
    }

    private static BattleUnitLoadout DisableCrit(BattleUnitLoadout unit)
    {
        var baseStats = new Dictionary<StatKey, float>(unit.BaseStats)
        {
            [StatKey.CritChance] = 0f,
            [StatKey.CritMultiplier] = 0f,
        };
        BattleSkillSpec Disable(BattleSkillSpec skill) => skill with { CanCrit = false };
        return unit with
        {
            BaseStats = baseStats,
            Skills = unit.Skills.Select(Disable).ToArray(),
            SignatureActive = unit.SignatureActive == null ? null : Disable(unit.SignatureActive),
            FlexActive = unit.FlexActive == null ? null : Disable(unit.FlexActive),
        };
    }

    private static BattleUnitLoadout BuildDummy(float armor)
    {
        var stats = new Dictionary<StatKey, float>
        {
            [StatKey.MaxHealth] = 100000f,
            [StatKey.Armor] = armor,
            [StatKey.Resist] = armor,
            [StatKey.AttackSpeed] = 1f,
            [StatKey.MoveSpeed] = 1f,
            [StatKey.AttackRange] = 0.5f,
            [StatKey.AttackWindup] = 0.2f,
            [StatKey.CastWindup] = 0.2f,
            [StatKey.AttackCooldown] = 1f,
            [StatKey.CollisionRadius] = 0.5f,
        };
        var wait = new TacticRule(0, TacticConditionType.Fallback, 0f, BattleActionType.WaitDefend, TargetSelectorType.Self);
        return new BattleUnitLoadout(
            "standard_dummy",
            "Standard Dummy",
            "dummy",
            "dummy",
            DeploymentAnchorId.FrontCenter,
            stats,
            new[] { new UnitRuleChain("dummy:wait", new[] { wait }) },
            Array.Empty<BattleSkillSpec>());
    }

    private static ArmObservation ObserveArm(
        BattleLoadoutSnapshot squad,
        IReadOnlyList<BattleUnitLoadout> neutral,
        IReadOnlyCollection<string> dpsArchetypes,
        int samples,
        int seedStart)
    {
        var wins = 0;
        var inRangeTicks = dpsArchetypes.ToDictionary(id => id, _ => 0L, StringComparer.Ordinal);
        var q2Ticks = 0L;
        var q3Ticks = 0L;
        var dpsDamage = 0d;
        var dpsOverkill = 0d;
        for (var sample = 0; sample < samples; sample++)
        {
            var state = BattleFactory.Create(
                squad.Allies,
                neutral,
                squad.TeamTactic.Posture,
                neutral.First().TeamTactic?.Posture ?? TeamPostureType.StandardAdvance,
                BattleSimulator.DefaultFixedStepSeconds,
                seedStart + sample);
            var trackedHealth = state.Enemies.ToDictionary(unit => unit.Id.Value, unit => unit.CurrentHealth, StringComparer.Ordinal);
            var trackedMaxHealth = state.Enemies.ToDictionary(unit => unit.Id.Value, unit => unit.MaxHealth, StringComparer.Ordinal);
            var dpsActorIds = state.Allies
                .Where(unit => dpsArchetypes.Contains(unit.Definition.ArchetypeId))
                .ToDictionary(unit => unit.Id.Value, unit => unit.Definition.ArchetypeId, StringComparer.Ordinal);
            var result = BattleResolver.Run(state, BattleSimulator.DefaultMaxSteps, step =>
            {
                if (step.StepIndex <= 0 || step.StepIndex > ObservationSteps)
                {
                    return;
                }

                foreach (var actor in state.Allies.Where(unit => dpsActorIds.ContainsKey(unit.Id.Value)))
                {
                    if (!actor.IsAlive)
                    {
                        continue;
                    }

                    var range = ResolveDamageRange(actor.Definition);
                    if (state.LivingEnemies.Any(target => MovementResolver.IsInActionRange(actor, target, range)))
                    {
                        inRangeTicks[dpsActorIds[actor.Id.Value]]++;
                    }
                }

                var targetGroups = state.Allies
                    .Where(unit => unit.IsAlive && dpsActorIds.ContainsKey(unit.Id.Value) && unit.CurrentTargetId != null)
                    .GroupBy(unit => unit.CurrentTargetId!.Value.Value, StringComparer.Ordinal)
                    .Select(group => group.Count())
                    .ToArray();
                if (targetGroups.Any(count => count >= 2))
                {
                    q2Ticks++;
                }

                if (targetGroups.Any(count => count >= 3))
                {
                    q3Ticks++;
                }

                foreach (var evt in step.Events)
                {
                    if (evt.TargetId == null || !trackedHealth.TryGetValue(evt.TargetId.Value.Value, out var remaining))
                    {
                        continue;
                    }

                    if (IsDamage(evt) && evt.ActorId.Value.StartsWith("ally_", StringComparison.Ordinal))
                    {
                        var value = Math.Max(0f, evt.Value);
                        if (dpsActorIds.ContainsKey(evt.ActorId.Value))
                        {
                            dpsDamage += value;
                            dpsOverkill += Math.Max(0f, value - remaining);
                        }

                        trackedHealth[evt.TargetId.Value.Value] = Math.Max(0f, remaining - value);
                    }
                    else if (evt.LogCode == BattleLogCode.ActiveSkillHeal && evt.ActorId.Value.StartsWith("enemy_", StringComparison.Ordinal))
                    {
                        trackedHealth[evt.TargetId.Value.Value] = Math.Min(trackedMaxHealth[evt.TargetId.Value.Value], remaining + Math.Max(0f, evt.Value));
                    }
                }
            });
            if (result.Winner == TeamSide.Ally)
            {
                wins++;
            }
        }

        var denominator = (double)samples * ObservationSteps;
        var msu = inRangeTicks.ToDictionary(pair => pair.Key, pair => (float)(pair.Value / denominator), StringComparer.Ordinal);
        var q2 = (float)(q2Ticks / denominator);
        var q3 = (float)(q3Ticks / denominator);
        var overkill = dpsDamage <= 0d ? 0f : (float)(dpsOverkill / dpsDamage);
        var focus = (1f + (0.08f * q2) + (0.10f * q3)) * (1f - overkill);
        return new ArmObservation(wins / (float)samples, msu, q2, q3, overkill, focus);
    }

    private static float ObserveWinRate(
        BattleLoadoutSnapshot squad,
        IReadOnlyList<BattleUnitLoadout> neutral,
        int samples,
        int seedStart)
    {
        var wins = 0;
        for (var sample = 0; sample < samples; sample++)
        {
            var state = BattleFactory.Create(
                squad.Allies,
                neutral,
                squad.TeamTactic.Posture,
                neutral.First().TeamTactic?.Posture ?? TeamPostureType.StandardAdvance,
                BattleSimulator.DefaultFixedStepSeconds,
                seedStart + sample);
            if (BattleResolver.Run(state, BattleSimulator.DefaultMaxSteps).Winner == TeamSide.Ally)
            {
                wins++;
            }
        }

        return wins / (float)samples;
    }

    private static float EffectiveFirepower(
        IEnumerable<string> ids,
        IReadOnlyDictionary<string, RawDamageReport> raw,
        ArmObservation observation)
        => observation.FocusFactor * ids.Sum(id => raw[id].ExpectedDps * observation.Msu12[id]);

    private static object BuildCritDump(CombatContentSnapshot content)
    {
        var representatives = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["vanguard"] = "warden",
            ["duelist"] = "slayer",
            ["ranger"] = "hunter",
            ["mystic"] = "priest",
        };
        var values = new Dictionary<string, object>(StringComparer.Ordinal);
        foreach (var pair in representatives)
        {
            var solo = Compile(
                content,
                $"balance.gate.crit.{pair.Key}",
                new[] { pair.Value },
                new[] { DeploymentAnchorId.FrontCenter },
                TeamPostureType.StandardAdvance).Allies[0];
            var baseStats = BuildStats(solo);
            const string sourceId = "balance_gate:overflow_probe";
            var overflow = solo.NumericPackages.Append(new CombatModifierPackage(
                sourceId,
                ModifierSource.Other,
                new[]
                {
                    new StatModifier(StatKey.CritChance, ModifierOp.Flat, 5f, ModifierSource.Other, sourceId),
                    new StatModifier(StatKey.CritMultiplier, ModifierOp.Flat, 5f, ModifierSource.Other, sourceId),
                }));
            var capped = new StatBlock(new Dictionary<StatKey, float>(solo.BaseStats), overflow.SelectMany(package => package.Modifiers));
            values[pair.Key] = new
            {
                base_chance = baseStats.Get(StatKey.CritChance),
                base_total_multiplier = 1f + baseStats.Get(StatKey.CritMultiplier),
                overflow_capped_chance = capped.Get(StatKey.CritChance),
                overflow_capped_total_multiplier = 1f + capped.Get(StatKey.CritMultiplier),
            };
        }

        return values;
    }

    private static object BuildPeakReport(
        IReadOnlyDictionary<string, RawDamageReport> raw,
        object critDump)
    {
        _ = critDump;
        var classCaps = new Dictionary<string, (float Chance, float Bonus)>(StringComparer.Ordinal)
        {
            ["ranger"] = (0.30f, 0.70f),
            ["duelist"] = (0.40f, 0.85f),
        };
        var rangerBase = RangerIds.Max(id => raw[id].CoreHit * raw[id].CritExpectation);
        var duelistBase = DuelistIds.Max(id => raw[id].CoreHit * raw[id].CritExpectation);
        var rangerHigh = RangerIds.Max(id => raw[id].CoreHit * (1f + classCaps["ranger"].Chance * classCaps["ranger"].Bonus));
        var duelistHigh = DuelistIds.Max(id => raw[id].CoreHit * (1f + classCaps["duelist"].Chance * classCaps["duelist"].Bonus));
        var baseRatio = duelistBase <= 0f ? 0f : rangerBase / duelistBase;
        var highRatio = duelistHigh <= 0f ? 0f : rangerHigh / duelistHigh;
        return new
        {
            base_ranger_duelist_ratio = baseRatio,
            capped_high_crit_ranger_duelist_ratio = highRatio,
            ranged_peak_le_080_duelist = baseRatio <= 0.80f && highRatio <= 0.80f,
        };
    }

    private static StatBlock BuildStats(BattleUnitLoadout unit)
        => new(
            new Dictionary<StatKey, float>(unit.BaseStats),
            unit.NumericPackages.SelectMany(package => package.Modifiers));

    private static BattleUnitLoadout FindByArchetype(IEnumerable<BattleUnitLoadout> units, string archetypeId)
        => units.Single(unit => string.Equals(unit.ArchetypeId, archetypeId, StringComparison.Ordinal));

    private static float ResolveDamageRange(BattleUnitLoadout unit)
    {
        var range = unit.BaseStats.TryGetValue(StatKey.AttackRange, out var baseRange) ? baseRange : 0.5f;
        foreach (var skill in unit.Skills.Where(skill => skill.Kind is SkillKind.Strike or SkillKind.Debuff))
        {
            range = Math.Max(range, skill.Range);
        }

        return Math.Max(0.5f, range);
    }

    private static bool IsDamage(BattleEvent evt)
        => evt.LogCode is BattleLogCode.BasicAttackDamage or BattleLogCode.ActiveSkillDamage or BattleLogCode.ComboPayoffDamage;

    private static float ParseFloat(string value, string argument)
    {
        if (!float.TryParse(value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var parsed))
        {
            throw new ArgumentException($"{argument} must be a number.");
        }

        return parsed;
    }

    private sealed record RawTrial(float Dps, float CoreHit);

    private sealed record RawDamageReport(
        float ExpectedDps,
        float NonCritDpsDef2,
        float NonCritDpsDef5,
        float CoreHit,
        float CritChance,
        float CritTotalMultiplier,
        float CritExpectation);

    private sealed record ArmObservation(
        float WinRate,
        IReadOnlyDictionary<string, float> Msu12,
        float Q2,
        float Q3,
        float Overkill,
        float FocusFactor);
}
