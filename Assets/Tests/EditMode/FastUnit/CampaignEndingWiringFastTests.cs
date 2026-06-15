using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using SM.Core;
using SM.Meta;
using SM.Persistence.Abstractions.Models;
using SM.Tests.EditMode.Fakes;
using SM.Tests.EditMode.Playthrough;
using SM.Unity;

namespace SM.Tests.EditMode;

/// <summary>
/// 엔딩 실게임 배선 (Stage 1.5b ①) — CampaignEndingGateFastTests가 director 메커니즘을(수동 Advance) 증명한다면,
/// 이 테스트는 **프로덕션 extract 경로(SessionExpeditionFlow.UpdateCampaignProgressForResolvedNode)가
/// ExtractCommitted를 스스로 발화**해 엔딩이 켜짐을 증명한다 — 수동 Advance 없이.
///
/// 핵심: 지금까지 프로덕션엔 ExtractCommitted 호출처가 0이라 엔딩(및 사이트별 영웅 해금·midpoint 등
/// ExtractCommitted에 걸린 전 narrative 계층)이 dead였다. extract 정산 끝의 발화 1줄이 이를 점화한다.
/// 이 골든은 그 1줄이 제거되면 실패한다 = 배선 회귀 잠금.
///
/// 구동은 CampaignPlaythroughRunner(#4) 재사용 — seed된 director를 주입한 세션을 실 캠페인으로 끝까지 돌린다.
/// fixture 최종 좌표(chapter_beta/site_beta_watch)에 엔딩 이벤트를 걸어 그 사이트 extract에서만 발화되게 한다.
/// </summary>
[Category("FastUnit")]
public sealed class CampaignEndingWiringFastTests
{
    private const string FixtureFinalChapterId = "chapter_beta";
    private const string FixtureFinalSiteId = "site_beta_watch";

    [Test]
    public void ProductionExtractPath_DrivesCampaign_FiresExtractCommitted_AndQueuesEndingAtFinalSite()
    {
        // --- 세션 + 4인 로스터 ---
        var lookup = EditorFreeCombatContentFixture.CreateRunLoopLookup();
        var session = GameSessionTestFactory.Create(lookup);
        session.BindProfile(new SaveProfile
        {
            ProfileId = "campaign_ending_wiring",
            Heroes = new List<HeroInstanceRecord>
            {
                CreateHero("hero-1", "vanguard"),
                CreateHero("hero-2", "ranger"),
                CreateHero("hero-3", "duelist"),
                CreateHero("hero-4", "mystic"),
            },
        });
        session.SetCurrentScene(SceneNames.Town);

        // BindProfile 이후 seed director 주입 — RebindNarrativeServices(BindProfile 끝)는 이미 끝났고
        // 이후 expedition 흐름은 director를 재구성하지 않으므로 캠페인 구동 내내 생존한다.
        session.OverrideStoryDirector(CreateEndingDirector(FixtureFinalChapterId, FixtureFinalSiteId));

        // 프로덕션 경로로 캠페인 완주 — 매 extract에서 production이 ExtractCommitted를 자동 발화한다.
        var result = new CampaignPlaythroughRunner(
            session,
            new ScriptedPlaythroughPolicy(rewardIndex: 0),
            new RecordingNavSink()).Run();

        Assert.That(result.StoryCleared, Is.True, "캠페인이 엔딩까지 완주.");

        // --- 프로덕션 발화 증명: 엔딩 flag + 엔딩 컷씬이 큐에 올랐다 (수동 Advance 없이) ---
        var narrative = session.NarrativeProgress;
        Assert.That(narrative.StoryFlags, Contains.Item("story_flag_campaign_complete"),
            "최종 사이트 extract에서 프로덕션이 ExtractCommitted 발화 → 캠페인 완료 flag.");
        Assert.That(narrative.StoryFlags, Contains.Item("story_flag_endless_open"),
            "엔딩과 함께 무한모드 해금 flag.");

        var endingCutscenes = narrative.PendingPresentations
            .Count(request => request.PresentationKey == "dialogue_scene_campaign_complete");
        Assert.That(endingCutscenes, Is.EqualTo(1),
            "엔딩 컷씬이 정확히 1회 큐잉 — 최종 사이트에서만 조건 충족 + OncePerProfile dedup. " +
            "비최종 사이트(alpha gate/depths)의 extract도 ExtractCommitted를 발화하나 ChapterIs/SiteIs 불충족으로 무발화.");
    }

    // story_event_campaign_complete.asset 재현, 단 fixture 최종 좌표에 게이트(CampaignEndingGate는 실게임 좌표).
    private static StoryDirectorService CreateEndingDirector(string chapterId, string siteId)
    {
        var endingSequence = new DialogueSequenceSpec(
            "dialogue_scene_campaign_complete",
            new[] { new DialogueLineSpec("line_1", "narrator", "loc.ending", string.Empty, string.Empty, 0f) });

        var campaignComplete = new StoryEventSpec(
            "story_event_campaign_complete",
            NarrativeMoment.ExtractCommitted,
            900,
            StoryOncePolicy.OncePerProfile,
            new[]
            {
                new StoryConditionSpec("condition_00", StoryConditionKind.ChapterIs, chapterId, string.Empty),
                new StoryConditionSpec("condition_01", StoryConditionKind.SiteIs, siteId, string.Empty),
            },
            new[]
            {
                new StoryEffectSpec("effect_00", StoryEffectKind.SetFlag, "story_flag_campaign_complete"),
                new StoryEffectSpec("effect_01", StoryEffectKind.SetFlag, "story_flag_endless_open"),
                new StoryEffectSpec("effect_02", StoryEffectKind.EnqueuePresentation, nameof(StoryPresentationKind.DialogueScene)),
            },
            "dialogue_scene_campaign_complete");

        return new StoryDirectorService(
            NarrativeProgressRecord.Empty,
            new[] { campaignComplete },
            new DialogueAssemblyService(new[] { endingSequence }, Array.Empty<HeroLoreSpec>()));
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
