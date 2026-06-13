using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using NUnit.Framework;
using SM.Combat.Model;
using SM.Combat.Services;
using SM.Core.Contracts;

namespace SM.Tests.EditMode;

/// <summary>
/// Formation consequence numeric sweep — P0에서 남긴 수치 대역을 FastUnit에서 고정한다.
/// Production truth는 BattleFormationConsequence에 두고, 여기서는 guard radius / protect bias
/// preset 조합이 피해, 표적 선택, 풀시뮬 결과를 실제로 가르는지만 본다.
/// </summary>
[Category("FastUnit")]
public sealed class BattleFormationConsequenceTuningSweepTests
{
    private static readonly float[] GuardRadiusPresets = { 2.5f, 3.0f };
    private static readonly float[] ProtectCarryBiasPresets = { 0f, 0.5f, 1f };

    private static readonly BehaviorProfile CleanBackline = new(
        0.25f, 0.2f, 0f, 0f, 0.5f, 0.5f,
        DodgeChance: 0f, BlockChance: 0f, BlockMitigation: 0f, Stability: 0.5f,
        FormationLine: FormationLine.Backline);

    private static readonly BehaviorProfile CleanFrontline = new(
        0.25f, 0.2f, 0f, 0f, 0.5f, 0.5f,
        DodgeChance: 0f, BlockChance: 0f, BlockMitigation: 0f, Stability: 0.5f,
        FormationLine: FormationLine.Frontline);

    [Test]
    public void ScreenTuningMatrix_KeepsDamageAndTargetingInChosenBand()
    {
        var rows = new List<ScreenTuningRow>();
        foreach (var guardRadius in GuardRadiusPresets)
        {
            foreach (var protectBias in ProtectCarryBiasPresets)
            {
                var protectedDamageState = CreateDamageProbe(guardRadius, protectBias, new CombatVector2(-3.75f, 0f));
                var exposedDamageState = CreateDamageProbe(guardRadius, protectBias, new CombatVector2(-4f, guardRadius + 0.15f));
                var protectedAttacker = protectedDamageState.Enemies.Single();
                var exposedAttacker = exposedDamageState.Enemies.Single();
                var protectedCarry = protectedDamageState.Allies.Single(unit => unit.Definition.Id == "carry");
                var exposedCarry = exposedDamageState.Allies.Single(unit => unit.Definition.Id == "carry");

                var protectedHit = HitResolutionService.ResolveBasicAttack(protectedDamageState, protectedAttacker, protectedCarry);
                var exposedHit = HitResolutionService.ResolveBasicAttack(exposedDamageState, exposedAttacker, exposedCarry);
                var keptRatio = protectedHit.Value / exposedHit.Value;
                var mitigation = 1f - keptRatio;
                var expectedMitigation = BattleFormationConsequence.ScreenMitigationBase
                                         + (BattleFormationConsequence.ScreenMitigationProtectCarryBonus * protectBias);

                var protectedPick = SelectProbeTarget(guardRadius, protectBias, protectedScreen: true);
                var exposedPick = SelectProbeTarget(guardRadius, protectBias, protectedScreen: false);
                rows.Add(new ScreenTuningRow(guardRadius, protectBias, mitigation, protectedPick, exposedPick));

                Assert.That(mitigation, Is.EqualTo(expectedMitigation).Within(0.015f),
                    $"screen mitigation should track base+protect bias for guard={guardRadius}, protect={protectBias}");
                Assert.That(mitigation, Is.GreaterThanOrEqualTo(0.119f).And.LessThanOrEqualTo(0.201f),
                    "fixed-point projection may land a few basis points around the authored 12%~20% band");
                Assert.That(protectedPick, Is.EqualTo("enemy_screen"),
                    "0.35m target penalty should keep screened carry from winning normal target selection");
                Assert.That(exposedPick, Is.EqualTo("enemy_carry"),
                    "moving the screen outside guard radius should expose the carry again");
            }
        }

        Assert.That(BattleFormationConsequence.ScreenedTargetScorePenalty, Is.EqualTo(0.35f).Within(0.0001f));
        TestContext.Out.WriteLine(BuildScreenReport(rows));
    }

    [Test]
    public void FlankBonusMatrix_KeepsSideAndRearReadable()
    {
        var front = ResolveFlankProbe(new CombatVector2(-2f, 0.5f));
        var side = ResolveFlankProbe(new CombatVector2(0f, 2f));
        var rear = ResolveFlankProbe(new CombatVector2(2f, 0f));

        Assert.That(side.Value / front.Value, Is.EqualTo(1f + BattleFormationConsequence.SideFlankDamageBonus).Within(0.015f));
        Assert.That(rear.Value / front.Value, Is.EqualTo(1f + BattleFormationConsequence.RearFlankDamageBonus).Within(0.015f));
        Assert.That(side.Value, Is.GreaterThan(front.Value));
        Assert.That(rear.Value, Is.GreaterThan(side.Value));
        Assert.That(BattleFormationConsequence.SideFlankDamageBonus, Is.EqualTo(0.06f).Within(0.0001f));
        Assert.That(BattleFormationConsequence.RearFlankDamageBonus, Is.EqualTo(0.12f).Within(0.0001f));

        TestContext.Out.WriteLine(string.Format(
            CultureInfo.InvariantCulture,
            "flank bonus matrix: front={0:0.###}, side={1:0.###} (+{2:P0}), rear={3:0.###} (+{4:P0})",
            front.Value,
            side.Value,
            BattleFormationConsequence.SideFlankDamageBonus,
            rear.Value,
            BattleFormationConsequence.RearFlankDamageBonus));
    }

    [Test]
    public void DeploymentSweep_GuardRadiusPresetsKeepFormationOutcomeLegible()
    {
        var rows = new List<DeploymentSweepRow>();
        foreach (var guardRadius in GuardRadiusPresets)
        {
            foreach (var protectBias in new[] { 0f, 1f })
            {
                var aligned = RunDeploymentScenario(
                    guardRadius,
                    protectBias,
                    vanguardAnchors: (DeploymentAnchorId.FrontTop, DeploymentAnchorId.FrontBottom),
                    rangerAnchor: DeploymentAnchorId.BackTop,
                    mysticAnchor: DeploymentAnchorId.BackBottom);
                var misaligned = RunDeploymentScenario(
                    guardRadius,
                    protectBias,
                    vanguardAnchors: (DeploymentAnchorId.FrontTop, DeploymentAnchorId.FrontTop),
                    rangerAnchor: DeploymentAnchorId.BackBottom,
                    mysticAnchor: DeploymentAnchorId.BackBottom);

                rows.Add(new DeploymentSweepRow(
                    guardRadius,
                    protectBias,
                    aligned.BacklineDamageTaken,
                    misaligned.BacklineDamageTaken,
                    aligned.AllySurvivors,
                    aligned.EnemySurvivors,
                    misaligned.AllySurvivors,
                    misaligned.EnemySurvivors));

                Assert.That(aligned.AllySurvivors, Is.GreaterThan(0),
                    $"aligned formation should survive at guard={guardRadius}, protect={protectBias}");
                Assert.That(aligned.EnemySurvivors, Is.Zero,
                    $"aligned formation should wipe enemies at guard={guardRadius}, protect={protectBias}");
                Assert.That(misaligned.AllySurvivors, Is.Zero,
                    $"misaligned formation should collapse at guard={guardRadius}, protect={protectBias}");
                Assert.That(misaligned.BacklineDamageTaken, Is.GreaterThan(aligned.BacklineDamageTaken * 1.15f),
                    $"backline damage should split by at least 15% at guard={guardRadius}, protect={protectBias}");
            }
        }

        TestContext.Out.WriteLine(BuildDeploymentReport(rows));
    }

    private static BattleState CreateDamageProbe(float guardRadius, float protectBias, CombatVector2 screenPosition)
    {
        var tactic = ProtectTactic(protectBias);
        var state = BattleFactory.Create(
            new[]
            {
                CombatTestFactory.CreateLoopAUnit("screen", behavior: CleanFrontline with { FrontlineGuardRadius = guardRadius }) with { TeamTactic = tactic },
                CombatTestFactory.CreateLoopAUnit("carry", classId: "ranger", behavior: CleanBackline) with { TeamTactic = tactic },
            },
            new[] { CombatTestFactory.CreateLoopAUnit("attacker", physPower: 8f) },
            seed: 3);
        state.Allies.Single(unit => unit.Definition.Id == "carry").SetPosition(new CombatVector2(-4f, 0f));
        state.Allies.Single(unit => unit.Definition.Id == "screen").SetPosition(screenPosition);
        state.Enemies.Single().SetPosition(new CombatVector2(2f, 0f));
        return state;
    }

    private static string SelectProbeTarget(float guardRadius, float protectBias, bool protectedScreen)
    {
        var tactic = ProtectTactic(protectBias);
        var state = BattleFactory.Create(
            new[] { CombatTestFactory.CreateLoopAUnit("shooter", classId: "ranger", attackRange: 8f) },
            new[]
            {
                CombatTestFactory.CreateLoopAUnit("enemy_screen", behavior: CleanFrontline with { FrontlineGuardRadius = guardRadius }) with { TeamTactic = tactic },
                CombatTestFactory.CreateLoopAUnit("enemy_carry", classId: "mystic", behavior: CleanBackline) with { TeamTactic = tactic },
            },
            seed: 5);
        var shooter = state.Allies.Single();
        var screen = state.Enemies.Single(unit => unit.Definition.Id == "enemy_screen");
        var carry = state.Enemies.Single(unit => unit.Definition.Id == "enemy_carry");
        shooter.SetPosition(new CombatVector2(-2f, 0f));
        carry.SetPosition(new CombatVector2(2.0f, 0f));
        screen.SetPosition(protectedScreen
            ? new CombatVector2(2.25f, 0f)
            : new CombatVector2(2.25f, guardRadius + 0.15f));

        return TargetScoringService
            .SelectTarget(state, shooter, TargetSelectorType.FirstEnemyInRange, BattleActionType.BasicAttack, null)
            ?.Definition.Id ?? string.Empty;
    }

    private static HitResolutionResult ResolveFlankProbe(CombatVector2 attackerPosition)
    {
        var state = BattleFactory.Create(
            new[]
            {
                CombatTestFactory.CreateLoopAUnit("attacker", physPower: 8f),
                CombatTestFactory.CreateLoopAUnit("bait"),
            },
            new[] { CombatTestFactory.CreateLoopAUnit("defender", behavior: CleanFrontline) },
            seed: 7);
        var attacker = state.Allies.Single(unit => unit.Definition.Id == "attacker");
        var defender = state.Enemies.Single();
        var bait = state.Allies.Single(unit => unit.Definition.Id == "bait");

        defender.SetPosition(new CombatVector2(0f, 0f));
        bait.SetPosition(new CombatVector2(-2f, 0f));
        defender.SetCurrentTarget(bait.Id);
        attacker.SetPosition(attackerPosition);
        return HitResolutionService.ResolveBasicAttack(state, attacker, defender);
    }

    private static DeploymentOutcome RunDeploymentScenario(
        float guardRadius,
        float protectBias,
        (DeploymentAnchorId First, DeploymentAnchorId Second) vanguardAnchors,
        DeploymentAnchorId rangerAnchor,
        DeploymentAnchorId mysticAnchor)
    {
        var allyTactic = new TeamTacticProfile(
            "standard_sweep",
            "Standard Sweep",
            TeamPostureType.StandardAdvance,
            ProtectCarryBias: protectBias);
        var enemyTactic = new TeamTacticProfile("collapse", "Collapse", TeamPostureType.CollapseWeakSide);
        var allyVanguardBehavior = CombatProfileDefaults.ResolveBehavior(null, "vanguard") with { FrontlineGuardRadius = guardRadius };

        var allies = new[]
        {
            CombatTestFactory.CreateLoopAUnit("ally_van_a", anchor: vanguardAnchors.First, hp: 90f, armor: 3f, behavior: allyVanguardBehavior) with { TeamTactic = allyTactic },
            CombatTestFactory.CreateLoopAUnit("ally_van_b", anchor: vanguardAnchors.Second, hp: 90f, armor: 3f, behavior: allyVanguardBehavior) with { TeamTactic = allyTactic },
            CombatTestFactory.CreateLoopAUnit("ally_ranger", classId: "ranger", anchor: rangerAnchor, hp: 45f, attackRange: 6f) with { TeamTactic = allyTactic },
            CombatTestFactory.CreateLoopAUnit("ally_mystic", classId: "mystic", anchor: mysticAnchor, hp: 45f, attackRange: 2.6f) with { TeamTactic = allyTactic },
        };
        var enemies = new[]
        {
            CombatTestFactory.CreateLoopAUnit("enemy_van", anchor: DeploymentAnchorId.FrontTop, hp: 90f, armor: 3f) with { TeamTactic = enemyTactic },
            CombatTestFactory.CreateLoopAUnit("enemy_duelist_a", classId: "duelist", anchor: DeploymentAnchorId.FrontBottom, hp: 60f, physPower: 6f) with { TeamTactic = enemyTactic },
            CombatTestFactory.CreateLoopAUnit("enemy_duelist_b", classId: "duelist", anchor: DeploymentAnchorId.FrontCenter, hp: 60f, physPower: 6f) with { TeamTactic = enemyTactic },
            CombatTestFactory.CreateLoopAUnit("enemy_ranger", classId: "ranger", anchor: DeploymentAnchorId.BackTop, hp: 45f, attackRange: 6f) with { TeamTactic = enemyTactic },
        };

        var state = BattleFactory.Create(allies, enemies, seed: 77);
        _ = new BattleSimulator(state, 180).RunToEnd();
        var backlineDamage = state.Allies
            .Where(unit => unit.Definition.Id is "ally_ranger" or "ally_mystic")
            .Sum(unit => unit.MaxHealth - unit.CurrentHealth);

        return new DeploymentOutcome(
            backlineDamage,
            state.Allies.Count(unit => unit.IsAlive),
            state.Enemies.Count(unit => unit.IsAlive));
    }

    private static TeamTacticProfile ProtectTactic(float protectBias)
    {
        return new TeamTacticProfile(
            $"protect_{protectBias.ToString("0.0", CultureInfo.InvariantCulture)}",
            "Protect",
            TeamPostureType.ProtectCarry,
            ProtectCarryBias: protectBias);
    }

    private static string BuildScreenReport(IReadOnlyList<ScreenTuningRow> rows)
    {
        var builder = new StringBuilder();
        builder.AppendLine("formation screen tuning matrix");
        builder.AppendLine("guard_radius | protect_bias | mitigation | protected_pick | exposed_pick");
        foreach (var row in rows)
        {
            builder.AppendLine(string.Format(
                CultureInfo.InvariantCulture,
                "{0:0.0} | {1:0.0} | {2:P0} | {3} | {4}",
                row.GuardRadius,
                row.ProtectBias,
                row.Mitigation,
                row.ProtectedPick,
                row.ExposedPick));
        }

        return builder.ToString();
    }

    private static string BuildDeploymentReport(IReadOnlyList<DeploymentSweepRow> rows)
    {
        var builder = new StringBuilder();
        builder.AppendLine("formation deployment outcome sweep");
        builder.AppendLine("guard_radius | protect_bias | aligned_backline_damage | misaligned_backline_damage | aligned_survivors | misaligned_survivors");
        foreach (var row in rows)
        {
            builder.AppendLine(string.Format(
                CultureInfo.InvariantCulture,
                "{0:0.0} | {1:0.0} | {2:0.0} | {3:0.0} | A:{4}/E:{5} | A:{6}/E:{7}",
                row.GuardRadius,
                row.ProtectBias,
                row.AlignedBacklineDamage,
                row.MisalignedBacklineDamage,
                row.AlignedAllySurvivors,
                row.AlignedEnemySurvivors,
                row.MisalignedAllySurvivors,
                row.MisalignedEnemySurvivors));
        }

        return builder.ToString();
    }

    private sealed record ScreenTuningRow(
        float GuardRadius,
        float ProtectBias,
        float Mitigation,
        string ProtectedPick,
        string ExposedPick);

    private sealed record DeploymentOutcome(
        float BacklineDamageTaken,
        int AllySurvivors,
        int EnemySurvivors);

    private sealed record DeploymentSweepRow(
        float GuardRadius,
        float ProtectBias,
        float AlignedBacklineDamage,
        float MisalignedBacklineDamage,
        int AlignedAllySurvivors,
        int AlignedEnemySurvivors,
        int MisalignedAllySurvivors,
        int MisalignedEnemySurvivors);
}
