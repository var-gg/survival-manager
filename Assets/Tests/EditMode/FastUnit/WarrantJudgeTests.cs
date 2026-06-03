using NUnit.Framework;
using SM.Meta;

namespace SM.Tests.EditMode;

[Category("FastUnit")]
public sealed class WarrantJudgeTests
{
    private static readonly WarrantSpec Swift = new("warrant_swift", WarrantKind.Swift, SwiftStepThreshold: 100);
    private static readonly WarrantSpec Intact = new("warrant_intact", WarrantKind.Intact);
    private static readonly EncounterContext SpecFallback = new(ResolvedSwiftTurnLimit: 0);

    private static BattleFactSet Facts(bool victory, int turns, int survivors, int total)
        => new(victory, turns, survivors, total);

    [Test]
    public void Judge_NotApplicable_WhenNoWarrantPledged()
    {
        var j = WarrantJudge.Judge(spec: null, Facts(true, 10, 4, 4), SpecFallback);
        Assert.That(j.Outcome, Is.EqualTo(WarrantOutcome.NotApplicable));
        Assert.That(j.FailureReason, Is.EqualTo(WarrantFailureReason.None));
    }

    [Test]
    public void Judge_FailedMission_WhenDefeat_NotJustBroken()
    {
        // 패배는 약속 이전의 실패 — FailedMission/Defeated/Major로 구분(단순 Broken 아님).
        var j = WarrantJudge.Judge(Swift, Facts(false, 5, 4, 4), SpecFallback);
        Assert.That(j.Outcome, Is.EqualTo(WarrantOutcome.FailedMission));
        Assert.That(j.FailureReason, Is.EqualTo(WarrantFailureReason.Defeated));
        Assert.That(j.Severity, Is.EqualTo(WarrantSeverity.Major));
    }

    [Test]
    public void Judge_Swift_Kept_WithinThreshold()
    {
        var j = WarrantJudge.Judge(Swift, Facts(true, 80, 2, 4), SpecFallback);
        Assert.That(j.Outcome, Is.EqualTo(WarrantOutcome.Kept));
        Assert.That(j.FailureReason, Is.EqualTo(WarrantFailureReason.None));
        Assert.That(j.Severity, Is.EqualTo(WarrantSeverity.None));
    }

    [Test]
    public void Judge_Swift_Kept_AtExactThreshold()
    {
        var j = WarrantJudge.Judge(Swift, Facts(true, 100, 4, 4), SpecFallback);
        Assert.That(j.Outcome, Is.EqualTo(WarrantOutcome.Kept));
    }

    [Test]
    public void Judge_Swift_Broken_OverThreshold_WithObservedFacts()
    {
        var j = WarrantJudge.Judge(Swift, Facts(true, 130, 4, 4), SpecFallback);
        Assert.That(j.Outcome, Is.EqualTo(WarrantOutcome.Broken));
        Assert.That(j.FailureReason, Is.EqualTo(WarrantFailureReason.TurnLimitExceeded));
        Assert.That(j.Severity, Is.EqualTo(WarrantSeverity.Minor));
        // 관측 사실 — "8턴 약속이었는데 130" 식 재구성 가능해야 한다.
        Assert.That(j.ObservedTurnCount, Is.EqualTo(130));
        Assert.That(j.ResolvedTurnLimit, Is.EqualTo(100));
    }

    [Test]
    public void Judge_Intact_Kept_WhenAllAlliesSurvive()
    {
        var j = WarrantJudge.Judge(Intact, Facts(true, 9999, 4, 4), SpecFallback);
        Assert.That(j.Outcome, Is.EqualTo(WarrantOutcome.Kept));
    }

    [Test]
    public void Judge_Intact_Broken_WhenSomeAlliesFall_IsMajor()
    {
        // 자기 사람을 잃는 것은 무겁게 본다(Major).
        var j = WarrantJudge.Judge(Intact, Facts(true, 10, 3, 4), SpecFallback);
        Assert.That(j.Outcome, Is.EqualTo(WarrantOutcome.Broken));
        Assert.That(j.FailureReason, Is.EqualTo(WarrantFailureReason.AllyKilled));
        Assert.That(j.Severity, Is.EqualTo(WarrantSeverity.Major));
    }

    [Test]
    public void Judge_Intact_Kept_WhenRosterCountUnknown()
    {
        var j = WarrantJudge.Judge(Intact, Facts(true, 10, 0, 0), SpecFallback);
        Assert.That(j.Outcome, Is.EqualTo(WarrantOutcome.Kept));
    }

    [Test]
    public void Judge_Swift_UsesEncounterContextLimit_OverSpecPlaceholder()
    {
        // GPT Pro §1.C/§5.1: encounter-relative 임계가 spec placeholder를 덮어쓴다.
        // spec 임계 100이지만 context가 6이면 130턴은 깨지고, resolved limit은 6으로 기록.
        var context = new EncounterContext(ResolvedSwiftTurnLimit: 6);
        var j = WarrantJudge.Judge(Swift, Facts(true, 7, 4, 4), context);
        Assert.That(j.Outcome, Is.EqualTo(WarrantOutcome.Broken));
        Assert.That(j.ResolvedTurnLimit, Is.EqualTo(6));

        var keptUnderContext = WarrantJudge.Judge(Swift, Facts(true, 6, 4, 4), context);
        Assert.That(keptUnderContext.Outcome, Is.EqualTo(WarrantOutcome.Kept));
    }

    [Test]
    public void Tokens_MapEachValue()
    {
        Assert.That(WarrantJudgment.OutcomeToken(WarrantOutcome.NotApplicable), Is.EqualTo("not_applicable"));
        Assert.That(WarrantJudgment.OutcomeToken(WarrantOutcome.Kept), Is.EqualTo("kept"));
        Assert.That(WarrantJudgment.OutcomeToken(WarrantOutcome.Broken), Is.EqualTo("broken"));
        Assert.That(WarrantJudgment.OutcomeToken(WarrantOutcome.FailedMission), Is.EqualTo("failed_mission"));

        Assert.That(WarrantJudgment.FailureReasonToken(WarrantFailureReason.Defeated), Is.EqualTo("defeated"));
        Assert.That(WarrantJudgment.FailureReasonToken(WarrantFailureReason.TurnLimitExceeded), Is.EqualTo("turn_limit_exceeded"));
        Assert.That(WarrantJudgment.FailureReasonToken(WarrantFailureReason.AllyKilled), Is.EqualTo("ally_killed"));

        Assert.That(WarrantJudgment.SeverityToken(WarrantSeverity.Major), Is.EqualTo("major"));
        Assert.That(WarrantJudgment.SeverityToken(WarrantSeverity.Minor), Is.EqualTo("minor"));
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
