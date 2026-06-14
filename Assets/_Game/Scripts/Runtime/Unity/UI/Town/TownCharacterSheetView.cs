using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine.UIElements;
using UnityEngine;

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

    private static readonly string[] FamilyTokenClasses =
    {
        "tcs-portrait-token--vanguard",
        "tcs-portrait-token--duelist",
        "tcs-portrait-token--ranger",
        "tcs-portrait-token--mystic",
        "tcs-portrait-token--beastkin",
    };

    private readonly VisualElement _modalRoot;
    private readonly VisualElement _portraitToken;
    private readonly VisualElement _portraitImage;
    private readonly VisualElement _heroRail;
    private readonly VisualElement _statGrid;
    private readonly VisualElement _skillList;
    private readonly VisualElement _equipmentRow;
    private readonly VisualElement _progressionTrack;
    private readonly Label _portraitGlyphLabel;
    private readonly Label _heroNameLabel;
    private readonly Label _heroMetaLabel;
    private readonly Label _roleLabel;
    private readonly Label _overviewTitleLabel;
    private readonly VisualElement _overviewBody;
    private readonly Label _loadoutTitleLabel;
    private readonly VisualElement _loadoutBody;
    private readonly Label _passivesTitleLabel;
    private readonly VisualElement _passivesBody;
    private readonly Label _synergyTitleLabel;
    private readonly VisualElement _synergyBody;
    private readonly Label _progressionTitleLabel;
    private readonly VisualElement _progressionBody;
    private readonly Button _closeButton;
    // wave-50 P2 action bar — 미존재 UXML(이전 build)에서도 panel 부팅이 깨지지 않게 nullable.
    private readonly Button? _dismissButton;
    private readonly Button? _retrainButton;
    private readonly Button? _passiveBoardButton;
    private readonly Button? _refitButton;

    public TownCharacterSheetView(VisualElement root)
    {
        if (root == null) throw new ArgumentNullException(nameof(root));
        _modalRoot = Require<VisualElement>(root, "TownCharacterSheetRoot");
        _portraitToken = Require<VisualElement>(root, "TcsPortraitToken");
        _portraitImage = Require<VisualElement>(root, "TcsPortraitImage");
        _heroRail = Require<VisualElement>(root, "TcsHeroRail");
        _statGrid = Require<VisualElement>(root, "TcsStatGrid");
        _skillList = Require<VisualElement>(root, "TcsSkillList");
        _equipmentRow = Require<VisualElement>(root, "TcsEquipmentRow");
        _progressionTrack = Require<VisualElement>(root, "TcsProgressionTrack");
        _portraitGlyphLabel = Require<Label>(root, "TcsPortraitGlyphLabel");
        _heroNameLabel = Require<Label>(root, "TcsHeroNameLabel");
        _heroMetaLabel = Require<Label>(root, "TcsHeroMetaLabel");
        _roleLabel = Require<Label>(root, "TcsRoleLabel");
        _overviewTitleLabel = Require<Label>(root, "TcsOverviewTitle");
        _overviewBody = Require<VisualElement>(root, "TcsOverviewBody");
        _loadoutTitleLabel = Require<Label>(root, "TcsLoadoutTitle");
        _loadoutBody = Require<VisualElement>(root, "TcsLoadoutBody");
        _passivesTitleLabel = Require<Label>(root, "TcsPassivesTitle");
        _passivesBody = Require<VisualElement>(root, "TcsPassivesBody");
        _synergyTitleLabel = Require<Label>(root, "TcsSynergyTitle");
        _synergyBody = Require<VisualElement>(root, "TcsSynergyBody");
        _progressionTitleLabel = Require<Label>(root, "TcsProgressionTitle");
        _progressionBody = Require<VisualElement>(root, "TcsProgressionBody");
        _closeButton = Require<Button>(root, "TownCharacterSheetCloseButton");
        _dismissButton = root.Q<Button>("TcsDismissButton");
        _retrainButton = root.Q<Button>("TcsRetrainButton");
        _passiveBoardButton = root.Q<Button>("TcsPassiveBoardButton");
        _refitButton = root.Q<Button>("TcsRefitButton");
    }

    private TownCharacterSheetPresenter? _presenter;

    public void Bind(TownCharacterSheetPresenter presenter)
    {
        if (presenter == null) throw new ArgumentNullException(nameof(presenter));
        _presenter = presenter;
        _closeButton.clicked += presenter.Close;
        if (_dismissButton != null) _dismissButton.clicked += presenter.OnDismissClicked;
        if (_retrainButton != null) _retrainButton.clicked += presenter.OnRetrainClicked;
        if (_passiveBoardButton != null) _passiveBoardButton.clicked += presenter.OnPassiveBoardClicked;
        if (_refitButton != null) _refitButton.clicked += presenter.OnRefitClicked;
    }

    /// <summary>해고·재훈련 service가 wire되지 않은 동안 버튼을 정직하게 잠근다 — 비용/환급을 보여주며
    /// 눌리는 척하다 무반응하던 silent dead button을 방지. 후속 wave가 BindActionBar로 핸들러를
    /// 주입하면(wired=true) 자동으로 다시 활성화된다.</summary>
    public void SetActionAvailability(bool dismissWired, bool retrainWired)
    {
        ApplyActionLock(_dismissButton, dismissWired, "해고");
        ApplyActionLock(_retrainButton, retrainWired, "재훈련");
    }

    private static void ApplyActionLock(Button? button, bool wired, string baseLabel)
    {
        if (button == null) return;
        button.SetEnabled(wired);
        button.text = wired ? baseLabel : $"{baseLabel} (준비 중)";
        button.tooltip = wired ? string.Empty : $"{baseLabel} 기능은 아직 준비 중입니다.";
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
        _portraitGlyphLabel.text = BuildPortraitGlyph(state.DisplayName);
        ApplyRoleFamilyClass(state.FamilyKey);
        RenderPortrait(state.PortraitSprite);
        RenderHeroRail(state.HeroRail);
        RenderStats(state.Stats);
        RenderSkills(state.Skills);
        RenderEquipment(state.Equipment);
        RenderProgressionTrack(state.ProgressionNodes);

        RenderPanel(state.Overview, _overviewTitleLabel, _overviewBody);
        RenderPanel(state.Loadout, _loadoutTitleLabel, _loadoutBody);
        RenderPanel(state.Passives, _passivesTitleLabel, _passivesBody);
        RenderPanel(state.Synergy, _synergyTitleLabel, _synergyBody);
        RenderPanel(state.Progression, _progressionTitleLabel, _progressionBody);
    }

    private void RenderPortrait(Texture2D? portraitSprite)
    {
        _portraitImage.style.backgroundImage = StyleKeyword.Null;
        if (portraitSprite != null)
        {
            _portraitImage.style.display = DisplayStyle.Flex;
            _portraitImage.style.backgroundImage = new StyleBackground(portraitSprite);
            _portraitGlyphLabel.style.display = DisplayStyle.None;
            return;
        }

        _portraitImage.style.display = DisplayStyle.None;
        _portraitGlyphLabel.style.display = DisplayStyle.Flex;
    }

    private void RenderHeroRail(IReadOnlyList<TownCharacterSheetHeroRailEntryViewState> entries)
    {
        _heroRail.Clear();
        foreach (var entry in entries)
        {
            var item = new VisualElement { name = $"TcsHeroRailEntry_{entry.HeroId}" };
            item.AddToClassList("tcs-hero-rail__entry");
            AddClassVariant(item, "tcs-hero-rail__entry--family", entry.FamilyKey);
            if (entry.IsSelected)
            {
                item.AddToClassList("tcs-hero-rail__entry--selected");
            }
            // 레일 항목 클릭 → 시트 내에서 해당 동료로 전환(Presenter.Open이 heroId 재바인딩).
            // 그동안 핸들러 0이라 포트레잇만 보이고 누르면 무반응이던 dead-end 해소.
            var railHeroId = entry.HeroId;
            item.RegisterCallback<ClickEvent>(_ => _presenter?.Open(railHeroId));
            item.tooltip = entry.IsSelected ? entry.DisplayName : $"{entry.DisplayName} · 클릭해 전환";

            var portrait = new VisualElement();
            portrait.AddToClassList("tcs-hero-rail__portrait");
            AddClassVariant(portrait, "tcs-hero-rail__portrait--family", entry.FamilyKey);
            if (entry.PortraitSprite != null)
            {
                portrait.style.backgroundImage = new StyleBackground(entry.PortraitSprite);
            }
            else
            {
                var fallback = new Label(BuildInitials(entry.DisplayName));
                fallback.AddToClassList("tcs-hero-rail__fallback");
                portrait.Add(fallback);
            }
            item.Add(portrait);

            var copy = new VisualElement();
            copy.AddToClassList("tcs-hero-rail__copy");
            var name = new Label(entry.DisplayName);
            name.AddToClassList("tcs-hero-rail__name");
            copy.Add(name);
            var meta = new Label(entry.MetaLabel);
            meta.AddToClassList("tcs-hero-rail__meta");
            copy.Add(meta);
            item.Add(copy);

            _heroRail.Add(item);
        }
    }

    private void RenderStats(IReadOnlyList<TownCharacterSheetStatViewState> stats)
    {
        _statGrid.Clear();
        foreach (var stat in stats)
        {
            var cell = new VisualElement();
            cell.AddToClassList("tcs-stat");
            AddClassVariant(cell, "tcs-stat--tone", stat.Tone);

            var label = new Label(stat.Label);
            label.AddToClassList("tcs-stat__label");
            cell.Add(label);

            var value = new Label(stat.Value);
            value.AddToClassList("tcs-stat__value");
            cell.Add(value);

            if (!string.IsNullOrWhiteSpace(stat.DeltaLabel))
            {
                var delta = new Label(stat.DeltaLabel);
                delta.AddToClassList("tcs-stat__delta");
                cell.Add(delta);
            }

            _statGrid.Add(cell);
        }
    }

    private void RenderSkills(IReadOnlyList<TownCharacterSheetSkillCardViewState> skills)
    {
        _skillList.Clear();
        foreach (var skill in skills)
        {
            var card = new VisualElement();
            card.AddToClassList("tcs-skill-card");
            card.AddToClassList(skill.IsSignature ? "tcs-skill-card--signature" : "tcs-skill-card--flex");

            var icon = new VisualElement();
            icon.AddToClassList("tcs-skill-card__icon");
            if (skill.IconSprite != null)
            {
                icon.style.backgroundImage = new StyleBackground(skill.IconSprite);
            }
            else
            {
                var fallback = new Label(BuildInitials(skill.Name));
                fallback.AddToClassList("tcs-skill-card__fallback");
                icon.Add(fallback);
            }
            card.Add(icon);

            var copy = new VisualElement();
            copy.AddToClassList("tcs-skill-card__copy");
            var name = new Label(skill.Name);
            name.AddToClassList("tcs-skill-card__name");
            copy.Add(name);
            var meta = new Label(skill.MetaLabel);
            meta.AddToClassList("tcs-skill-card__meta");
            copy.Add(meta);
            var description = new Label(skill.Description);
            description.AddToClassList("tcs-skill-card__description");
            description.tooltip = skill.Description;
            copy.Add(description);
            card.Add(copy);

            _skillList.Add(card);
        }
    }

    private void RenderEquipment(IReadOnlyList<TownCharacterSheetEquipmentSlotViewState> equipment)
    {
        _equipmentRow.Clear();
        foreach (var slot in equipment)
        {
            var item = new VisualElement();
            item.AddToClassList("tcs-equipment");
            item.AddToClassList(slot.IsFilled ? "tcs-equipment--filled" : "tcs-equipment--empty");
            AddClassVariant(item, "tcs-equipment--slot", slot.SlotKey);

            var icon = new VisualElement();
            icon.AddToClassList("tcs-equipment__icon");
            if (slot.IconSprite != null)
            {
                icon.style.backgroundImage = new StyleBackground(slot.IconSprite);
            }
            item.Add(icon);

            var slotLabel = new Label(slot.SlotLabel);
            slotLabel.AddToClassList("tcs-equipment__slot");
            item.Add(slotLabel);

            var name = new Label(slot.ItemLabel);
            name.AddToClassList("tcs-equipment__name");
            item.Add(name);

            var meta = new Label(slot.MetaLabel);
            meta.AddToClassList("tcs-equipment__meta");
            item.Add(meta);

            _equipmentRow.Add(item);
        }
    }

    private void RenderProgressionTrack(IReadOnlyList<TownCharacterSheetProgressionNodeViewState> nodes)
    {
        _progressionTrack.Clear();
        foreach (var node in nodes)
        {
            var element = new VisualElement();
            element.AddToClassList("tcs-progression-node");
            AddClassVariant(element, "tcs-progression-node--state", node.State);
            element.tooltip = node.Label;

            var label = new Label(ShortenNodeLabel(node.Label));
            label.AddToClassList("tcs-progression-node__label");
            element.Add(label);
            _progressionTrack.Add(element);
        }
    }

    private void ApplyRoleFamilyClass(string familyKey)
    {
        foreach (var roleClass in RoleFamilyClasses)
        {
            _roleLabel.RemoveFromClassList(roleClass);
        }

        foreach (var familyClass in FamilyTokenClasses)
        {
            _portraitToken.RemoveFromClassList(familyClass);
        }

        if (!string.IsNullOrWhiteSpace(familyKey))
        {
            var normalized = NormalizeFamilyKey(familyKey);
            _roleLabel.AddToClassList($"tcs-role--{normalized}");
            _portraitToken.AddToClassList($"tcs-portrait-token--{normalized}");
        }
    }

    private static string NormalizeFamilyKey(string familyKey)
    {
        var normalized = familyKey.Trim().ToLowerInvariant();
        return normalized is "vanguard" or "duelist" or "ranger" or "mystic" or "beastkin"
            ? normalized
            : "vanguard";
    }

    private static string BuildPortraitGlyph(string displayName)
    {
        var trimmed = displayName?.Trim();
        return string.IsNullOrWhiteSpace(trimmed) ? "✦" : trimmed[..1];
    }

    private static void RenderPanel(TownCharacterSheetPanelViewState panel, Label title, VisualElement body)
    {
        title.text = panel.Title;
        body.Clear();
        foreach (var row in panel.Rows)
        {
            body.Add(BuildRow(row));
        }
    }

    private static VisualElement BuildRow(TownCharacterSheetPanelRowViewState row)
    {
        var element = new VisualElement();
        element.AddToClassList("tcs-row");

        // Label이 비면 full-width note row(예: empty-state 안내, overview 헤딩).
        if (string.IsNullOrWhiteSpace(row.Label))
        {
            element.AddToClassList("tcs-row--note");
            var note = new Label(row.Value);
            note.AddToClassList("tcs-row__value");
            note.AddToClassList("tcs-row__value--note");
            element.Add(note);
            element.tooltip = row.Value;
            return element;
        }

        var label = new Label(row.Label);
        label.AddToClassList("tcs-row__label");
        element.Add(label);

        var value = new Label(row.Value);
        value.AddToClassList("tcs-row__value");
        if (!string.IsNullOrWhiteSpace(row.Tone))
        {
            value.AddToClassList($"tcs-row__value--tone-{row.Tone.Trim().ToLowerInvariant()}");
        }
        element.Add(value);
        element.tooltip = $"{row.Label}: {row.Value}";
        return element;
    }

    private static void AddClassVariant(VisualElement element, string prefix, string key)
    {
        if (element == null || string.IsNullOrWhiteSpace(key))
        {
            return;
        }

        element.AddToClassList($"{prefix}-{key.Trim().ToLowerInvariant()}");
    }

    private static string BuildInitials(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "?";
        }

        var parts = value.Split(new[] { ' ', '-', '_' }, StringSplitOptions.RemoveEmptyEntries);
        return parts.Length == 0
            ? value[..1].ToUpperInvariant()
            : string.Concat(parts[..Math.Min(2, parts.Length)].Select(part => part[..1].ToUpperInvariant()));
    }

    private static string ShortenNodeLabel(string label)
    {
        if (string.IsNullOrWhiteSpace(label))
        {
            return "?";
        }

        var trimmed = label.Trim();
        return trimmed.Length <= 2 ? trimmed : trimmed[..1];
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
