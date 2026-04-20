using System.Linq;
using NUnit.Framework;
using SM.Combat.Model;
using SM.Combat.Services;

namespace SM.Tests.EditMode;

[Category("ManualLoopD")]
public sealed class LoopASymmetricMirrorPolicyTests
{
    [Test]
    [Explicit("Tracks symmetric mirror 4v4 timeout/draw policy separately from the FastUnit asymmetric end oracle.")]
    public void SymmetricMirror4v4_ReportsCurrentOutcomeWithoutEnforcingDrawPolicy()
    {
        var resultA = RunMirrorBattle();
        var resultB = RunMirrorBattle();

        Assert.That(resultA.Winner, Is.EqualTo(resultB.Winner));
        Assert.That(resultA.StepCount, Is.EqualTo(resultB.StepCount));
        Assert.That(SummarizeFinalUnits(resultA), Is.EqualTo(SummarizeFinalUnits(resultB)));

        var aliveAllies = resultA.FinalUnits.Count(unit => unit.Side == TeamSide.Ally && unit.IsAlive);
        var aliveEnemies = resultA.FinalUnits.Count(unit => unit.Side == TeamSide.Enemy && unit.IsAlive);
        var timedOut = resultA.StepCount >= BattleSimulator.DefaultMaxSteps;
        TestContext.Out.WriteLine(
            $"symmetric_mirror_4v4 winner={resultA.Winner}; steps={resultA.StepCount}; timedOut={timedOut}; aliveAllies={aliveAllies}; aliveEnemies={aliveEnemies}");
    }

    private static BattleResult RunMirrorBattle()
    {
        var allies = CreateMirrorTeam("ally", "human");
        var enemies = CreateMirrorTeam("enemy", "undead");
        return BattleResolver.Run(
            CombatTestFactory.CreateBattleState(allies, enemies, seed: 42),
            BattleSimulator.DefaultMaxSteps);
    }

    private static BattleUnitLoadout[] CreateMirrorTeam(string prefix, string race)
    {
        return new[]
        {
            CombatTestFactory.CreateLoopAUnit($"{prefix}_guardian", race: race, classId: "vanguard", hp: 110f, physPower: 4f, armor: 4f, attackSpeed: 2f, moveSpeed: 1.65f, anchor: DeploymentAnchorId.FrontTop),
            CombatTestFactory.CreateLoopAUnit($"{prefix}_raider", race: race, classId: "duelist", hp: 72f, physPower: 8f, armor: 1f, attackSpeed: 5f, moveSpeed: 2.05f, anchor: DeploymentAnchorId.FrontBottom),
            CombatTestFactory.CreateLoopAUnit($"{prefix}_hunter", race: race, classId: "ranger", hp: 68f, physPower: 6f, armor: 2f, attackSpeed: 5f, moveSpeed: 1.85f, attackRange: 3.2f, anchor: DeploymentAnchorId.BackTop),
            CombatTestFactory.CreateLoopAUnit($"{prefix}_hexer", race: race, classId: "mystic", hp: 65f, physPower: 4f, armor: 2f, attackSpeed: 4f, moveSpeed: 1.7f, attackRange: 2.8f, anchor: DeploymentAnchorId.BackBottom),
        };
    }

    private static string[] SummarizeFinalUnits(BattleResult result)
    {
        return result.FinalUnits
            .OrderBy(unit => unit.Id, System.StringComparer.Ordinal)
            .Select(unit => $"{unit.Id}:{unit.Side}:{unit.IsAlive}:{unit.CurrentHealth:0.000}:{unit.Position.X:0.000}:{unit.Position.Y:0.000}")
            .ToArray();
    }
}
