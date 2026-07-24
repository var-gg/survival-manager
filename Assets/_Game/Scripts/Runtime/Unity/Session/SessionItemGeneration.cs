using SM.Persistence.Abstractions.Models;

namespace SM.Unity;

public sealed partial class GameSessionState
{
    private SessionInventoryItemBuilder InventoryItemBuilder => new(_combatContentLookup, BuildStableSeed);

    private InventoryItemRecord CreateGeneratedInventoryItem(
        string itemBaseId,
        string itemInstanceId = "",
        string equippedHeroId = "",
        int rolledRarityTier = -1)
    {
        return InventoryItemBuilder.CreateGeneratedInventoryItem(
            Profile,
            itemBaseId,
            itemInstanceId,
            equippedHeroId,
            rolledRarityTier);
    }

    /// <summary>
    /// PlayMode 진입 시 SeedDemoProfile이 한 번에 4 hero에게만 item을 주므로, Equipment Refit / Inventory
    /// 같이 inventory를 사용하는 panel이 dev/preview 진입 시 텅 빈 상태가 된다. 이 helper는 inventory가
    /// 비어있을 때만 모든 hero에 baseline item을 한 개씩 채워준다 (affix 자동 생성 path 사용).
    /// production gameplay에서는 SeedDemoProfile 또는 reward path가 inventory를 미리 채우므로 no-op.
    /// </summary>
    public void SeedDevDemoInventoryIfEmpty()
    {
        DevDemoInventorySeeder.SeedIfNeeded(Profile, _combatContentLookup, InventoryItemBuilder);
    }

}
