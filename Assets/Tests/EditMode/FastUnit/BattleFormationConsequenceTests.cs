using System.Linq;
using NUnit.Framework;
using SM.Combat.Model;
using SM.Combat.Services;
using SM.Core.Contracts;

namespace SM.Tests.EditMode;

/// <summary>
/// P0 positional consequence — 위치가 전투 결과를 바꾸는 최소 규칙 검증.
/// 스크린(차단/보호), 측면/후방 보정, cinematic detector(SaveMoment/BacklineDive),
/// 그리고 "같은 스쿼드, 배치만 변경 → 결과가 달라진다" 골든.
/// </summary>
[Category("FastUnit")]
public sealed class BattleFormationConsequenceTests
{
    private static readonly BehaviorProfile CleanBackline = new(
        0.25f, 0.2f, 0f, 0f, 0.5f, 0.5f,
        DodgeChance: 0f, BlockChance: 0f, BlockMitigation: 0f, Stability: 0.5f,
        FormationLine: FormationLine.Backline);

    private static readonly BehaviorProfile CleanFrontline = new(
        0.25f, 0.2f, 0f, 0f, 0.5f, 0.5f,
        DodgeChance: 0f, BlockChance: 0f, BlockMitigation: 0f, Stability: 0.5f,
        FormationLine: FormationLine.Frontline,
        FrontlineGuardRadius: 3f);

    [Test]
    public void ScreenedBackline_TakesLessDamage_ThanExposedBackline()
    {
        var screenedState = CreateScreenScenario(screenNearCarry: true);
        var exposedState = CreateScreenScenario(screenNearCarry: false);
        var screenedAttacker = screenedState.Enemies.Single();
        var exposedAttacker = exposedState.Enemies.Single();
        var screenedCarry = screenedState.Allies.Single(unit => unit.Definition.Id == "carry");
        var exposedCarry = exposedState.Allies.Single(unit => unit.Definition.Id == "carry");

        var screened = HitResolutionService.ResolveBasicAttack(screenedState, screenedAttacker, screenedCarry);
        var exposed = HitResolutionService.ResolveBasicAttack(exposedState, exposedAttacker, exposedCarry);

        Assert.That(screened.Value, Is.LessThan(exposed.Value), "스크린이 멀쩡한 후열은 받는 피해가 줄어야 한다");
        var keptRatio = screened.Value / exposed.Value;
        Assert.That(keptRatio, Is.InRange(0.78f, 0.90f), "경감 폭은 12%~20% 대역(ProtectCarryBias 0..1)");
        Assert.That(screened.Note, Does.Contain("screened"));
        Assert.That(exposed.Note, Does.Not.Contain("screened"));
        Assert.That(screenedState.ActivityTelemetry.ScreenAbsorbCount, Is.EqualTo(1));
        Assert.That(screenedState.ActivityTelemetry.ScreenMitigationContribution, Is.GreaterThan(0f));
        Assert.That(exposedState.ActivityTelemetry.ScreenAbsorbCount, Is.Zero);
    }

    [Test]
    public void RearAttack_DealsMoreDamage_ThanFrontalAttack()
    {
        var rearState = CreateFlankScenario(out var rearAttacker, out var rearDefender, attackerPosition: new CombatVector2(2f, 0f));
        var frontState = CreateFlankScenario(out var frontAttacker, out var frontDefender, attackerPosition: new CombatVector2(-2f, 0.5f));

        var rear = HitResolutionService.ResolveBasicAttack(rearState, rearAttacker, rearDefender);
        var front = HitResolutionService.ResolveBasicAttack(frontState, frontAttacker, frontDefender);

        Assert.That(rear.Value / front.Value, Is.EqualTo(1f + BattleFormationConsequence.RearFlankDamageBonus).Within(0.015f),
            "교전 시선의 등 뒤에서 들어온 공격은 후방 보너스를 받는다");
        Assert.That(rear.Note, Does.Contain("rear"));
        Assert.That(front.Note, Does.Not.Contain("rear").And.Not.Contain("flank"));
        Assert.That(rearState.ActivityTelemetry.RearStrikeCount, Is.EqualTo(1));
        Assert.That(frontState.ActivityTelemetry.FlankStrikeCount, Is.Zero);
    }

    [Test]
    public void SideAttack_DealsSideBonus()
    {
        var sideState = CreateFlankScenario(out var sideAttacker, out var sideDefender, attackerPosition: new CombatVector2(0f, 2f));
        var frontState = CreateFlankScenario(out var frontAttacker, out var frontDefender, attackerPosition: new CombatVector2(-2f, 0.5f));

        var side = HitResolutionService.ResolveBasicAttack(sideState, sideAttacker, sideDefender);
        var front = HitResolutionService.ResolveBasicAttack(frontState, frontAttacker, frontDefender);

        Assert.That(side.Value / front.Value, Is.EqualTo(1f + BattleFormationConsequence.SideFlankDamageBonus).Within(0.015f));
        Assert.That(side.Note, Does.Contain("flank"));
        Assert.That(sideState.ActivityTelemetry.FlankStrikeCount, Is.EqualTo(1));
        Assert.That(sideState.ActivityTelemetry.RearStrikeCount, Is.Zero);
    }

    [Test]
    public void Targeting_PrefersFrontliner_WhileBacklineIsScreened()
    {
        var tactic = new TeamTacticProfile("plain", "Plain", TeamPostureType.StandardAdvance);
        var state = BattleFactory.Create(
            new[] { CombatTestFactory.CreateLoopAUnit("shooter", classId: "ranger", attackRange: 8f) with { TeamTactic = tactic } },
            new[]
            {
                CombatTestFactory.CreateLoopAUnit("enemy_screen", behavior: CleanFrontline) with { TeamTactic = tactic },
                CombatTestFactory.CreateLoopAUnit("enemy_carry", classId: "mystic", behavior: CleanBackline) with { TeamTactic = tactic },
            },
            seed: 5);
        var shooter = state.Allies.Single();
        var screen = state.Enemies.Single(unit => unit.Definition.Id == "enemy_screen");
        var carry = state.Enemies.Single(unit => unit.Definition.Id == "enemy_carry");
        shooter.SetPosition(new CombatVector2(-2f, 0f));
        carry.SetPosition(new CombatVector2(2.0f, 0f));

        // 스크린이 carry 옆에 멀쩡: carry가 0.25m 더 가까워도(< 페널티 0.35) 표적은 frontliner.
        screen.SetPosition(new CombatVector2(2.25f, 0f));
        var screenedPick = TargetScoringService.SelectTarget(state, shooter, TargetSelectorType.FirstEnemyInRange, BattleActionType.BasicAttack, null);
        Assert.That(screenedPick?.Definition.Id, Is.EqualTo("enemy_screen"), "스크린이 멀쩡하면 일반 타게팅은 후열을 피해간다");

        // 스크린이 가드 반경(3m) 밖으로 이탈: 후열이 노출되어 가장 가까운 표적으로 잡힌다.
        screen.SetPosition(new CombatVector2(2.25f, 3.4f));
        var exposedPick = TargetScoringService.SelectTarget(state, shooter, TargetSelectorType.FirstEnemyInRange, BattleActionType.BasicAttack, null);
        Assert.That(exposedPick?.Definition.Id, Is.EqualTo("enemy_carry"), "스크린이 깨지면 노출된 후열이 표적이 된다");
    }

    [Test]
    public void SaveMomentDetector_CountsClutchHeal_OnNearDeathAlly()
    {
        var healSkill = new BattleSkillSpec(
            "clutch_heal",
            "Clutch Heal",
            SkillKind.Heal,
            8f,
            6f,
            ResolvedSlotKind: ActionSlotKind.FlexActive,
            Lane: ActionLane.Primary,
            LockRule: ActionLockRule.SoftCommit,
            TargetRuleData: new TargetRule());
        var state = BattleFactory.Create(
            new[]
            {
                CombatTestFactory.CreateLoopAUnit("healer", classId: "mystic", flexActive: healSkill),
                CombatTestFactory.CreateLoopAUnit("victim", hp: 20f, behavior: CleanFrontline),
            },
            new[] { CombatTestFactory.CreateLoopAUnit("enemy") },
            seed: 9);
        var healer = state.Allies.Single(unit => unit.Definition.Id == "healer");
        var victim = state.Allies.Single(unit => unit.Definition.Id == "victim");
        victim.TakeDamage(17f); // 3/20 = 15% — 빈사

        healer.BeginWindup(BattleActionType.ActiveSkill, victim.Id, "clutch_heal");
        var events = CombatActionResolver.Resolve(state, healer);

        Assert.That(state.ActivityTelemetry.SaveMomentCount, Is.EqualTo(1));
        Assert.That(events.Any(evt => evt.Note.Contains("save_moment")), Is.True);
    }

    [Test]
    public void BacklineDiveKillDetector_CountsKill_OnlyUnderDiveIntent()
    {
        var state = BattleFactory.Create(
            new[] { CombatTestFactory.CreateLoopAUnit("diver", classId: "duelist", physPower: 9f) },
            new[]
            {
                CombatTestFactory.CreateLoopAUnit("enemy_carry", classId: "ranger", hp: 20f, behavior: CleanBackline),
            },
            seed: 13);
        var diver = state.Allies.Single();
        var carry = state.Enemies.Single();
        carry.TakeDamage(19f); // 1 HP — 다음 타격으로 사망
        diver.SetCombatIntent(new CombatIntent(CombatIntentType.Dive, carry.Id, null, default, 0, 0));

        diver.BeginWindup(BattleActionType.BasicAttack, carry.Id, null);
        var events = CombatActionResolver.Resolve(state, diver);

        Assert.That(carry.IsAlive, Is.False);
        Assert.That(state.ActivityTelemetry.BacklineDiveKillCount, Is.EqualTo(1));
        Assert.That(events.Any(evt => evt.EventKind == BattleEventKind.Kill && evt.Note.Contains("backline_dive")), Is.True);
    }

    [Test]
    public void Golden_DeploymentAloneChangesBacklineOutcome()
    {
        // 같은 멤버·같은 스탯·같은 시드 — 다른 것은 배치 앵커뿐.
        // 정렬 배치: 후열이 같은 레인의 전열 뒤(2.1m < 가드 3m, 스크린 성립).
        // 대각 배치: 전열은 위 레인, 후열은 아래 레인(4.17m > 3m, 스크린 붕괴).
        var aligned = RunDeploymentScenario(
            vanguardAnchors: (DeploymentAnchorId.FrontTop, DeploymentAnchorId.FrontBottom),
            rangerAnchor: DeploymentAnchorId.BackTop,
            mysticAnchor: DeploymentAnchorId.BackBottom);
        var misaligned = RunDeploymentScenario(
            vanguardAnchors: (DeploymentAnchorId.FrontTop, DeploymentAnchorId.FrontTop),
            rangerAnchor: DeploymentAnchorId.BackBottom,
            mysticAnchor: DeploymentAnchorId.BackBottom);

        // 풀시뮬에서 스크린 "흡수"는 드물다 — 후열이 맞기 시작하는 시점은 보통 전열이 이미 무너진 뒤이고,
        // 스크린의 실전 가치는 피해 경감보다 표적 보호(타게팅 페널티)다. 흡수량 자체는 focused 단위
        // 테스트가 검증하고, 골든은 결과 레벨의 주장만 한다: 같은 스쿼드, 배치만 바꿨는데 승패가 뒤집힌다.
        Assert.That(aligned.AllySurvivors, Is.GreaterThan(0),
            $"정렬 배치는 살아남는 쪽이어야 한다 (aligned units: {aligned.UnitsDebug})");
        Assert.That(aligned.EnemySurvivors, Is.Zero,
            $"정렬 배치는 적을 전멸시켜야 한다 (aligned units: {aligned.UnitsDebug})");
        Assert.That(misaligned.AllySurvivors, Is.Zero,
            $"대각 배치(스크린 붕괴)는 전멸해야 한다 (misaligned units: {misaligned.UnitsDebug})");
        Assert.That(
            misaligned.BacklineDamageTaken,
            Is.GreaterThan(aligned.BacklineDamageTaken * 1.15f),
            $"배치만 바꿔도 후열 누적 피해가 유의미하게 갈려야 한다 (aligned={aligned.BacklineDamageTaken:0.0}, misaligned={misaligned.BacklineDamageTaken:0.0})");
        Assert.That(
            aligned.Telemetry.FlankStrikeCount + aligned.Telemetry.RearStrikeCount,
            Is.GreaterThan(0),
            "측면/후방 보정이 실제 시뮬 안에서 발동해야 한다");
    }

    private sealed record DeploymentOutcome(
        float BacklineDamageTaken,
        int AllySurvivors,
        int EnemySurvivors,
        BattleActivityTelemetrySnapshot Telemetry,
        string UnitsDebug);

    private static DeploymentOutcome RunDeploymentScenario(
        (DeploymentAnchorId First, DeploymentAnchorId Second) vanguardAnchors,
        DeploymentAnchorId rangerAnchor,
        DeploymentAnchorId mysticAnchor)
    {
        // Phase 2의 HoldLine은 전열 홈을 1m 가까이 물려 대각 배치의 스크린 붕괴까지 완충해 버린다 —
        // 이 골든은 "배치"가 결정 변수여야 하므로 아군은 전열이 전진하는 StandardAdvance로 돌린다
        // (양 시나리오 동일 posture — 계약 불변: 같은 스쿼드·시드, 배치만 바꿔 승패 반전).
        var holdLine = new TeamTacticProfile("standard", "Standard", TeamPostureType.StandardAdvance);
        // 적은 다이브가 허용되는 자세(CollapseWeakSide)여야 후열 접촉이 실제로 발생한다 —
        // StandardAdvance에서는 RoleBrain이 Dive 진입을 막아 후열이 한 대도 안 맞는 무풍 골든이 된다.
        var collapse = new TeamTacticProfile("collapse", "Collapse", TeamPostureType.CollapseWeakSide);
        var allies = new[]
        {
            CombatTestFactory.CreateLoopAUnit("ally_van_a", anchor: vanguardAnchors.First, hp: 90f, armor: 3f) with { TeamTactic = holdLine },
            CombatTestFactory.CreateLoopAUnit("ally_van_b", anchor: vanguardAnchors.Second, hp: 90f, armor: 3f) with { TeamTactic = holdLine },
            CombatTestFactory.CreateLoopAUnit("ally_ranger", classId: "ranger", anchor: rangerAnchor, hp: 45f, attackRange: 6f) with { TeamTactic = holdLine },
            CombatTestFactory.CreateLoopAUnit("ally_mystic", classId: "mystic", anchor: mysticAnchor, hp: 45f, attackRange: 2.6f) with { TeamTactic = holdLine },
        };
        var enemies = new[]
        {
            CombatTestFactory.CreateLoopAUnit("enemy_van", anchor: DeploymentAnchorId.FrontTop, hp: 90f, armor: 3f) with { TeamTactic = collapse },
            CombatTestFactory.CreateLoopAUnit("enemy_duelist_a", classId: "duelist", anchor: DeploymentAnchorId.FrontBottom, hp: 60f, physPower: 6f) with { TeamTactic = collapse },
            CombatTestFactory.CreateLoopAUnit("enemy_duelist_b", classId: "duelist", anchor: DeploymentAnchorId.FrontCenter, hp: 60f, physPower: 6f) with { TeamTactic = collapse },
            CombatTestFactory.CreateLoopAUnit("enemy_ranger", classId: "ranger", anchor: DeploymentAnchorId.BackTop, hp: 45f, attackRange: 6f) with { TeamTactic = collapse },
        };

        var state = BattleFactory.Create(allies, enemies, seed: 77);
        var result = new BattleSimulator(state, 180).RunToEnd();
        var backlineDamage = state.Allies
            .Where(unit => unit.Definition.Id is "ally_ranger" or "ally_mystic")
            .Sum(unit => unit.MaxHealth - unit.CurrentHealth);
        var unitsDebug = string.Join(
            ", ",
            state.AllUnits.Select(unit => $"{unit.Definition.Id}:{unit.CurrentHealth:0}/{unit.MaxHealth:0}@({unit.Position.X:0.0},{unit.Position.Y:0.0})"));
        return new DeploymentOutcome(
            backlineDamage,
            state.Allies.Count(unit => unit.IsAlive),
            state.Enemies.Count(unit => unit.IsAlive),
            result.ActivityTelemetry!,
            unitsDebug);
    }

    private static BattleState CreateScreenScenario(bool screenNearCarry)
    {
        var state = BattleFactory.Create(
            new[]
            {
                CombatTestFactory.CreateLoopAUnit("screen", behavior: CleanFrontline),
                CombatTestFactory.CreateLoopAUnit("carry", classId: "ranger", behavior: CleanBackline),
            },
            new[] { CombatTestFactory.CreateLoopAUnit("attacker", physPower: 8f) },
            seed: 3);
        state.Allies.Single(unit => unit.Definition.Id == "carry").SetPosition(new CombatVector2(-4f, 0f));
        state.Allies.Single(unit => unit.Definition.Id == "screen").SetPosition(
            screenNearCarry ? new CombatVector2(-3.2f, 0f) : new CombatVector2(-4f, 3.8f));
        state.Enemies.Single().SetPosition(new CombatVector2(2f, 0f));
        return state;
    }

    private static BattleState CreateFlankScenario(
        out UnitSnapshot attacker,
        out UnitSnapshot defender,
        CombatVector2 attackerPosition)
    {
        var state = BattleFactory.Create(
            new[]
            {
                CombatTestFactory.CreateLoopAUnit("attacker", physPower: 8f),
                CombatTestFactory.CreateLoopAUnit("bait"),
            },
            new[] { CombatTestFactory.CreateLoopAUnit("defender", behavior: CleanFrontline) },
            seed: 7);
        attacker = state.Allies.Single(unit => unit.Definition.Id == "attacker");
        defender = state.Enemies.Single();
        var bait = state.Allies.Single(unit => unit.Definition.Id == "bait");

        // 수비자는 (0,0)에서 (-2,0)의 bait와 교전 중 — 시선은 -X.
        defender.SetPosition(new CombatVector2(0f, 0f));
        bait.SetPosition(new CombatVector2(-2f, 0f));
        defender.SetCurrentTarget(bait.Id);
        attacker.SetPosition(attackerPosition);
        return state;
    }
}
