using SM.Combat.Model;
using SM.Combat.Services;
using SM.Core.Contracts;
using SM.Meta;
using SM.Meta.Model;
using SM.Meta.Serialization;
using SM.Meta.Services;

internal static class DuelistBuildBalanceGate
{
    internal const int PairedSamples = 32;
    private const int SeedStart = 54000;
    private const int DiveSurvivalStep = 60;
    private const int BruiserSurvivalStep = 100;

    private static readonly string[] DiveIsolationNodes =
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

    private static readonly string[] DiveExecutionNodes =
    {
        "passive_duelist_small_02",
        "passive_duelist_small_04",
        "passive_duelist_notable_01",
        "passive_duelist_small_06",
        "passive_duelist_small_08",
        "passive_duelist_small_13",
        "passive_duelist_notable_03",
        "passive_duelist_notable_07",
    };

    private static readonly string[] BruiserBastionNodes =
    {
        "passive_duelist_small_01",
        "passive_duelist_small_03",
        "passive_duelist_notable_02",
        "passive_duelist_small_09",
        "passive_duelist_small_11",
        "passive_duelist_notable_06",
        "passive_duelist_keystone_02",
        "passive_duelist_small_10",
    };

    private static readonly string[] BruiserSunderNodes =
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

    internal static DuelistBuildBalanceResult Run(CombatContentSnapshot content)
    {
        var statusRules = CombatStatusRuleCompiler.Compile(content);
        var diveIsolation = CompilePlayerSquad(content, "balance.duelist.dive.isolation", DiveIsolationNodes);
        var diveExecution = CompilePlayerSquad(content, "balance.duelist.dive.execution", DiveExecutionNodes);
        var bruiserBastion = CompilePlayerSquad(content, "balance.duelist.bruiser.bastion", BruiserBastionNodes);
        var bruiserSunder = CompilePlayerSquad(content, "balance.duelist.bruiser.sunder", BruiserSunderNodes);

        var neutral = CompileOpponentSquad(
            content,
            "balance.duelist.opponent.neutral",
            new[] { "guardian", "raider", "scout", "priest" });
        var backlineBoss = CompileOpponentSquad(
            content,
            "balance.duelist.opponent.backline",
            new[] { "guardian", "hunter", "marksman", "priest" });
        var twoTankBoss = CompileOpponentSquad(
            content,
            "balance.duelist.opponent.two-tank",
            new[] { "guardian", "bulwark", "raider", "priest" });

        var diveIsolationNeutral = Observe(diveIsolation, neutral, statusRules, DiveSurvivalStep);
        var diveExecutionNeutral = Observe(diveExecution, neutral, statusRules, DiveSurvivalStep);
        var bruiserBastionNeutral = Observe(bruiserBastion, neutral, statusRules, BruiserSurvivalStep);
        var bruiserSunderNeutral = Observe(bruiserSunder, neutral, statusRules, BruiserSurvivalStep);

        // The specialist comparisons are declared before observation: isolation is the backline answer,
        // while sunder is the two-tank answer. Both arms reuse the exact same seed sequence per cell.
        var diveOnBackline = Observe(diveIsolation, backlineBoss, statusRules, DiveSurvivalStep);
        var bruiserOnBackline = Observe(bruiserBastion, backlineBoss, statusRules, BruiserSurvivalStep);
        var diveOnTwoTank = Observe(diveExecution, twoTankBoss, statusRules, DiveSurvivalStep);
        var bruiserOnTwoTank = Observe(bruiserSunder, twoTankBoss, statusRules, BruiserSurvivalStep);

        // Overall viability is the equal-weight balanced boss family. Route choice is informed by authored identity,
        // never by battle outcomes: isolation/bastion answer backline pressure; execution/sunder answer two tanks.
        var diveWinRate = (diveOnBackline.WinRate + diveOnTwoTank.WinRate) * 0.5f;
        var bruiserWinRate = (bruiserOnBackline.WinRate + bruiserOnTwoTank.WinRate) * 0.5f;
        var neutralDiffPp = MathF.Abs(diveWinRate - bruiserWinRate) * 100f;
        var backlineAdvantagePp = (diveOnBackline.WinRate - bruiserOnBackline.WinRate) * 100f;
        var twoTankAdvantagePp = (bruiserOnTwoTank.WinRate - diveOnTwoTank.WinRate) * 100f;
        var specialistAdvantagePp = (backlineAdvantagePp + twoTankAdvantagePp) * 0.5f;
        var diveSurvival = diveOnBackline.SubjectSurvivalRate;
        var bruiserSurvival = bruiserOnTwoTank.SubjectSurvivalRate;
        var inBand = IsNeutralWinRateInBand(diveWinRate)
                     && IsNeutralWinRateInBand(bruiserWinRate)
                     && neutralDiffPp <= 5f
                     && IsSpecialistAdvantageInBand(backlineAdvantagePp)
                     && IsSpecialistAdvantageInBand(twoTankAdvantagePp)
                     && diveSurvival is >= 0.45f and <= 0.55f
                     && bruiserSurvival is >= 0.65f and <= 0.75f;

        return new DuelistBuildBalanceResult(
            PairedSamples,
            SeedStart,
            diveWinRate,
            bruiserWinRate,
            neutralDiffPp,
            backlineAdvantagePp,
            twoTankAdvantagePp,
            specialistAdvantagePp,
            diveSurvival,
            bruiserSurvival,
            inBand,
            diveIsolationNeutral,
            diveExecutionNeutral,
            bruiserBastionNeutral,
            bruiserSunderNeutral,
            diveOnBackline,
            bruiserOnBackline,
            diveOnTwoTank,
            bruiserOnTwoTank);
    }

    internal static object BuildReport(DuelistBuildBalanceResult result)
    {
        return new
        {
            paired_samples = result.PairedSamples,
            seed_start = result.SeedStart,
            build_a = "dive_assassin",
            build_b = "tank_bruiser",
            build_a_win_rate = result.DiveBuildWinRate,
            build_b_win_rate = result.BruiserBuildWinRate,
            neutral_diff_pp = result.NeutralDiffPp,
            backline_boss_dive_advantage_pp = result.BacklineAdvantagePp,
            two_tank_boss_bruiser_advantage_pp = result.TwoTankAdvantagePp,
            specialist_matchup_pp = result.SpecialistAdvantagePp,
            informed_choice_share = new { dive = 0.5f, bruiser = 0.5f },
            boss_family = new
            {
                backline = new[] { "guardian", "hunter", "marksman", "priest" },
                two_tank = new[] { "guardian", "bulwark", "raider", "priest" },
                equal_weight = true,
            },
            representative_routes = new
            {
                backline = new { dive = "dive_isolation", bruiser = "bruiser_bastion" },
                two_tank = new { dive = "dive_execution", bruiser = "bruiser_sunder" },
            },
            dive_six_second_survival_rate = result.DiveSixSecondSurvivalRate,
            bruiser_ten_second_survival_rate = result.BruiserTenSecondSurvivalRate,
            in_band = result.InBand,
            cells = new
            {
                neutral = new
                {
                    dive_isolation = result.DiveIsolationNeutral,
                    dive_execution = result.DiveExecutionNeutral,
                    bruiser_bastion = result.BruiserBastionNeutral,
                    bruiser_sunder = result.BruiserSunderNeutral,
                },
                backline_boss = new
                {
                    dive_isolation = result.DiveOnBacklineBoss,
                    bruiser_bastion = result.BruiserOnBacklineBoss,
                },
                two_tank_boss = new
                {
                    dive_execution = result.DiveOnTwoTankBoss,
                    bruiser_sunder = result.BruiserOnTwoTankBoss,
                },
            },
        };
    }

    private static bool IsNeutralWinRateInBand(float value) => value is >= 0.48f and <= 0.53f;

    private static bool IsSpecialistAdvantageInBand(float value) => value is >= 8f and <= 15f;

    private static DuelistBuildCellObservation Observe(
        BattleLoadoutSnapshot player,
        BattleLoadoutSnapshot opponent,
        CombatStatusRules statusRules,
        int survivalStep)
    {
        var wins = 0;
        var survived = 0;
        for (var sample = 0; sample < PairedSamples; sample++)
        {
            var state = BattleFactory.Create(
                player.Allies,
                opponent.Allies,
                player.TeamTactic.Posture,
                opponent.TeamTactic.Posture,
                BattleSimulator.DefaultFixedStepSeconds,
                SeedStart + sample,
                statusRules: statusRules);
            var subject = state.Allies.Single(unit =>
                string.Equals(unit.Definition.ArchetypeId, "slayer", StringComparison.Ordinal));
            var reachedHorizonAlive = false;
            var result = BattleResolver.Run(state, BattleSimulator.DefaultMaxSteps, step =>
            {
                if (step.StepIndex == survivalStep && subject.IsAlive)
                {
                    reachedHorizonAlive = true;
                }
            });

            if (result.Winner == TeamSide.Ally)
            {
                wins++;
            }

            if (reachedHorizonAlive || (result.StepCount < survivalStep && subject.IsAlive))
            {
                survived++;
            }
        }

        return new DuelistBuildCellObservation(
            wins / (float)PairedSamples,
            survived / (float)PairedSamples,
            survivalStep * BattleSimulator.DefaultFixedStepSeconds);
    }

    private static BattleLoadoutSnapshot CompilePlayerSquad(
        CombatContentSnapshot content,
        string blueprintId,
        IReadOnlyList<string> duelistNodes)
        => Compile(
            content,
            blueprintId,
            new[] { "warden", "slayer", "scout", "priest" },
            new[]
            {
                DeploymentAnchorId.FrontCenter,
                DeploymentAnchorId.FrontTop,
                DeploymentAnchorId.BackBottom,
                DeploymentAnchorId.BackCenter,
            },
            duelistNodes,
            equipDuelistBlade: true);

    private static BattleLoadoutSnapshot CompileOpponentSquad(
        CombatContentSnapshot content,
        string blueprintId,
        IReadOnlyList<string> archetypeIds)
        => Compile(
            content,
            blueprintId,
            archetypeIds,
            new[]
            {
                DeploymentAnchorId.FrontCenter,
                DeploymentAnchorId.FrontTop,
                DeploymentAnchorId.BackBottom,
                DeploymentAnchorId.BackCenter,
            },
            Array.Empty<string>(),
            equipDuelistBlade: false);

    private static BattleLoadoutSnapshot Compile(
        CombatContentSnapshot content,
        string blueprintId,
        IReadOnlyList<string> archetypeIds,
        IReadOnlyList<DeploymentAnchorId> anchors,
        IReadOnlyList<string> duelistNodes,
        bool equipDuelistBlade)
    {
        var heroes = new List<HeroRecord>();
        var loadouts = new Dictionary<string, HeroLoadoutState>(StringComparer.Ordinal);
        var progressions = new Dictionary<string, HeroProgressionState>(StringComparer.Ordinal);
        var itemInstances = new Dictionary<string, ItemInstanceState>(StringComparer.Ordinal);
        var passiveSelections = new Dictionary<string, PassiveBoardSelectionState>(StringComparer.Ordinal);
        var assignments = new Dictionary<DeploymentAnchorId, string>();
        for (var index = 0; index < archetypeIds.Count; index++)
        {
            var archetypeId = archetypeIds[index];
            if (!content.Archetypes.TryGetValue(archetypeId, out var archetype))
            {
                throw new InvalidDataException($"Duelist build balance gate missing archetype '{archetypeId}'.");
            }

            var heroId = $"{blueprintId}.hero.{index}";
            var isDuelistSubject = string.Equals(archetypeId, "slayer", StringComparison.Ordinal)
                                   && duelistNodes.Count > 0;
            var boardId = isDuelistSubject ? "board_duelist" : string.Empty;
            var equippedItems = Array.Empty<string>();
            if (isDuelistSubject && equipDuelistBlade)
            {
                var itemInstanceId = $"{heroId}.item.blade";
                itemInstances[itemInstanceId] = new ItemInstanceState(
                    itemInstanceId,
                    "item_slayer_blade",
                    Array.Empty<string>(),
                    heroId);
                equippedItems = new[] { itemInstanceId };
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
                equippedItems,
                Array.Empty<string>(),
                boardId,
                isDuelistSubject ? duelistNodes : Array.Empty<string>(),
                Array.Empty<string>());
            progressions[heroId] = new HeroProgressionState(
                heroId,
                1,
                0,
                isDuelistSubject ? duelistNodes : Array.Empty<string>(),
                archetype.Skills.Select(skill => skill.Id).Distinct(StringComparer.Ordinal).ToArray());
            if (isDuelistSubject)
            {
                passiveSelections[heroId] = new PassiveBoardSelectionState(heroId, boardId, duelistNodes);
            }

            assignments[anchors[index]] = heroId;
        }

        return new LoadoutCompiler().Compile(
            heroes,
            loadouts,
            progressions,
            itemInstances,
            new Dictionary<string, SkillInstanceState>(StringComparer.Ordinal),
            passiveSelections,
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
}

internal sealed record DuelistBuildCellObservation(
    float WinRate,
    float SubjectSurvivalRate,
    float SurvivalHorizonSeconds);

internal sealed record DuelistBuildBalanceResult(
    int PairedSamples,
    int SeedStart,
    float DiveBuildWinRate,
    float BruiserBuildWinRate,
    float NeutralDiffPp,
    float BacklineAdvantagePp,
    float TwoTankAdvantagePp,
    float SpecialistAdvantagePp,
    float DiveSixSecondSurvivalRate,
    float BruiserTenSecondSurvivalRate,
    bool InBand,
    DuelistBuildCellObservation DiveIsolationNeutral,
    DuelistBuildCellObservation DiveExecutionNeutral,
    DuelistBuildCellObservation BruiserBastionNeutral,
    DuelistBuildCellObservation BruiserSunderNeutral,
    DuelistBuildCellObservation DiveOnBacklineBoss,
    DuelistBuildCellObservation BruiserOnBacklineBoss,
    DuelistBuildCellObservation DiveOnTwoTankBoss,
    DuelistBuildCellObservation BruiserOnTwoTankBoss);
