using NUnit.Framework;
using SM.Combat.Model;
using SM.Combat.Services;
using SM.Core.Ids;
using SM.Core.Stats;
using System.Collections.Generic;
using System.Reflection;

namespace SM.Tests.EditMode;

/// <summary>
/// Phase 4 damage 고정소수점화 검증 (ADR-0029). <see cref="HitResolutionService"/>의 damage 곱셈 체인이
/// Hp64×Fixed32 결정 산술로 계산되는지(=결과 float가 Q16.16 projection인지)와 min-damage 1.0 HP floor를
/// 격리 상태로 못박는다. sim-level 결정성/outcome은 BattleDeterminismBaseline/BattleResolution이 덮는다.
/// </summary>
[Category("FastUnit")]
public sealed class HitResolutionServiceTests
{
    private static UnitSnapshot MakeUnit(string id, TeamSide side, string classId = "vanguard")
    {
        var loadout = CombatTestFactory.CreateLoopAUnit(id, classId: classId);
        return new UnitSnapshot(
            new EntityId(id),
            side,
            loadout,
            BattleFactory.ResolveAnchorPosition(side, DeploymentAnchorId.FrontCenter),
            BattleFactory.ResolveSpawnPosition(side, DeploymentAnchorId.FrontCenter));
    }

    private static BattleState MakeState(UnitSnapshot actor, UnitSnapshot target)
    {
        return new BattleState(
            new[] { actor },
            new[] { target },
            TeamPostureType.StandardAdvance,
            TeamPostureType.StandardAdvance,
            BattleSimulator.DefaultFixedStepSeconds,
            7);
    }

    [Test]
    public void BasicAttackDamage_IsQ16_16Projection_OfFixedComputation()
    {
        var actor = MakeUnit("actor", TeamSide.Ally);
        var target = MakeUnit("target", TeamSide.Enemy);
        var state = MakeState(actor, target);

        var result = HitResolutionService.ResolveBasicAttack(state, actor, target);

        // dodge면 0, 아니면 fixed 곱셈 체인의 float projection이어야 한다 → Value × 65536이 정수(raw).
        if (!result.WasDodged)
        {
            var raw = result.Value * 65536f;
            Assert.That(raw, Is.EqualTo(System.MathF.Round(raw)).Within(0.01f),
                "damage는 Hp64(Q16.16) 권위에서 계산된 정확한 projection이어야 한다");
        }
    }

    [Test]
    public void BasicAttackDamage_RespectsMinDamageFloor_OrDodgeZero()
    {
        var actor = MakeUnit("actor", TeamSide.Ally);
        var target = MakeUnit("target", TeamSide.Enemy);
        var state = MakeState(actor, target);

        var result = HitResolutionService.ResolveBasicAttack(state, actor, target);

        // min-damage floor는 1.0 HP. 회피 시에만 0.
        Assert.That(result.WasDodged ? result.Value == 0f : result.Value >= 1f, Is.True,
            "피격 damage는 1.0 HP 이상이거나(floor), 회피 시 0이어야 한다");
    }

    [TestCase(0.04f, 400)]
    [TestCase(0.06f, 600)]
    [TestCase(0.12f, 1200)]
    [TestCase(0.03125f, 313)]
    public void ProbabilityToBasisPoints_UsesBackendStableNearestRounding(float probability, int expected)
    {
        var method = typeof(HitResolutionService).GetMethod(
            "ProbabilityToBasisPoints",
            BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new AssertionException("ProbabilityToBasisPoints method was not found.");

        var actual = (int)method.Invoke(null, new object[] { probability });

        Assert.That(actual, Is.EqualTo(expected));
    }

    [Test]
    public void ExecuteLowHp_UsesInclusiveThirtyFivePercentPreHitBoundary_Deterministically()
    {
        var atBoundary = ResolveExecuteHit(targetDamage: 65f, tagged: true);
        var repeated = ResolveExecuteHit(targetDamage: 65f, tagged: true);
        var aboveBoundary = ResolveExecuteHit(targetDamage: 64.99f, tagged: true);
        var untaggedAtBoundary = ResolveExecuteHit(targetDamage: 65f, tagged: false);

        Assert.That(atBoundary.Note, Does.Contain("execute"));
        Assert.That(atBoundary.Value, Is.EqualTo(repeated.Value));
        Assert.That(atBoundary.Note, Is.EqualTo(repeated.Note));
        Assert.That(atBoundary.Value, Is.GreaterThan(untaggedAtBoundary.Value));
        Assert.That(aboveBoundary.Note, Does.Not.Contain("execute"));
    }

    private static HitResolutionResult ResolveExecuteHit(float targetDamage, bool tagged)
    {
        var noAvoidance = new BehaviorProfile(
            0.25f,
            0f,
            0f,
            0f,
            0f,
            1f,
            0f,
            0f,
            0f,
            1f);
        var actorLoadout = CombatTestFactory.CreateLoopAUnit(
            "execute_actor",
            classId: "duelist",
            hp: 100f,
            physPower: 8f,
            armor: 0f,
            behavior: noAvoidance);
        if (tagged)
        {
            actorLoadout = actorLoadout with
            {
                RulePackages = new[]
                {
                    new CombatRuleModifierPackage(
                        "test:execute",
                        ModifierSource.Other,
                        new[]
                        {
                            new RuleModifier(RuleModifierKind.BehaviorTag, CombatBehaviorTags.ExecuteLowHp),
                        }),
                },
            };
        }

        var targetLoadout = CombatTestFactory.CreateLoopAUnit(
            "execute_target",
            classId: "dummy",
            hp: 100f,
            armor: 0f,
            behavior: noAvoidance);
        var actor = new UnitSnapshot(
            new EntityId("execute_actor"),
            TeamSide.Ally,
            actorLoadout,
            new CombatVector2(-1f, 0f),
            new CombatVector2(-1f, 0f));
        var target = new UnitSnapshot(
            new EntityId("execute_target"),
            TeamSide.Enemy,
            targetLoadout,
            new CombatVector2(1f, 0f),
            new CombatVector2(1f, 0f));
        target.TakeDamage(targetDamage);
        var state = MakeState(actor, target);

        return HitResolutionService.ResolveBasicAttack(state, actor, target);
    }
}
