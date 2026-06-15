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
/// 결정 정책 seam 골든 (#4) — CampaignCompletionGoldenFastTests가 선택을 인라인으로 하드코딩한다면,
/// 이 골든은 그 선택을 <see cref="IPlaythroughDecisionPolicy"/>로 뽑아낸 뒤 <see cref="CampaignPlaythroughRunner"/>가
/// 같은 캠페인을 같은 엔딩까지 끌고 가는지를 증명한다.
/// = "policy를 갈아끼워도(실제 LLM이든 스크립트든) 게임은 시작부터 엔딩까지 C#으로 완주한다"는 byte-identical 보증.
///
/// 인라인 골든과 동일 결정값: 보상 항상 0번, 진형 4인 배치(역할 인지) → 자동 승리 fixture에서 동일 결과.
/// </summary>
[Category("FastUnit")]
public sealed class CampaignPlaythroughPolicyGoldenFastTests
{
    [Test]
    public void PolicyDrivenRunner_TraversesCampaignToEnding_MatchesInlineGolden()
    {
        // --- 세션 + 4인 로스터 (씬 0) ---
        var lookup = EditorFreeCombatContentFixture.CreateRunLoopLookup();
        var session = GameSessionTestFactory.Create(lookup);
        session.BindProfile(new SaveProfile
        {
            ProfileId = "campaign_playthrough_policy_golden",
            Heroes = new List<HeroInstanceRecord>
            {
                CreateHero("hero-1", "vanguard"),
                CreateHero("hero-2", "ranger"),
                CreateHero("hero-3", "duelist"),
                CreateHero("hero-4", "mystic"),
            },
        });
        session.SetCurrentScene(SceneNames.Town);

        // --- policy 갈아끼우기: 스크립트 정책이 모든 결정을 내린다 ---
        var runner = new CampaignPlaythroughRunner(
            session,
            new ScriptedPlaythroughPolicy(rewardIndex: 0),
            new RecordingNavSink());
        var result = runner.Run();

        // --- 종착: 인라인 골든과 동일한 엔딩 ---
        Assert.That(result.StoryCleared, Is.True,
            "policy 구동 runner가 캠페인을 엔딩(StoryCleared)까지 완주.");
        Assert.That(result.EndlessUnlocked, Is.True,
            "엔딩과 함께 무한모드 해금.");
        Assert.That(result.ClearedSiteIds,
            Is.EqualTo(new[] { "site_alpha_gate", "site_alpha_depths", "site_beta_watch" }),
            "캠페인 순서대로 사이트를 거쳤다(alpha 2 → beta 1) — 인라인 골든과 동일.");
        Assert.That(result.ClearedChapterIds,
            Is.EquivalentTo(new[] { "chapter_alpha", "chapter_beta" }),
            "두 챕터 모두 클리어 기록.");
        Assert.That(result.TotalBattleNodes, Is.EqualTo(12),
            "3사이트 × 4전투 노드 = 12 — 인라인 골든과 동일.");

        // --- 사이트별 관찰: 매 사이트 4전투 + extract + 보상 0번 선택 ---
        Assert.That(result.SiteObservations.Count, Is.EqualTo(3));
        foreach (var site in result.SiteObservations)
        {
            Assert.That(site.BattleNodeIds.Count, Is.EqualTo(4),
                $"{site.SiteId}: 전투 노드 4개(skirmish_a→skirmish_b→elite→boss).");
            Assert.That(site.ExtractNodeId, Is.EqualTo($"{site.SiteId}:extract"),
                $"{site.SiteId}: 마지막은 extract 노드.");
            Assert.That(site.ChosenRewardIndex, Is.EqualTo(0),
                $"{site.SiteId}: 스크립트 정책은 보상 0번 선택.");
            Assert.That(site.RewardOptionCount, Is.GreaterThan(0),
                $"{site.SiteId}: 보상 후보가 제시됨.");
        }
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
