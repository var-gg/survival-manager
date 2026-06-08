using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using SM.Combat.Model;
using SM.Meta;
using SM.Persistence.Abstractions.Models;
using SM.Persistence.Json;
using SM.Tests.EditMode.Fakes;
using SM.Unity;

namespace SM.Tests.EditMode;

/// <summary>
/// 출전 편성 게이트 계약 — 자동배치를 fallback으로 강등하고, 유저가 명시 편성한 출전 배치를
/// 1급 결정으로 존중·영속한다. 게이트 surface(SortieConfirm overlay)는 PlayMode witness 영역이고,
/// 본 계약은 session-level FastUnit으로 결정론 검증한다.
/// </summary>
[Category("FastUnit")]
public sealed class SortieGateContractFastTests
{
    [Test]
    public void Fallback_AutoFills_WhenDeploymentUntouched()
    {
        var session = CreateBoundSession();
        session.EnsureBattleDeployReady();

        Assert.That(session.IsDeploymentUserAuthored, Is.False,
            "신규 세션은 유저가 출전 편성을 만진 적이 없다.");
        Assert.That(session.BattleDeployHeroIds.Count, Is.EqualTo(session.ExpeditionSquadHeroIds.Count),
            "유저 미편성 + 빈 배치는 fallback 자동배치가 원정대를 채운다.");
    }

    [Test]
    public void UserAuthored_DeploymentNotOverwritten_OnEnsure()
    {
        var session = CreateBoundSession();
        var heroId = session.ExpeditionSquadHeroIds[0];

        Assert.That(session.AssignHeroToAnchor(DeploymentAnchorId.BackBottom, heroId), Is.True);
        Assert.That(session.IsDeploymentUserAuthored, Is.True,
            "유저가 anchor를 명시 배치하면 편성 의도가 기록된다.");

        // 자동배치가 유저 배치를 침범하지 않는다 — 반복 Ensure 후에도 유지.
        session.EnsureBattleDeployReady();
        session.EnsureBattleDeployReady();

        Assert.That(session.DeploymentAssignments[DeploymentAnchorId.BackBottom], Is.EqualTo(heroId),
            "유저가 BackBottom에 배치한 hero가 반복 Ensure 후에도 유지된다.");
    }

    [Test]
    public void Fallback_Suppressed_AfterUserEmptiesDeployment()
    {
        var session = CreateBoundSession();
        var heroId = session.ExpeditionSquadHeroIds[0];

        Assert.That(session.AssignHeroToAnchor(DeploymentAnchorId.FrontCenter, heroId), Is.True);
        // 유저가 의도적으로 모든 anchor를 비운다(비우기도 1급 편성 의도).
        foreach (var anchor in session.DeploymentAnchors)
        {
            session.AssignHeroToAnchor(anchor, null);
        }

        Assert.That(session.IsDeploymentUserAuthored, Is.True,
            "비우기도 유저 의도다 — 편성 플래그 유지.");
        Assert.That(session.BattleDeployHeroIds.Count, Is.EqualTo(0));

        session.EnsureBattleDeployReady();

        Assert.That(session.BattleDeployHeroIds.Count, Is.EqualTo(0),
            "유저가 비운 배치는 자동배치가 다시 채우지 않는다.");
    }

    [Test]
    public void Reconcile_ClearsStaleHero_WhenRemovedFromSquad()
    {
        var session = CreateBoundSession();
        var heroId = session.ExpeditionSquadHeroIds[0];

        Assert.That(session.AssignHeroToAnchor(DeploymentAnchorId.FrontTop, heroId), Is.True);
        Assert.That(session.BattleDeployHeroIds, Does.Contain(heroId));

        // squad에서 제거 → reconcile(플래그 무관 항상)이 유령 배치를 정리한다.
        session.ToggleExpeditionHero(heroId);
        session.EnsureBattleDeployReady();

        Assert.That(session.BattleDeployHeroIds, Does.Not.Contain(heroId),
            "squad에서 빠진 hero는 배치에서 정리된다(편성 플래그와 무관하게 항상).");
    }

    [Test]
    public void DeploymentUserAuthored_RoundTrips_ThroughProfileReload()
    {
        var session = CreateBoundSession();
        var heroId = session.ExpeditionSquadHeroIds[0];
        Assert.That(session.AssignHeroToAnchor(DeploymentAnchorId.BackTop, heroId), Is.True);

        var reloaded = GameSessionTestFactory.Create(EditorFreeCombatContentFixture.CreateRunLoopLookup());
        reloaded.BindProfile(CloneProfile(session.Profile));
        reloaded.SetCurrentScene(SceneNames.Town);

        Assert.That(reloaded.IsDeploymentUserAuthored, Is.True,
            "유저 편성 플래그가 save/reload를 버틴다.");
        Assert.That(reloaded.DeploymentAssignments[DeploymentAnchorId.BackTop], Is.EqualTo(heroId),
            "유저가 BackTop에 배치한 hero가 reload 후 복원된다(자동배치로 덮이지 않음).");
    }

    private static GameSessionState CreateBoundSession()
    {
        var session = GameSessionTestFactory.Create(EditorFreeCombatContentFixture.CreateRunLoopLookup());
        session.BindProfile(new SaveProfile
        {
            ProfileId = "sortie_gate_test",
            Heroes = new List<HeroInstanceRecord>
            {
                CreateHero("hero-1", "Hero One", "vanguard"),
                CreateHero("hero-2", "Hero Two", "ranger"),
                CreateHero("hero-3", "Hero Three", "duelist"),
                CreateHero("hero-4", "Hero Four", "mystic"),
            },
        });
        session.SetCurrentScene(SceneNames.Town);
        return session;
    }

    private static HeroInstanceRecord CreateHero(string heroId, string name, string classId)
    {
        return new HeroInstanceRecord
        {
            HeroId = heroId,
            Name = name,
            ArchetypeId = $"{classId}_archetype",
            RaceId = "human",
            ClassId = classId,
            PositiveTraitId = "trait_positive",
            NegativeTraitId = "trait_negative",
            EquippedItemIds = new List<string>(),
        };
    }

    private static SaveProfile CloneProfile(SaveProfile profile)
    {
        var root = Path.Combine(Path.GetTempPath(), "SM_SortieGateContractFastTests", Guid.NewGuid().ToString("N"));
        try
        {
            var repository = new JsonSaveRepository(root);
            repository.Save(profile);
            return repository.LoadOrCreate(profile.ProfileId);
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, true);
            }
        }
    }
}
