using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using SM.Combat.Model;
using SM.Combat.Services;
using SM.Core.Contracts;
using UnityEngine;

namespace SM.Tests.EditMode;

/// <summary>
/// ludonarrative 루프 P2b 게이트 — Warrant separability 측정 하네스(GPT Pro 검수 #1).
/// 질문: 자동전투에서 "속전형"과 "온전형" 빌드가 실제로 다른 turn/casualty 분포를 내는가,
/// 아니면 burst가 두 축을 지배해 tension이 붕괴하는가.
/// (build × enemy-tier) 매트릭스를 N seed로 돌려 {승률, win stepCount, ally 손실}을 측정.
/// 결과는 Logs/warrant-separability.txt 에 dump(해석은 사람/agent). assertion은 lenient —
/// 측정 자체가 목적이라 hard separability assert는 안 건다(분리 판정은 report 수치로).
/// (BatchOnly: FastUnit gate 밖.)
/// </summary>
[Category("BatchOnly")]
public sealed class WarrantSeparabilitySimTests
{
    private const int SeedCount = 40;
    private const int MaxTicks = 300;

    [Test]
    public void SwiftVsIntact_AcrossEnemyTiers_DistributionsAreMeasured()
    {
        var builds = new (string Name, System.Func<IReadOnlyList<BattleUnitLoadout>> Allies, TeamPostureType Posture)[]
        {
            ("swift_glass", BuildSwiftAllies, TeamPostureType.StandardAdvance),
            ("intact_tanky_dps", BuildIntactAllies, TeamPostureType.HoldLine),
        };
        // 적 tier: offense(physPower/attackSpeed)를 주로 올려 fragile 빌드의 생존을 위협(hp만 올리면 timeout만 늘어남).
        var tiers = new (string Name, float Hp, float Off, float Spd)[]
        {
            ("easy", 1.0f, 1.0f, 1.0f),
            ("hard", 1.2f, 1.7f, 1.3f),
            ("brutal", 1.4f, 2.4f, 1.6f),
        };

        var cells = new List<Cell>();
        foreach (var tier in tiers)
        {
            foreach (var build in builds)
            {
                cells.Add(RunCell(build.Name, tier.Name, build.Allies, build.Posture, () => BuildEnemies(tier.Hp, tier.Off, tier.Spd)));
            }
        }

        var report = BuildReport(cells, tiers.Select(t => t.Name).ToArray());
        WriteReport(report);
        TestContext.WriteLine(report);

        Assert.That(cells.Sum(c => c.Wins), Is.GreaterThan(0), "전 cell 0승이면 sim 설정이 깨진 것.");
    }

    [Test]
    public void Protect_PostureSeparatesProtecteeSurvival_IsMeasured()
    {
        // build축이 미분리였으니(위 test), tension은 objective에서 와야 한다. 가장 단순한 후보 = protect.
        // 같은 squad+보호대상을 posture만 바꿔 돌려 보호대상 생존율이 갈리는지 측정.
        // guard(ProtectCarry) 생존% > aggressive(AllInBackline)면 protect는 posture로 통제되는 진짜 선택(GPT Pro §5.B).
        var postures = new (string Name, TeamPostureType P)[]
        {
            ("guard", TeamPostureType.ProtectCarry),
            ("neutral", TeamPostureType.StandardAdvance),
            ("aggressive", TeamPostureType.AllInBackline),
        };
        var enemyKinds = new (string Name, System.Func<IReadOnlyList<BattleUnitLoadout>> F)[]
        {
            ("mixed", () => BuildEnemies(1f, 1f, 1f)),
            ("melee", BuildMeleeEnemies),
        };
        var rows = new List<ProtectCell>();
        foreach (var ek in enemyKinds)
        {
            foreach (var p in postures)
            {
                rows.Add(RunProtect($"{ek.Name}/{p.Name}", p.P, ek.F));
            }
        }

        var report = BuildProtectReport(rows);
        WriteReport(report, "warrant-protect-separability.txt");
        TestContext.WriteLine(report);

        Assert.That(rows.Sum(r => r.Wins), Is.GreaterThan(0), "전 posture 0승이면 sim 설정 문제.");
    }

    // ---- builds ---------------------------------------------------------

    private static IReadOnlyList<BattleUnitLoadout> BuildEnemies(float hpMul, float offMul, float spdMul)
    {
        return new[]
        {
            CombatTestFactory.CreateLoopAUnit("enemy_front", race: "undead", classId: "vanguard", hp: 70f * hpMul, physPower: 5f * offMul, armor: 2f, attackSpeed: 2.5f * spdMul, moveSpeed: 1.65f, anchor: DeploymentAnchorId.FrontTop),
            CombatTestFactory.CreateLoopAUnit("enemy_bruiser", race: "undead", classId: "duelist", hp: 60f * hpMul, physPower: 6f * offMul, armor: 1f, attackSpeed: 3.5f * spdMul, moveSpeed: 2.0f, anchor: DeploymentAnchorId.FrontBottom),
            CombatTestFactory.CreateLoopAUnit("enemy_ranged", race: "undead", classId: "ranger", hp: 50f * hpMul, physPower: 5f * offMul, armor: 1f, attackSpeed: 3.5f * spdMul, moveSpeed: 1.85f, attackRange: 5.6f, anchor: DeploymentAnchorId.BackTop),
            CombatTestFactory.CreateLoopAUnit("enemy_mage", race: "undead", classId: "mystic", hp: 48f * hpMul, physPower: 4f * offMul, armor: 1f, attackSpeed: 3f * spdMul, moveSpeed: 1.7f, attackRange: 2.8f, anchor: DeploymentAnchorId.BackBottom),
        };
    }

    // 속전형: 높은 physPower/attackSpeed, 낮은 hp/armor — 빠르게 죽이지만 fragile.
    private static IReadOnlyList<BattleUnitLoadout> BuildSwiftAllies()
    {
        return new[]
        {
            CombatTestFactory.CreateLoopAUnit("swift_a", classId: "duelist", hp: 60f, physPower: 11f, armor: 1f, attackSpeed: 5.5f, moveSpeed: 2.05f, anchor: DeploymentAnchorId.FrontTop),
            CombatTestFactory.CreateLoopAUnit("swift_b", classId: "duelist", hp: 58f, physPower: 11f, armor: 1f, attackSpeed: 5.5f, moveSpeed: 2.05f, anchor: DeploymentAnchorId.FrontBottom),
            CombatTestFactory.CreateLoopAUnit("swift_c", classId: "ranger", hp: 52f, physPower: 10f, armor: 1f, attackSpeed: 5.5f, moveSpeed: 1.9f, attackRange: 5.6f, anchor: DeploymentAnchorId.BackTop),
            CombatTestFactory.CreateLoopAUnit("swift_d", classId: "mystic", hp: 50f, physPower: 10f, armor: 1f, attackSpeed: 5f, moveSpeed: 1.75f, attackRange: 2.8f, anchor: DeploymentAnchorId.BackBottom),
        };
    }

    // 온전형(공정): 충분한 DPS(timeout 회피) + 높은 EHP. swift보다 느리게 죽이되 더 잘 버틴다.
    private static IReadOnlyList<BattleUnitLoadout> BuildIntactAllies()
    {
        return new[]
        {
            CombatTestFactory.CreateLoopAUnit("intact_a", classId: "vanguard", hp: 120f, physPower: 8f, armor: 5f, attackSpeed: 3.5f, moveSpeed: 1.7f, anchor: DeploymentAnchorId.FrontTop),
            CombatTestFactory.CreateLoopAUnit("intact_b", classId: "vanguard", hp: 115f, physPower: 8f, armor: 5f, attackSpeed: 3.5f, moveSpeed: 1.7f, anchor: DeploymentAnchorId.FrontBottom),
            CombatTestFactory.CreateLoopAUnit("intact_c", classId: "duelist", hp: 100f, physPower: 8f, armor: 4f, attackSpeed: 3.5f, moveSpeed: 1.8f, anchor: DeploymentAnchorId.BackTop),
            CombatTestFactory.CreateLoopAUnit("intact_d", classId: "vanguard", hp: 110f, physPower: 8f, armor: 5f, attackSpeed: 3.5f, moveSpeed: 1.7f, anchor: DeploymentAnchorId.BackBottom),
        };
    }

    // ---- run + aggregate ------------------------------------------------

    private sealed class Cell
    {
        public string Build = string.Empty;
        public string Tier = string.Empty;
        public int Wins;
        public int Timeouts;
        public readonly List<int> WinStepCounts = new();
        public readonly List<int> AllyDeaths = new();
        public int ZeroDeathBattles;
    }

    private static Cell RunCell(string build, string tier, System.Func<IReadOnlyList<BattleUnitLoadout>> allyFactory, TeamPostureType posture, System.Func<IReadOnlyList<BattleUnitLoadout>> enemyFactory)
    {
        var cell = new Cell { Build = build, Tier = tier };
        for (var seed = 1; seed <= SeedCount; seed++)
        {
            var state = CombatTestFactory.CreateBattleState(allyFactory(), enemyFactory(), allyPosture: posture, seed: seed);
            var battle = BattleResolver.Run(state, MaxTicks);

            var deaths = battle.FinalUnits.Count(u => u.Side == TeamSide.Ally && !u.IsAlive);
            cell.AllyDeaths.Add(deaths);
            if (deaths == 0) cell.ZeroDeathBattles++;
            if (battle.StepCount >= MaxTicks) cell.Timeouts++;
            if (battle.Winner == TeamSide.Ally)
            {
                cell.Wins++;
                cell.WinStepCounts.Add(battle.StepCount);
            }
        }

        return cell;
    }

    // ---- report ---------------------------------------------------------

    private static string BuildReport(IReadOnlyList<Cell> cells, IReadOnlyList<string> tierOrder)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("=== Warrant separability sim — (build x enemy-tier) ===");
        sb.AppendLine($"seeds={SeedCount}, maxTicks={MaxTicks}. 속전=swift_glass, 온전=intact_tanky_dps.");
        sb.AppendLine();
        sb.AppendLine($"{"tier",-8}{"build",-20}{"win%",-7}{"timeout",-9}{"winStep(med)",-14}{"deaths(mean)",-14}{"zeroDeath"}");
        foreach (var tier in tierOrder)
        {
            foreach (var c in cells.Where(x => x.Tier == tier))
            {
                sb.AppendLine($"{c.Tier,-8}{c.Build,-20}{100.0 * c.Wins / SeedCount,-7:0}{c.Timeouts + "/" + SeedCount,-9}{Median(c.WinStepCounts),-14:0.0}{Mean(c.AllyDeaths),-14:0.00}{c.ZeroDeathBattles}/{SeedCount}");
            }
        }
        sb.AppendLine();
        sb.AppendLine("--- tier별 분리 판정 ---");
        foreach (var tier in tierOrder)
        {
            var swift = cells.FirstOrDefault(x => x.Tier == tier && x.Build == "swift_glass");
            var intact = cells.FirstOrDefault(x => x.Tier == tier && x.Build == "intact_tanky_dps");
            if (swift == null || intact == null) continue;
            var sStep = Median(swift.WinStepCounts);
            var iStep = Median(intact.WinStepCounts);
            var sDed = Mean(swift.AllyDeaths);
            var iDed = Mean(intact.AllyDeaths);
            var fasterSwift = sStep < iStep;
            var safintact = iDed < sDed;
            var verdict = (fasterSwift && safintact) ? "분리(속전 빠르고 위험 / 온전 느리고 안전)"
                : (sStep < iStep && sDed < iDed) ? "속전 지배(둘 다 우세 — tension 붕괴)"
                : (sStep > iStep && sDed > iDed) ? "온전 지배(둘 다 우세)"
                : "혼합/불명확";
            sb.AppendLine($"  [{tier}] swift(step {sStep:0},死 {sDed:0.00}) vs intact(step {iStep:0},死 {iDed:0.00}) → {verdict}");
        }
        sb.AppendLine();
        sb.AppendLine("결론 가이드: 어느 tier에서도 '분리'가 안 나오면 build축만으로 Swift/Intact는 진짜 선택이 아니다");
        sb.AppendLine("(GPT Pro #1 위험 실증). 그 경우 tension은 build가 아니라 objective/encounter에서 와야 한다(P3).");
        return sb.ToString();
    }

    private static double Median(IReadOnlyList<int> values)
    {
        if (values.Count == 0) return 0;
        var s = values.OrderBy(x => x).ToList();
        var mid = s.Count / 2;
        return s.Count % 2 == 1 ? s[mid] : (s[mid - 1] + s[mid]) / 2.0;
    }

    private static double Mean(IReadOnlyList<int> values) => values.Count == 0 ? 0 : values.Average();

    // ---- protect objective ---------------------------------------------

    private sealed class ProtectCell
    {
        public string Posture = string.Empty;
        public int Wins;
        public int ProtecteeSurvived;       // 전 전투
        public int ProtecteeSurvivedOnWin;  // 승리한 전투 중
        public readonly List<int> WinSteps = new();
        public readonly List<int> SquadDeaths = new();  // 보호대상 제외
    }

    private static ProtectCell RunProtect(string label, TeamPostureType posture, System.Func<IReadOnlyList<BattleUnitLoadout>> enemyFactory)
    {
        var cell = new ProtectCell { Posture = label };
        for (var seed = 1; seed <= SeedCount; seed++)
        {
            var state = CombatTestFactory.CreateBattleState(BuildProtectSquad(), enemyFactory(), allyPosture: posture, seed: seed);
            var battle = BattleResolver.Run(state, MaxTicks);

            var protectee = battle.FinalUnits.FirstOrDefault(u => u.Id == "protectee");
            var protecteeAlive = protectee != null && protectee.IsAlive;
            if (protecteeAlive) cell.ProtecteeSurvived++;

            cell.SquadDeaths.Add(battle.FinalUnits.Count(u => u.Side == TeamSide.Ally && u.Id != "protectee" && !u.IsAlive));

            if (battle.Winner == TeamSide.Ally)
            {
                cell.Wins++;
                cell.WinSteps.Add(battle.StepCount);
                if (protecteeAlive) cell.ProtecteeSurvivedOnWin++;
            }
        }

        return cell;
    }

    // 전투 유닛 4 + 보호대상 1(fragile, 후방, 약공격) — "지켜야 할 사람".
    private static IReadOnlyList<BattleUnitLoadout> BuildProtectSquad()
    {
        return new[]
        {
            CombatTestFactory.CreateLoopAUnit("guard_front_a", classId: "vanguard", hp: 110f, physPower: 7f, armor: 4f, attackSpeed: 3f, moveSpeed: 1.7f, anchor: DeploymentAnchorId.FrontTop),
            CombatTestFactory.CreateLoopAUnit("guard_front_b", classId: "vanguard", hp: 105f, physPower: 7f, armor: 4f, attackSpeed: 3f, moveSpeed: 1.7f, anchor: DeploymentAnchorId.FrontBottom),
            CombatTestFactory.CreateLoopAUnit("guard_dps", classId: "duelist", hp: 80f, physPower: 9f, armor: 2f, attackSpeed: 4.5f, moveSpeed: 1.9f, anchor: DeploymentAnchorId.FrontCenter),
            CombatTestFactory.CreateLoopAUnit("guard_ranged", classId: "ranger", hp: 70f, physPower: 7f, armor: 2f, attackSpeed: 4f, moveSpeed: 1.8f, attackRange: 5.6f, anchor: DeploymentAnchorId.BackTop),
            CombatTestFactory.CreateLoopAUnit("protectee", classId: "mystic", hp: 35f, physPower: 2f, armor: 0f, attackSpeed: 2f, moveSpeed: 1.6f, attackRange: 2.0f, anchor: DeploymentAnchorId.BackCenter),
        };
    }

    // 근접 전용 적 — 후방 protectee에 닿으려면 전열을 통과해야 한다(positioning/posture가 의미를 가질 조건).
    private static IReadOnlyList<BattleUnitLoadout> BuildMeleeEnemies()
    {
        return new[]
        {
            CombatTestFactory.CreateLoopAUnit("enemy_m_a", race: "undead", classId: "duelist", hp: 60f, physPower: 6f, armor: 1f, attackSpeed: 3.5f, moveSpeed: 2.0f, attackRange: 1.2f, anchor: DeploymentAnchorId.FrontTop),
            CombatTestFactory.CreateLoopAUnit("enemy_m_b", race: "undead", classId: "duelist", hp: 58f, physPower: 6f, armor: 1f, attackSpeed: 3.5f, moveSpeed: 2.0f, attackRange: 1.2f, anchor: DeploymentAnchorId.FrontBottom),
            CombatTestFactory.CreateLoopAUnit("enemy_m_c", race: "undead", classId: "duelist", hp: 56f, physPower: 6f, armor: 1f, attackSpeed: 3.5f, moveSpeed: 2.05f, attackRange: 1.2f, anchor: DeploymentAnchorId.FrontCenter),
            CombatTestFactory.CreateLoopAUnit("enemy_m_d", race: "undead", classId: "raider", hp: 54f, physPower: 7f, armor: 0f, attackSpeed: 4f, moveSpeed: 2.1f, attackRange: 1.2f, anchor: DeploymentAnchorId.BackCenter),
        };
    }

    private static string BuildProtectReport(IReadOnlyList<ProtectCell> rows)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("=== Protect objective separability — posture x protectee survival ===");
        sb.AppendLine($"seeds={SeedCount}, maxTicks={MaxTicks}. squad 4 + protectee(fragile, back-center). enemy=moderate fixed.");
        sb.AppendLine();
        sb.AppendLine($"{"posture",-22}{"win%",-7}{"protectee surv%",-17}{"surv|win%",-12}{"winStep(med)",-14}{"squadDeaths(mean)"}");
        foreach (var r in rows)
        {
            var survPct = 100.0 * r.ProtecteeSurvived / SeedCount;
            var survOnWin = r.Wins > 0 ? 100.0 * r.ProtecteeSurvivedOnWin / r.Wins : 0;
            sb.AppendLine($"{r.Posture,-22}{100.0 * r.Wins / SeedCount,-7:0}{survPct,-17:0}{survOnWin,-12:0}{Median(r.WinSteps),-14:0.0}{Mean(r.SquadDeaths):0.00}");
        }
        sb.AppendLine();
        sb.AppendLine("분리: guard(ProtectCarry) protectee 생존% > aggressive(AllInBackline) → protect는 posture로 통제되는 진짜 선택.");
        sb.AppendLine("미분리: 생존%가 posture 무관(항상 살/죽) → 단순 fragile ally론 부족, 적 targeting priority 등 combat 메커니즘 필요(설계 입력).");
        return sb.ToString();
    }

    private static void WriteReport(string report, string fileName = "warrant-separability.txt")
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
