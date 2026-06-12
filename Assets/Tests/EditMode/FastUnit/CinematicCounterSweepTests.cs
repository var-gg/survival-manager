using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using NUnit.Framework;
using SM.Combat.Model;
using SM.Combat.Services;

namespace SM.Tests.EditMode;

/// <summary>
/// DIAGNOSTIC (게이트 아님). 사용자 피드백 "showcase 판에서 측면만 뜨고 차단/구출/다이브킬이 0" —
/// 위치 사건 detector(차단 ScreenAbsorb / 측면·후방 Flank·Rear / 다이브킬 BacklineDiveKill / 구출
/// SaveMoment)가 showcase형 비대칭 4v4에서 시드별로 얼마나 발현되는지 측정한다. 발현 0의 원인 분해를
/// 위해 보조 프로브(힐 횟수, 빈사 진입 유닛 수, Dive 의도 발동 step 수)도 같이 기록한다.
/// 결과는 Captures/cinematic-counter-sweep/report.txt — 수치가 튜닝 설계를 끌고 간다.
/// </summary>
[Category("FastUnit")]
public sealed class CinematicCounterSweepTests
{
    private const int SeedCount = 24;
    private const int MaxSteps = 900; // 90s 안전 상한 — 통상 showcase는 ~10s 내 결착
    private const float NearDeathRatio = 0.25f; // CombatActionResolver.SaveMomentHealthRatio와 동일

    private static (List<BattleUnitLoadout> Allies, List<BattleUnitLoadout> Enemies) BuildShowcaseLikeFight()
    {
        var healSkill = new SkillDefinition("ally_heal", "Heal", SkillKind.Heal, 5f, 3f);
        var enemyHealSkill = new SkillDefinition("enemy_heal", "Heal", SkillKind.Heal, 5f, 3f);

        // 공격팀(CollapseWeakSide): 근접 듀얼리스트 + 힐러 + 원거리 + 보조 캐스터 — showcase 좌팀 미러.
        var allies = new List<BattleUnitLoadout>
        {
            CombatTestFactory.CreateUnit("ally_raider", classId: "duelist", anchor: DeploymentAnchorId.FrontCenter,
                hp: 72f, attack: 8f, defense: 1f, moveSpeed: 2.05f, attackRange: 1.3f, attackCooldown: 0.7f),
            CombatTestFactory.CreateUnit("ally_priest", classId: "mystic", anchor: DeploymentAnchorId.BackTop,
                hp: 60f, attack: 3f, defense: 2f, moveSpeed: 1.7f, attackRange: 2.8f, healPower: 5f,
                tactics: new[]
                {
                    new TacticRule(0, TacticConditionType.AllyHpBelow, 0.65f, BattleActionType.ActiveSkill, TargetSelectorType.LowestHpAlly, "ally_heal"),
                    new TacticRule(1, TacticConditionType.LowestHpEnemy, 0f, BattleActionType.BasicAttack, TargetSelectorType.NearestEnemy),
                    new TacticRule(2, TacticConditionType.Fallback, 0f, BattleActionType.WaitDefend, TargetSelectorType.Self),
                },
                skills: new[] { healSkill }),
            CombatTestFactory.CreateUnit("ally_marksman", classId: "ranger", anchor: DeploymentAnchorId.BackCenter,
                hp: 58f, attack: 7f, defense: 1f, moveSpeed: 1.85f, attackRange: 4.5f, attackCooldown: 0.9f),
            CombatTestFactory.CreateUnit("ally_hexer", classId: "mystic", anchor: DeploymentAnchorId.BackBottom,
                hp: 62f, attack: 5f, defense: 2f, moveSpeed: 1.7f, attackRange: 2.8f, attackCooldown: 0.8f),
        };

        // 방어팀(ProtectCarry): 가디언 전열 + 원거리/힐러/캐스터 후열 — showcase 우팀 미러.
        var enemies = new List<BattleUnitLoadout>
        {
            CombatTestFactory.CreateUnit("enemy_guardian", race: "undead", classId: "vanguard", anchor: DeploymentAnchorId.FrontCenter,
                hp: 110f, attack: 4f, defense: 4f, moveSpeed: 1.65f, attackRange: 1.2f, attackCooldown: 0.9f),
            CombatTestFactory.CreateUnit("enemy_shaman", race: "undead", classId: "ranger", anchor: DeploymentAnchorId.BackTop,
                hp: 58f, attack: 6f, defense: 1f, moveSpeed: 1.8f, attackRange: 3.2f, attackCooldown: 0.9f),
            CombatTestFactory.CreateUnit("enemy_priest", race: "undead", classId: "mystic", anchor: DeploymentAnchorId.BackCenter,
                hp: 60f, attack: 3f, defense: 2f, moveSpeed: 1.7f, attackRange: 2.8f, healPower: 5f,
                tactics: new[]
                {
                    new TacticRule(0, TacticConditionType.AllyHpBelow, 0.65f, BattleActionType.ActiveSkill, TargetSelectorType.LowestHpAlly, "enemy_heal"),
                    new TacticRule(1, TacticConditionType.LowestHpEnemy, 0f, BattleActionType.BasicAttack, TargetSelectorType.NearestEnemy),
                    new TacticRule(2, TacticConditionType.Fallback, 0f, BattleActionType.WaitDefend, TargetSelectorType.Self),
                },
                skills: new[] { enemyHealSkill }),
            CombatTestFactory.CreateUnit("enemy_hexer", race: "undead", classId: "mystic", anchor: DeploymentAnchorId.BackBottom,
                hp: 62f, attack: 5f, defense: 2f, moveSpeed: 1.7f, attackRange: 2.8f, attackCooldown: 0.8f),
        };

        return (allies, enemies);
    }

    private sealed record SeedResult(
        int Seed,
        bool Finished,
        int Steps,
        int ScreenAbsorb,
        int ScreenDeterrence,
        int Flank,
        int Rear,
        int DiveKill,
        int SaveMoment,
        int HealCasts,
        int NearDeathUnits,
        int DiveIntentSteps);

    [Test]
    public void Sweep_PositionalCounters_AcrossSeeds_ShowcaseLikeFight()
    {
        var results = new List<SeedResult>();
        for (var seed = 1; seed <= SeedCount; seed++)
        {
            var (allies, enemies) = BuildShowcaseLikeFight();
            var state = CombatTestFactory.CreateBattleState(
                allies, enemies,
                allyPosture: TeamPostureType.CollapseWeakSide,
                enemyPosture: TeamPostureType.ProtectCarry,
                seed: seed);
            var simulator = new BattleSimulator(state, MaxSteps);

            var healCasts = 0;
            var diveIntentSteps = 0;
            var nearDeath = new HashSet<string>();
            var steps = 0;
            while (!simulator.IsFinished && steps < MaxSteps)
            {
                var step = simulator.Step();
                steps++;
                healCasts += step.Events.Count(evt => evt.LogCode == BattleLogCode.ActiveSkillHeal);
                foreach (var unit in state.AllUnits)
                {
                    if (!unit.IsAlive)
                    {
                        continue;
                    }

                    if (unit.HealthRatio < NearDeathRatio)
                    {
                        nearDeath.Add(unit.Id.Value);
                    }

                    if (unit.CurrentCombatIntent.Type == CombatIntentType.Dive)
                    {
                        diveIntentSteps++;
                    }
                }
            }

            var telemetry = state.ActivityTelemetry;
            results.Add(new SeedResult(
                seed,
                simulator.IsFinished,
                steps,
                telemetry.ScreenAbsorbCount,
                telemetry.ScreenDeterrenceCount,
                telemetry.FlankStrikeCount,
                telemetry.RearStrikeCount,
                telemetry.BacklineDiveKillCount,
                telemetry.SaveMomentCount,
                healCasts,
                nearDeath.Count,
                diveIntentSteps));
        }

        var report = BuildReport(results);
        WriteReport(report);
        TestContext.Out.WriteLine(report);

        Assert.That(results.All(result => result.Finished), Is.True,
            "모든 시드가 수렴해야 한다(미수렴 = 별도 버그)");
    }

    private static string BuildReport(IReadOnlyList<SeedResult> results)
    {
        var builder = new StringBuilder();
        builder.AppendLine("cinematic counter sweep — showcase형 4v4 (CollapseWeakSide vs ProtectCarry)");
        builder.AppendLine("seed | steps | 흡수 | 우회 | 측면 | 후방 | 다이브킬 | 구출 || heals | nearDeath | diveSteps");
        foreach (var r in results)
        {
            builder.AppendLine(string.Format(
                CultureInfo.InvariantCulture,
                "{0,4} | {1,5} | {2,4} | {3,4} | {4,4} | {5,4} | {6,8} | {7,4} || {8,5} | {9,9} | {10,9}",
                r.Seed, r.Steps, r.ScreenAbsorb, r.ScreenDeterrence, r.Flank, r.Rear, r.DiveKill, r.SaveMoment,
                r.HealCasts, r.NearDeathUnits, r.DiveIntentSteps));
        }

        builder.AppendLine();
        AppendPresence(builder, results, "차단(흡수+우회)", r => r.ScreenAbsorb + r.ScreenDeterrence);
        AppendPresence(builder, results, "측면(Flank)", r => r.Flank);
        AppendPresence(builder, results, "후방(Rear)", r => r.Rear);
        AppendPresence(builder, results, "다이브킬(BacklineDiveKill)", r => r.DiveKill);
        AppendPresence(builder, results, "구출(SaveMoment)", r => r.SaveMoment);
        builder.AppendLine();
        builder.AppendLine(string.Format(
            CultureInfo.InvariantCulture,
            "probes: heals seeds≥1 {0}/{1} · nearDeath seeds≥1 {2}/{1} · diveIntent seeds≥1 {3}/{1}",
            results.Count(r => r.HealCasts > 0), results.Count,
            results.Count(r => r.NearDeathUnits > 0),
            results.Count(r => r.DiveIntentSteps > 0)));
        var readable = results.Count(r =>
            new[] { r.ScreenAbsorb + r.ScreenDeterrence, r.Flank + r.Rear, r.DiveKill, r.SaveMoment }.Count(c => c > 0) >= 2);
        builder.AppendLine(string.Format(
            CultureInfo.InvariantCulture,
            "가독 목표(서로 다른 사건 2종 이상 발현): {0}/{1} 시드",
            readable, results.Count));
        return builder.ToString();
    }

    private static void AppendPresence(StringBuilder builder, IReadOnlyList<SeedResult> results, string label, System.Func<SeedResult, int> selector)
    {
        var present = results.Count(r => selector(r) > 0);
        var mean = results.Average(r => (double)selector(r));
        builder.AppendLine(string.Format(
            CultureInfo.InvariantCulture,
            "{0}: seeds≥1 {1}/{2} · mean {3:0.00}",
            label, present, results.Count, mean));
    }

    private static void WriteReport(string report)
    {
        var directory = Path.Combine("Captures", "cinematic-counter-sweep");
        Directory.CreateDirectory(directory);
        File.WriteAllText(Path.Combine(directory, "report.txt"), report);
    }
}
