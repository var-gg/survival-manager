using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace SM.Unity.UI.Town.Preview;

public interface ICompendiumActions
{
    void SelectTab(CompendiumTab tab);
    void SelectEntry(string id);
    void PlaySelectedPreview();
}

public sealed class CompendiumView
{
    private readonly VisualElement _root;
    private readonly VisualElement _tabRow;
    private readonly VisualElement _entryList;
    private readonly VisualElement _detailIcon;
    private readonly VisualElement _detailMetrics;
    private readonly CompendiumVfxPreviewView _vfxPreview;
    private readonly Label _title;
    private readonly Label _subtitle;
    private readonly Label _detailTitle;
    private readonly Label _detailSubtitle;
    private readonly Label _detailDescription;
    private readonly Label _detailHook;
    private readonly Button _closeButton;
    private readonly Button _vfxReplayButton;

    private ICompendiumActions? _actions;

    public CompendiumView(VisualElement root)
    {
        if (root == null) throw new ArgumentNullException(nameof(root));
        _root = root.Q<VisualElement>("CompendiumRoot")
            ?? throw new ArgumentException("CompendiumRoot 못 찾음");
        _tabRow = Require<VisualElement>(root, "CompendiumTabRow");
        _entryList = Require<VisualElement>(root, "CompendiumEntryList");
        _detailIcon = Require<VisualElement>(root, "CompendiumDetailIcon");
        _detailMetrics = Require<VisualElement>(root, "CompendiumDetailMetrics");
        _vfxPreview = new CompendiumVfxPreviewView(root);
        _title = Require<Label>(root, "CompendiumTitle");
        _subtitle = Require<Label>(root, "CompendiumSubtitle");
        _detailTitle = Require<Label>(root, "CompendiumDetailTitle");
        _detailSubtitle = Require<Label>(root, "CompendiumDetailSubtitle");
        _detailDescription = Require<Label>(root, "CompendiumDetailDescription");
        _detailHook = Require<Label>(root, "CompendiumDetailHook");
        _closeButton = Require<Button>(root, "CompendiumCloseButton");
        _vfxReplayButton = Require<Button>(root, "CompendiumVfxReplayButton");
    }

    public void Bind(ICompendiumActions actions)
    {
        _actions = actions ?? throw new ArgumentNullException(nameof(actions));
        _vfxReplayButton.clicked -= HandleVfxReplayClicked;
        _vfxReplayButton.clicked += HandleVfxReplayClicked;
    }

    public void BindClose(Action close)
    {
        if (close == null) return;
        _closeButton.clicked += close;
    }

    public void Open()
    {
        _root.style.display = DisplayStyle.Flex;
        _root.RemoveFromClassList("sm-modal-anim--enter");
        var wrapper = _root.parent?.parent;
        if (wrapper != null) wrapper.style.display = DisplayStyle.Flex;
    }

    public void Close()
    {
        _root.style.display = DisplayStyle.None;
        _root.AddToClassList("sm-modal-anim--enter");
        var wrapper = _root.parent?.parent;
        if (wrapper != null) wrapper.style.display = DisplayStyle.None;
    }

    public void Render(CompendiumViewState state)
    {
        if (state == null) throw new ArgumentNullException(nameof(state));

        _title.text = state.Title;
        _subtitle.text = state.Subtitle;
        _closeButton.text = state.CloseLabel;
        RenderTabs(state.Tabs);
        RenderEntries(state);
        RenderDetail(state.Detail);
    }

    private void RenderTabs(IReadOnlyList<CompendiumTabViewState> tabs)
    {
        _tabRow.Clear();
        foreach (var tab in tabs)
        {
            var button = new Button { text = tab.Label };
            button.AddToClassList("cmp-tab");
            if (tab.IsSelected)
            {
                button.AddToClassList("cmp-tab--selected");
            }

            var captured = tab.Tab;
            button.clicked += () => _actions?.SelectTab(captured);
            _tabRow.Add(button);
        }
    }

    private void RenderEntries(CompendiumViewState state)
    {
        _entryList.Clear();
        switch (state.ActiveTab)
        {
            case CompendiumTab.Status:
                foreach (var entry in state.Statuses)
                {
                    _entryList.Add(BuildEntry(entry.Id, entry.Name, entry.GroupLabel, entry.VfxCueId, entry.IsSelected, null));
                }
                break;
            case CompendiumTab.Synergy:
                foreach (var entry in state.Synergies)
                {
                    _entryList.Add(BuildEntry(entry.Id, entry.Name, entry.CountedTagLabel, entry.TierSummary, entry.IsSelected, null));
                }
                break;
            case CompendiumTab.Characters:
                foreach (var entry in state.Characters)
                {
                    _entryList.Add(BuildEntry(entry.Id, entry.DisplayName, entry.ClassLabel, entry.UnlockLabel, entry.IsSelected, null, !entry.IsUnlocked));
                }
                break;
            default:
                foreach (var entry in state.Skills)
                {
                    _entryList.Add(BuildEntry(entry.Id, entry.Name, entry.SlotLabel, entry.VfxHookId, entry.IsSelected, entry.IconSprite));
                }
                break;
        }
    }

    private VisualElement BuildEntry(
        string id,
        string title,
        string meta,
        string hook,
        bool isSelected,
        Texture2D? icon,
        bool isLocked = false)
    {
        var button = new Button { text = string.Empty };
        button.AddToClassList("cmp-entry");
        if (isSelected)
        {
            button.AddToClassList("cmp-entry--selected");
        }
        if (isLocked)
        {
            button.AddToClassList("cmp-entry--locked");
        }

        var iconBox = new VisualElement();
        iconBox.AddToClassList("cmp-entry__icon");
        if (icon != null)
        {
            iconBox.style.backgroundImage = new StyleBackground(icon);
        }
        button.Add(iconBox);

        var copy = new VisualElement();
        copy.AddToClassList("cmp-entry__copy");
        var titleLabel = new Label(title);
        titleLabel.AddToClassList("cmp-entry__title");
        copy.Add(titleLabel);
        var metaLabel = new Label(meta);
        metaLabel.AddToClassList("cmp-entry__meta");
        copy.Add(metaLabel);
        var hookLabel = new Label(hook);
        hookLabel.AddToClassList("cmp-entry__hook");
        copy.Add(hookLabel);
        button.Add(copy);

        var captured = id;
        button.clicked += () => _actions?.SelectEntry(captured);
        return button;
    }

    private void RenderDetail(CompendiumDetailViewState detail)
    {
        _detailIcon.style.backgroundImage = detail.IconSprite != null
            ? new StyleBackground(detail.IconSprite)
            : new StyleBackground();
        _detailTitle.text = detail.Title;
        _detailSubtitle.text = detail.Subtitle;
        _detailDescription.text = detail.Description;
        _detailHook.text = detail.HookLabel;
        _vfxReplayButton.text = detail.VfxPreview.ReplayLabel;
        _vfxReplayButton.style.display = detail.VfxPreview.CanPreview ? DisplayStyle.Flex : DisplayStyle.None;
        _vfxPreview.Render(detail.VfxPreview);

        _detailMetrics.Clear();
        foreach (var metric in detail.Metrics)
        {
            var row = new VisualElement();
            row.AddToClassList("cmp-metric");
            var label = new Label(metric.Label);
            label.AddToClassList("cmp-metric__label");
            row.Add(label);
            var value = new Label(metric.Value);
            value.AddToClassList("cmp-metric__value");
            row.Add(value);
            _detailMetrics.Add(row);
        }
    }

    private void HandleVfxReplayClicked()
    {
        _actions?.PlaySelectedPreview();
    }

    private static T Require<T>(VisualElement root, string name) where T : VisualElement
    {
        return root.Q<T>(name) ?? throw new InvalidOperationException($"Missing UITK element '{name}'.");
    }
}
