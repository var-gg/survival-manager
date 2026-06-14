using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;

namespace SM.Unity.UI.HeroDetail;

/// <summary>
/// HeroDetailPanel.uxml의 named element을 바인딩하고 <see cref="HeroDetailViewState"/>를 렌더한다.
/// TownCharacterSheetView 패턴(Require + 컨테이너 Clear/fill + class modifier)을 따른다.
/// 4-slot 위계는 sm-hd-slot--{signature|flex-active|flex-retrain|late-unlock} modifier로,
/// 아이콘 상태는 sm-hd-slot-icon--{present|fallback|missing}로 구분한다.
/// </summary>
public sealed class HeroDetailView
{
    private static readonly string[] FamilyClasses =
    {
        "sm-hd-left--vanguard",
        "sm-hd-left--duelist",
        "sm-hd-left--ranger",
        "sm-hd-left--mystic",
        "sm-hd-left--beastkin",
    };

    private readonly VisualElement _root;
    private readonly VisualElement _left;
    private readonly Label _portraitGlyph;
    private readonly Label _title;
    private readonly Label _name;
    private readonly Label _level;
    private readonly Label _tier;
    private readonly Label _role;
    private readonly Label _archetype;
    private readonly VisualElement _stats;
    private readonly VisualElement _skills;
    private readonly VisualElement _equip;
    private readonly VisualElement _traits;
    private readonly Button _close;

    public HeroDetailView(VisualElement root)
    {
        if (root == null) throw new ArgumentNullException(nameof(root));
        _root = Require<VisualElement>(root, "HeroDetailRoot");
        _left = Require<VisualElement>(root, "HeroDetailLeft");
        _portraitGlyph = Require<Label>(root, "HeroDetailPortraitGlyph");
        _title = Require<Label>(root, "HeroDetailTitle");
        _name = Require<Label>(root, "HeroDetailName");
        _level = Require<Label>(root, "HeroDetailLevel");
        _tier = Require<Label>(root, "HeroDetailTier");
        _role = Require<Label>(root, "HeroDetailRole");
        _archetype = Require<Label>(root, "HeroDetailArchetype");
        _stats = Require<VisualElement>(root, "HeroDetailStats");
        _skills = Require<VisualElement>(root, "HeroDetailSkills");
        _equip = Require<VisualElement>(root, "HeroDetailEquip");
        _traits = Require<VisualElement>(root, "HeroDetailTraits");
        _close = Require<Button>(root, "HeroDetailClose");
    }

    public void BindClose(Action onClose)
    {
        if (onClose != null)
        {
            _close.clicked += onClose;
        }
    }

    public void Open()
    {
        _root.style.display = DisplayStyle.Flex;
    }

    public void Close()
    {
        _root.style.display = DisplayStyle.None;
    }

    public void Render(HeroDetailViewState state)
    {
        if (state == null) throw new ArgumentNullException(nameof(state));
        _title.text = state.DisplayName;
        _name.text = state.DisplayName;
        _level.text = state.LevelLabel;
        _tier.text = state.TierLabel;
        _role.text = state.RoleLabel;
        _archetype.text = state.ArchetypeLabel;
        _portraitGlyph.text = BuildGlyph(state.DisplayName);
        ApplyFamilyClass(state.FamilyKey);
        RenderStats(state.Stats);
        RenderSkills(state.SkillSlots);
        RenderEquip(state.Equipment);
        RenderTraits(state.Traits);
    }

    private void RenderStats(IReadOnlyList<HeroDetailStatViewState> stats)
    {
        _stats.Clear();
        foreach (var stat in stats)
        {
            var cell = new VisualElement();
            cell.AddToClassList("sm-hd-stat");
            if (!string.IsNullOrWhiteSpace(stat.Tone))
            {
                cell.AddToClassList($"sm-hd-stat--tone-{stat.Tone.Trim().ToLowerInvariant()}");
            }

            var label = new Label(stat.Label);
            label.AddToClassList("sm-hd-stat__label");
            cell.Add(label);

            var value = new Label(stat.Value);
            value.AddToClassList("sm-hd-stat__value");
            cell.Add(value);

            _stats.Add(cell);
        }
    }

    private void RenderSkills(IReadOnlyList<HeroDetailSkillSlotViewState> slots)
    {
        _skills.Clear();
        foreach (var slot in slots)
        {
            var card = new VisualElement();
            card.AddToClassList("sm-hd-slot");
            card.AddToClassList($"sm-hd-slot--{SlotKindClass(slot.SlotKind)}");
            if (slot.IsPassive)
            {
                card.AddToClassList("sm-hd-slot--passive");
            }

            var icon = new VisualElement();
            icon.AddToClassList("sm-hd-slot__icon");
            icon.AddToClassList($"sm-hd-slot-icon--{IconStateClass(slot.IconState)}");
            var iconGlyph = new Label(BuildInitials(slot.Name));
            iconGlyph.AddToClassList("sm-hd-slot__icon-glyph");
            icon.Add(iconGlyph);
            var badge = new Label(SlotBadge(slot.SlotKind));
            badge.AddToClassList("sm-hd-slot__badge");
            icon.Add(badge);
            card.Add(icon);

            var name = new Label(slot.Name);
            name.AddToClassList("sm-hd-slot__name");
            card.Add(name);

            var meta = new Label(slot.MetaLabel);
            meta.AddToClassList("sm-hd-slot__meta");
            card.Add(meta);

            var cd = new Label(slot.CooldownText);
            cd.AddToClassList("sm-hd-slot__cd");
            card.Add(cd);

            card.tooltip = string.IsNullOrWhiteSpace(slot.Description) ? slot.Name : slot.Description;
            _skills.Add(card);
        }
    }

    private void RenderEquip(IReadOnlyList<HeroDetailEquipSlotViewState> equipment)
    {
        _equip.Clear();
        foreach (var slot in equipment)
        {
            var item = new VisualElement();
            item.AddToClassList("sm-hd-equip-slot");
            item.AddToClassList(slot.IsFilled ? "sm-hd-equip-slot--filled" : "sm-hd-equip-slot--empty");

            var label = new Label(slot.SlotLabel);
            label.AddToClassList("sm-hd-equip-slot__label");
            item.Add(label);

            var name = new Label(slot.ItemLabel);
            name.AddToClassList("sm-hd-equip-slot__name");
            item.Add(name);

            var meta = new Label(slot.MetaLabel);
            meta.AddToClassList("sm-hd-equip-slot__meta");
            item.Add(meta);

            _equip.Add(item);
        }
    }

    private void RenderTraits(IReadOnlyList<HeroDetailTraitViewState> traits)
    {
        _traits.Clear();
        foreach (var trait in traits)
        {
            var row = new VisualElement();
            row.AddToClassList("sm-hd-trait");
            if (!string.IsNullOrWhiteSpace(trait.Kind))
            {
                row.AddToClassList($"sm-hd-trait--{trait.Kind.Trim().ToLowerInvariant()}");
            }

            var name = new Label(trait.Name);
            name.AddToClassList("sm-hd-trait__name");
            row.Add(name);

            _traits.Add(row);
        }
    }

    private void ApplyFamilyClass(string familyKey)
    {
        foreach (var familyClass in FamilyClasses)
        {
            _left.RemoveFromClassList(familyClass);
        }

        _left.AddToClassList($"sm-hd-left--{NormalizeFamily(familyKey)}");
    }

    private static string NormalizeFamily(string familyKey)
    {
        var normalized = (familyKey ?? string.Empty).Trim().ToLowerInvariant();
        return normalized is "vanguard" or "duelist" or "ranger" or "mystic" or "beastkin"
            ? normalized
            : "vanguard";
    }

    private static string SlotKindClass(HeroDetailSlotKind kind)
    {
        return kind switch
        {
            HeroDetailSlotKind.SignatureLock => "signature",
            HeroDetailSlotKind.FlexActive => "flex-active",
            HeroDetailSlotKind.FlexRetrain => "flex-retrain",
            HeroDetailSlotKind.LateUnlock => "late-unlock",
            _ => "flex-retrain",
        };
    }

    private static string IconStateClass(HeroDetailIconState state)
    {
        return state switch
        {
            HeroDetailIconState.Present => "present",
            HeroDetailIconState.Fallback => "fallback",
            HeroDetailIconState.Missing => "missing",
            _ => "missing",
        };
    }

    private static string SlotBadge(HeroDetailSlotKind kind)
    {
        return kind switch
        {
            HeroDetailSlotKind.SignatureLock => "●",
            HeroDetailSlotKind.FlexActive => "✓",
            HeroDetailSlotKind.FlexRetrain => "↻",
            HeroDetailSlotKind.LateUnlock => "✦",
            _ => string.Empty,
        };
    }

    private static string BuildGlyph(string displayName)
    {
        var trimmed = displayName?.Trim();
        return string.IsNullOrWhiteSpace(trimmed) ? "✦" : trimmed[..1];
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

    private static T Require<T>(VisualElement root, string name) where T : VisualElement
    {
        return root.Q<T>(name) ?? throw new InvalidOperationException($"Missing UITK element '{name}'.");
    }
}
