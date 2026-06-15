using System.Collections.Generic;
using NUnit.Framework;
using SM.Combat.Model;
using SM.Persistence.Abstractions.Models;
using SM.Tests.EditMode.Fakes;
using SM.Unity;

namespace SM.Tests.EditMode;

/// <summary>
/// 원정 루프 화면 전환의 단일 진실(순수 ExpeditionFlowResolver) 결정표 — 씬·VisualElement 없이
/// 세션 상태만으로 "이 전환 이벤트가 어느 화면으로 가는가"를 검증한다. controller 인라인 분기와
/// 동일한 결정을 codify한 것이므로(후속 stage에서 controller가 이걸 채택), 이 테스트가 canonical 전환을 잠근다.
/// </summary>
[Category("FastUnit")]
public sealed class ExpeditionFlowResolverFastTests
{
    [Test]
    public void ExpeditionEntry_FreshSession_GoesToAtlas()
    {
        var session = CreateSession();
        Assert.That(session.HasPendingRewardSettlement, Is.False);
        Assert.That(ExpeditionFlowResolver.ResolveExpeditionEntry(session), Is.EqualTo(NavTarget.Atlas),
            "원정 진행 중도 보상 정산도 없으면 Atlas로 진입(resume·new 공통).");
    }

    [Test]
    public void ExpeditionEntry_PendingSettlement_GoesToReward()
    {
        var session = CreateSession();
        session.BeginNewExpedition();
        Assert.That(session.PrepareSelectedBattleNodeHandoff(), Is.True);
        session.MarkBattleResolved(victory: true, stepCount: 8, eventCount: 4);

        Assert.That(session.HasPendingRewardSettlement, Is.True);
        Assert.That(ExpeditionFlowResolver.ResolveExpeditionEntry(session), Is.EqualTo(NavTarget.Reward),
            "보상 정산이 밀려 있으면 Town '원정'은 정산 화면으로 직행.");
    }

    [Test]
    public void AtlasContinue_BattleNode_GoesToBattle()
    {
        var session = CreateSession();
        session.BeginNewExpedition();

        Assert.That(session.GetSelectedExpeditionNode()?.RequiresBattle, Is.True, "첫 노드는 전투 노드.");
        Assert.That(ExpeditionFlowResolver.ResolveAtlasContinue(session), Is.EqualTo(NavTarget.Battle),
            "선택 노드가 전투면 Atlas '계속'은 Battle로 간다.");
    }

    [Test]
    public void FixedTransitions_AfterBattleAndReward()
    {
        Assert.That(ExpeditionFlowResolver.AfterBattleResolved, Is.EqualTo(NavTarget.Reward),
            "전투 종료 → 보상 정산(고정).");
        Assert.That(ExpeditionFlowResolver.AfterRewardSettled, Is.EqualTo(NavTarget.Town),
            "보상 정산 종료 → Town 복귀(고정).");
    }

    private static GameSessionState CreateSession()
    {
        var lookup = EditorFreeCombatContentFixture.CreateRunLoopLookup();
        var session = GameSessionTestFactory.Create(lookup);
        session.BindProfile(new SaveProfile
        {
            ProfileId = "expedition_flow_resolver_test",
            Heroes = new List<HeroInstanceRecord>
            {
                CreateHero("hero-1", "vanguard"),
                CreateHero("hero-2", "ranger"),
                CreateHero("hero-3", "duelist"),
                CreateHero("hero-4", "mystic"),
            },
        });
        session.SetCurrentScene(SceneNames.Town);
        return session;
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
