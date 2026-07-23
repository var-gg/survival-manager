using SM.Content.Definitions;
using SM.Core.Content;
using SM.Persistence.Abstractions.Models;

namespace SM.Unity.UI.Town;

internal static class InventoryItemGradePresentation
{
    internal static ItemRarityTierValue Resolve(
        InventoryItemRecord item,
        ItemBaseDefinition fallbackDefinition)
    {
        return item.RolledRarityTier >= (int)ItemRarityTierValue.Common
               && item.RolledRarityTier <= (int)ItemRarityTierValue.Legendary
            ? (ItemRarityTierValue)item.RolledRarityTier
            : fallbackDefinition.RarityTier;
    }
}
