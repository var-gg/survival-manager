using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using SM.Combat.Model;
using SM.Editor.SeedData;
using SM.Editor.Validation;
using SM.Persistence.Abstractions.Models;
using SM.Unity;

namespace SM.Tests.EditMode;

/// <summary>
/// class@3 / race@4 doctrine tier의 실 콘텐츠 headless witness.
/// 8인 프로필을 BindProfile한 뒤 6개 anchor 중 앞 4개를 명시 배치하고, 프로덕션과 같은
/// TryBuildSelectedBattleState -> TryResolveSelectedBattleNodeViaSimulation 경로를 탄다.
/// 내부 상수나 손조립 BattleState를 사용하지 않고 public TeamRuleSet, CombatBeat,
/// BattleUnitReadModel만 관찰한다.
/// </summary>
[Category("BatchOnly")]
public sealed class DoctrineTierWitnessTests
{
    private const string ChapterId = "chapter_ashen_gate";
    private const string SiteId = "site_ashen_gate";
    private const string EncounterId = "site_ashen_gate_skirmish_1";
    private const int FixedBattleSeed = 72_689_751;

    [SetUp]
    public void SetUp()
    {
        SampleSeedGenerator.RequireCanonicalSampleContentReady(nameof(DoctrineTierWitnessTests));
    }

    [Test]
    public void VanguardThree_RealSessionBattle_GrantsBulwarkAndAppliesGuardedAtBattleStart()
    {
        var session = CreateSession(
            "doctrine-class-three-witness",
            new[]
            {
                "warden", "guardian", "bulwark", "slayer",
                "scout", "hexer", "raider", "marksman",
            },
            new[] { "warden", "guardian", "bulwark", "slayer" });
        var state = BuildFirstBattleState(session);

        Assert.That(state.Allies.Count(unit => unit.Definition.ClassId == "vanguard"), Is.EqualTo(3),
            "전제: 4개 실 배치 anchor 중 정확히 vanguard 3명이어야 한다");
        Assert.That(
            state.Allies.GroupBy(unit => unit.Definition.RaceId).Select(group => group.Count()).OrderBy(count => count),
            Is.EqualTo(new[] { 1, 1, 2 }),
            "race@4가 섞이지 않은 1/1/2 race 분포여야 class@3의 단독 witness가 된다");
        Assert.That(state.TeamRuleSet.Has(TeamSide.Ally, TeamRuleSet.BulwarkRuleId), Is.True,
            "실 LoadoutCompiler/BattleFactory 경로가 vanguard@3를 rule.bulwark로 부여해야 한다");

        Assert.That(
            session.TryResolveSelectedBattleNodeViaSimulation(out var result, out var error),
            Is.True,
            error);
        Assert.That(result.StepCount, Is.GreaterThan(0),
            "headless witness가 auto-resolve가 아니라 실 BattleSimulator tick을 돌아야 한다");

        var beats = result.Beats ?? Array.Empty<CombatBeat>();
        var bulwarkBeats = beats
            .Where(beat => beat.Side == TeamSide.Ally
                           && beat.Type == CombatBeatType.BattleStartEffect
                           && beat.Tag == TeamRuleSet.BulwarkRuleId)
            .ToList();
        Assert.That(bulwarkBeats.Count(), Is.EqualTo(3),
            "rule.bulwark가 개전 시 vanguard 3명 각각에 ally-side BattleStartEffect를 남겨야 한다");

        var finalVanguards = result.FinalUnits
            .Where(unit => unit.Side == TeamSide.Ally && unit.ClassId == "vanguard")
            .ToList();
        Assert.That(finalVanguards.Count(), Is.EqualTo(3));
        Assert.That(finalVanguards.Count(unit => unit.IsAlive), Is.GreaterThan(0),
            "class@3 witness requires at least one living vanguard at the final read surface");
        Assert.That(
            finalVanguards
                .Where(unit => unit.IsAlive)
                .All(unit => (unit.StatusIds ?? Array.Empty<string>()).Contains("guarded", StringComparer.Ordinal)),
            Is.True,
            "public final read model에서 생존한 vanguard는 guarded를 유지해야 class@3 효과가 전투 중 보존됨을 증명한다");
    }

    [Test]
    public void UndeadFour_RealSessionBattle_GrantsDeathTollAndFiresOnBattlefieldDeath()
    {
        var session = CreateSession(
            "doctrine-race-four-witness",
            new[]
            {
                "guardian", "reaver", "marksman", "hexer",
                "warden", "raider", "scout", "priest",
            },
            new[] { "guardian", "reaver", "marksman", "hexer" });
        var state = BuildFirstBattleState(session);

        Assert.That(state.Allies.Count(unit => unit.Definition.RaceId == "undead"), Is.EqualTo(4),
            "전제: 4개 실 배치 anchor 전원이 undead여야 한다");
        Assert.That(
            state.Allies.GroupBy(unit => unit.Definition.ClassId).Max(group => group.Count()),
            Is.LessThanOrEqualTo(2),
            "class@3가 섞이지 않아야 race@4의 단독 witness가 된다");
        Assert.That(state.TeamRuleSet.Has(TeamSide.Ally, TeamRuleSet.DeathTollRuleId), Is.True,
            "실 LoadoutCompiler/BattleFactory 경로가 undead@4를 rule.deathtoll로 부여해야 한다");

        Assert.That(
            session.TryResolveSelectedBattleNodeViaSimulation(out var result, out var error),
            Is.True,
            error);
        Assert.That(result.StepCount, Is.GreaterThan(0),
            "race@4 witness가 실 BattleSimulator tick을 돌아야 한다");
        Assert.That(result.Events.Any(battleEvent => battleEvent.EventKind == BattleEventKind.Kill), Is.True,
            "Death Toll의 battlefield-death hook을 평가할 실 사망 사건이 있어야 한다");

        var beats = result.Beats ?? Array.Empty<CombatBeat>();
        Assert.That(
            beats.Any(beat => beat.Side == TeamSide.Ally
                              && beat.Type == CombatBeatType.OnKillEffect
                              && beat.Tag == TeamRuleSet.DeathTollRuleId),
            Is.True,
            "rule.deathtoll이 실 사망 hook에서 ally-side OnKillEffect로 발화해야 한다");
        Assert.That(
            result.FinalUnits.Any(unit => unit.Side == TeamSide.Ally
                                          && unit.RaceId == "undead"
                                          && (unit.StatusIds ?? Array.Empty<string>()).Contains(
                                              "team-rule.deathtoll",
                                              StringComparer.Ordinal)),
            Is.True,
            "public final read model의 undead 유닛에 Death Toll 영구 marker 상태가 남아야 실제 효과 적용을 증명한다");
    }

    [Test]
    public void TwoOfEachControl_RealSessionBattle_DoesNotGrantDoctrineRules()
    {
        var session = CreateSession(
            "doctrine-two-of-each-control",
            new[]
            {
                "warden", "guardian", "slayer", "raider",
                "scout", "marksman", "hexer", "priest",
            },
            new[] { "warden", "guardian", "slayer", "raider" });
        var state = BuildFirstBattleState(session);

        Assert.That(
            state.Allies.GroupBy(unit => unit.Definition.ClassId).Select(group => group.Count()).OrderBy(count => count),
            Is.EqualTo(new[] { 2, 2 }),
            "control 배치는 vanguard/duelist가 각각 2명이어야 한다");
        Assert.That(
            state.Allies.GroupBy(unit => unit.Definition.RaceId).Select(group => group.Count()).OrderBy(count => count),
            Is.EqualTo(new[] { 1, 1, 2 }),
            "control 배치는 어느 race도 4명에 도달하지 않는 1/1/2 분포여야 한다");

        var doctrineRuleIds = DoctrineRuleIds();
        Assert.That(doctrineRuleIds.Count(), Is.EqualTo(7));
        Assert.That(
            doctrineRuleIds.All(ruleId => !state.TeamRuleSet.Has(TeamSide.Ally, ruleId)),
            Is.True,
            "2-of-each control은 class@3/race@4 doctrine TeamRuleSet을 하나도 부여받지 않아야 한다");

        Assert.That(
            session.TryResolveSelectedBattleNodeViaSimulation(out var result, out var error),
            Is.True,
            error);
        Assert.That(result.StepCount, Is.GreaterThan(0));
        Assert.That(
            (result.Beats ?? Array.Empty<CombatBeat>())
                .Any(beat => beat.Side == TeamSide.Ally
                             && doctrineRuleIds.Contains(beat.Tag, StringComparer.Ordinal)),
            Is.False,
            "lower-tier SynergyActivated beat가 있어도 doctrine rule id의 효과 beat는 없어야 threshold control이 된다");
    }

    private static GameSessionState CreateSession(
        string profileId,
        IReadOnlyList<string> rosterArchetypeIds,
        IReadOnlyList<string> deployedArchetypeIds)
    {
        Assert.That(rosterArchetypeIds.Count(), Is.EqualTo(8), "BindProfile baseline과 같은 8인 roster를 시드한다");
        Assert.That(deployedArchetypeIds.Count(), Is.EqualTo(4), "배치 상한에 맞춰 앞 4개 battle anchor만 명시 배치한다");
        Assert.That(rosterArchetypeIds.Distinct(StringComparer.Ordinal).Count(), Is.EqualTo(8));
        Assert.That(deployedArchetypeIds.All(rosterArchetypeIds.Contains), Is.True,
            "배치 대상은 모두 시드한 8인 roster에 속해야 한다");

        var lookup = new RuntimeCombatContentLookup();
        var profile = new SaveProfile
        {
            ProfileId = profileId,
            CampaignProgress = new CampaignProgressRecord
            {
                SelectedChapterId = ChapterId,
                SelectedSiteId = SiteId,
            },
        };
        foreach (var archetypeId in rosterArchetypeIds)
        {
            Assert.That(lookup.TryGetArchetype(archetypeId, out var archetype), Is.True,
                $"실 canonical archetype '{archetypeId}'가 doctrine witness roster에 필요하다");
            profile.Heroes.Add(new HeroInstanceRecord
            {
                HeroId = $"hero.{archetypeId}",
                Name = archetypeId,
                ArchetypeId = archetypeId,
                RaceId = archetype.Race?.Id ?? string.Empty,
                ClassId = archetype.Class?.Id ?? string.Empty,
            });
        }

        var session = new GameSessionState(lookup);
        session.BindProfile(profile);
        session.SetCurrentScene(SceneNames.Town);
        Assert.That(session.Profile.Heroes.Count(), Is.EqualTo(8));
        Assert.That(session.ExpeditionSquadHeroIds.Count(), Is.EqualTo(8));

        var anchors = session.DeploymentAnchors.ToList();
        Assert.That(anchors.Count(), Is.EqualTo(6), "authored battle anchor는 6칸이어야 한다");
        foreach (var anchor in anchors)
        {
            Assert.That(session.AssignHeroToAnchor(anchor, null), Is.True);
        }

        var heroIdByArchetype = session.Profile.Heroes.ToDictionary(
            hero => hero.ArchetypeId,
            hero => hero.HeroId,
            StringComparer.Ordinal);
        for (var index = 0; index < deployedArchetypeIds.Count(); index++)
        {
            Assert.That(
                session.AssignHeroToAnchor(anchors[index], heroIdByArchetype[deployedArchetypeIds[index]]),
                Is.True,
                $"anchor {anchors[index]}에 {deployedArchetypeIds[index]} 배치가 성공해야 한다");
        }

        Assert.That(session.BattleDeployHeroIds.Count(), Is.EqualTo(4));
        Assert.That(
            session.BattleDeployHeroIds,
            Is.EqualTo(deployedArchetypeIds.Select(archetypeId => heroIdByArchetype[archetypeId]).ToList()),
            "명시한 4인 stack/control 배치가 앞 4개 anchor order에 그대로 고정돼야 한다");
        return session;
    }

    private static BattleState BuildFirstBattleState(GameSessionState session)
    {
        session.BeginNewExpedition();
        while (CampaignDefaultRouteNavigator.TryAdvanceIntermediateNonBattle(session))
        {
        }

        Assert.That(
            session.TryBuildSelectedBattleState(
                out var state,
                out var encounter,
                out var allySnapshot,
                out var error),
            Is.True,
            error);
        Assert.That(allySnapshot.Allies.Count(), Is.EqualTo(4));
        Assert.That(encounter.Context.ChapterId, Is.EqualTo(ChapterId));
        Assert.That(encounter.Context.SiteId, Is.EqualTo(SiteId));
        Assert.That(encounter.Context.EncounterId, Is.EqualTo(EncounterId));
        Assert.That(encounter.Context.BattleSeed, Is.EqualTo(FixedBattleSeed),
            "authored first-node 좌표에서 계산한 fixed seed로 모든 doctrine 시나리오를 비교한다");
        TestContext.WriteLine(
            $"[DoctrineWitness] encounter={encounter.Context.EncounterId} seed={encounter.Context.BattleSeed} " +
            $"allies={string.Join(",", state.Allies.Select(unit => $"{unit.Definition.RaceId}/{unit.Definition.ClassId}"))}");
        return state;
    }

    private static string[] DoctrineRuleIds()
    {
        return new[]
        {
            TeamRuleSet.PhalanxRuleId,
            TeamRuleSet.BloodrushRuleId,
            TeamRuleSet.DeathTollRuleId,
            TeamRuleSet.BulwarkRuleId,
            TeamRuleSet.ExecuteRuleId,
            TeamRuleSet.KillzoneRuleId,
            TeamRuleSet.ResonanceRuleId,
        };
    }
}
