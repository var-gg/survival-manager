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
/// 발화 세션 통합 — RewardCommitted (패턴 증명). "발화는 세션, 표시는 씬"의 첫 적용:
/// 보상 확정 moment를 씬 RewardScreenController가 아니라 **세션 GameSessionState.ApplyRewardChoice**가 발화한다.
/// 따라서 헤드리스 드라이버와 실게임이 같은 한 소스에서 RewardCommitted를 받는다(연결).
///
/// 이 테스트는 seed된 director를 주입한 세션을 CampaignPlaythroughRunner로 실 캠페인 구동 →
/// 프로덕션 ApplyRewardChoice가 RewardCommitted를 **자동 발화**(수동 Advance 없이)함을 증명한다.
/// site_alpha_gate 보상 확정에 걸린 이벤트가 그 사이트에서만 1회 발화됨을 단언 = 컨텍스트(SiteIs)까지 검증.
///
/// 씬 표시(bridge.PresentPending)는 PlayMode 책임 — 여기선 발화(헤드리스 연결)만 잠근다.
/// </summary>
[Category("FastUnit")]
public sealed class RewardCommittedWiringFastTests
{
    [Test]
    public void ProductionApplyRewardChoice_FiresRewardCommitted_WithSiteContext_DuringCampaign()
    {
        var lookup = EditorFreeCombatContentFixture.CreateRunLoopLookup();
        var session = GameSessionTestFactory.Create(lookup);
        session.BindProfile(new SaveProfile
        {
            ProfileId = "reward_committed_wiring",
            Heroes = new List<HeroInstanceRecord>
            {
                CreateHero("hero-1", "vanguard"),
                CreateHero("hero-2", "ranger"),
                CreateHero("hero-3", "duelist"),
                CreateHero("hero-4", "mystic"),
            },
        });
        session.SetCurrentScene(SceneNames.Town);

        // 첫 사이트(site_alpha_gate) 보상 확정에 걸린 이벤트 — BindProfile 이후 주입(클로버 회피).
        session.OverrideStoryDirector(CreateRewardDirector("site_alpha_gate"));

        // 실 캠페인 구동 — 매 사이트 ApplyRewardChoice가 프로덕션에서 RewardCommitted를 자동 발화한다.
        var result = new CampaignPlaythroughRunner(
            session,
            new ScriptedPlaythroughPolicy(rewardIndex: 0),
            new RecordingNavSink()).Run();

        Assert.That(result.StoryCleared, Is.True, "캠페인 완주(매 사이트 보상 확정 통과).");

        var narrative = session.NarrativeProgress;
        Assert.That(narrative.StoryFlags, Contains.Item("story_flag_reward_alpha_gate"),
            "site_alpha_gate 보상 확정에서 프로덕션 ApplyRewardChoice가 RewardCommitted를 발화 → flag.");

        var rewardCutscenes = narrative.PendingPresentations
            .Count(request => request.PresentationKey == "dialogue_scene_reward_alpha_gate");
        Assert.That(rewardCutscenes, Is.EqualTo(1),
            "보상 연출이 정확히 1회 큐잉 — site_alpha_gate에서만 SiteIs 충족 + OncePerProfile. " +
            "다른 사이트 보상 확정도 RewardCommitted를 발화하나 컨텍스트(SiteId) 불충족으로 무발화 = 발화원이 세션이고 컨텍스트가 정확함을 증명.");
    }

    // RewardCommitted moment, SiteIs 게이트 단일 — 보상 확정 시 발화 경로 검증용.
    private static StoryDirectorService CreateRewardDirector(string siteId)
    {
        var rewardSequence = new DialogueSequenceSpec(
            "dialogue_scene_reward_alpha_gate",
            new[] { new DialogueLineSpec("line_1", "narrator", "loc.reward", string.Empty, string.Empty, 0f) });

        var rewardEvent = new StoryEventSpec(
            "story_event_reward_alpha_gate",
            NarrativeMoment.RewardCommitted,
            500,
            StoryOncePolicy.OncePerProfile,
            new[]
            {
                new StoryConditionSpec("condition_00", StoryConditionKind.SiteIs, siteId, string.Empty),
            },
            new[]
            {
                new StoryEffectSpec("effect_00", StoryEffectKind.SetFlag, "story_flag_reward_alpha_gate"),
                new StoryEffectSpec("effect_01", StoryEffectKind.EnqueuePresentation, nameof(StoryPresentationKind.DialogueScene)),
            },
            "dialogue_scene_reward_alpha_gate");

        return new StoryDirectorService(
            NarrativeProgressRecord.Empty,
            new[] { rewardEvent },
            new DialogueAssemblyService(new[] { rewardSequence }, Array.Empty<HeroLoreSpec>()));
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
