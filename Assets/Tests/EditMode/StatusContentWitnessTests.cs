using NUnit.Framework;
using SM.Editor.SeedData;
using SM.Meta.Services;
using SM.Unity;

namespace SM.Tests.EditMode;

/// <summary>
/// 실 committed 상태이상 콘텐츠의 튜닝값이 전투 규칙(CombatStatusRules)까지 도달하는지 단언하는
/// witness — "상태이상 숫자 콘텐츠화" 1차(guarded 받는피해 delta의 리터럴→콘텐츠 승격) 실 asset 쌍.
/// 과거 guarded 감소율은 UnitSnapshot에 -0.1 리터럴로 박혀 있어 에디터 없이 튜닝이 불가능했다.
/// </summary>
[Category("BatchOnly")]
public sealed class StatusContentWitnessTests
{
    [SetUp]
    public void SetUp()
    {
        SampleSeedGenerator.RequireCanonicalSampleContentReady(nameof(StatusContentWitnessTests));
    }

    [Test]
    public void RealGuardedFamily_CarriesIncomingDamageDelta()
    {
        var snapshot = new RuntimeCombatContentLookup().Snapshot;
        var rules = CombatStatusRuleCompiler.Compile(snapshot);

        Assert.That(rules.TryGetStatusFamily("guarded", out var guarded), Is.True,
            "실 콘텐츠에 guarded 상태 패밀리가 존재해야 한다");
        Assert.That(guarded.IncomingDamageDelta, Is.EqualTo(-0.1f).Within(0.0001f),
            "guarded의 받는피해 delta(-0.1)가 콘텐츠(status_family_guarded.asset)에서 전투 규칙까지 실려야 한다 — " +
            "0이면 콘텐츠 미저작으로 guarded가 무효과가 되는 회귀");
        Assert.That(rules.ResolveIncomingDamageDelta("guarded"), Is.EqualTo(-0.1f).Within(0.0001f));
    }
}
