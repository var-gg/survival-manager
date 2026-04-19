using System.Collections.Generic;
using NUnit.Framework;
using SM.Combat.Model;
using SM.Persistence.Abstractions.Models;
using SM.Tests.EditMode.Fakes;
using SM.Unity;
using SM.Unity.Sandbox;
using UnityEngine;

namespace SM.Tests.EditMode;

[Category("BatchOnly")]
public sealed class TownBuildHotPathBatchOnlyTests
{
    [Test]
    public void PrepareQuickBattleSmoke_UsesConfiguredAllySlots_WhenPresent()
    {
        var session = CreateBoundSession();
        var config = ScriptableObject.CreateInstance<CombatSandboxConfig>();
        try
        {
            config.AllySlots = new List<CombatSandboxAllySlot>
            {
                new() { HeroId = "hero-2", Anchor = DeploymentAnchorId.BackBottom },
                new() { HeroId = "hero-1", Anchor = DeploymentAnchorId.FrontTop },
            };

            session.PrepareQuickBattleSmoke(config);

            Assert.That(session.GetAssignedHeroId(DeploymentAnchorId.FrontTop), Is.EqualTo("hero-1"));
            Assert.That(session.GetAssignedHeroId(DeploymentAnchorId.BackBottom), Is.EqualTo("hero-2"));
            Assert.That(session.BattleDeployHeroIds, Is.EqualTo(new[] { "hero-1", "hero-2" }));
            Assert.That(session.GetAssignedHeroId(DeploymentAnchorId.FrontCenter), Is.Null);
            Assert.That(session.GetAssignedHeroId(DeploymentAnchorId.BackCenter), Is.Null);
        }
        finally
        {
            Object.DestroyImmediate(config);
        }
    }

    private static GameSessionState CreateBoundSession()
    {
        var session = GameSessionTestFactory.Create(EditorFreeCombatContentFixture.CreateTownBuildLookup());
        session.BindProfile(new SaveProfile
        {
            Heroes = new List<HeroInstanceRecord>
            {
                new()
                {
                    HeroId = "hero-1", Name = "Test Hero 1",
                    ArchetypeId = "test_archetype_a", RaceId = "test_race", ClassId = "test_class",
                    EquippedItemIds = new List<string>(),
                },
                new()
                {
                    HeroId = "hero-2", Name = "Test Hero 2",
                    ArchetypeId = "test_archetype_b", RaceId = "test_race", ClassId = "test_class",
                    EquippedItemIds = new List<string>(),
                },
            },
        });
        session.SetCurrentScene(SceneNames.Town);
        return session;
    }
}
