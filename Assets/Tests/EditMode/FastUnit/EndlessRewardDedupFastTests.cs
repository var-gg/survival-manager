using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using SM.Core;
using SM.Core.Content;
using SM.Persistence.Abstractions.Models;
using SM.Tests.EditMode.Fakes;
using SM.Unity;

namespace SM.Tests.EditMode;

/// <summary>
/// 무한 순환 보상 dedup 스킵 — **실 프로덕션 ApplyRewardChoice 경로**를 직접 구동한다.
///
/// 배경(2026-07-06 적대 리뷰 확정): EndlessCyclePlaythroughGoldenFastTests는 AutoResolve 레인이라
/// reward-choice 정산이 extract 노드에서만 일어나고, extract는 RewardSourceId가 빈 문자열이라
/// SessionRewardSettlementFlow.ApplyRewardChoice의 SourceId dedup 자체가 트리거되지 않는다 —
/// 즉 `isEndlessCycleRun` 가드를 통째로 지워도 그 골든은 그대로 green(vacuous). 이 테스트는 그 사각을 닫는다:
/// 전투 노드 정산(MarkBattleResolved)으로 **non-empty RewardSourceId**를 스탬프한 상태에서,
/// 같은 SourceId가 이미 ledger에 있어도 무한 회차면 '기지급(Recovered)' 강등 없이 재지급됨을 잠근다.
/// 대조군(스토리 cycle 0)은 같은 조건에서 정상적으로 Recovered 강등됨을 함께 잠가 가드 조건이 실제로
/// 의미 있음을 증명한다.
/// </summary>
[Category("FastUnit")]
public sealed class EndlessRewardDedupFastTests
{
    [Test]
    public void EndlessCycleRun_ReSettlesSameSourceId_WithoutRecoveredDowngrade()
    {
        var session = CreateEndlessUnlockedSession("endless_reward_dedup_cycle");
        session.BeginEndlessExpedition();
        Assert.That(session.ActiveRun!.EndlessCycleIndex, Is.GreaterThan(0),
            "무한 회차 run으로 시작해야 dedup 스킵 분기를 탄다.");

        var rewardSourceId = ResolveBattleSettlement(session);
        Assert.That(rewardSourceId, Is.Not.Empty,
            "전투 노드 정산은 non-empty RewardSourceId를 스탬프한다(가드가 실제로 트리거되는 전제).");

        // 같은 SourceId가 이미 정산 기록으로 존재하는 상태를 만든다(단, CommitId는 달라 CommitId dedup은 미발화).
        SeedPriorRewardChoiceEntry(session, rewardSourceId, commitId: "prior_commit_from_earlier_cycle");

        Assert.That(session.ApplyRewardChoice(0), Is.True);

        var summary = session.LastCommittedRewardSummary;
        Assert.That(summary, Is.Not.Null);
        Assert.That(summary!.WasRecoveredSettlement, Is.False,
            "무한 회차는 SourceId dedup을 건너뛰므로 같은 SourceId 재방문도 실제 지급된다(Recovered 강등 아님). "
            + "이 단언이 실패하면 SessionRewardSettlementFlow의 isEndlessCycleRun 가드가 회귀한 것.");
    }

    [Test]
    public void StoryRun_ReSettlesSameSourceId_IsDowngradedToRecovered()
    {
        // 대조군 — 스토리 run(cycle 0)에서는 SourceId dedup이 정상 발화해야 한다(가드 조건의 반증 방지).
        var session = CreateEndlessUnlockedSession("endless_reward_dedup_story");
        session.BeginNewExpedition();
        Assert.That(session.ActiveRun!.EndlessCycleIndex, Is.EqualTo(0),
            "스토리 run은 사이클 0.");

        var rewardSourceId = ResolveBattleSettlement(session);
        Assert.That(rewardSourceId, Is.Not.Empty);

        SeedPriorRewardChoiceEntry(session, rewardSourceId, commitId: "prior_commit_unrelated");

        Assert.That(session.ApplyRewardChoice(0), Is.True);

        var summary = session.LastCommittedRewardSummary;
        Assert.That(summary, Is.Not.Null);
        Assert.That(summary!.WasRecoveredSettlement, Is.True,
            "스토리 run은 같은 SourceId 재정산이 Recovered로 강등된다 — 무한 회차 스킵이 story 경로를 훼손하지 않음을 증명.");
    }

    // 전투 노드 → 정산 핸드오프 → MarkBattleResolved(승리)로 non-empty RewardSourceId/CommitId를 스탬프하고
    // pending settlement까지 만든 뒤 그 RewardSourceId를 반환한다(RunLoopContractFastTests와 동일 계약).
    private static string ResolveBattleSettlement(GameSessionState session)
    {
        Assert.That(session.PrepareSelectedBattleNodeHandoff(), Is.True,
            "첫 노드는 전투 노드여야 한다(fixture site의 skirmish).");
        session.BuildBattleLoadoutSnapshot();
        session.MarkBattleResolved(victory: true, stepCount: 8, eventCount: 4);
        return session.ActiveRun?.Overlay.RewardSourceId ?? string.Empty;
    }

    private static void SeedPriorRewardChoiceEntry(GameSessionState session, string sourceId, string commitId)
    {
        session.Profile.RewardLedger.Add(new RewardLedgerEntryRecord
        {
            EntryId = Guid.NewGuid().ToString("N"),
            RunId = "prior_run",
            RewardId = "reward.prior",
            RewardType = RewardType.Gold.ToString(),
            Amount = 1,
            CreatedAtUtc = DateTime.UnixEpoch.ToString("O"),
            Summary = "prior settlement",
            SourceId = sourceId,
            // HasRecordedRewardSettlement은 SourceKind가 ':reward_choice'로 끝나는 항목만 센다.
            SourceKind = "skirmish:reward_choice",
            CommitId = commitId,
        });
    }

    private static GameSessionState CreateEndlessUnlockedSession(string profileId)
    {
        var lookup = EditorFreeCombatContentFixture.CreateRunLoopLookup();
        var session = GameSessionTestFactory.Create(lookup);
        session.BindProfile(new SaveProfile
        {
            ProfileId = profileId,
            Heroes = new List<HeroInstanceRecord>
            {
                CreateHero("hero-1", "vanguard"),
                CreateHero("hero-2", "ranger"),
                CreateHero("hero-3", "duelist"),
                CreateHero("hero-4", "mystic"),
            },
            CampaignProgress = new CampaignProgressRecord
            {
                StoryCleared = true,
                EndlessUnlocked = true,
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
