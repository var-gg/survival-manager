using System.Collections.Generic;
using NUnit.Framework;
using SM.Persistence.Abstractions.Models;
using SM.Tests.EditMode.Fakes;
using SM.Unity;
using SM.Unity.Narrative;

namespace SM.Tests.EditMode;

/// <summary>
/// narrative 전환 결정의 단일 진실(순수 NarrativeMomentResolver) — 씬 없이 세션 상태만으로
/// "방금 정산한 노드가 extract(=엔딩 게이트 발화점)인가"와 엔딩 컨텍스트 조립을 검증한다.
/// nav seam의 ExpeditionFlowResolverFastTests와 동형.
/// </summary>
[Category("FastUnit")]
public sealed class NarrativeMomentResolverFastTests
{
    [Test]
    public void ShouldCommitExtract_TrueOnlyForSelectedSiteExtractNode()
    {
        var session = CreateSession();
        var site = session.SelectedCampaignSiteId;
        Assert.That(string.IsNullOrEmpty(site), Is.False, "bind 후 캠페인 사이트가 선택돼 있다.");

        Assert.That(NarrativeMomentResolver.ShouldCommitExtract(session, $"{site}:extract"), Is.True,
            "선택 사이트의 extract 노드 → 엔딩 moment 발화 대상.");
        Assert.That(NarrativeMomentResolver.ShouldCommitExtract(session, $"{site}:skirmish_a"), Is.False,
            "일반 전투 노드는 extract가 아니다.");
        Assert.That(NarrativeMomentResolver.ShouldCommitExtract(session, "other_site:extract"), Is.False,
            "다른 사이트의 extract는 현재 선택 사이트가 아니다.");
        Assert.That(NarrativeMomentResolver.ShouldCommitExtract(session, ""), Is.False);
        Assert.That(NarrativeMomentResolver.ShouldCommitExtract(session, null), Is.False);
    }

    [Test]
    public void BuildExtractContext_CarriesSelectedChapterAndSite()
    {
        var session = CreateSession();

        var ctx = NarrativeMomentResolver.BuildExtractContext(session);

        Assert.That(ctx.SiteId, Is.EqualTo(session.SelectedCampaignSiteId), "엔딩 컨텍스트 site = 세션 선택 site.");
        Assert.That(ctx.ChapterId, Is.EqualTo(session.SelectedCampaignChapterId), "엔딩 컨텍스트 chapter = 세션 선택 chapter.");
    }

    private static GameSessionState CreateSession()
    {
        var lookup = EditorFreeCombatContentFixture.CreateRunLoopLookup();
        var session = GameSessionTestFactory.Create(lookup);
        session.BindProfile(new SaveProfile
        {
            ProfileId = "narrative_moment_resolver_test",
            Heroes = new List<HeroInstanceRecord>
            {
                new()
                {
                    HeroId = "hero-1",
                    Name = "hero-1",
                    ArchetypeId = "vanguard_archetype",
                    RaceId = "human",
                    ClassId = "vanguard",
                    PositiveTraitId = "trait_positive",
                    NegativeTraitId = "trait_negative",
                    EquippedItemIds = new List<string>(),
                },
            },
        });
        session.SetCurrentScene(SceneNames.Town);
        return session;
    }
}
