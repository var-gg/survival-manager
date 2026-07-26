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
    private const string CharacterPath = "_Game/Art/Characters";

    private static readonly IReadOnlyDictionary<string, string> CharacterArtAliases = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["bastion_penitent"] = "hero_bastion_penitent",
        ["bulwark"] = "hero_fang_bulwark",
        ["guardian"] = "hero_crypt_guardian",
        ["hexer"] = "hero_grave_hexer",
        ["hunter"] = "hero_longshot_hunter",
        ["marksman"] = "hero_dread_marksman",
        ["mirror_cantor"] = "hero_mirror_cantor",
        ["pale_executor"] = "hero_pale_executor",
        ["priest"] = "hero_dawn_priest",
        ["raider"] = "hero_pack_raider",
        ["reaver"] = "hero_grave_reaver",
        ["rift_stalker"] = "hero_rift_stalker",
        ["scout"] = "hero_trail_scout",
        ["shaman"] = "hero_storm_shaman",
        ["slayer"] = "hero_oath_slayer",
        ["warden"] = "hero_iron_warden",
        // Town 허브 NPC 거점 short key → 포트레잇 subject (달목/쇠매/솔길/갈마).
        ["dalmok"] = "npc_dalmok_inn_keeper",
        ["soemae"] = "npc_swemae_blacksmith",
        ["solgil"] = "npc_solgil_general_store",
        ["galma"] = "npc_galma_mercenary_post",
    };

    // Migration-only fallback for pre-IconId authored assets and older test fixtures.
    // Canonical affix assets are required to author IconId and take precedence below.
    private static readonly IReadOnlyDictionary<string, string> LegacyAffixIconIds = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["affix_sharp"] = "affix_atk",
        ["affix_focusing"] = "affix_magic_atk",
        ["affix_sturdy"] = "affix_armor",
        ["affix_warded"] = "affix_resist_magic",
        ["affix_blessed"] = "affix_heal",
        ["affix_hasty"] = "affix_speed",
        ["affix_fierce"] = "affix_atk",
        ["affix_precise"] = "affix_crit",
        ["affix_piercing"] = "affix_pierce",
        ["affix_vital"] = "affix_hp",
        ["affix_ironclad"] = "affix_armor",
        ["affix_mender"] = "affix_heal",
        ["affix_lithe"] = "affix_speed",
        ["affix_lucid"] = "affix_cooldown",
        ["affix_farshot"] = "affix_pierce",
        ["affix_guarded"] = "affix_block",
        ["affix_channeling"] = "affix_cast_speed",
        ["affix_cleansing"] = "affix_cleanse",
        ["affix_bracing"] = "affix_hp",
        ["affix_resolute"] = "affix_resist_phys",
        ["affix_relentless"] = "affix_charge",
        ["affix_watchful"] = "affix_crit",
        ["affix_packborn"] = "affix_link",
        ["affix_wraithbound"] = "affix_amplify",
    };

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
               ?? ResolveCharacter(key)
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
               ?? Load($"{SkillPath}/skill_icon_{StripPrefix(skillId, "skill_")}");
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

        var iconId = ResolveAffixIconId(affixId);
        return Load($"{AffixPath}/{iconId}");
    }

    public Texture2D? ResolveCharacter(string characterId)
    {
        if (string.IsNullOrWhiteSpace(characterId))
        {
            return null;
        }

        var artKey = ResolveCharacterArtKey(characterId);
        return Load($"{CharacterPath}/{artKey}/portrait_face_default")
               ?? Load($"{CharacterPath}/{artKey}/portrait_stance_idle")
               ?? Load($"{CharacterPath}/{artKey}/portrait_full")
               ?? Load($"{CharacterPath}/{artKey}/portrait_full_body");
    }

    public Texture2D? ResolveCharacterPortrait(string characterId)
    {
        if (string.IsNullOrWhiteSpace(characterId))
        {
            return null;
        }

        var artKey = ResolveCharacterArtKey(characterId);
        return Load($"{CharacterPath}/{artKey}/portrait_bust_default_L")
               ?? Load($"{CharacterPath}/{artKey}/portrait_bust_default_R")
               ?? Load($"{CharacterPath}/{artKey}/portrait_full")
               ?? Load($"{CharacterPath}/{artKey}/portrait_full_body")
               ?? Load($"{CharacterPath}/{artKey}/portrait_stance_idle")
               ?? Load($"{CharacterPath}/{artKey}/portrait_face_default");
    }

    public Texture2D? ResolveCharacterStandee(string characterId)
    {
        if (string.IsNullOrWhiteSpace(characterId))
        {
            return null;
        }

        var artKey = ResolveCharacterArtKey(characterId);
        return Load($"{CharacterPath}/{artKey}/portrait_full")
               ?? Load($"{CharacterPath}/{artKey}/portrait_full_body")
               ?? Load($"{CharacterPath}/{artKey}/portrait_stance_idle")
               ?? Load($"{CharacterPath}/{artKey}/portrait_bust_default_L")
               ?? Load($"{CharacterPath}/{artKey}/portrait_bust_default_R")
               ?? Load($"{CharacterPath}/{artKey}/portrait_face_default");
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

    private string ResolveAffixIconId(string affixId)
    {
        if (_lookup.TryGetAffixDefinition(affixId, out var affix) && !string.IsNullOrWhiteSpace(affix.IconId))
        {
            return affix.IconId;
        }

        if (LegacyAffixIconIds.TryGetValue(affixId, out var legacyIconId))
        {
            return legacyIconId;
        }

        return affixId.StartsWith("affix_", StringComparison.Ordinal)
            ? affixId
            : $"affix_{affixId}";
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

    private static string ResolveCharacterArtKey(string characterId)
    {
        var key = characterId.Trim();
        return CharacterArtAliases.TryGetValue(key, out var artKey)
            ? artKey
            : key;
    }
}
