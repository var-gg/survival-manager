using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using SM.Atlas.Model;
using SM.Atlas.Services;
using SM.Core.Content;
using SM.Meta.Model;
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
    public void SpineStages_ExposeSiteNodeIndexAndStageKindFromExpeditionProgress()
    {
        var session = CreateBoundSession();
        session.BeginNewExpedition();
        var region = AtlasGrayboxDataFactory.CreateRegion();

        var presenter = new AtlasScreenPresenter(region, session.EnsureAtlasSession(region));
        var state = presenter.Build();

        Assert.That(state.SpineStages.Count, Is.EqualTo(5));
        // SiteNodeIndex는 0-based 5-node SiteTrack (skirmish/skirmish/elite/boss/extract).
        Assert.That(
            state.SpineStages.Select(s => s.SiteNodeIndex).ToArray(),
            Is.EqualTo(new[] { 0, 1, 2, 3, 4 }));
        // StageKind id는 task-atlas-sitetrack-absorption-v1 acceptance #1/#3의 baseline.
        Assert.That(
            state.SpineStages.Select(s => s.StageKind).ToArray(),
            Is.EqualTo(new[] { "skirmish", "skirmish", "elite", "boss", "extract" }));
        // Korean label은 readability 유지.
        Assert.That(
            state.SpineStages.Select(s => s.Label).ToArray(),
            Is.EqualTo(new[] { "진입", "교전", "단서", "보스", "추출" }));
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
    public void BossLockedNode_DoesNotBypassHandoff_FallsBackToCurrentStage()
    {
        var session = CreateBoundSession();
        session.BeginNewExpedition();
        var region = AtlasGrayboxDataFactory.CreateRegion();

        // hex_1_0은 Boss kind / SiteNodeIndex=3. current=0 / siteSpineIndex=0이라 locked 상태.
        // 사용자가 그것을 click해도 direct battle bypass가 일어나면 안 된다 (acceptance #5).
        session.SelectAtlasNode(region, "hex_1_0");
        var applied = session.TryApplyAtlasSelectionToExpedition(region);

        // Locked boss로 직접 handoff는 금지. fallback으로 1단계 unlocked candidate로 진행한다.
        Assert.That(applied, Is.True, "current stage candidate가 unlocked면 handoff는 그쪽으로 진행돼야 한다.");
        Assert.That(session.SelectedExpeditionNodeIndex, Is.EqualTo(0),
            "Boss locked 상태에서는 SelectedExpeditionNodeIndex가 current(=0)에 머문다.");
        Assert.That(session.GetSelectedExpeditionNode()?.Index, Is.EqualTo(0));
        Assert.That(session.RunBattlePayload?.SiteNodeIndex ?? 0, Is.LessThan(3),
            "RunBattlePayload SiteNodeIndex가 boss(3) 직진하지 않는다.");
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

    [Test]
    public void AtlasSelectionHandoff_BranchingBriefingEntry_SelectsDirectMappedEdge()
    {
        var session = CreateBoundSession(CreateCanonicalIndexBranchGraph());
        session.BeginNewExpedition();
        var region = AtlasGrayboxDataFactory.CreateRegion();

        Assert.That(session.CurrentExpeditionNodeIndex, Is.EqualTo(5));
        Assert.That(session.GetCurrentExpeditionNode()?.NodeKind, Is.EqualTo(SiteNodeKindValue.Briefing));
        Assert.That(session.EnsureAtlasSession(region).SiteSpineIndex, Is.EqualTo(0));

        session.SelectAtlasNode(region, "hex_m2_1");
        var result = session.ApplyAtlasSelectionToExpedition(region);

        Assert.That(result.Succeeded, Is.True, result.Diagnostic);
        Assert.That(result.Failure, Is.EqualTo(GameSessionState.AtlasExpeditionHandoffFailure.None));
        Assert.That(session.SelectedExpeditionNodeIndex, Is.EqualTo(0));
    }

    [Test]
    public void AtlasSelectionHandoff_DiagnosisDistinguishesCandidateMappingAndGraphRefusal()
    {
        var baseline = AtlasGrayboxDataFactory.CreateRegion();
        var stageZeroHexes = baseline.StageCandidates
            .Where(candidate => candidate.SiteStageIndex == 1)
            .Select(candidate => candidate.HexId)
            .ToHashSet();

        var candidateUnavailable = ApplyFailure(baseline with
        {
            StageCandidates = Array.Empty<AtlasStageCandidate>(),
        });
        var regionNodeMissing = ApplyFailure(baseline with
        {
            Nodes = baseline.Nodes.Where(node => !stageZeroHexes.Contains(node.NodeId)).ToArray(),
        });
        var regionNodeUnmapped = ApplyFailure(baseline with
        {
            Nodes = baseline.Nodes
                .Select(node => stageZeroHexes.Contains(node.NodeId) ? node with { SiteNodeIndex = -1 } : node)
                .ToArray(),
        });
        var selectionRefused = ApplyFailure(baseline with
        {
            Nodes = baseline.Nodes
                .Select(node => stageZeroHexes.Contains(node.NodeId) ? node with { SiteNodeIndex = 3 } : node)
                .ToArray(),
        });

        Assert.That(
            candidateUnavailable.Failure,
            Is.EqualTo(GameSessionState.AtlasExpeditionHandoffFailure.CandidateUnavailable));
        Assert.That(
            regionNodeMissing.Failure,
            Is.EqualTo(GameSessionState.AtlasExpeditionHandoffFailure.RegionNodeMissing));
        Assert.That(
            regionNodeUnmapped.Failure,
            Is.EqualTo(GameSessionState.AtlasExpeditionHandoffFailure.RegionNodeUnmapped));
        Assert.That(
            selectionRefused.Failure,
            Is.EqualTo(GameSessionState.AtlasExpeditionHandoffFailure.ExpeditionSelectionRefused));
        Assert.That(candidateUnavailable.Diagnostic, Does.Contain("action="));
        Assert.That(regionNodeMissing.Diagnostic, Does.Contain("candidateHexId="));
        Assert.That(regionNodeUnmapped.Diagnostic, Does.Contain("candidateSiteNodeIndex=-1"));
        Assert.That(selectionRefused.Diagnostic, Does.Contain("currentNext="));
    }

    private static GameSessionState.AtlasExpeditionHandoffResult ApplyFailure(AtlasRegionDefinition region)
    {
        var session = CreateBoundSession();
        session.BeginNewExpedition();
        return session.ApplyAtlasSelectionToExpedition(region);
    }

    private static GameSessionState CreateBoundSession(SiteGraphTemplate? graph = null)
    {
        var session = GameSessionTestFactory.Create(EditorFreeCombatContentFixture.CreateRunLoopLookup(graph));
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

    private static SiteGraphTemplate CreateCanonicalIndexBranchGraph()
    {
        return new SiteGraphTemplate(
            "site_graph_alpha_gate_atlas_test",
            "site_alpha_gate",
            new[]
            {
                GraphNode("safe_1", 1, SiteNodeKindValue.Skirmish, "site_alpha_gate:skirmish_a", next: new[] { "safe_2" }),
                GraphNode("safe_2", 2, SiteNodeKindValue.Skirmish, "site_alpha_gate:skirmish_b", next: new[] { "event" }),
                GraphNode("elite", 4, SiteNodeKindValue.Elite, "site_alpha_gate:elite", next: new[] { "boss" }),
                GraphNode("boss", 6, SiteNodeKindValue.Boss, "site_alpha_gate:boss", next: new[] { "extract" }),
                GraphNode("extract", 7, SiteNodeKindValue.Extract, rewardSourceId: "reward_source_extract"),
                GraphNode("entry", 0, SiteNodeKindValue.Briefing, next: new[] { "safe_1", "risk" }),
                GraphNode("risk", 1, SiteNodeKindValue.Skirmish, "site_alpha_gate:skirmish_b", "risk", new[] { "safe_2" }),
                GraphNode("event", 3, SiteNodeKindValue.Event, eventId: "site_event_test", next: new[] { "elite" }),
            });
    }

    private static SiteGraphNodeTemplate GraphNode(
        string nodeId,
        int rank,
        SiteNodeKindValue kind,
        string encounterId = "",
        string laneTag = "safe",
        IReadOnlyList<string>? next = null,
        string eventId = "",
        string rewardSourceId = "")
    {
        return new SiteGraphNodeTemplate(
            nodeId,
            rank,
            kind,
            laneTag,
            encounterId,
            eventId,
            next ?? Array.Empty<string>(),
            0,
            rewardSourceId);
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
