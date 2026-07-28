using System;
using System.Linq;
using SM.Core.Results;
using static SM.Unity.SessionOperationFailureBoundary;

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
                return RefuseSessionOperation(SessionOperationFailureCodes.EncounterPrepUnavailable, "Encounter-prep equipment changes require a selected unresolved battle node.");
            }

            if (!_session.ExpeditionSquadHeroIds.Contains(heroId, StringComparer.Ordinal)
                || !_session.TryGetHero(heroId, out var targetHero))
            {
                return RefuseSessionOperation(SessionOperationFailureCodes.EncounterPrepHeroInvalid, $"Encounter-prep target hero '{heroId}' is not in the current expedition squad.");
            }

            var item = _session.Profile.Inventory.FirstOrDefault(value =>
                string.Equals(value.ItemInstanceId, itemInstanceId, StringComparison.Ordinal));
            if (item == null)
            {
                return RefuseSessionOperation(SessionOperationFailureCodes.ItemNotFound, $"Inventory item '{itemInstanceId}' was not found.");
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
