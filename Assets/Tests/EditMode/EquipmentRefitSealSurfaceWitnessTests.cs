using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using SM.Core.Content;
using SM.Unity.UI.Town.Preview;
using UnityEditor;
using UnityEngine.UIElements;

namespace SM.Tests.EditMode;

[Category("BatchOnly")]
public sealed class EquipmentRefitSealSurfaceWitnessTests
{
    private const string UxmlPath =
        "Assets/_Game/UI/Panels/EquipmentRefit/EquipmentRefit.uxml";

    [Test]
    public void ProductionView_RoutesOperationLockRequestAndConfirmationControls()
    {
        var root = CloneProductionTree();
        var view = new EquipmentRefitView(root);
        var actions = new RecordingActions();
        view.Bind(actions);
        var state = BuildState();

        view.Render(state);

        var sealButton = Require<Button>(root, "RefitOperationSealButton");
        Assert.That(sealButton.enabledSelf, Is.True);
        Invoke(sealButton);
        Assert.That(actions.LastOperation, Is.EqualTo(CraftOperationKindValue.Seal));

        state = state with
        {
            SelectedOperation = CraftOperationKindValue.Seal,
            Affixes = new[]
            {
                state.Affixes[0] with
                {
                    LockToggleEnabled = true,
                    LockLabel = "Lock",
                },
                state.Affixes[1] with
                {
                    LockToggleEnabled = true,
                    LockLabel = "Lock",
                },
            },
        };
        view.Render(state);

        Invoke(Require<Button>(root, "AffixLock_affix_a"));
        Assert.That(actions.LastToggledAffixId, Is.EqualTo("affix_a"));

        Invoke(Require<Button>(root, "CraftRequestButton"));
        Assert.That(actions.CraftRequestCount, Is.EqualTo(1));

        view.Render(state with { ConfirmationVisible = true });
        var confirmation = Require<VisualElement>(root, "CraftConfirmation");
        Assert.That(confirmation.style.display.value, Is.EqualTo(DisplayStyle.Flex));
        Invoke(Require<Button>(root, "CraftConfirmButton"));
        Assert.That(actions.CraftConfirmCount, Is.EqualTo(1));
    }

    [Test]
    public void ProductionView_KeepsDisallowedSealVisibleDisabledWithReason()
    {
        var root = CloneProductionTree();
        var view = new EquipmentRefitView(root);
        view.Bind(new RecordingActions());
        var state = BuildState() with
        {
            SealOperationSelectable = false,
            SealOperationReason = "Seal unavailable: This item does not allow Seal.",
        };

        view.Render(state);

        var sealButton = Require<Button>(root, "RefitOperationSealButton");
        var reason = Require<Label>(root, "SealUnavailableReason");
        Assert.That(sealButton, Is.Not.Null);
        Assert.That(sealButton.enabledSelf, Is.False);
        Assert.That(reason.style.display.value, Is.EqualTo(DisplayStyle.Flex));
        Assert.That(reason.text, Does.Contain("does not allow Seal"));
    }

    [TestCase("Seal must leave at least one affix unlocked.")]
    [TestCase("Not enough Echo. Seal requires 90 Echo.")]
    public void ProductionView_ShowsSelectedOperationBlockReason(string blockReason)
    {
        var root = CloneProductionTree();
        var view = new EquipmentRefitView(root);
        view.Bind(new RecordingActions());

        view.Render(BuildState() with
        {
            SelectedOperation = CraftOperationKindValue.Seal,
            SelectedOperationCanPurchase = false,
            SelectedOperationStatusMessage = blockReason,
        });

        var status = Require<Label>(root, "CraftStatusLabel");
        var request = Require<Button>(root, "CraftRequestButton");
        Assert.That(status.text, Is.EqualTo(blockReason));
        Assert.That(request.enabledSelf, Is.False);
    }

    private static VisualElement CloneProductionTree()
    {
        var tree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(UxmlPath);
        Assert.That(tree, Is.Not.Null, $"Missing production UXML: {UxmlPath}");
        var root = new VisualElement();
        tree!.CloneTree(root);
        return root;
    }

    private static EquipmentRefitViewState BuildState()
    {
        return new EquipmentRefitViewState(
            SelectedItemName: "Witness Blade",
            SelectedItemSlotLabel: "Weapon",
            SelectedItemRarityKey: "epic",
            SelectedItemFamilyKey: "blade",
            SelectedItemFamilyLabel: "Blade",
            SelectedItemIdentityKey: "baseline",
            SelectedItemIdentityLabel: string.Empty,
            SelectedItemShowsIdentityBadge: false,
            SelectedItemCanRefit: true,
            EquippedHeroLabel: "Unequipped",
            EquippedHeroPortrait: null,
            EchoSprite: null,
            CurrentQualityPercent: 10d,
            NextFloorPercent: 40d,
            RefitCost: 40,
            RefitMaxed: false,
            RefitStatusMessage: "Quality 10.0% → guaranteed floor 40.0%",
            Affixes: new[]
            {
                new EquipmentRefitAffixRowViewState(
                    "affix_a",
                    "prefix",
                    "접두",
                    "utility",
                    "Affix A",
                    new[] { "Attack +1" },
                    "Roll quality 50%",
                    null),
                new EquipmentRefitAffixRowViewState(
                    "affix_b",
                    "suffix",
                    "접미",
                    "utility",
                    "Affix B",
                    new[] { "Armor +1" },
                    "Roll quality 50%",
                    null),
            },
            Pool: new List<EquipmentRefitPoolRowViewState>(),
            SelectedOperation: CraftOperationKindValue.Reforge,
            ReforgeOperationSelectable: true,
            SealOperationSelectable: true,
            SealOperationReason: string.Empty,
            SelectedOperationCanPurchase: true,
            SelectedOperationCost: 40,
            SelectedOperationCostLabel: "40 Echo",
            SelectedOperationStatusMessage: "Service quote 40 Echo",
            ConfirmationVisible: false,
            PanelTitle: "EQUIPMENT REFIT",
            OperationSelectorLabel: "Choose operation",
            ReforgeOperationLabel: "Reforge",
            SealOperationLabel: "Seal",
            CraftActionLabel: "Reforge (-40 Echo)",
            ConfirmationMessage:
                "Spend 40 Echo on Reforge? The previous roll cannot be restored.",
            ConfirmLabel: "Confirm",
            CancelLabel: "Cancel");
    }

    private static void Invoke(Button button)
    {
        Assert.That(button.enabledInHierarchy, Is.True, $"{button.name} must be enabled.");
        using var click = ClickEvent.GetPooled();
        var method = typeof(Clickable).GetMethod(
            "Invoke",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(method, Is.Not.Null);
        method!.Invoke(button.clickable, new object[] { click });
    }

    private static T Require<T>(VisualElement root, string name)
        where T : VisualElement
    {
        return root.Q<T>(name)
               ?? throw new AssertionException($"Missing UITK element '{name}'.");
    }

    private sealed class RecordingActions : IEquipmentRefitActions
    {
        internal CraftOperationKindValue LastOperation { get; private set; }
        internal string LastToggledAffixId { get; private set; } = string.Empty;
        internal int CraftRequestCount { get; private set; }
        internal int CraftConfirmCount { get; private set; }

        public void OnPoolItemSelected(string itemInstanceId)
        {
        }

        public void OnOperationSelected(CraftOperationKindValue operation)
        {
            LastOperation = operation;
        }

        public void OnAffixLockToggled(string affixId)
        {
            LastToggledAffixId = affixId;
        }

        public void OnCraftRequested()
        {
            CraftRequestCount++;
        }

        public void OnCraftConfirmed()
        {
            CraftConfirmCount++;
        }

        public void OnCraftCancelled()
        {
        }
    }
}
