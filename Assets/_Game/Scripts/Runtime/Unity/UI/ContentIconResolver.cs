using System;
using System.Collections.Generic;
using SM.Content.Definitions;
using UnityEngine;

namespace SM.Unity.UI;

internal sealed class ContentIconResolver
{
    private const string SkillPath = "_Game/Art/Icons/Skill";
    private const string ItemPath = "_Game/Art/Icons/Item";
    private const string AugmentPath = "_Game/Art/Icons/Augment";
    private const string AffixPath = "_Game/Art/Icons/Affix";

    private readonly ICombatContentLookup _lookup;
    private readonly Dictionary<string, Texture2D?> _cache = new(StringComparer.Ordinal);

    public ContentIconResolver(ICombatContentLookup lookup)
    {
        _lookup = lookup ?? throw new ArgumentNullException(nameof(lookup));
    }

    public Texture2D? ResolveAny(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return null;
        }

        return ResolveSkill(key)
               ?? ResolveItem(key)
               ?? ResolveAugment(key)
               ?? ResolveAffix(key)
               ?? ResolveDirect(key);
    }

    public Texture2D? ResolveSkill(string skillId, string characterId = "")
    {
        if (string.IsNullOrWhiteSpace(skillId))
        {
            return null;
        }

        var iconId = ResolveSkillIconId(skillId);
        return Load($"{SkillPath}/{iconId}")
               ?? Load($"{SkillPath}/skill_icon_{skillId}");
    }

    public Texture2D? ResolveItem(string itemId)
    {
        if (string.IsNullOrWhiteSpace(itemId))
        {
            return null;
        }

        var iconId = ResolveItemIconId(itemId);
        return Load($"{ItemPath}/{iconId}")
               ?? Load($"{ItemPath}/item_icon_{itemId}")
               ?? ResolveItemCategory(itemId);
    }

    public Texture2D? ResolveAugment(string augmentId)
    {
        if (string.IsNullOrWhiteSpace(augmentId))
        {
            return null;
        }

        var iconId = ResolveAugmentIconId(augmentId);
        return Load($"{AugmentPath}/{iconId}")
               ?? Load($"{AugmentPath}/augment_{augmentId}");
    }

    public Texture2D? ResolveAffix(string affixId)
    {
        if (string.IsNullOrWhiteSpace(affixId))
        {
            return null;
        }

        var iconId = affixId.StartsWith("affix_", StringComparison.Ordinal)
            ? affixId
            : $"affix_{affixId}";
        return Load($"{AffixPath}/{iconId}");
    }

    private Texture2D? ResolveDirect(string key)
    {
        return Load($"{SkillPath}/{key}")
               ?? Load($"{SkillPath}/skill_icon_{key}")
               ?? Load($"{ItemPath}/{key}")
               ?? Load($"{ItemPath}/item_icon_{key}")
               ?? Load($"{AugmentPath}/{key}")
               ?? Load($"{AugmentPath}/augment_{key}")
               ?? Load($"{AffixPath}/{key}")
               ?? Load($"{AffixPath}/affix_{key}");
    }

    private string ResolveSkillIconId(string skillId)
    {
        if (_lookup.TryGetSkillDefinition(skillId, out var skill) && !string.IsNullOrWhiteSpace(skill.IconId))
        {
            return skill.IconId;
        }

        return skillId.StartsWith("skill_icon_", StringComparison.Ordinal)
            ? skillId
            : $"skill_icon_{StripPrefix(skillId, "skill_")}";
    }

    private string ResolveItemIconId(string itemId)
    {
        if (_lookup.TryGetItemDefinition(itemId, out var item))
        {
            if (!string.IsNullOrWhiteSpace(item.IconId))
            {
                return item.IconId;
            }

            return item.SlotType switch
            {
                ItemSlotType.Weapon => $"item_icon_{ResolveWeaponFamily(item)}",
                ItemSlotType.Armor => "item_icon_armor",
                _ => "item_icon_trinket",
            };
        }

        return itemId.StartsWith("item_icon_", StringComparison.Ordinal)
            ? itemId
            : $"item_icon_{itemId}";
    }

    private string ResolveAugmentIconId(string augmentId)
    {
        if (_lookup.TryGetAugmentDefinition(augmentId, out var augment) && !string.IsNullOrWhiteSpace(augment.IconId))
        {
            return augment.IconId;
        }

        return augmentId.StartsWith("augment_", StringComparison.Ordinal)
            ? augmentId
            : $"augment_{augmentId}";
    }

    private Texture2D? ResolveItemCategory(string key)
    {
        return key switch
        {
            "weapon" => Load($"{ItemPath}/item_icon_blade"),
            "armor" => Load($"{ItemPath}/item_icon_armor"),
            "accessory" => Load($"{ItemPath}/item_icon_trinket"),
            "shield" => Load($"{ItemPath}/item_icon_shield"),
            "blade" => Load($"{ItemPath}/item_icon_blade"),
            "bow" => Load($"{ItemPath}/item_icon_bow"),
            "focus" => Load($"{ItemPath}/item_icon_focus"),
            _ => null,
        };
    }

    private static string ResolveWeaponFamily(ItemBaseDefinition item)
    {
        if (!string.IsNullOrWhiteSpace(item.WeaponFamilyTag))
        {
            return item.WeaponFamilyTag switch
            {
                "shield" or "bow" or "focus" or "blade" => item.WeaponFamilyTag,
                _ => "blade",
            };
        }

        if (item.Id.Contains("shield", StringComparison.Ordinal))
        {
            return "shield";
        }

        if (item.Id.Contains("bow", StringComparison.Ordinal))
        {
            return "bow";
        }

        if (item.Id.Contains("focus", StringComparison.Ordinal) || item.Id.Contains("bead", StringComparison.Ordinal))
        {
            return "focus";
        }

        return "blade";
    }

    private Texture2D? Load(string resourcePath)
    {
        if (_cache.TryGetValue(resourcePath, out var cached))
        {
            return cached;
        }

        var texture = Resources.Load<Texture2D>(resourcePath);
        _cache[resourcePath] = texture;
        return texture;
    }

    private static string StripPrefix(string value, string prefix)
    {
        return value.StartsWith(prefix, StringComparison.Ordinal)
            ? value[prefix.Length..]
            : value;
    }
}
