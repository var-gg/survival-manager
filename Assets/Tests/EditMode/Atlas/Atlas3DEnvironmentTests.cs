using System.Linq;
using NUnit.Framework;
using SM.Atlas.Model;
using SM.Atlas.Services;
using SM.Unity.Atlas;
using SM.Unity.UI.Atlas;
using UnityEngine;
using UnityEngine.UIElements;

namespace SM.Tests.EditMode.Atlas;

[Category("BatchOnly")]
public sealed class Atlas3DEnvironmentTests
{
    [Test]
    public void HexWorldMapper_MapsAxialCoordinatesDeterministically()
    {
        var origin = AtlasHexWorldMapper.ToWorld(new AtlasHexCoordinate(0, 0));
        var east = AtlasHexWorldMapper.ToWorld(new AtlasHexCoordinate(1, 0));
        var southEast = AtlasHexWorldMapper.ToWorld(new AtlasHexCoordinate(0, 1));

        Assert.That(origin, Is.EqualTo(new Vector3(0f, AtlasHexWorldMapper.SurfaceY, 0f)));
        Assert.That(east.x, Is.EqualTo(AtlasHexWorldMapper.HorizontalStep).Within(0.0001f));
        Assert.That(southEast.x, Is.EqualTo(AtlasHexWorldMapper.HorizontalStep * 0.5f).Within(0.0001f));
        Assert.That(southEast.z, Is.EqualTo(AtlasHexWorldMapper.VerticalStep).Within(0.0001f));
    }

    [Test]
    public void HexDiscMesh_FacesUpAndAcceptsTopDownRaycasts()
    {
        var go = new GameObject("AtlasHexDiscProbe");
        try
        {
            var collider = go.AddComponent<MeshCollider>();
            collider.sharedMesh = AtlasHexWorldMapper.CreateHexDiscMesh();
            Physics.SyncTransforms();

            Assert.That(collider.sharedMesh.normals, Is.All.Matches<Vector3>(normal => normal.y > 0.9f));
            Assert.That(Physics.Raycast(new Vector3(0f, 2f, 0f), Vector3.down, out var hit, 5f), Is.True);
            Assert.That(hit.collider, Is.EqualTo(collider));
        }
        finally
        {
            Object.DestroyImmediate(go);
        }
    }

    [Test]
    public void LeylinePlan_CoversAllNineteenHexesInStableOrder()
    {
        var region = AtlasGrayboxDataFactory.CreateRegion();
        var plan = AtlasHexLeylineRenderer.BuildPlan(region);

        Assert.That(plan.Count, Is.EqualTo(19));
        Assert.That(plan.Select(entry => entry.NodeId).Distinct().Count(), Is.EqualTo(19));
        Assert.That(plan.First().Hex.R, Is.EqualTo(-2));
        Assert.That(plan.Last().Hex.R, Is.EqualTo(2));
    }

    [Test]
    public void AuraPlan_ProducesOverlapEntriesFromV2ViewState()
    {
        var state = new AtlasScreenPresenter(AtlasGrayboxDataFactory.CreateRegion()).Build();
        var plan = AtlasSigilAuraVFXController.BuildAuraPlan(state);

        Assert.That(plan.Count, Is.GreaterThan(0));
        Assert.That(plan.Count(entry => entry.IsOverlap), Is.GreaterThan(0));
        Assert.That(AtlasSigilAuraVFXController.ResolveCategoryColor(AtlasModifierCategory.RewardBias).a, Is.GreaterThan(0f));
        Assert.That(AtlasSigilAuraVFXController.ResolveOverlapColor().r, Is.GreaterThan(0.9f));
    }

    [Test]
    public void ScreenView_UsesTransparentWorldSpacerInsteadOfInline2DBoard()
    {
        var root = CreateAtlasRoot();
        var view = new AtlasScreenView(root);
        view.Render(new AtlasScreenPresenter(AtlasGrayboxDataFactory.CreateRegion()).Build());

        var boardPane = root.Q<VisualElement>("atlas-board-pane");
        var board = root.Q<VisualElement>("atlas-board");

        Assert.That(boardPane.pickingMode, Is.EqualTo(PickingMode.Position));
        Assert.That(board.pickingMode, Is.EqualTo(PickingMode.Ignore));
        Assert.That(board.style.display.value, Is.EqualTo(DisplayStyle.None));
        Assert.That(board.childCount, Is.EqualTo(0));
        Assert.That(root.Query<Button>(className: "atlas-hex-hit-zone").ToList().Count, Is.EqualTo(19));
    }

    [Test]
    public void StandeePlan_PlacesFourSmallP09StyleStandees()
    {
        var region = AtlasGrayboxDataFactory.CreateRegion();
        var plan = AtlasCharacterStandeePresenter.BuildPlan(region);

        Assert.That(plan.Count, Is.EqualTo(4));
        Assert.That(plan.Select(entry => entry.NodeId), Has.Member("hex_m2_1"));
        Assert.That(plan, Is.All.Matches<AtlasCharacterStandeePresenter.StandeeEntry>(entry =>
            entry.Scale is >= 0.70f and <= 0.85f));
    }

    private static VisualElement CreateAtlasRoot()
    {
        var root = new VisualElement { name = "atlas-root" };
        var content = new VisualElement { name = "atlas-content" };
        var boardPane = new VisualElement { name = "atlas-board-pane" };
        boardPane.Add(new VisualElement { name = "atlas-board" });
        boardPane.Add(new VisualElement { name = "atlas-layer-overlay" });
        boardPane.Add(new VisualElement { name = "atlas-stage-candidate-overlay" });
        content.Add(new VisualElement { name = "atlas-sigil-pool" });
        content.Add(boardPane);
        content.Add(new VisualElement { name = "atlas-stage-candidate-list" });
        root.Add(content);
        root.Add(new VisualElement { name = "atlas-spine-progress-strip" });
        root.Add(new Label { name = "atlas-region-title" });
        root.Add(new Label { name = "atlas-placement-summary" });
        root.Add(new Label { name = "atlas-preview-title" });
        root.Add(new Label { name = "atlas-preview-judgement" });
        root.Add(new Label { name = "atlas-preview-enemy" });
        root.Add(new Label { name = "atlas-preview-modifiers" });
        root.Add(new Label { name = "atlas-preview-reward" });
        root.Add(new Label { name = "atlas-preview-recommendations" });
        root.Add(new Label { name = "atlas-boundary-note" });
        root.Add(new Label { name = "atlas-debug-hash" });
        return root;
    }
}
