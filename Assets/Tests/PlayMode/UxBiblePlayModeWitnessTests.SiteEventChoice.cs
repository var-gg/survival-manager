using System.Collections;
using System.Linq;
using NUnit.Framework;
using SM.Unity;
using SM.Unity.UI;
using SM.Unity.UI.Atlas;
using UnityEngine.UIElements;
using UnityEngine.TestTools;

namespace SM.Tests.PlayMode;

public sealed partial class UxBiblePlayModeWitnessTests
{
    [UnityTest]
    public IEnumerator SiteEventChoice_IsReachableAndAppliesSelectedOutcome()
    {
        yield return EnterOfflineTownFromBoot();

        var root = GameSessionRoot.Instance!;
        root.SessionState.BeginNewExpedition();
        yield return RunSiteEventChoiceWitness(root);
    }

    private IEnumerator RunSiteEventChoiceWitness(GameSessionRoot root)
    {
        var session = root.SessionState;
        for (var step = 0; step < 8; step++)
        {
            var current = session.GetCurrentExpeditionNode();
            Assert.That(current, Is.Not.Null, "The authored route should retain a current node.");
            if (!string.IsNullOrWhiteSpace(current!.EventId))
            {
                break;
            }

            Assert.That(
                session.ResolveSelectedExpeditionNode(),
                Is.True,
                $"Witness setup could not advance past '{current.GraphNodeId}'.");
        }

        var eventNode = session.GetCurrentExpeditionNode();
        Assert.That(eventNode, Is.Not.Null);
        Assert.That(eventNode!.EventId, Is.EqualTo("site_event_collapsed_aid_station"));
        root.SceneFlow.GoToAtlas();

        yield return WaitForScene(SceneNames.Atlas);
        yield return WaitForComponent<AtlasScreenController>();
        var atlas = RequireAny<AtlasScreenController>("Atlas controller should exist for the site event witness.");
        var atlasHost = RequirePanelHost("AtlasRuntimePanelHost");
        atlas.EnsureRuntimeControls();
        atlas.ContinueToExpedition();

        yield return WaitForVisible(atlasHost.Root, "SiteEventChoiceOverlay");
        yield return WaitFrames(2);
        var presentation = session.PendingSiteEvent;
        Assert.That(presentation, Is.Not.Null, "The Atlas continuation path must leave the event pending for the human choice.");
        Assert.That(presentation!.Choices.Count, Is.EqualTo(3));

        var overlay = Require<VisualElement>(atlasHost.Root, "SiteEventChoiceOverlay");
        AssertNoRedText(overlay, "Site Event Choice");
        foreach (var choice in presentation.Choices)
        {
            var title = Require<Label>(overlay, $"SiteEventChoiceTitle_{choice.Id}");
            Assert.That(title.text, Is.Not.Empty, $"{choice.Id} title");
            Assert.That(title.text, Does.Not.StartWith("content."), $"{choice.Id} title");
            Assert.That(title.text, Does.Not.StartWith("ui."), $"{choice.Id} title");
            Assert.That(Require<VisualElement>(overlay, $"SiteEventChoiceIcon_{choice.Id}"), Is.Not.Null);
            Assert.That(
                Require<VisualElement>(overlay, $"SiteEventOutcomePreviews_{choice.Id}").childCount,
                Is.EqualTo(choice.OutcomePreviews.Count),
                $"{choice.Id} must render every outcome preview.");
        }

        Assert.That(
            overlay.Query<Label>(className: "site-event-choice__icon-missing-label").ToList().Count,
            Is.EqualTo(3),
            "Every declared-missing icon must show the honest localized fallback.");
        Assert.That(
            overlay.Query<Button>(className: "site-event-choice__card").ToList()
                .Any(card => card.ClassListContains("site-event-choice__card--selected")),
            Is.True,
            "The first legal choice should expose the selected gold state.");

        var echoBefore = session.Profile.Currencies.Echo;
        yield return Capture("site_event_choice");
        ClickButton(overlay, "SiteEventChoice_burn_it");

        yield return WaitForScene(SceneNames.Reward);
        Assert.That(session.PendingSiteEvent, Is.Null, "Choosing a card must clear the pending event.");
        Assert.That(session.Profile.Currencies.Echo, Is.EqualTo(echoBefore + 6), "The selected burn_it outcome must apply through the event applier.");
        Assert.That(session.HasPendingRewardSettlement, Is.True);
        _packet?.RecordPass("Site Event Choice opened from Atlas, rendered all authored choices, and applied the selected outcome.");
    }
}
