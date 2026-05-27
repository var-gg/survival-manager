using System;
using System.Linq;
using SM.Core.Content;
using SM.Persistence.Abstractions.Models;

namespace SM.Unity;

internal static class DevDemoInventorySeeder
{
    private const int TargetAffixCount = 5;

    internal static void SeedIfNeeded(
        SaveProfile profile,
        ICombatContentLookup lookup,
        SessionInventoryItemBuilder itemBuilder)
    {
        var allItemIds = lookup.GetCanonicalItemIds();
        if (allItemIds.Count == 0 || profile.Heroes.Count == 0)
        {
            return;
        }

        var hasRichItem = profile.Inventory.Any(item => item.AffixIds != null && item.AffixIds.Count >= 3);
        if (profile.Inventory.Count > 0 && hasRichItem)
        {
            return;
        }

        if (profile.Inventory.Count > 0 && !hasRichItem)
        {
            foreach (var item in profile.Inventory)
            {
                itemBuilder.EnsureAffixPadding(item, item.ItemBaseId, TargetAffixCount);
            }
            return;
        }

        var richItemIds = allItemIds
            .Where(id => lookup.TryGetItemDefinition(id, out var def)
                         && (def.RarityTier >= ItemRarityTierValue.Rare
                             || def.IdentityKind != ItemIdentityValue.Baseline))
            .ToList();
        var seedPool = richItemIds.Count > 0 ? richItemIds : allItemIds;

        for (var i = 0; i < profile.Heroes.Count; i++)
        {
            var hero = profile.Heroes[i];
            hero.EquippedItemIds ??= new System.Collections.Generic.List<string>();
            var itemBaseId = seedPool[i % seedPool.Count];
            var instanceId = $"dev-item-{i + 1:D2}";
            var record = itemBuilder.CreateGeneratedInventoryItem(profile, itemBaseId, instanceId, hero.HeroId);
            itemBuilder.EnsureAffixPadding(record, itemBaseId, TargetAffixCount);
            profile.Inventory.Add(record);
            if (!hero.EquippedItemIds.Any(id => string.Equals(id, instanceId, StringComparison.Ordinal)))
            {
                hero.EquippedItemIds.Add(instanceId);
            }
        }
    }
}
