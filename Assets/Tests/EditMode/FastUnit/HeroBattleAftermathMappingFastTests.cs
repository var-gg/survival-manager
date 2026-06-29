using System.Collections.Generic;
using NUnit.Framework;
using SM.Combat.Model;
using SM.Core;
using SM.Persistence.Abstractions.Models;
using SM.Tests.EditMode.Fakes;
using SM.Unity;

namespace SM.Tests.EditMode;

/// <summary>
/// 전투 정산 HP/EXP 반영 회귀 잠금 (P0 매핑 결함) — BattleFactory가 ally unit EntityId를
/// "ally_{index}_{heroId}"로 만든다(BattleFactory.cs:31). 그래서 finalUnits.Id가 "ally_0_hero-1"인데,
/// ApplyHeroBattleAftermath는 그동안 그 id를 roster hero id("hero-1")로 *직접* lookup해 항상 매핑 실패 →
/// 전투를 이겨도 HP/EXP가 한 번도 반영되지 않았다(씬·헤드리스 공통, 기존 테스트 미커버).
/// 접두사를 벗겨 매핑하도록 고친 뒤, 승리한 ally hero의 EXP·HP가 실제로 반영됨을 잠근다.
///
/// 엔지니어링 감사 follow-up — analysis-engineering-audit-headless-divergence-fallback-deadcode-2026-06.
/// 이 결함은 #3 강화 게이트가 헤드리스 실 sim에서 '4승 후 진척 0'으로 처음 포착했다.
/// </summary>
[Category("FastUnit")]
public sealed class HeroBattleAftermathMappingFastTests
{
    [Test]
    public void MarkBattleResolved_Victory_ReflectsHpAndExp_DespiteAllyPrefixedUnitId()
    {
        var session = GameSessionTestFactory.Create();
        session.BindProfile(new SaveProfile
        {
            ProfileId = "hero_aftermath_mapping",
            Heroes = new List<HeroInstanceRecord> { CreateHero("hero-1", "vanguard") },
        });
        session.SetCurrentScene(SceneNames.Town);

        // BattleFactory 포맷대로 "ally_{index}_" 접두사가 붙은 finalUnit — 매핑이 깨졌던 바로 그 형태.
        var finalUnits = new List<BattleUnitReadModel>
        {
            new(
                Id: "ally_0_hero-1",
                Name: "Hero 1",
                Side: TeamSide.Ally,
                Anchor: DeploymentAnchorId.FrontCenter,
                RaceId: "human",
                ClassId: "vanguard",
                Position: new CombatVector2(0f, 0f),
                CurrentHealth: 42f,
                MaxHealth: 100f,
                IsAlive: true,
                ActionState: CombatActionState.AcquireTarget,
                PendingActionType: null,
                TargetId: null,
                TargetName: null,
                WindupProgress: 0f,
                CooldownRemaining: 0f,
                CurrentEnergy: 0f,
                MaxEnergy: 100f,
                IsDefending: false),
        };

        session.MarkBattleResolved(victory: true, stepCount: 10, eventCount: 5, finalUnits);

        var progression = session.Profile.HeroProgressions.Find(record => record.HeroId == "hero-1");
        Assert.That(progression, Is.Not.Null, "hero-1 진척 레코드가 존재해야 한다.");
        Assert.That(progression!.Level > 1 || progression.Experience > 0, Is.True,
            "승리가 hero-1의 EXP/레벨에 반영 — 'ally_{index}_' 접두사 매핑 결함 회귀 잠금(이전엔 둘 다 0).");

        var hero = session.Profile.Heroes.Find(record => record.HeroId == "hero-1");
        Assert.That(hero, Is.Not.Null);
        Assert.That(hero!.CurrentHp, Is.EqualTo(42),
            "전투 종료 HP가 hero record에 반영(이전엔 기본값 0 유지).");
        Assert.That(hero.MaxHp, Is.EqualTo(100), "MaxHp도 반영.");
    }

    private static HeroInstanceRecord CreateHero(string heroId, string classId)
    {
        return new HeroInstanceRecord
        {
            HeroId = heroId,
            Name = heroId,
            ArchetypeId = $"{classId}_archetype",
            RaceId = "human",
            ClassId = classId,
            PositiveTraitId = "trait_positive",
            NegativeTraitId = "trait_negative",
            EquippedItemIds = new List<string>(),
        };
    }
}
