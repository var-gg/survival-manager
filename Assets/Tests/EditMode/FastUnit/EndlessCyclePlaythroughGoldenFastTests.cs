using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using SM.Core;
using SM.Persistence.Abstractions.Models;
using SM.Tests.EditMode.Fakes;
using SM.Tests.EditMode.Playthrough;
using SM.Unity;

namespace SM.Tests.EditMode;

/// <summary>
/// 무한 순환 골든 — 캠페인 엔딩(StoryCleared/EndlessUnlocked) 이후 **무한 순환 2회차를 씬 없이 완주**한다.
/// CampaignPlaythroughPolicyGoldenFastTests의 연장: 같은 runner/policy로 엔딩까지 간 뒤
/// RunEndlessCycles가 실게임과 같은 세션 API(BeginEndlessExpedition)를 타며 메타루프를 완주한다.
///
/// 잠그는 계약(메타루프 레벨):
/// - 사이클 영속: Profile.Narrative.EndlessCycle.CycleIndex/Heat가 2회 후 2/2.
/// - 원정 identity: 회차별 ExpeditionId 접미(#c1/#c2)로 ledger/telemetry 충돌 없음.
/// - 사이클 스탬프 보존: 정산 직전까지 ActiveRun.EndlessCycleIndex가 유지된다.
/// - 정산 발생: 회차마다 RewardLedger에 신규 항목이 기록된다(무한이 정상 진행).
/// - 엔딩 재점화 없음(재클리어 멱등).
///
/// 범위 주의(적대 리뷰 2026-07-06): 이 골든은 AutoResolve 레인이라 reward-choice 정산이 extract
/// 노드(빈 RewardSourceId)에서만 일어나 SessionRewardSettlementFlow의 SourceId dedup **스킵 가드는
/// 여기서 트리거되지 않는다** — 그 가드의 회귀 잠금은 non-empty SourceId를 전투 노드에서 정산하는
/// EndlessRewardDedupFastTests가 소유한다. CommitId cycle-salt 분화도 순수 해시 테스트
/// (EndlessCycleServiceFastTests)가 별도로 잠근다.
/// </summary>
[Category("FastUnit")]
public sealed class EndlessCyclePlaythroughGoldenFastTests
{
    [Test]
    public void PolicyDrivenRunner_AfterEnding_CompletesTwoEndlessCycles_WithCycleDistinctRewards()
    {
        var lookup = EditorFreeCombatContentFixture.CreateRunLoopLookup();
        var session = GameSessionTestFactory.Create(lookup);
        session.BindProfile(new SaveProfile
        {
            ProfileId = "endless_cycle_golden",
            Heroes = new List<HeroInstanceRecord>
            {
                CreateHero("hero-1", "vanguard"),
                CreateHero("hero-2", "ranger"),
                CreateHero("hero-3", "duelist"),
                CreateHero("hero-4", "mystic"),
            },
        });
        session.SetCurrentScene(SceneNames.Town);

        var runner = new CampaignPlaythroughRunner(
            session,
            new ScriptedPlaythroughPolicy(rewardIndex: 0),
            new RecordingNavSink());

        // --- 1) 캠페인 엔딩까지(기존 골든과 동일 경로) ---
        var campaign = runner.Run();
        Assert.That(campaign.StoryCleared, Is.True);
        Assert.That(campaign.EndlessUnlocked, Is.True);

        // --- 2) 무한 순환 2회 ---
        var endless = runner.RunEndlessCycles(2);

        Assert.That(endless.Cycles, Has.Count.EqualTo(2), "2회차 모두 완주(패배 중단 없음).");
        Assert.That(endless.PersistedCycleIndex, Is.EqualTo(2), "사이클 truth가 narrative에 영속.");
        Assert.That(endless.PersistedHeat, Is.EqualTo(2));

        var first = endless.Cycles[0];
        var second = endless.Cycles[1];
        Assert.That(first.CycleIndex, Is.EqualTo(1));
        Assert.That(second.CycleIndex, Is.EqualTo(2));
        Assert.That(first.Heat, Is.EqualTo(1));
        Assert.That(second.Heat, Is.EqualTo(2));

        // 원정 identity 회차 분리(RunId는 StartRun 생성 GUID — 회차 identity는 ExpeditionId가 운반).
        Assert.That(first.ExpeditionId, Does.EndWith("#c1"));
        Assert.That(second.ExpeditionId, Does.EndWith("#c2"));
        Assert.That(first.ExpeditionId, Is.Not.EqualTo(second.ExpeditionId));

        // 사이트 몸통은 캠페인과 동일 구조(전투 4 + extract) — 기존 기계 재사용 증명.
        foreach (var cycle in endless.Cycles)
        {
            Assert.That(cycle.Site.BattleNodeIds, Has.Count.EqualTo(4));
            Assert.That(cycle.Site.ExtractNodeId, Does.EndWith(":extract"));
            Assert.That(cycle.Site.RewardOptionCount, Is.GreaterThan(0), "회차 재방문에도 보상 후보가 제시된다.");
            Assert.That(cycle.Site.EndlessCycleIndexAtSettlement, Is.EqualTo(cycle.CycleIndex),
                $"cycle {cycle.CycleIndex}: 정산 직전까지 ActiveRun.EndlessCycleIndex 보존.");
            // 보상 실지급 증명 — recovered 강등 경로는 ledger에 아무것도 쓰지 않는다(델타 0).
            // 수술(SourceId 프로필-수명 dedup을 무한 회차에서 스킵) 회귀 잠금. 통화 델타가 아니라 ledger
            // 델타를 보는 이유: 이 정산의 옵션이 증강 카드일 수 있어 통화 이동은 지급의 필요조건이 아니다.
            Assert.That(cycle.Site.RewardLedgerDelta, Is.GreaterThanOrEqualTo(1),
                $"cycle {cycle.CycleIndex}: 보상 선택이 ledger에 신규 기록됐다(recovered 아님). "
                + $"[chosenKind={cycle.Site.ChosenRewardKind}]");
        }

        // 엔딩 재점화 없음: 회차 재클리어가 캠페인 진행도를 되돌리거나 중복 기록하지 않는다(멱등).
        Assert.That(session.Profile.CampaignProgress.StoryCleared, Is.True);
        Assert.That(session.Profile.CampaignProgress.ClearedSiteIds.Distinct().Count(),
            Is.EqualTo(session.Profile.CampaignProgress.ClearedSiteIds.Count),
            "ClearedSiteIds 중복 없음(재클리어 멱등).");
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
