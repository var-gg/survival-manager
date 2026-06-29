using System.Collections.Generic;
using NUnit.Framework;
using SM.Core;
using SM.Persistence.Abstractions.Models;
using SM.Tests.EditMode.Fakes;
using SM.Unity;

namespace SM.Tests.EditMode;

/// <summary>
/// 노드 효과 이중 정산 회귀 잠금 — 헤드리스 sim 경로는 전투 노드 victory 시
/// <c>TryResolveSelectedBattleNodeViaSimulation → MarkBattleResolved</c>(내부에서 노드 정산 1회)를 탄 뒤
/// 곧바로 <c>ResolveSelectedExpeditionNode</c>로 커서를 전진시킨다. 이때 ResolveSelectedExpeditionNode가
/// 이미 정산된 노드를 재정산하면 Gold/Echo 효과가 2배 적립되어 헤드리스 정산이 씬(MarkBattleResolved만 경유)과
/// 갈라진다. 이 테스트는 같은 전투 노드를 두 경로로 정산해도 효과가 정확히 1회만 적용됨을 잠근다.
///
/// 엔지니어링 감사(analysis-engineering-audit-headless-divergence-fallback-deadcode-2026-06) 최우선 #1.
/// </summary>
[Category("FastUnit")]
public sealed class ExpeditionNodeEffectIdempotencyFastTests
{
    [Test]
    public void HeadlessResolveAfterMarkBattleResolved_DoesNotReapplyNodeEffect()
    {
        // FakeCombatContentLookup = authored 캠페인 없음 → fallback 원정 노드(camp → ambush-route(Gold +4) → …).
        var session = GameSessionTestFactory.Create();
        session.BindProfile(new SaveProfile
        {
            ProfileId = "node_effect_idempotency",
            Heroes = new List<HeroInstanceRecord>
            {
                CreateHero("hero-1", "vanguard"),
                CreateHero("hero-2", "ranger"),
                CreateHero("hero-3", "duelist"),
                CreateHero("hero-4", "mystic"),
            },
        });
        session.SetCurrentScene(SceneNames.Town);

        session.BeginNewExpedition();
        // camp(효과 없음) 정산 → 첫 전투 노드(ambush-route, Gold +4)로 전진.
        session.ResolveSelectedExpeditionNode();

        var battleNode = session.GetSelectedExpeditionNode();
        Assert.That(battleNode, Is.Not.Null, "camp 다음 전투 노드가 선택되어야 한다.");
        Assert.That(battleNode!.Id, Is.EqualTo("ambush-route"),
            "fallback 원정에서 camp 다음은 Gold 효과를 가진 전투 노드.");
        Assert.That(battleNode.RequiresBattle, Is.True);

        var goldBefore = session.Profile.Currencies.Gold;
        var nodeGold = battleNode.EffectAmount; // 4

        // --- 헤드리스 sim 정산 경로 재현 ---
        // 1) 전투 해소 → MarkBattleResolved가 노드 효과를 1회 적용.
        session.MarkBattleResolved(victory: true, stepCount: 1, eventCount: 1);
        var goldAfterBattle = session.Profile.Currencies.Gold;

        // 비공허 보증: 전투 해소가 실제로 노드 효과를 적용했다.
        Assert.That(goldAfterBattle, Is.GreaterThanOrEqualTo(goldBefore + nodeGold),
            "MarkBattleResolved가 전투 노드 Gold 효과를 1회 적용(테스트가 공허하지 않음).");

        // 2) 곧이어 커서 전진 resolve — 이미 정산된 노드라 효과를 재적용하면 안 된다.
        session.ResolveSelectedExpeditionNode();
        var goldAfterResolve = session.Profile.Currencies.Gold;

        // 회귀 잠금: 두 번째 resolve는 통화를 0 추가(이중 정산 방지). 버그 상태면 +nodeGold 만큼 더 늘어난다.
        Assert.That(goldAfterResolve, Is.EqualTo(goldAfterBattle),
            "이미 정산된 전투 노드를 ResolveSelectedExpeditionNode가 재정산하지 않는다 — 헤드리스=씬 노드당 1회 계약.");
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
