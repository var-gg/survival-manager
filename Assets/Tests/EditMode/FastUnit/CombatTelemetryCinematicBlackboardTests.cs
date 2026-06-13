using System;
using System.Linq;
using NUnit.Framework;
using SM.Combat.Model;
using SM.Combat.Services;

namespace SM.Tests.EditMode;

[Category("FastUnit")]
public sealed class CombatTelemetryCinematicBlackboardTests
{
    [Test]
    public void CinematicMomentDetector_CoversRequiredV1MomentIds()
    {
        var records = new[]
        {
            Metric("SaveMomentCount", 1f, 0.1f),
            Event(TelemetryEventKind.KillCredited, 0.2f, reason: DecisionReasonCode.PunishExposedBackline),
            Metric("ScreenAbsorbCount", 1f, 0.3f),
            Event(TelemetryEventKind.InterruptApplied, 0.4f, statusId: "silence"),
            Event(TelemetryEventKind.BarrierApplied, 0.5f),
            Event(TelemetryEventKind.StatusApplied, 0.6f, statusId: "sunder"),
            Event(TelemetryEventKind.GuardBroken, 0.7f),
            Event(TelemetryEventKind.PositioningIntentUpdated, 0.8f, textA: "BacklineDive"),
            Event(TelemetryEventKind.SkillCastResolved, 0.9f, synergyId: "synergy_human"),
            Event(TelemetryEventKind.TacticReadEvaluated, 1.0f, textA: "perfect_read", boolValue: true),
        };

        var moments = CinematicMomentDetector.Detect(records);

        Assert.That(moments.Select(moment => moment.Id), Is.EquivalentTo(CinematicMomentDetector.RequiredMomentIds));
        Assert.That(moments.Select(moment => moment.Id).Distinct().Count(), Is.EqualTo(10));
    }

    [Test]
    public void BattleSummary_KeepsLegacyDecisiveMomentStrings_AndAddsStructuredMoments()
    {
        var records = new[]
        {
            Event(TelemetryEventKind.DamageApplied, 0.1f, value: 5f, salience: SalienceClass.Minor),
            Event(TelemetryEventKind.KillCredited, 0.2f, reason: DecisionReasonCode.SecureKill, salience: SalienceClass.Major),
            Event(TelemetryEventKind.GuardBroken, 0.3f, salience: SalienceClass.Major),
        };
        var readability = BattleTelemetryAnalysisService.BuildReadabilityReport(records, combatantCount: 2);
        var result = new BattleResult(TeamSide.Ally, 3, 0.3f, Array.Empty<BattleEvent>(), Array.Empty<BattleUnitReadModel>(), records);

        var summary = BattleTelemetryAnalysisService.BuildBattleSummary(result, records, new TelemetryContext { ScenarioId = "smoke", Seed = 7 }, readability);

        Assert.That(summary.DecisiveMoments.Any(moment => moment.StartsWith("first_death:", StringComparison.Ordinal)), Is.True);
        Assert.That(summary.DecisiveMoments.Any(moment => moment.StartsWith("first_cc_or_guard:", StringComparison.Ordinal)), Is.True);
        Assert.That(summary.CinematicMoments.Any(moment => moment.Id == CinematicMomentId.ControlBreak), Is.True);
    }

    [Test]
    public void AiPerceptionBlackboard_ExtractsPureBattleFeatures()
    {
        var allyCarry = CombatTestFactory.CreateLoopAUnit("ally_carry", classId: "mystic", hp: 30f);
        var allyDiver = CombatTestFactory.CreateLoopAUnit("ally_diver", classId: "duelist", hp: 30f);
        var enemyLow = CombatTestFactory.CreateLoopAUnit("enemy_low", classId: "ranger", hp: 40f, attackRange: 4f);
        var enemyFront = CombatTestFactory.CreateLoopAUnit("enemy_front", classId: "vanguard", hp: 50f);
        var state = CombatTestFactory.CreateBattleState(new[] { allyCarry, allyDiver }, new[] { enemyLow, enemyFront });
        var actor = state.Allies.Single(unit => unit.Definition.Id == "ally_diver");
        var carry = state.Allies.Single(unit => unit.Definition.Id == "ally_carry");
        var lowEnemy = state.Enemies.Single(unit => unit.Definition.Id == "enemy_low");
        carry.TakeDamage(15f);
        lowEnemy.TakeDamage(30f);
        actor.SetCombatIntent(new CombatIntent(CombatIntentType.Dive, lowEnemy.Id, null, default, state.StepIndex + 5, 10));

        var blackboard = AiPerceptionBlackboardService.Build(state, actor);
        var evaluatorRead = TacticEvaluator.BuildPerceptionBlackboard(state, actor);

        Assert.That(blackboard.ActorUnitId, Is.EqualTo(actor.Id.Value));
        Assert.That(blackboard.AliveAllyCount, Is.EqualTo(2));
        Assert.That(blackboard.AliveEnemyCount, Is.EqualTo(2));
        Assert.That(blackboard.LowestAllyHealthRatio, Is.LessThan(1f));
        Assert.That(blackboard.LowestEnemyHealthRatio, Is.LessThan(1f));
        Assert.That(blackboard.NearestEnemyDistance, Is.GreaterThanOrEqualTo(0f));
        Assert.That(blackboard.ActorIsBacklineDiver, Is.True);
        Assert.That(evaluatorRead.ActorUnitId, Is.EqualTo(actor.Id.Value));
    }

    private static TelemetryEventRecord Metric(string metricId, float value, float timeSeconds)
        => new()
        {
            EventKind = TelemetryEventKind.ActivityMetricRecorded,
            TimeSeconds = timeSeconds,
            ValueA = value,
            StringValueA = metricId,
            Explain = Explain(DecisionReasonCode.DefaultCadence, SalienceClass.Ambient),
        };

    private static TelemetryEventRecord Event(
        TelemetryEventKind kind,
        float timeSeconds,
        string statusId = "",
        string textA = "",
        string synergyId = "",
        float value = 0f,
        bool boolValue = false,
        DecisionReasonCode reason = DecisionReasonCode.DefaultCadence,
        SalienceClass salience = SalienceClass.Major)
        => new()
        {
            EventKind = kind,
            TimeSeconds = timeSeconds,
            Actor = Entity("actor"),
            Target = Entity("target", sideIndex: 1),
            StatusId = statusId,
            SynergyId = synergyId,
            ValueA = value,
            BoolValueA = boolValue,
            StringValueA = textA,
            Explain = Explain(reason, salience),
        };

    private static TelemetryEntityRef Entity(string id, int sideIndex = 0)
        => new()
        {
            UnitInstanceId = id,
            UnitBlueprintId = id,
            SideIndex = sideIndex,
        };

    private static ExplainStamp Explain(DecisionReasonCode reason, SalienceClass salience)
        => new()
        {
            SourceKind = ExplainedSourceKind.SystemRule,
            SourceContentId = "test",
            SourceDisplayName = "test",
            ReasonCode = reason,
            Salience = salience,
        };
}
