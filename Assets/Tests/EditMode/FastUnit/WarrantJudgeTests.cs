using NUnit.Framework;
using SM.Meta;

namespace SM.Tests.EditMode;

[Category("FastUnit")]
public sealed class WarrantJudgeTests
{
    private static readonly WarrantSpec Swift = new("warrant_swift", WarrantKind.Swift, SwiftStepThreshold: 100);
    private static readonly WarrantSpec Intact = new("warrant_intact", WarrantKind.Intact);

    [Test]
    public void Judge_NotApplicable_WhenNoWarrantPledged()
    {
        Assert.That(
            WarrantJudge.Judge(spec: null, victory: true, survivorAllyCount: 4, totalAllyCount: 4, stepCount: 10),
            Is.EqualTo(WarrantOutcome.NotApplicable));
    }

    [Test]
    public void Judge_Broken_WhenDefeat_RegardlessOfCondition()
    {
        // 패배는 어떤 서약이든 깬 것 — Swift 조건(빠른 turn)을 만족해도 패배면 Broken.
        Assert.That(
            WarrantJudge.Judge(Swift, victory: false, survivorAllyCount: 4, totalAllyCount: 4, stepCount: 1),
            Is.EqualTo(WarrantOutcome.Broken));
        Assert.That(
            WarrantJudge.Judge(Intact, victory: false, survivorAllyCount: 4, totalAllyCount: 4, stepCount: 1),
            Is.EqualTo(WarrantOutcome.Broken));
    }

    [Test]
    public void Judge_Swift_Kept_WhenWithinStepThreshold()
    {
        Assert.That(
            WarrantJudge.Judge(Swift, victory: true, survivorAllyCount: 2, totalAllyCount: 4, stepCount: 80),
            Is.EqualTo(WarrantOutcome.Kept));
    }

    [Test]
    public void Judge_Swift_Kept_AtExactThreshold()
    {
        // 경계: stepCount == threshold 는 이행(<=).
        Assert.That(
            WarrantJudge.Judge(Swift, victory: true, survivorAllyCount: 4, totalAllyCount: 4, stepCount: 100),
            Is.EqualTo(WarrantOutcome.Kept));
    }

    [Test]
    public void Judge_Swift_Broken_WhenOverStepThreshold()
    {
        Assert.That(
            WarrantJudge.Judge(Swift, victory: true, survivorAllyCount: 4, totalAllyCount: 4, stepCount: 101),
            Is.EqualTo(WarrantOutcome.Broken));
    }

    [Test]
    public void Judge_Intact_Kept_WhenAllAlliesSurvive()
    {
        // 온전은 turn 수와 무관 — 손실 0이면 stepCount가 커도 Kept.
        Assert.That(
            WarrantJudge.Judge(Intact, victory: true, survivorAllyCount: 4, totalAllyCount: 4, stepCount: 9999),
            Is.EqualTo(WarrantOutcome.Kept));
    }

    [Test]
    public void Judge_Intact_Broken_WhenSomeAlliesFall()
    {
        Assert.That(
            WarrantJudge.Judge(Intact, victory: true, survivorAllyCount: 3, totalAllyCount: 4, stepCount: 10),
            Is.EqualTo(WarrantOutcome.Broken));
    }

    [Test]
    public void Judge_Intact_Kept_WhenRosterCountUnknown()
    {
        // totalAllyCount == 0 (로스터 미상) + 승리 → vacuously kept.
        Assert.That(
            WarrantJudge.Judge(Intact, victory: true, survivorAllyCount: 0, totalAllyCount: 0, stepCount: 10),
            Is.EqualTo(WarrantOutcome.Kept));
    }

    [Test]
    public void ToToken_MapsEachOutcome()
    {
        Assert.That(WarrantJudge.ToToken(WarrantOutcome.NotApplicable), Is.EqualTo("not_applicable"));
        Assert.That(WarrantJudge.ToToken(WarrantOutcome.Kept), Is.EqualTo("kept"));
        Assert.That(WarrantJudge.ToToken(WarrantOutcome.Broken), Is.EqualTo("broken"));
    }

    [Test]
    public void Catalog_Resolves_KnownWarrantIds()
    {
        Assert.That(WarrantCatalog.TryResolve(WarrantCatalog.SwiftId, out var swift), Is.True);
        Assert.That(swift.Kind, Is.EqualTo(WarrantKind.Swift));

        Assert.That(WarrantCatalog.TryResolve(WarrantCatalog.IntactId, out var intact), Is.True);
        Assert.That(intact.Kind, Is.EqualTo(WarrantKind.Intact));
    }

    [Test]
    public void Catalog_ReturnsFalse_ForEmptyOrUnknownId()
    {
        Assert.That(WarrantCatalog.TryResolve("", out _), Is.False);
        Assert.That(WarrantCatalog.TryResolve(null, out _), Is.False);
        Assert.That(WarrantCatalog.TryResolve("warrant_does_not_exist", out _), Is.False);
    }
}
