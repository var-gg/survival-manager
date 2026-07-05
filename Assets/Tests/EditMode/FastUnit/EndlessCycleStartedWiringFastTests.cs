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
/// 발화 세션 통합 — EndlessCycleStarted. "발화는 세션": 무한 순환 시작 moment를 씬이 아니라
/// **세션 GameSessionState.BeginEndlessExpedition**이 발화한다(BattleResolvedWiringFastTests 동형).
///
/// 함께 잠그는 세션 계약: 사이클 truth 전이(CycleIndex/Heat 1 증가), run 스탬프(EndlessCycleIndex),
/// run identity 회차 접미(#cN), Profile.Narrative 영속 미러, CanBeginEndlessCycle 게이트.
/// </summary>
[Category("FastUnit")]
public sealed class EndlessCycleStartedWiringFastTests
{
    [Test]
    public void BeginEndlessExpedition_FiresEndlessCycleStarted_AndStampsCycleTruth()
    {
        var session = CreateUnlockedSession("endless_cycle_wiring");
        Assert.That(EndlessEntryResolver.IsEndlessEntryActive(session.Profile.CampaignProgress), Is.True,
            "EndlessUnlocked → CTA 판정 활성.");

        // BindProfile 이후 주입(클로버 회피) — EndlessCycleStarted에 걸린 이벤트.
        session.OverrideStoryDirector(CreateEndlessDirector());

        session.BeginEndlessExpedition();

        // 발화: 프로덕션이 EndlessCycleStarted를 자동 발화 → flag + 연출 1회 큐잉.
        var narrative = session.NarrativeProgress;
        Assert.That(narrative.StoryFlags, Contains.Item("story_flag_endless_cycle_started"),
            "BeginEndlessExpedition에서 세션이 EndlessCycleStarted를 발화 → flag.");
        Assert.That(
            narrative.PendingPresentations.Count(request => request.PresentationKey == "dialogue_scene_endless_open"),
            Is.EqualTo(1),
            "순환 시작 연출이 정확히 1회 큐잉.");

        // 사이클 truth: director Progress → Profile.Narrative 미러 + run 스탬프 + run id 접미.
        Assert.That(narrative.EndlessCycle.CycleIndex, Is.EqualTo(1));
        Assert.That(narrative.EndlessCycle.Heat, Is.EqualTo(1));
        Assert.That(session.ActiveRun!.EndlessCycleIndex, Is.EqualTo(1), "ActiveRun에 회차 스탬프.");
        Assert.That(session.ActiveRun!.ExpeditionId, Does.EndWith("#c1"),
            "원정 identity가 회차로 분리(RunId는 StartRun 생성 GUID라 항상 유일).");

        // 영속 미러(SaveProfile 대상 record)까지 도달.
        Assert.That(session.Profile.Narrative.EndlessCycle.CycleIndex, Is.EqualTo(1));
        Assert.That(session.Profile.ActiveRun!.EndlessCycleIndex, Is.EqualTo(1));
    }

    [Test]
    public void BeginEndlessExpedition_WithoutUnlock_FallsBackToStoryExpedition()
    {
        var lookup = EditorFreeCombatContentFixture.CreateRunLoopLookup();
        var session = GameSessionTestFactory.Create(lookup);
        session.BindProfile(new SaveProfile
        {
            ProfileId = "endless_cycle_locked",
            Heroes = CreateHeroes(),
        });
        session.SetCurrentScene(SceneNames.Town);

        Assert.That(EndlessEntryResolver.IsEndlessEntryActive(session.Profile.CampaignProgress), Is.False,
            "미해금 프로필은 CTA 판정 비활성.");

        session.BeginEndlessExpedition();

        Assert.That(session.ActiveRun!.EndlessCycleIndex, Is.EqualTo(0), "폴백은 스토리 원정 — 사이클 스탬프 없음.");
        Assert.That(session.Profile.Narrative.EndlessCycle.CycleIndex, Is.EqualTo(0), "사이클 truth 무변.");
        Assert.That(session.ActiveRun!.ExpeditionId, Does.Not.Contain("#c"), "원정 id 접미 없음.");
    }

    [Test]
    public void EndlessEntryResolver_ActivatesOnlyWhenUnlocked()
    {
        Assert.That(EndlessEntryResolver.IsEndlessEntryActive(null), Is.False);
        Assert.That(EndlessEntryResolver.IsEndlessEntryActive(new CampaignProgressRecord()), Is.False);
        Assert.That(
            EndlessEntryResolver.IsEndlessEntryActive(new CampaignProgressRecord { EndlessUnlocked = true }),
            Is.True);
    }

    private static GameSessionState CreateUnlockedSession(string profileId)
    {
        var lookup = EditorFreeCombatContentFixture.CreateRunLoopLookup();
        var session = GameSessionTestFactory.Create(lookup);
        session.BindProfile(new SaveProfile
        {
            ProfileId = profileId,
            Heroes = CreateHeroes(),
            CampaignProgress = new CampaignProgressRecord
            {
                StoryCleared = true,
                EndlessUnlocked = true,
            },
        });
        session.SetCurrentScene(SceneNames.Town);
        return session;
    }

    private static StoryDirectorService CreateEndlessDirector()
    {
        var sequence = new DialogueSequenceSpec(
            "dialogue_scene_endless_open",
            new[] { new DialogueLineSpec("line_1", "narrator", "loc.endless", string.Empty, string.Empty, 0f) });

        var cycleEvent = new StoryEventSpec(
            "story_event_endless_cycle_started",
            NarrativeMoment.EndlessCycleStarted,
            500,
            StoryOncePolicy.OncePerProfile,
            Array.Empty<StoryConditionSpec>(),
            new[]
            {
                new StoryEffectSpec("effect_00", StoryEffectKind.SetFlag, "story_flag_endless_cycle_started"),
                new StoryEffectSpec("effect_01", StoryEffectKind.EnqueuePresentation, nameof(StoryPresentationKind.DialogueScene)),
            },
            "dialogue_scene_endless_open");

        return new StoryDirectorService(
            NarrativeProgressRecord.Empty,
            new[] { cycleEvent },
            new DialogueAssemblyService(new[] { sequence }, Array.Empty<HeroLoreSpec>()));
    }

    private static List<HeroInstanceRecord> CreateHeroes()
    {
        return new List<HeroInstanceRecord>
        {
            CreateHero("hero-1", "vanguard"),
            CreateHero("hero-2", "ranger"),
            CreateHero("hero-3", "duelist"),
            CreateHero("hero-4", "mystic"),
        };
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
