using System.Collections.Generic;
using NUnit.Framework;
using SM.Core;
using SM.Persistence.Abstractions.Models;
using SM.Tests.EditMode.Fakes;
using SM.Unity;

namespace SM.Tests.EditMode;

/// <summary>
/// 전투 시드 결정성 회귀 잠금 — BattleContextHash/BattleSeed는 run 인스턴스(per-run GUID인 RunId)가 아니라
/// 콘텐츠 좌표(chapter/site/node/encounter) + 분대(squad) + atlas 상태로만 결정돼야 한다. 같은 콘텐츠·분대면
/// fresh run마다 RunId가 달라도 같은 seed가 나와야 replay·결정적 회귀가 성립한다.
///
/// 수정 전: ComputeContextHash/AtlasContextHasher가 run.RunId(GUID)를 hash 입력에 섞어 fresh run마다
/// 같은 셋업도 승패가 변동했다(엔지니어링 감사 최우선 #2). 이 테스트는 두 fresh run의 RunId가 서로 다른데도
/// BattleContextHash·BattleSeed가 동일함을 단언해 그 회귀를 잠근다.
///
/// 엔지니어링 감사(analysis-engineering-audit-headless-divergence-fallback-deadcode-2026-06) 최우선 #2.
/// </summary>
[Category("FastUnit")]
public sealed class SeedDeterminismFastTests
{
    [Test]
    public void FreshRuns_SameContentAndSquad_ProduceSameBattleSeed_DespiteDifferentRunId()
    {
        var lookup = EditorFreeCombatContentFixture.CreateRunLoopLookup();

        var first = StampFirstBattleContext(lookup);
        var second = StampFirstBattleContext(lookup);

        Assert.That(first.Hash, Is.Not.Empty,
            "Battle loadout compile 후 BattleContextHash가 stamping돼야 한다(비공허 보증).");
        Assert.That(second.RunId, Is.Not.EqualTo(first.RunId),
            "fresh run마다 RunId(GUID)는 서로 달라야 한다 — 결정성이 RunId 동일성 덕분이 아님을 보장.");
        Assert.That(second.Hash, Is.EqualTo(first.Hash),
            "같은 콘텐츠·분대면 fresh run이어도 BattleContextHash가 동일 — seed가 run 인스턴스가 아닌 콘텐츠로 결정.");
        Assert.That(second.Seed, Is.EqualTo(first.Seed),
            "같은 콘텐츠·분대면 fresh run이어도 BattleSeed가 동일 — replay·회귀 결정성.");
    }

    private static (string RunId, string Hash, int Seed) StampFirstBattleContext(ICombatContentLookup lookup)
    {
        var session = GameSessionTestFactory.Create(lookup);
        session.BindProfile(new SaveProfile
        {
            ProfileId = "seed_determinism",
            Heroes = new List<HeroInstanceRecord>
            {
                CreateHero("hero-1", "vanguard"),
                CreateHero("hero-2", "ranger"),
                CreateHero("hero-3", "duelist"),
                CreateHero("hero-4", "mystic"),
            },
        });
        session.SetCurrentScene(SceneNames.Town);

        session.BeginNewExpedition();
        Assert.That(session.PrepareSelectedBattleNodeHandoff(), Is.True,
            "첫 노드는 전투 노드 — handoff 준비 성공.");
        // BattleContextHash/BattleSeed는 BuildBattleLoadoutSnapshot(TryBuildBattleContext + SetBattleContext)에서 stamping된다.
        session.BuildBattleLoadoutSnapshot();

        var run = session.ActiveRun;
        Assert.That(run, Is.Not.Null, "원정 시작 후 ActiveRun이 있어야 한다.");
        return (run!.RunId, run.Overlay.BattleContextHash, run.Overlay.BattleSeed);
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
