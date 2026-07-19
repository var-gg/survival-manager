using System;
using System.Linq;
using SM.Core.Results;

namespace SM.Unity;

public sealed partial class GameSessionState
{
    /// <summary>선택된 expedition 전투 직전에만 보유 장비를 출전 영웅 사이에서 무료로 재배치한다.</summary>
    internal Result ReequipOwnedItemForEncounter(string itemInstanceId, string heroId) =>
        _deploymentFlow.ReequipOwnedItemForEncounter(itemInstanceId, heroId);

    internal sealed partial class SessionDeploymentFlow
    {
        internal Result ReequipOwnedItemForEncounter(string itemInstanceId, string heroId)
        {
            var selectedNode = _session.GetSelectedExpeditionNode();
            if (selectedNode?.RequiresBattle != true
                || _session._resolvedExpeditionNodeIds.Contains(selectedNode.Id))
            {
                return Result.Fail("Encounter prep 장비 변경은 선택된 미해결 전투 노드 직전에서만 가능합니다.");
            }

            if (!_session.ExpeditionSquadHeroIds.Contains(heroId, StringComparer.Ordinal)
                || !_session.TryGetHero(heroId, out var targetHero))
            {
                return Result.Fail("Encounter prep 장비 대상은 현재 expedition squad 영웅이어야 합니다.");
            }

            var item = _session.Profile.Inventory.FirstOrDefault(value =>
                string.Equals(value.ItemInstanceId, itemInstanceId, StringComparison.Ordinal));
            if (item == null)
            {
                return Result.Fail($"아이템 '{itemInstanceId}'을 찾을 수 없습니다.");
            }

            if (!string.IsNullOrWhiteSpace(item.EquippedHeroId)
                && _session.TryGetHero(item.EquippedHeroId, out var previousHero))
            {
                previousHero.EquippedItemIds.RemoveAll(id => string.Equals(id, itemInstanceId, StringComparison.Ordinal));
                _session.SyncHeroBuildState(previousHero);
            }

            item.EquippedHeroId = heroId;
            if (!targetHero.EquippedItemIds.Contains(itemInstanceId, StringComparer.Ordinal))
            {
                targetHero.EquippedItemIds.Add(itemInstanceId);
            }

            _session.SyncHeroBuildState(targetHero);
            _session.CaptureBlueprintState();
            _session.SyncActiveRunIfPresent();
            return Result.Success();
        }
    }
}
