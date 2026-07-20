using System.Collections.Generic;
using System.Text;
using SM.Combat.Model;
using SM.Core.Stats;

namespace SM.Combat.Services;

/// <summary>
/// Cross-platform 골든 해시 코퍼스 생성기 (ADR-0029 / deterministic-sim-and-fixed-point-migration Phase 0).
/// 고정 seed 집합으로 결정적 전투를 끝까지 돌리며 <see cref="BattleStateCanonicalHash"/>를 keyframe·final에서
/// 찍어 안정된 텍스트 코퍼스를 만든다. backend(Editor Mono / x86 IL2CPP / ARM IL2CPP)마다 같은 코퍼스를
/// 산출하면 cross-platform bit-exact가 성립한다.
///
/// <para><b>순수</b>(IO 없음): 문자열만 반환한다. 파일 쓰기·실행 진입은 editor/tool 레이어가 담당
/// (<c>BattleHashCorpusDumpEntry</c>). 관측 전용이라 sim behavior는 건드리지 않는다. 현 단계는 float sim의
/// same-binary 재현을 코퍼스로 고정하고, Fixed 마이그레이션 후 같은 진입점으로 cross-platform을 비교한다.</para>
///
/// <para>코퍼스는 Phase 6에서 dense collision / many modifiers / tiny durations / boundary HP / tie-heavy
/// fixture로 확장한다. 지금은 movement + 평타 + RNG(넉백·크릿)를 타는 melee 1v1 starter다.</para>
/// </summary>
public static class BattleHashCorpus
{
    public static readonly IReadOnlyList<int> DefaultSeeds = new[] { 7, 23, 42, 101, 2024 };

    public const int DefaultKeyframeInterval = 10;
    public const int DefaultMaxSteps = 400;

    public static string Generate(IReadOnlyList<int>? seeds = null, int keyframeInterval = DefaultKeyframeInterval)
    {
        var seedList = seeds ?? DefaultSeeds;
        var interval = keyframeInterval < 1 ? 1 : keyframeInterval;
        var builder = new StringBuilder();
        builder.Append("# CanonicalStateHashV1 corpus schema=")
            .Append(BattleStateCanonicalHash.SchemaVersion)
            .Append(" interval=")
            .Append(interval)
            .Append('\n');

        // fixture 2종: melee1v1(최소 궤적) + squad4v4(멀티유닛/멀티레인/원거리/시너지 패키지 merge).
        // squad는 1v1이 못 여는 표면 — 다수 후보 타깃 enumeration, >2 addend float 집계,
        // BattleFactory.ResolveTeamPackages(GroupBy dedup)+SynergyService 폴백 — 을 해시에 노출한다
        // (엔지니어링 감사 신규발견 (a) follow-up: 1v1-only 코퍼스가 squad 스케일 엔트로피를 못 잡던 갭).
        builder.Append("fixture=melee1v1\n");
        foreach (var seed in seedList)
        {
            AppendRun(builder, BuildMelee1v1(seed), seed, interval);
        }

        builder.Append("fixture=squad4v4\n");
        foreach (var seed in seedList)
        {
            AppendRun(builder, BuildSquad4v4(seed), seed, interval);
        }

        return builder.ToString();
    }

    private static void AppendRun(StringBuilder builder, BattleState state, int seed, int interval)
    {
        var simulator = new BattleSimulator(state, DefaultMaxSteps);
        builder.Append("seed=").Append(seed).Append('\n');

        // step 0 (초기 상태)
        builder.Append(simulator.State.StepIndex).Append(':').Append(BattleStateCanonicalHash.Compute(simulator.State)).Append('\n');

        var guard = 0;
        while (!simulator.IsFinished && guard++ < 10000)
        {
            simulator.Step();
            var step = simulator.State.StepIndex;
            if (simulator.IsFinished || step % interval == 0)
            {
                builder.Append(step).Append(':').Append(BattleStateCanonicalHash.Compute(simulator.State)).Append('\n');
            }
        }

        builder.Append("final:")
            .Append(BattleStateCanonicalHash.Compute(simulator.State))
            .Append(" steps=").Append(simulator.State.StepIndex)
            .Append(" winner=").Append(simulator.Winner?.ToString() ?? "none")
            .Append('\n');
    }

    // CombatTestFactory의 melee 1v1과 동일 shape (test asm을 참조할 수 없어 self-contained로 재구성).
    private static BattleState BuildMelee1v1(int seed)
    {
        var ally = BuildUnit("ally_blade", "human", DeploymentAnchorId.FrontCenter, hp: 60f, moveSpeed: 2.1f);
        var enemy = BuildUnit("enemy_blade", "undead", DeploymentAnchorId.FrontCenter, hp: 60f, moveSpeed: 1.9f);
        return BattleFactory.Create(
            new[] { ally },
            new[] { enemy },
            TeamPostureType.StandardAdvance,
            TeamPostureType.StandardAdvance,
            BattleSimulator.DefaultFixedStepSeconds,
            seed);
    }

    // squad 스케일 fixture — 전열 vanguard×2(class synergy 2/3 발화) + 후열 원거리/보조, 팀 전원 같은 race
    // (race synergy 4/4 발화). BattleFactory.ResolveTeamPackages → SynergyService.BuildForTeam 폴백 →
    // MergePackages 경로가 실제로 돌아 팀 패키지 dedup/merge 표면이 해시 스트림에 접힌다.
    private static BattleState BuildSquad4v4(int seed)
    {
        var allies = new[]
        {
            BuildUnit("ally_van_top", "human", DeploymentAnchorId.FrontTop, hp: 70f, moveSpeed: 1.9f),
            BuildUnit("ally_van_bot", "human", DeploymentAnchorId.FrontBottom, hp: 70f, moveSpeed: 1.9f),
            BuildUnit("ally_ranger", "human", DeploymentAnchorId.BackTop, hp: 45f, moveSpeed: 2.1f, classId: "ranger", phys: 8f, attackRange: 2.8f, critChance: 0.04f, critMultiplierBonus: 0.55f),
            BuildUnit("ally_mystic", "human", DeploymentAnchorId.BackBottom, hp: 40f, moveSpeed: 2.0f, classId: "mystic", phys: 5f, attackRange: 2.4f, heal: 6f, critChance: 0.05f, critMultiplierBonus: 0.45f),
        };
        var enemies = new[]
        {
            BuildUnit("enemy_van_top", "undead", DeploymentAnchorId.FrontTop, hp: 70f, moveSpeed: 1.8f),
            BuildUnit("enemy_van_bot", "undead", DeploymentAnchorId.FrontBottom, hp: 70f, moveSpeed: 1.8f),
            BuildUnit("enemy_ranger", "undead", DeploymentAnchorId.BackTop, hp: 45f, moveSpeed: 2.0f, classId: "ranger", phys: 8f, attackRange: 2.8f, critChance: 0.04f, critMultiplierBonus: 0.55f),
            BuildUnit("enemy_mystic", "undead", DeploymentAnchorId.BackBottom, hp: 40f, moveSpeed: 1.9f, classId: "mystic", phys: 5f, attackRange: 2.4f, heal: 6f, critChance: 0.05f, critMultiplierBonus: 0.45f),
        };
        return BattleFactory.Create(
            allies,
            enemies,
            TeamPostureType.StandardAdvance,
            TeamPostureType.StandardAdvance,
            BattleSimulator.DefaultFixedStepSeconds,
            seed);
    }

    private static BattleUnitLoadout BuildUnit(
        string id,
        string race,
        DeploymentAnchorId anchor,
        float hp,
        float moveSpeed,
        string classId = "vanguard",
        float phys = 6f,
        float attackRange = 1.2f,
        float heal = 0f,
        float critChance = 0.06f,
        float critMultiplierBonus = 0.5f)
    {
        return new BattleUnitLoadout(
            id,
            id,
            race,
            classId,
            anchor,
            new Dictionary<StatKey, float>
            {
                [StatKey.MaxHealth] = hp,
                [StatKey.PhysPower] = phys,
                [StatKey.HealPower] = heal,
                [StatKey.Armor] = 2f,
                [StatKey.MoveSpeed] = moveSpeed,
                [StatKey.AttackRange] = attackRange,
                [StatKey.AggroRadius] = 8f,
                [StatKey.AttackWindup] = 0.1f,
                [StatKey.AttackCooldown] = 0.5f,
                // ClassDefinition의 authored total multiplier는 runtime bonus(×1 + stat)로 투영된다.
                // 이 self-contained 결정성 fixture도 production class baseline을 명시해 global crit 변경을 관측한다.
                [StatKey.CritChance] = critChance,
                [StatKey.CritMultiplier] = critMultiplierBonus,
                [StatKey.LeashDistance] = 6f,
                [StatKey.TargetSwitchDelay] = 0.3f,
            },
            new[]
            {
                new UnitRuleChain($"rules:{id}", new[]
                {
                    new TacticRule(0, TacticConditionType.LowestHpEnemy, 0f, BattleActionType.BasicAttack, TargetSelectorType.LowestHpEnemy),
                    new TacticRule(1, TacticConditionType.Fallback, 0f, BattleActionType.WaitDefend, TargetSelectorType.Self),
                }),
            },
            System.Array.Empty<BattleSkillSpec>());
    }
}
