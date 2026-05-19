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

    [Test]
    public void AtlasModifierPayload_IsReadableAfterSelectionHandoff()
    {
        var session = CreateBoundSession();
        session.BeginNewExpedition();
        var region = AtlasGrayboxDataFactory.CreateRegion();

        session.SelectAtlasSigil(region, "sigil_beast_spoils");
        session.PlaceSelectedAtlasSigil(region, "hex_m1_m1");
        session.SelectAtlasNode(region, "hex_m2_1");

        Assert.That(session.TryApplyAtlasSelectionToExpedition(region), Is.True);
        var payload = session.AtlasExpeditionModifierPayload;

        Assert.That(payload, Is.Not.Null);
        Assert.That(payload!.RegionId, Is.EqualTo(region.RegionId));
        Assert.That(payload.AtlasNodeId, Is.EqualTo("hex_m2_1"));
        Assert.That(payload.SiteNodeIndex, Is.EqualTo(0));
        Assert.That(payload.ExpeditionNodeId, Is.EqualTo(session.GetSelectedExpeditionNode()?.Id));
        Assert.That(payload.StageCandidatePathHash, Is.Not.Empty);
        Assert.That(payload.NodeOverlayHash, Is.Not.Empty);
        Assert.That(payload.BattleContextHash, Is.Not.Empty);
        Assert.That(payload.RewardBiasPercent, Is.GreaterThan(0));
        Assert.That(payload.HasAnyModifier, Is.True);
    }

    [Test]
    public void RunBattlePayload_IsPopulatedAfterSelectionHandoff()
    {
        var session = CreateBoundSession();
        session.BeginNewExpedition();
        var region = AtlasGrayboxDataFactory.CreateRegion();

        session.SelectAtlasSigil(region, "sigil_beast_spoils");
        session.PlaceSelectedAtlasSigil(region, "hex_m1_m1");
        session.SelectAtlasNode(region, "hex_m2_1");

        Assert.That(session.TryApplyAtlasSelectionToExpedition(region), Is.True);

        var modifier = session.AtlasExpeditionModifierPayload;
        var payload = session.RunBattlePayload;
        var atlas = session.AtlasSession;

        Assert.That(payload, Is.Not.Null, "Atlas selection handoff 직후 RunBattlePayload가 세팅돼야 한다.");
        Assert.That(modifier, Is.Not.Null);
        Assert.That(atlas, Is.Not.Null);

        // Atlas Identity와 modifier payload 핵심 필드가 RunBattlePayload로 일관 운반된다.
        Assert.That(payload!.RunId, Is.EqualTo(atlas!.Identity.RunId));
        Assert.That(payload.ChapterId, Is.EqualTo(atlas.Identity.ChapterId));
        Assert.That(payload.SiteId, Is.EqualTo(atlas.Identity.SiteId));
        Assert.That(payload.EncounterId, Is.EqualTo(atlas.Identity.EncounterId));
        Assert.That(payload.SquadSnapshotId, Is.EqualTo(atlas.Identity.SquadSnapshotId));
        Assert.That(payload.SiteNodeIndex, Is.EqualTo(modifier!.SiteNodeIndex));
        Assert.That(payload.ExpeditionNodeId, Is.EqualTo(modifier.ExpeditionNodeId));
        Assert.That(payload.StageCandidatePathHash, Is.EqualTo(modifier.StageCandidatePathHash));
        Assert.That(payload.NodeOverlayHash, Is.EqualTo(modifier.NodeOverlayHash));
        Assert.That(payload.BattleContextHash, Is.EqualTo(modifier.BattleContextHash));
        Assert.That(payload.RewardBiasPercent, Is.EqualTo(modifier.RewardBiasPercent));
        Assert.That(payload.IsValid, Is.True);
    }

    [Test]
    public void RunBattlePayload_RebuildIsDeterministicForSameSelection()
    {
        var session = CreateBoundSession();
        session.BeginNewExpedition();
        var region = AtlasGrayboxDataFactory.CreateRegion();

        session.SelectAtlasSigil(region, "sigil_beast_spoils");
        session.PlaceSelectedAtlasSigil(region, "hex_m1_m1");
        session.SelectAtlasNode(region, "hex_m2_1");

        Assert.That(session.TryApplyAtlasSelectionToExpedition(region), Is.True);
        var first = session.RunBattlePayload;

        // 같은 session(=같은 RunId/SquadSnapshotId)에서 advance 없이 다시 handoff —
        // 같은 sigil 배치와 candidate를 재계산하므로 hash 3종이 안정 재현돼야 한다.
        Assert.That(session.TryApplyAtlasSelectionToExpedition(region), Is.True);
        var second = session.RunBattlePayload;

        Assert.That(first, Is.Not.Null);
        Assert.That(second, Is.Not.Null);

        Assert.That(first!.RunId, Is.EqualTo(second!.RunId));
        Assert.That(first.SquadSnapshotId, Is.EqualTo(second.SquadSnapshotId));
        Assert.That(first.BattleContextHash, Is.EqualTo(second.BattleContextHash));
        Assert.That(first.NodeOverlayHash, Is.EqualTo(second.NodeOverlayHash));
        Assert.That(first.StageCandidatePathHash, Is.EqualTo(second.StageCandidatePathHash));
        Assert.That(first.EncounterId, Is.EqualTo(second.EncounterId));
        Assert.That(first.SiteNodeIndex, Is.EqualTo(second.SiteNodeIndex));
        Assert.That(first.RewardBiasPercent, Is.EqualTo(second.RewardBiasPercent));
    }

    [Test]
    public void RunBattlePayload_StageAdvanceDoesNotDriftRunIdentity()
    {
        var session = CreateBoundSession();
        session.BeginNewExpedition();
        var region = AtlasGrayboxDataFactory.CreateRegion();

        session.SelectAtlasSigil(region, "sigil_beast_spoils");
        session.PlaceSelectedAtlasSigil(region, "hex_m1_m1");
        session.SelectAtlasNode(region, "hex_m2_1");

        Assert.That(session.TryApplyAtlasSelectionToExpedition(region), Is.True);
        var stage0 = session.RunBattlePayload;
        Assert.That(stage0, Is.Not.Null, "stage 0 payload는 첫 handoff 직후 세팅돼야 한다.");

        // Stage advance — current node가 0 → 1로 진행되면 다음 node 선택이 가능해진다.
        Assert.That(session.ResolveSelectedExpeditionNode(), Is.True);
        Assert.That(session.CurrentExpeditionNodeIndex, Is.EqualTo(1));

        session.SelectAtlasNode(region, "hex_m1_2");
        Assert.That(session.TryApplyAtlasSelectionToExpedition(region), Is.True);
        var stage1 = session.RunBattlePayload;
        Assert.That(stage1, Is.Not.Null, "stage 1 payload는 advance 이후 재handoff에서 세팅돼야 한다.");

        // Run-level identity는 stage advance에서 drift하지 않는다.
        Assert.That(stage1!.RunId, Is.EqualTo(stage0!.RunId));
        Assert.That(stage1.ChapterId, Is.EqualTo(stage0.ChapterId));
        Assert.That(stage1.SiteId, Is.EqualTo(stage0.SiteId));
        Assert.That(stage1.SquadSnapshotId, Is.EqualTo(stage0.SquadSnapshotId));

        // Stage-dependent field는 변경된다 — drift는 expected field로 국한.
        Assert.That(stage1.SiteNodeIndex, Is.EqualTo(1));
        Assert.That(stage1.SiteNodeIndex, Is.Not.EqualTo(stage0.SiteNodeIndex));
        Assert.That(stage1.BattleContextHash, Is.Not.EqualTo(stage0.BattleContextHash));
    }

    [Test]
    public void AtlasSelectionHandoff_KeepsAuthoredEncounterIdentityWhenModifiersApply()
    {
        var session = CreateBoundSession();
        session.BeginNewExpedition();
        var region = AtlasGrayboxDataFactory.CreateRegion();

        session.SelectAtlasSigil(region, "sigil_beast_spoils");
        session.PlaceSelectedAtlasSigil(region, "hex_m1_m1");
        session.SelectAtlasNode(region, "hex_m2_1");

        Assert.That(session.TryApplyAtlasSelectionToExpedition(region), Is.True);
        var selectedEncounterId = session.GetSelectedExpeditionNode()?.Id;
        var payload = session.AtlasExpeditionModifierPayload;

        Assert.That(selectedEncounterId, Is.Not.Empty);
        Assert.That(payload, Is.Not.Null);
        Assert.That(payload!.ExpeditionNodeId, Is.EqualTo(selectedEncounterId));
        Assert.That(payload.HasAnyModifier, Is.True);

        Assert.That(session.PrepareSelectedBattleNodeHandoff(), Is.True);
        Assert.That(session.ActiveRun?.Overlay.EncounterId, Is.EqualTo(selectedEncounterId));
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
