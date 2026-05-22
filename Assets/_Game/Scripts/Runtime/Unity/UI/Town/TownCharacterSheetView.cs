using System;
using UnityEngine.UIElements;

namespace SM.Unity.UI.Town;

public sealed class TownCharacterSheetView
{
    private static readonly string[] RoleFamilyClasses =
    {
        "tcs-role--vanguard",
        "tcs-role--duelist",
        "tcs-role--ranger",
        "tcs-role--mystic",
        "tcs-role--beastkin",
    };

    private readonly VisualElement _modalRoot;
    private readonly Label _heroNameLabel;
    private readonly Label _heroMetaLabel;
    private readonly Label _roleLabel;
    private readonly Label _overviewTitleLabel;
    private readonly Label _overviewBodyLabel;
    private readonly Label _loadoutTitleLabel;
    private readonly Label _loadoutBodyLabel;
    private readonly Label _passivesTitleLabel;
    private readonly Label _passivesBodyLabel;
    private readonly Label _synergyTitleLabel;
    private readonly Label _synergyBodyLabel;
    private readonly Label _progressionTitleLabel;
    private readonly Label _progressionBodyLabel;
    private readonly Button _closeButton;

    public TownCharacterSheetView(VisualElement root)
    {
        if (root == null) throw new ArgumentNullException(nameof(root));
        _modalRoot = Require<VisualElement>(root, "TownCharacterSheetRoot");
        _heroNameLabel = Require<Label>(root, "TcsHeroNameLabel");
        _heroMetaLabel = Require<Label>(root, "TcsHeroMetaLabel");
        _roleLabel = Require<Label>(root, "TcsRoleLabel");
        _overviewTitleLabel = Require<Label>(root, "TcsOverviewTitle");
        _overviewBodyLabel = Require<Label>(root, "TcsOverviewBody");
        _loadoutTitleLabel = Require<Label>(root, "TcsLoadoutTitle");
        _loadoutBodyLabel = Require<Label>(root, "TcsLoadoutBody");
        _passivesTitleLabel = Require<Label>(root, "TcsPassivesTitle");
        _passivesBodyLabel = Require<Label>(root, "TcsPassivesBody");
        _synergyTitleLabel = Require<Label>(root, "TcsSynergyTitle");
        _synergyBodyLabel = Require<Label>(root, "TcsSynergyBody");
        _progressionTitleLabel = Require<Label>(root, "TcsProgressionTitle");
        _progressionBodyLabel = Require<Label>(root, "TcsProgressionBody");
        _closeButton = Require<Button>(root, "TownCharacterSheetCloseButton");
    }

    public void Bind(TownCharacterSheetPresenter presenter)
    {
        if (presenter == null) throw new ArgumentNullException(nameof(presenter));
        _closeButton.clicked += presenter.Close;
    }

    public void Open()
    {
        _modalRoot.style.display = DisplayStyle.Flex;
        _modalRoot.RemoveFromClassList("sm-modal-anim--enter");
        var wrapper = FindModalOverlay();
        if (wrapper != null)
        {
            wrapper.style.display = DisplayStyle.Flex;
        }
    }

    public void Close()
    {
        _modalRoot.style.display = DisplayStyle.None;
        _modalRoot.AddToClassList("sm-modal-anim--enter");
        var wrapper = FindModalOverlay();
        if (wrapper != null)
        {
            wrapper.style.display = DisplayStyle.None;
        }
    }

    public void Render(TownCharacterSheetViewState state)
    {
        if (state == null) throw new ArgumentNullException(nameof(state));
        _heroNameLabel.text = state.DisplayName;
        _heroMetaLabel.text = state.ArchetypeLabel;
        _roleLabel.text = state.RoleLabel;
        ApplyRoleFamilyClass(state.FamilyKey);

        RenderPanel(state.Overview, _overviewTitleLabel, _overviewBodyLabel);
        RenderPanel(state.Loadout, _loadoutTitleLabel, _loadoutBodyLabel);
        RenderPanel(state.Passives, _passivesTitleLabel, _passivesBodyLabel);
        RenderPanel(state.Synergy, _synergyTitleLabel, _synergyBodyLabel);
        RenderPanel(state.Progression, _progressionTitleLabel, _progressionBodyLabel);
    }

    private void ApplyRoleFamilyClass(string familyKey)
    {
        foreach (var roleClass in RoleFamilyClasses)
        {
            _roleLabel.RemoveFromClassList(roleClass);
        }

        if (!string.IsNullOrWhiteSpace(familyKey))
        {
            _roleLabel.AddToClassList($"tcs-role--{familyKey}");
        }
    }

    private static void RenderPanel(TownCharacterSheetPanelViewState panel, Label title, Label body)
    {
        title.text = panel.Title;
        body.text = panel.Body;
        body.tooltip = panel.Body;
    }

    private VisualElement? FindModalOverlay()
    {
        for (var current = _modalRoot.parent; current != null; current = current.parent)
        {
            if (current.ClassListContains("town-hub__modal-overlay"))
            {
                return current;
            }
        }

        return _modalRoot.parent?.parent ?? _modalRoot.parent;
    }

    private static T Require<T>(VisualElement root, string name) where T : VisualElement
    {
        return root.Q<T>(name) ?? throw new InvalidOperationException($"Missing UITK element '{name}'.");
    }
}
