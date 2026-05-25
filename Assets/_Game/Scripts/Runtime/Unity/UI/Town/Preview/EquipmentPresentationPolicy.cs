using System;
using System.Collections.Generic;
using System.Linq;

namespace SM.Unity.UI.Town.Preview;

public readonly struct EquipmentPresentation
{
    public EquipmentPresentation(
        string slotKey,
        string slotLabel,
        string familyKey,
        string familyLabel,
        string rarityKey,
        string rawRarityKey,
        bool isLaunchSupportedRarity,
        string identityKey,
        string identityLabel,
        bool showsIdentityBadge,
        bool canRefit)
    {
        SlotKey = slotKey;
        SlotLabel = slotLabel;
        FamilyKey = familyKey;
        FamilyLabel = familyLabel;
        RarityKey = rarityKey;
        RawRarityKey = rawRarityKey;
        IsLaunchSupportedRarity = isLaunchSupportedRarity;
        IdentityKey = identityKey;
        IdentityLabel = identityLabel;
        ShowsIdentityBadge = showsIdentityBadge;
        CanRefit = canRefit;
    }

    public string SlotKey { get; }
    public string SlotLabel { get; }
    public string FamilyKey { get; }
    public string FamilyLabel { get; }
    public string RarityKey { get; }
    public string RawRarityKey { get; }
    public bool IsLaunchSupportedRarity { get; }
    public string IdentityKey { get; }
    public string IdentityLabel { get; }
    public bool ShowsIdentityBadge { get; }
    public bool CanRefit { get; }
}

public static class EquipmentPresentationPolicy
{
    private static readonly IReadOnlyDictionary<string, string> SlotLabels = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        { "weapon", "무기" },
        { "armor", "방어구" },
        { "accessory", "장신구" },
        { "item", "장비" },
    };

    private static readonly IReadOnlyDictionary<string, string> FamilyLabels = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        { "shield", "방패" },
        { "blade", "검" },
        { "bow", "활" },
        { "focus", "매개체" },
    };

    public static EquipmentPresentation Build(
        string slotKey,
        string weaponFamilyTag,
        string rarityName,
        string identityName,
        IEnumerable<string>? allowedCraftOperations)
    {
        var normalizedSlot = NormalizeSlotKey(slotKey);
        var familyKey = normalizedSlot == "weapon" ? NormalizeWeaponFamily(weaponFamilyTag) : string.Empty;
        var familyLabel = !string.IsNullOrEmpty(familyKey) && FamilyLabels.TryGetValue(familyKey, out var resolvedFamilyLabel)
            ? resolvedFamilyLabel
            : string.Empty;
        var rarity = NormalizeRarity(rarityName);
        var identity = NormalizeIdentity(identityName);

        return new EquipmentPresentation(
            slotKey: normalizedSlot,
            slotLabel: SlotLabels.TryGetValue(normalizedSlot, out var slotLabel) ? slotLabel : normalizedSlot,
            familyKey: familyKey,
            familyLabel: familyLabel,
            rarityKey: rarity.RarityKey,
            rawRarityKey: rarity.RawRarityKey,
            isLaunchSupportedRarity: rarity.IsLaunchSupported,
            identityKey: identity.IdentityKey,
            identityLabel: identity.IdentityLabel,
            showsIdentityBadge: identity.ShowsBadge,
            canRefit: CanRefit(allowedCraftOperations));
    }

    private static string NormalizeSlotKey(string slotKey)
    {
        return (slotKey ?? string.Empty).Trim().ToLowerInvariant() switch
        {
            "weapon" => "weapon",
            "armor" => "armor",
            "accessory" => "accessory",
            _ => "item",
        };
    }

    private static string NormalizeWeaponFamily(string weaponFamilyTag)
    {
        return (weaponFamilyTag ?? string.Empty).Trim().ToLowerInvariant() switch
        {
            "shield" => "shield",
            "bow" => "bow",
            "focus" => "focus",
            "blade" => "blade",
            _ => "blade",
        };
    }

    private static (string RarityKey, string RawRarityKey, bool IsLaunchSupported) NormalizeRarity(string rarityName)
    {
        var raw = (rarityName ?? string.Empty).Trim().ToLowerInvariant();
        return raw switch
        {
            "common" => ("common", "common", true),
            "rare" => ("rare", "rare", true),
            "epic" => ("epic", "epic", true),
            "magic" => ("rare", "magic", false),
            "legendary" => ("epic", "legendary", false),
            _ => ("common", string.IsNullOrEmpty(raw) ? "common" : raw, false),
        };
    }

    private static (string IdentityKey, string IdentityLabel, bool ShowsBadge) NormalizeIdentity(string identityName)
    {
        return (identityName ?? string.Empty).Trim().ToLowerInvariant() switch
        {
            "named" => ("named", "NAMED", true),
            "unique" => ("unique", "SIGNATURE", true),
            _ => ("baseline", string.Empty, false),
        };
    }

    private static bool CanRefit(IEnumerable<string>? allowedCraftOperations)
    {
        if (allowedCraftOperations == null)
        {
            return true;
        }

        var operations = allowedCraftOperations
            .Where(operation => !string.IsNullOrWhiteSpace(operation))
            .Select(operation => operation.Trim())
            .ToArray();
        return operations.Length == 0 ||
               operations.Any(operation =>
                   string.Equals(operation, "Reforge", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(operation, "Refit", StringComparison.OrdinalIgnoreCase));
    }
}
