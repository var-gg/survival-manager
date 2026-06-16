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
/// 발화 세션 통합 — BattleResolved (콜백 게이트 moment). "발화는 세션, 표시+continuation은 씬":
/// 전투 해소 moment를 씬 BattleScreenController(Continue)가 아니라 **세션 GameSessionState.MarkBattleResolved**가 발화한다.
/// 씬 FinishBattle과 헤드리스 sim(TryResolveSelectedBattleNodeViaSimulation)이 모두 MarkBattleResolved를 경유하므로
/// 같은 한 소스에서 BattleResolved를 받는다(연결). 씬 표시는 bridge.PresentPending(GoToReward) — PlayMode 책임.
///
/// BindProfile/BeginNewExpedition으로 site_alpha_gate 컨텍스트를 세운 뒤 MarkBattleResolved 호출 →
/// 프로덕션이 BattleResolved를 **자동 발화**(수동 Advance 없이)하고 SiteIs 게이트가 정확히 충족됨을 단언.
/// 이 1줄(발화)이 제거되면 실패 = 배선 회귀 잠금.
/// </summary>
[Category("FastUnit")]
public sealed class BattleResolvedWiringFastTests
{
    [Test]
    public void ProductionMarkBattleResolved_FiresBattleResolved_WithSiteContext()
    {
        var lookup = EditorFreeCombatContentFixture.CreateRunLoopLookup();
        var session = GameSessionTestFactory.Create(lookup);
        session.BindProfile(new SaveProfile
        {
            ProfileId = "battle_resolved_wiring",
            Heroes = new List<HeroInstanceRecord>
            {
                CreateHero("hero-1", "vanguard"),
                CreateHero("hero-2", "ranger"),
                CreateHero("hero-3", "duelist"),
                CreateHero("hero-4", "mystic"),
            },
        });
        session.SetCurrentScene(SceneNames.Town);

        // 첫 원정 시작 → 선택 사이트 = site_alpha_gate 컨텍스트 확립.
        session.BeginNewExpedition();
        Assert.That(session.SelectedCampaignSiteId, Is.EqualTo("site_alpha_gate"),
            "BeginNewExpedition 후 기본 선택 사이트.");

        // BindProfile/BeginNewExpedition 이후 주입(클로버 회피) — site_alpha_gate 전투 해소에 걸린 이벤트.
        session.OverrideStoryDirector(CreateBattleResolvedDirector("site_alpha_gate"));

        // 프로덕션 경로: 전투 해소 정산 → MarkBattleResolved가 BattleResolved를 자동 발화한다.
        session.MarkBattleResolved(victory: true, stepCount: 12, eventCount: 5);

        var narrative = session.NarrativeProgress;
        Assert.That(narrative.StoryFlags, Contains.Item("story_flag_battle_alpha_gate"),
            "MarkBattleResolved에서 프로덕션이 BattleResolved를 발화 → flag.");

        var cutscenes = narrative.PendingPresentations
            .Count(request => request.PresentationKey == "dialogue_scene_battle_alpha_gate");
        Assert.That(cutscenes, Is.EqualTo(1),
            "전투 해소 연출이 정확히 1회 큐잉 — 발화원이 세션이고 SiteIs 컨텍스트가 정확함을 증명(수동 Advance 없이).");
    }

    private static StoryDirectorService CreateBattleResolvedDirector(string siteId)
    {
        var sequence = new DialogueSequenceSpec(
            "dialogue_scene_battle_alpha_gate",
            new[] { new DialogueLineSpec("line_1", "narrator", "loc.battle", string.Empty, string.Empty, 0f) });

        var battleEvent = new StoryEventSpec(
            "story_event_battle_alpha_gate",
            NarrativeMoment.BattleResolved,
            500,
            StoryOncePolicy.OncePerProfile,
            new[]
            {
                new StoryConditionSpec("condition_00", StoryConditionKind.SiteIs, siteId, string.Empty),
            },
            new[]
            {
                new StoryEffectSpec("effect_00", StoryEffectKind.SetFlag, "story_flag_battle_alpha_gate"),
                new StoryEffectSpec("effect_01", StoryEffectKind.EnqueuePresentation, nameof(StoryPresentationKind.DialogueScene)),
            },
            "dialogue_scene_battle_alpha_gate");

        return new StoryDirectorService(
            NarrativeProgressRecord.Empty,
            new[] { battleEvent },
            new DialogueAssemblyService(new[] { sequence }, Array.Empty<HeroLoreSpec>()));
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
