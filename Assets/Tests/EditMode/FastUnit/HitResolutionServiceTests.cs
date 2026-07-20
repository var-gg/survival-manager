using NUnit.Framework;
using SM.Combat.Model;
using SM.Combat.Services;
using SM.Core.Ids;
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
}
