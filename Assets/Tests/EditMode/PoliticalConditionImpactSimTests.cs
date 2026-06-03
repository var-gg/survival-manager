using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using SM.Combat.Model;
using SM.Combat.Services;
using SM.Core.Contracts;
using SM.Meta;
using UnityEngine;

namespace SM.Tests.EditMode;

/// <summary>
/// ADR-0028 slice 3 후속 — 정치 조건 magnitude 영향 측정(GPT Pro 검수 #2).
/// 질문: 지원(아군 +HP/+pow)·경계(적 +HP/+pow) 버프가 실제로 전투 결과를 바꾸는가, 아니면 noise인가.
/// burst 지배 자동전투에서 작은 flat 버프는 breakpoint를 넘을 때만 보인다(GPT Pro) — 그걸 데이터로 확인한다.
/// 같은 squad/적/seed로 baseline vs (지원 적용) vs (경계 적용)을 돌려 {승률, win step, 손실} delta를 측정.
/// magnitude는 <see cref="PoliticalCombatConditionService"/> 상수를 직접 stat delta로 적용(package fold와 동치).
/// 결과는 Logs/political-condition-impact.txt 에 dump. assertion은 lenient(측정이 목적).
/// (BatchOnly: FastUnit gate 밖.)
/// </summary>
[Category("BatchOnly")]
public sealed class PoliticalConditionImpactSimTests
{
    private const int SeedCount = 40;
    private const int MaxTicks = 300;

    [Test]
    public void SupportAndAlert_OutcomeShift_AcrossTiers_IsMeasured()
    {
        // 적 tier: offense를 주로 올려 squad 생존을 위협(burst 지배 환경 재현).
        var tiers = new (string Name, float Hp, float Off, float Spd)[]
        {
            ("easy", 1.0f, 1.0f, 1.0f),
            ("hard", 1.2f, 1.6f, 1.3f),
            ("brutal", 1.4f, 2.2f, 1.5f),
        };

        var rows = new List<Row>();
        foreach (var tier in tiers)
        {
            // A: 통제 — baseline squad vs baseline enemy.
            var control = RunCell($"{tier.Name}:control", BuildSquad(0f, 0f), () => BuildEnemies(tier.Hp, tier.Off, tier.Spd, 0f, 0f));
            // B: 지원 — squad에 +HP/+pow(발행 세력 지원) vs baseline enemy.
            var support = RunCell($"{tier.Name}:+support", BuildSquad(PoliticalCombatConditionService.SupportMaxHealthBonus, PoliticalCombatConditionService.SupportPhysPowerBonus), () => BuildEnemies(tier.Hp, tier.Off, tier.Spd, 0f, 0f));
            // C: 경계 — baseline squad vs 적에 +HP/+pow(거스른 세력 경계).
            var alert = RunCell($"{tier.Name}:+alert", BuildSquad(0f, 0f), () => BuildEnemies(tier.Hp, tier.Off, tier.Spd, PoliticalCombatConditionService.AlertMaxHealthBonus, PoliticalCombatConditionService.AlertPhysPowerBonus));
            rows.Add(new Row { Tier = tier.Name, Control = control, Support = support, Alert = alert });
        }

        var report = BuildReport(rows);
        WriteReport(report);
        TestContext.WriteLine(report);

        Assert.That(rows.Sum(r => r.Control.Wins + r.Support.Wins + r.Alert.Wins), Is.GreaterThan(0), "전 cell 0승이면 sim 설정이 깨진 것.");
    }

    // ---- squads ---------------------------------------------------------

    // 중간 스케일 squad(속전/온전 사이) — 매그니튜드(+4HP/+2pow)가 의미 있을 수 있는 구간.
    private static IReadOnlyList<BattleUnitLoadout> BuildSquad(float hpDelta, float powDelta) => new[]
    {
        CombatTestFactory.CreateLoopAUnit("ally_van", classId: "vanguard", hp: 75f + hpDelta, physPower: 8f + powDelta, armor: 4f, attackSpeed: 3.5f, moveSpeed: 1.7f, anchor: DeploymentAnchorId.FrontTop),
        CombatTestFactory.CreateLoopAUnit("ally_duel", classId: "duelist", hp: 60f + hpDelta, physPower: 9f + powDelta, armor: 2f, attackSpeed: 4.5f, moveSpeed: 1.9f, anchor: DeploymentAnchorId.FrontBottom),
        CombatTestFactory.CreateLoopAUnit("ally_rng", classId: "ranger", hp: 55f + hpDelta, physPower: 9f + powDelta, armor: 1f, attackSpeed: 4f, moveSpeed: 1.85f, attackRange: 5.6f, anchor: DeploymentAnchorId.BackTop),
        CombatTestFactory.CreateLoopAUnit("ally_mys", classId: "mystic", hp: 50f + hpDelta, physPower: 8f + powDelta, armor: 1f, attackSpeed: 3.5f, moveSpeed: 1.75f, attackRange: 2.8f, anchor: DeploymentAnchorId.BackBottom),
    };

    private static IReadOnlyList<BattleUnitLoadout> BuildEnemies(float hpMul, float offMul, float spdMul, float hpDelta, float powDelta) => new[]
    {
        CombatTestFactory.CreateLoopAUnit("enemy_front", race: "undead", classId: "vanguard", hp: 70f * hpMul + hpDelta, physPower: 5f * offMul + powDelta, armor: 2f, attackSpeed: 2.5f * spdMul, moveSpeed: 1.65f, anchor: DeploymentAnchorId.FrontTop),
        CombatTestFactory.CreateLoopAUnit("enemy_bruiser", race: "undead", classId: "duelist", hp: 60f * hpMul + hpDelta, physPower: 6f * offMul + powDelta, armor: 1f, attackSpeed: 3.5f * spdMul, moveSpeed: 2.0f, anchor: DeploymentAnchorId.FrontBottom),
        CombatTestFactory.CreateLoopAUnit("enemy_ranged", race: "undead", classId: "ranger", hp: 50f * hpMul + hpDelta, physPower: 5f * offMul + powDelta, armor: 1f, attackSpeed: 3.5f * spdMul, moveSpeed: 1.85f, attackRange: 5.6f, anchor: DeploymentAnchorId.BackTop),
        CombatTestFactory.CreateLoopAUnit("enemy_mage", race: "undead", classId: "mystic", hp: 48f * hpMul + hpDelta, physPower: 4f * offMul + powDelta, armor: 1f, attackSpeed: 3f * spdMul, moveSpeed: 1.7f, attackRange: 2.8f, anchor: DeploymentAnchorId.BackBottom),
    };

    // ---- run + aggregate ------------------------------------------------

    private sealed class Cell
    {
        public string Label = string.Empty;
        public int Wins;
        public readonly List<int> WinStepCounts = new();
        public readonly List<int> AllyDeaths = new();
        public double WinPct => 100.0 * Wins / SeedCount;
        public double WinStepMedian => Median(WinStepCounts);
        public double DeathsMean => Mean(AllyDeaths);
    }

    private sealed class Row
    {
        public string Tier = string.Empty;
        public Cell Control = new();
        public Cell Support = new();
        public Cell Alert = new();
    }

    private static Cell RunCell(string label, IReadOnlyList<BattleUnitLoadout> allies, System.Func<IReadOnlyList<BattleUnitLoadout>> enemyFactory)
    {
        var cell = new Cell { Label = label };
        for (var seed = 1; seed <= SeedCount; seed++)
        {
            var state = CombatTestFactory.CreateBattleState(allies, enemyFactory(), allyPosture: TeamPostureType.StandardAdvance, seed: seed);
            var battle = BattleResolver.Run(state, MaxTicks);
            cell.AllyDeaths.Add(battle.FinalUnits.Count(u => u.Side == TeamSide.Ally && !u.IsAlive));
            if (battle.Winner == TeamSide.Ally)
            {
                cell.Wins++;
                cell.WinStepCounts.Add(battle.StepCount);
            }
        }

        return cell;
    }

    // ---- report ---------------------------------------------------------

    private static string BuildReport(IReadOnlyList<Row> rows)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("=== 정치 조건 magnitude 영향 sim — support/alert outcome shift ===");
        sb.AppendLine($"seeds={SeedCount}, maxTicks={MaxTicks}. squad ~50-75HP/8-9pow(중간 스케일).");
        sb.AppendLine($"support=아군 +{PoliticalCombatConditionService.SupportMaxHealthBonus:0}HP/+{PoliticalCombatConditionService.SupportPhysPowerBonus:0}pow, alert=적 +{PoliticalCombatConditionService.AlertMaxHealthBonus:0}HP/+{PoliticalCombatConditionService.AlertPhysPowerBonus:0}pow.");
        sb.AppendLine();
        sb.AppendLine($"{"tier",-8}{"config",-12}{"win%",-7}{"winStep(med)",-14}{"deaths(mean)"}");
        foreach (var r in rows)
        {
            AppendCell(sb, r.Tier, "control", r.Control);
            AppendCell(sb, r.Tier, "+support", r.Support);
            AppendCell(sb, r.Tier, "+alert", r.Alert);
        }
        sb.AppendLine();
        sb.AppendLine("--- delta(정치 조건 영향) ---");
        sb.AppendLine("support = (+support) − control (아군 유리해야 +win/−step/−death). alert = (+alert) − control (적 유리해야 −win/+death).");
        foreach (var r in rows)
        {
            var sWin = r.Support.WinPct - r.Control.WinPct;
            var sStep = r.Support.WinStepMedian - r.Control.WinStepMedian;
            var sDed = r.Support.DeathsMean - r.Control.DeathsMean;
            var aWin = r.Alert.WinPct - r.Control.WinPct;
            var aDed = r.Alert.DeathsMean - r.Control.DeathsMean;
            var sFloored = IsFloored(r.Control) && IsFloored(r.Support);
            var aFloored = IsFloored(r.Control) && IsFloored(r.Alert);
            sb.AppendLine($"  [{r.Tier}] support Δwin {sWin,6:+0;-0}pp Δstep {sStep,7:+0.0;-0.0} Δ死 {sDed,7:+0.00;-0.00} → {Verdict(sWin, sStep, sDed, sFloored)}");
            sb.AppendLine($"  [{r.Tier}] alert   Δwin {aWin,6:+0;-0}pp Δ死 {aDed,7:+0.00;-0.00} → {Verdict(-aWin, 0d, aDed, aFloored)}");
        }
        sb.AppendLine();
        sb.AppendLine("가이드: |Δwin|≥5pp 또는 |Δ死|≥0.2 또는 |Δstep|≥5%면 'breakpoint 넘음(체감 가능)', 아니면 'noise(magnitude↑ 필요)'.");
        sb.AppendLine("noise면 GPT Pro #2 실증 — placeholder magnitude를 올리거나 신뢰-비례 scaling 도입(balance 후속).");
        return sb.ToString();
    }

    private static void AppendCell(System.Text.StringBuilder sb, string tier, string config, Cell c) =>
        sb.AppendLine($"{tier,-8}{config,-12}{c.WinPct,-7:0}{c.WinStepMedian,-14:0.0}{c.DeathsMean:0.00}");

    // 전멸+불승(floor) — buff가 보일 여지 자체가 없음. magnitude 문제와 구분(측정 불가).
    private static bool IsFloored(Cell cell) => cell.Wins == 0 && cell.DeathsMean >= 3.99;

    // 어느 한 지표라도 임계 넘으면 체감(breakpoint). floor면 측정 불가(magnitude 무관). 둘 다 아니면 noise.
    private static string Verdict(double winDeltaFavorable, double stepDelta, double deathDelta, bool floored)
    {
        if (floored)
        {
            return "floor(전멸 — 측정 불가, magnitude 무관)";
        }

        var meaningful = winDeltaFavorable >= 5.0 || System.Math.Abs(deathDelta) >= 0.2 || System.Math.Abs(stepDelta) >= 5.0;
        return meaningful ? "체감(breakpoint 넘음)" : "noise(magnitude↑ 필요)";
    }

    private static double Median(IReadOnlyList<int> values)
    {
        if (values.Count == 0) return 0;
        var s = values.OrderBy(x => x).ToList();
        var mid = s.Count / 2;
        return s.Count % 2 == 1 ? s[mid] : (s[mid - 1] + s[mid]) / 2.0;
    }

    private static double Mean(IReadOnlyList<int> values) => values.Count == 0 ? 0 : values.Average();

    private static void WriteReport(string report, string fileName = "political-condition-impact.txt")
    {
        try
        {
            var root = Directory.GetParent(Application.dataPath)!.FullName;
            var dir = Path.Combine(root, "Logs");
            Directory.CreateDirectory(dir);
            File.WriteAllText(Path.Combine(dir, fileName), report);
        }
        catch
        {
            // 보고용 — 파일 쓰기 실패해도 TestContext.WriteLine로 회수.
        }
    }
}
