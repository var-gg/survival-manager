using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;

namespace SM.Unity.UI.Town.Preview;

public interface ICompendiumActions
{
    void SelectTab(CompendiumTab tab);
    void SelectEntry(string id);
    void PlaySelectedPreview();
    void SetSearchText(string searchText);
    void SetSkillClassFilter(string value);
    void SetSkillSlotFilter(string value);
    void SetSkillVfxFamilyFilter(string value);
}

public sealed class CompendiumView
{
    private static readonly HashSet<string> DetailKeywordSkipLabels = new(StringComparer.OrdinalIgnoreCase)
    {
        "ID",
        "Icon",
        "Prefab",
        "Animation",
        "Cue",
        "아이콘",
        "프리팹",
        "동작",
        "큐",
    };

    private static readonly HashSet<string> DetailKeywordSkipValues = new(StringComparer.OrdinalIgnoreCase)
    {
        "-",
        "0",
        "None",
        "없음",
    };

    private readonly VisualElement _root;
    private readonly VisualElement _tabRow;
    private readonly TextField _searchField;
    private readonly VisualElement _skillFilters;
    private readonly ScrollView _entryScroll;
    private readonly DropdownField _classFilter;
    private readonly DropdownField _slotFilter;
    private readonly DropdownField _vfxFamilyFilter;
    private readonly Label _resultSummary;
    private readonly VisualElement _entryList;
    private readonly VisualElement _detailIcon;
    private readonly VisualElement _detailKeywordRow;
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
    private IReadOnlyList<CompendiumFilterOptionViewState> _classOptions = Array.Empty<CompendiumFilterOptionViewState>();
    private IReadOnlyList<CompendiumFilterOptionViewState> _slotOptions = Array.Empty<CompendiumFilterOptionViewState>();
    private IReadOnlyList<CompendiumFilterOptionViewState> _vfxFamilyOptions = Array.Empty<CompendiumFilterOptionViewState>();

    public CompendiumView(VisualElement root)
    {
        if (root == null) throw new ArgumentNullException(nameof(root));
        _root = root.Q<VisualElement>("CompendiumRoot")
            ?? throw new ArgumentException("CompendiumRoot 못 찾음");
        _tabRow = Require<VisualElement>(root, "CompendiumTabRow");
        _searchField = Require<TextField>(root, "CompendiumSearchField");
        _skillFilters = Require<VisualElement>(root, "CompendiumSkillFilters");
        _entryScroll = Require<ScrollView>(root, "CompendiumEntryScroll");
        _classFilter = Require<DropdownField>(root, "CompendiumClassFilter");
        _slotFilter = Require<DropdownField>(root, "CompendiumSlotFilter");
        _vfxFamilyFilter = Require<DropdownField>(root, "CompendiumVfxFamilyFilter");
        _resultSummary = Require<Label>(root, "CompendiumResultSummary");
        _entryList = Require<VisualElement>(root, "CompendiumEntryList");
        _detailIcon = Require<VisualElement>(root, "CompendiumDetailIcon");
        _detailKeywordRow = Require<VisualElement>(root, "CompendiumDetailKeywordRow");
        _detailMetrics = Require<VisualElement>(root, "CompendiumDetailMetrics");
        _vfxPreview = new CompendiumVfxPreviewView(root);
        _title = Require<Label>(root, "CompendiumTitle");
        _subtitle = Require<Label>(root, "CompendiumSubtitle");
        _detailTitle = Require<Label>(root, "CompendiumDetailTitle");
        _detailSubtitle = Require<Label>(root, "CompendiumDetailSubtitle");
        _detailDescription = Require<Label>(root, "CompendiumDetailDescription");
        _detailHook = Require<Label>(root, "CompendiumDetailHook");
        _detailHook.style.display = DisplayStyle.None;
        _closeButton = Require<Button>(root, "CompendiumCloseButton");
        _vfxReplayButton = Require<Button>(root, "CompendiumVfxReplayButton");

        _entryScroll.mode = ScrollViewMode.Vertical;
        _entryScroll.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
        _entryScroll.verticalScrollerVisibility = ScrollerVisibility.Auto;
    }

    public void Bind(ICompendiumActions actions)
    {
        _actions = actions ?? throw new ArgumentNullException(nameof(actions));
        _vfxReplayButton.clicked -= HandleVfxReplayClicked;
        _vfxReplayButton.clicked += HandleVfxReplayClicked;
        _searchField.RegisterValueChangedCallback(evt => _actions?.SetSearchText(evt.newValue ?? string.Empty));
        _classFilter.RegisterValueChangedCallback(evt => DispatchOption(_classOptions, evt.newValue, value => _actions?.SetSkillClassFilter(value)));
        _slotFilter.RegisterValueChangedCallback(evt => DispatchOption(_slotOptions, evt.newValue, value => _actions?.SetSkillSlotFilter(value)));
        _vfxFamilyFilter.RegisterValueChangedCallback(evt => DispatchOption(_vfxFamilyOptions, evt.newValue, value => _actions?.SetSkillVfxFamilyFilter(value)));
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
        RenderFilters(state.Filters);
        RenderTabs(state.Tabs);
        RenderEntries(state);
        RenderDetail(state.Detail);
    }

    private void RenderFilters(CompendiumFilterBarViewState filters)
    {
        _searchField.label = filters.SearchPlaceholder;
        _searchField.tooltip = filters.SearchPlaceholder;
        _searchField.SetValueWithoutNotify(filters.SearchText);

        _skillFilters.style.display = filters.ShowSkillFilters ? DisplayStyle.Flex : DisplayStyle.None;
        _resultSummary.text = filters.ResultSummary;

        _classOptions = filters.ClassOptions;
        _slotOptions = filters.SlotOptions;
        _vfxFamilyOptions = filters.VfxFamilyOptions;
        RenderDropdown(_classFilter, filters.ClassLabel, filters.ClassOptions, filters.ClassValue);
        RenderDropdown(_slotFilter, filters.SlotLabel, filters.SlotOptions, filters.SlotValue);
        RenderDropdown(_vfxFamilyFilter, filters.VfxFamilyLabel, filters.VfxFamilyOptions, filters.VfxFamilyValue);
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
        _entryList.EnableInClassList("cmp-entry-list--skill-grid", state.ActiveTab == CompendiumTab.Skills);
        _entryList.EnableInClassList("cmp-entry-list--character-grid", state.ActiveTab == CompendiumTab.Characters);
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
                    _entryList.Add(BuildCharacterCard(entry));
                }
                break;
            default:
                VisualElement? skillRow = null;
                for (var index = 0; index < state.Skills.Count; index++)
                {
                    if (index % 2 == 0)
                    {
                        skillRow = new VisualElement();
                        skillRow.AddToClassList("cmp-skill-row");
                        _entryList.Add(skillRow);
                    }

                    skillRow?.Add(BuildSkillCard(state.Skills[index]));
                }
                break;
        }
    }

    private VisualElement BuildSkillCard(CompendiumSkillViewState skill)
    {
        var button = new Button { text = string.Empty };
        button.AddToClassList("cmp-skill-card");
        if (skill.IsSelected)
        {
            button.AddToClassList("cmp-skill-card--selected");
        }

        var top = new VisualElement();
        top.AddToClassList("cmp-skill-card__top");

        var iconBox = new VisualElement();
        iconBox.AddToClassList("cmp-skill-card__icon");
        if (skill.IconSprite != null)
        {
            iconBox.style.backgroundImage = new StyleBackground(skill.IconSprite);
        }
        top.Add(iconBox);

        var headline = new VisualElement();
        headline.AddToClassList("cmp-skill-card__headline");
        var intent = new Label(skill.IntentLabel);
        intent.AddToClassList("cmp-skill-card__intent");
        headline.Add(intent);
        var title = new Label(skill.Name);
        title.AddToClassList("cmp-skill-card__title");
        headline.Add(title);
        top.Add(headline);
        button.Add(top);

        var chips = new VisualElement();
        chips.AddToClassList("cmp-skill-card__chips");
        chips.Add(BuildSkillChip(skill.SlotLabel, "slot"));
        chips.Add(BuildSkillChip(skill.ClassLabel, "class"));
        button.Add(chips);

        var combatLine = new Label(skill.CombatLineLabel);
        combatLine.AddToClassList("cmp-skill-card__line");
        button.Add(combatLine);

        var quickStats = new Label(skill.QuickStatLabel);
        quickStats.AddToClassList("cmp-skill-card__stats");
        button.Add(quickStats);

        if (skill.HasStatusPayload)
        {
            var status = new Label(skill.StatusLabel);
            status.AddToClassList("cmp-skill-card__status");
            button.Add(status);
        }

        var vfx = new Label(skill.VfxFamilyLabel);
        vfx.AddToClassList("cmp-skill-card__vfx");
        button.Add(vfx);

        var captured = skill.Id;
        button.clicked += () => _actions?.SelectEntry(captured);
        return button;
    }

    private static Label BuildSkillChip(string text, string variant)
    {
        var chip = new Label(text);
        chip.AddToClassList("cmp-skill-card__chip");
        chip.AddToClassList($"cmp-skill-card__chip--{variant}");
        return chip;
    }

    private VisualElement BuildCharacterCard(CompendiumCharacterViewState character)
    {
        var button = new Button { text = string.Empty };
        button.AddToClassList("cmp-character-card");
        if (character.IsSelected)
        {
            button.AddToClassList("cmp-character-card--selected");
        }
        if (!character.IsUnlocked)
        {
            button.AddToClassList("cmp-character-card--locked");
        }

        var portrait = new VisualElement();
        portrait.AddToClassList("cmp-character-card__portrait");
        if (character.PortraitSprite != null)
        {
            portrait.style.backgroundImage = new StyleBackground(character.PortraitSprite);
        }
        else
        {
            portrait.Add(BuildCharacterSilhouette());
        }
        button.Add(portrait);

        var title = new Label(character.DisplayName);
        title.AddToClassList("cmp-character-card__title");
        button.Add(title);

        var tags = new VisualElement();
        tags.AddToClassList("cmp-character-card__tags");
        tags.Add(BuildCharacterTag(character.ClassLabel));
        tags.Add(BuildCharacterTag(character.RoleLabel));
        button.Add(tags);

        var race = new Label(character.RaceLabel);
        race.AddToClassList("cmp-character-card__race");
        button.Add(race);

        var unlock = new Label(character.UnlockLabel);
        unlock.AddToClassList("cmp-character-card__unlock");
        button.Add(unlock);

        var captured = character.Id;
        button.clicked += () => _actions?.SelectEntry(captured);
        return button;
    }

    private static VisualElement BuildCharacterSilhouette()
    {
        var silhouette = new VisualElement();
        silhouette.AddToClassList("cmp-character-card__silhouette");
        var head = new VisualElement();
        head.AddToClassList("cmp-character-card__silhouette-head");
        silhouette.Add(head);
        var body = new VisualElement();
        body.AddToClassList("cmp-character-card__silhouette-body");
        silhouette.Add(body);
        return silhouette;
    }

    private static Label BuildCharacterTag(string text)
    {
        var tag = new Label(text);
        tag.AddToClassList("cmp-character-card__tag");
        return tag;
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

        RenderDetailKeywordRow(detail);
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

    private void RenderDetailKeywordRow(CompendiumDetailViewState detail)
    {
        _detailKeywordRow.Clear();
        foreach (var metric in detail.Metrics
                     .Where(IsDetailKeywordMetric)
                     .Take(6))
        {
            var chip = new Label($"{metric.Label}: {metric.Value}");
            chip.AddToClassList("cmp-detail__keyword");
            _detailKeywordRow.Add(chip);
        }
    }

    private static bool IsDetailKeywordMetric(CompendiumMetricViewState metric)
    {
        return !DetailKeywordSkipLabels.Contains(metric.Label)
               && !string.IsNullOrWhiteSpace(metric.Value)
               && !DetailKeywordSkipValues.Contains(metric.Value);
    }

    private void HandleVfxReplayClicked()
    {
        _actions?.PlaySelectedPreview();
    }

    private static void RenderDropdown(
        DropdownField field,
        string label,
        IReadOnlyList<CompendiumFilterOptionViewState> options,
        string selectedValue)
    {
        field.label = label;
        field.choices = options.Select(option => option.Label).ToList();
        var selectedLabel = options.FirstOrDefault(option => string.Equals(option.Value, selectedValue, StringComparison.Ordinal))?.Label
                            ?? options.FirstOrDefault()?.Label
                            ?? string.Empty;
        field.SetValueWithoutNotify(selectedLabel);
        field.SetEnabled(options.Count > 1);
    }

    private static void DispatchOption(
        IReadOnlyList<CompendiumFilterOptionViewState> options,
        string label,
        Action<string> dispatch)
    {
        var option = options.FirstOrDefault(candidate => string.Equals(candidate.Label, label, StringComparison.Ordinal));
        dispatch(option?.Value ?? string.Empty);
    }

    private static T Require<T>(VisualElement root, string name) where T : VisualElement
    {
        return root.Q<T>(name) ?? throw new InvalidOperationException($"Missing UITK element '{name}'.");
    }
}
