using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using SM.Atlas.Model;
using SM.Atlas.Services;
using SM.Persistence.Abstractions.Models;
using SM.Tests.EditMode.Fakes;
using SM.Unity;
using SM.Unity.UI.Atlas;

namespace SM.Tests.EditMode;

[Category("FastUnit")]
public sealed class GameSessionAtlasFlowFastTests
{
    [Test]
    public void BeginNewExpedition_SeedsAtlasIdentityFromRunAndCampaign()
    {
        var session = CreateBoundSession();
        session.BeginNewExpedition();
        var region = AtlasGrayboxDataFactory.CreateRegion();

        var atlas = session.EnsureAtlasSession(region);

        Assert.That(atlas.Identity.RunId, Is.EqualTo(session.ActiveRun!.RunId));
        Assert.That(atlas.Identity.ChapterId, Is.EqualTo(session.SelectedCampaignChapterId));
        Assert.That(atlas.Identity.SiteId, Is.EqualTo(session.SelectedCampaignSiteId));
        Assert.That(atlas.TraversalMode, Is.EqualTo(AtlasTraversalMode.StoryFirstClear));
        Assert.That(atlas.Placements.Count, Is.EqualTo(2));
    }

    [Test]
    public void AtlasInteractions_AreOwnedByGameSessionState()
    {
        var session = CreateBoundSession();
        session.BeginNewExpedition();
        var region = AtlasGrayboxDataFactory.CreateRegion();

        session.SelectAtlasSigil(region, "sigil_ruin_scholar");
        session.PlaceSelectedAtlasSigil(region, "hex_1_m2");
        session.SelectAtlasNode(region, "hex_m2_1");
        session.SelectAtlasNode(region, "hex_m1_2");

        var atlas = session.AtlasSession;
        Assert.That(atlas, Is.Not.Null);
        Assert.That(atlas!.SelectedSigilId, Is.EqualTo("sigil_ruin_scholar"));
        Assert.That(atlas.SelectedNodeId, Is.EqualTo("hex_m1_2"));
        Assert.That(atlas.Placements.Any(placement => placement.SigilId == "sigil_ruin_scholar"), Is.True);
        Assert.That(atlas.StageCandidatePath, Is.EqualTo(new[] { "hex_m2_1", "hex_m1_2" }));

        var presenter = new AtlasScreenPresenter(region, atlas);
        var viewState = presenter.Build();
        Assert.That(viewState.SigilPool.Single(item => item.SigilId == "sigil_ruin_scholar").IsPlaced, Is.True);
        Assert.That(viewState.Tiles.Single(tile => tile.NodeId == "hex_m1_2").IsSelected, Is.True);
    }

    [Test]
    public void CampaignSiteChange_InvalidatesAtlasSession()
    {
        var session = CreateBoundSession();
        var region = AtlasGrayboxDataFactory.CreateRegion();
        var first = session.EnsureAtlasSession(region);

        Assert.That(session.TryCycleCampaignSite(1), Is.True);

        Assert.That(session.AtlasSession, Is.Null);
        var second = session.EnsureAtlasSession(region);
        Assert.That(second.Identity.SiteId, Is.Not.EqualTo(first.Identity.SiteId));
        Assert.That(second.Identity.SiteId, Is.EqualTo(session.SelectedCampaignSiteId));
    }

    [Test]
    public void AtlasSelectionHandoff_SelectsCurrentExpeditionNode()
    {
        var session = CreateBoundSession();
        session.BeginNewExpedition();
        var region = AtlasGrayboxDataFactory.CreateRegion();

        session.SelectAtlasNode(region, "hex_m2_1");
        session.SelectAtlasNode(region, "hex_m1_2");

        Assert.That(session.TryApplyAtlasSelectionToExpedition(region), Is.True);
        Assert.That(session.SelectedExpeditionNodeIndex, Is.EqualTo(0));
        Assert.That(session.GetSelectedExpeditionNode()?.Index, Is.EqualTo(session.CurrentExpeditionNodeIndex));
    }

    [Test]
    public void AtlasSelectionHandoff_UsesCurrentProgressAfterCurrentNodeAdvances()
    {
        var session = CreateBoundSession();
        session.BeginNewExpedition();
        var region = AtlasGrayboxDataFactory.CreateRegion();
        session.SelectAtlasNode(region, "hex_m2_1");
        session.SelectAtlasNode(region, "hex_m1_2");
        Assert.That(session.TryApplyAtlasSelectionToExpedition(region), Is.True);
        Assert.That(session.ResolveSelectedExpeditionNode(), Is.True);

        Assert.That(session.CurrentExpeditionNodeIndex, Is.EqualTo(1));
        Assert.That(session.TryApplyAtlasSelectionToExpedition(region), Is.True);
        Assert.That(session.SelectedExpeditionNodeIndex, Is.EqualTo(1));
    }

    private static GameSessionState CreateBoundSession()
    {
        var session = GameSessionTestFactory.Create(EditorFreeCombatContentFixture.CreateRunLoopLookup());
        session.BindProfile(new SaveProfile
        {
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
}
