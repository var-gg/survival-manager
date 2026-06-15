using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using SM.Core;
using SM.Meta;
using SM.Persistence.Abstractions.Models;
using SM.Tests.EditMode.Fakes;
using SM.Unity;

namespace SM.Tests.EditMode;

/// <summary>
/// 발화 세션 통합 — SiteEntered (RewardCommitted 패턴 복제). "발화는 세션, 표시는 씬":
/// 사이트 진입 moment를 씬 AtlasScreenController가 아니라 **세션 GameSessionState.BeginNewExpedition**이 발화한다.
/// runner가 사이트마다 BeginNewExpedition을 호출하므로 헤드리스 드라이버와 실게임이 같은 한 소스를 공유한다(연결).
///
/// seed된 director를 주입한 세션에서 BeginNewExpedition을 호출 → 프로덕션이 SiteEntered를 **자동 발화**(수동 Advance 없이)
/// 하고, 선택 사이트(SiteIs)에 걸린 이벤트가 발화됨을 단언 = 발화원이 세션이고 컨텍스트가 정확함을 증명.
/// 씬 표시(bridge.PresentPending)는 PlayMode 책임(AtlasScreenController.ContinueToExpedition).
/// </summary>
[Category("FastUnit")]
public sealed class SiteEnteredWiringFastTests
{
    [Test]
    public void ProductionBeginNewExpedition_FiresSiteEntered_WithSiteContext()
    {
        var lookup = EditorFreeCombatContentFixture.CreateRunLoopLookup();
        var session = GameSessionTestFactory.Create(lookup);
        session.BindProfile(new SaveProfile
        {
            ProfileId = "site_entered_wiring",
            Heroes = new List<HeroInstanceRecord>
            {
                CreateHero("hero-1", "vanguard"),
                CreateHero("hero-2", "ranger"),
                CreateHero("hero-3", "duelist"),
                CreateHero("hero-4", "mystic"),
            },
        });
        session.SetCurrentScene(SceneNames.Town);

        // 첫 원정 기본 선택 사이트 = site_alpha_gate (캠페인 골든과 동일 전제).
        Assert.That(session.SelectedCampaignSiteId, Is.EqualTo("site_alpha_gate"),
            "BindProfile 후 기본 선택 사이트.");

        // BindProfile 이후 주입(클로버 회피) — site_alpha_gate 진입에 걸린 이벤트.
        session.OverrideStoryDirector(CreateSiteDirector("site_alpha_gate"));

        // 프로덕션 경로: 원정 시작 → BeginNewExpedition이 SiteEntered를 자동 발화한다.
        session.BeginNewExpedition();

        var narrative = session.NarrativeProgress;
        Assert.That(narrative.StoryFlags, Contains.Item("story_flag_site_alpha_gate"),
            "BeginNewExpedition에서 프로덕션이 SiteEntered를 발화 → site flag.");

        var siteCutscenes = narrative.PendingPresentations
            .Count(request => request.PresentationKey == "dialogue_scene_site_alpha_gate");
        Assert.That(siteCutscenes, Is.EqualTo(1),
            "사이트 진입 연출이 정확히 1회 큐잉 — 발화원이 세션이고 SiteIs 컨텍스트가 정확함을 증명(수동 Advance 없이).");
    }

    private static StoryDirectorService CreateSiteDirector(string siteId)
    {
        var siteSequence = new DialogueSequenceSpec(
            "dialogue_scene_site_alpha_gate",
            new[] { new DialogueLineSpec("line_1", "narrator", "loc.site", string.Empty, string.Empty, 0f) });

        var siteEvent = new StoryEventSpec(
            "story_event_site_alpha_gate",
            NarrativeMoment.SiteEntered,
            500,
            StoryOncePolicy.OncePerProfile,
            new[]
            {
                new StoryConditionSpec("condition_00", StoryConditionKind.SiteIs, siteId, string.Empty),
            },
            new[]
            {
                new StoryEffectSpec("effect_00", StoryEffectKind.SetFlag, "story_flag_site_alpha_gate"),
                new StoryEffectSpec("effect_01", StoryEffectKind.EnqueuePresentation, nameof(StoryPresentationKind.DialogueScene)),
            },
            "dialogue_scene_site_alpha_gate");

        return new StoryDirectorService(
            NarrativeProgressRecord.Empty,
            new[] { siteEvent },
            new DialogueAssemblyService(new[] { siteSequence }, Array.Empty<HeroLoreSpec>()));
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
