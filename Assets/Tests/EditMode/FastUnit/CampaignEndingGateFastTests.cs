using System;
using System.Linq;
using NUnit.Framework;
using SM.Core;
using SM.Meta;

namespace SM.Tests.EditMode;

/// <summary>
/// 캠페인 엔딩 게이트 — "시작→엔딩 headless 플레이"의 종착점이 C#에서 도달·검출 가능함을 증명한다.
/// story_event_campaign_complete.asset(moment=ExtractCommitted, ChapterIs+SiteIs 조건,
/// SetFlag×2 + EnqueuePresentation DialogueScene)을 충실히 재현해 StoryDirectorService에 주입하고,
/// 최종 사이트 extract에서 ExtractCommitted를 발화하면 엔딩 flag + 엔딩 컷씬이 큐에 오름을 단언한다.
///
/// 즉 NarrativeMomentResolver.ShouldCommitExtract가 참이 되는 지점에서 AdvanceNarrative(ExtractCommitted)를
/// 부르면 엔딩이 켜진다 — 씬·플레이어 입력 없이. (프로덕션 발화 배선은 PlayMode 후속이나, 메커니즘은 여기서 고정.)
/// </summary>
[Category("FastUnit")]
public sealed class CampaignEndingGateFastTests
{
    private const string FinalChapterId = "chapter_heartforge_descent";
    private const string FinalSiteId = "site_worldscar_depths";

    [Test]
    public void ExtractCommit_AtFinalSite_RaisesCampaignCompleteAndEndlessFlags_AndQueuesEndingCutscene()
    {
        var director = CreateDirectorWithCampaignCompleteEvent();

        director.Advance(
            NarrativeMoment.ExtractCommitted,
            new StoryMomentContext { ChapterId = FinalChapterId, SiteId = FinalSiteId });

        Assert.That(director.Progress.StoryFlags, Contains.Item("story_flag_campaign_complete"),
            "최종 사이트 extract → 캠페인 완료 flag.");
        Assert.That(director.Progress.StoryFlags, Contains.Item("story_flag_endless_open"),
            "엔딩과 함께 무한모드 해금 flag(같은 event effect라 순서 의존 없음).");
        Assert.That(
            director.Progress.PendingPresentations.Any(request => request.PresentationKey == "dialogue_scene_campaign_complete"),
            Is.True,
            "엔딩 컷씬이 연출 큐에 오른다 — headless 드라이버가 drain으로 통과.");
    }

    [Test]
    public void ExtractCommit_BeforeFinalSite_DoesNotRaiseEnding()
    {
        var director = CreateDirectorWithCampaignCompleteEvent();

        // 다른 chapter/site에서 extract → ChapterIs/SiteIs 조건 불충족 → 엔딩 안 켜짐.
        director.Advance(
            NarrativeMoment.ExtractCommitted,
            new StoryMomentContext { ChapterId = "chapter_alpha", SiteId = "site_alpha_gate" });

        Assert.That(director.Progress.StoryFlags, Is.Empty, "비최종 사이트 extract는 엔딩 flag를 켜지 않는다.");
        Assert.That(director.Progress.PendingPresentations, Is.Empty, "엔딩 컷씬도 큐에 오르지 않는다.");
    }

    // story_event_campaign_complete.asset 충실 재현 (moment=7 ExtractCommitted, priority 900, OncePerProfile).
    private static StoryDirectorService CreateDirectorWithCampaignCompleteEvent()
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
                new StoryConditionSpec("condition_00", StoryConditionKind.ChapterIs, FinalChapterId, string.Empty),
                new StoryConditionSpec("condition_01", StoryConditionKind.SiteIs, FinalSiteId, string.Empty),
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
}
