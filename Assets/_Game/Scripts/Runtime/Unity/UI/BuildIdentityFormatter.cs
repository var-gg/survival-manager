using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using SM.Content.Definitions;

namespace SM.Unity.UI;

public sealed class BuildIdentityFormatter
{
    private readonly ContentTextResolver _contentText;

    public BuildIdentityFormatter(ContentTextResolver contentText)
    {
        _contentText = contentText;
    }

    public string BuildBlueprintSummary(GameSessionState session)
    {
        var equippedPermanentId = session.Profile.PermanentAugmentLoadouts
            .FirstOrDefault(record => string.Equals(record.BlueprintId, session.Profile.ActiveBlueprintId, StringComparison.Ordinal))
            ?.EquippedAugmentIds.FirstOrDefault() ?? string.Empty;
        var benchAugmentIds = session.Profile.UnlockedPermanentAugmentIds
            .Where(id => !string.Equals(id, equippedPermanentId, StringComparison.Ordinal))
            .ToList();

        var builder = new StringBuilder();
        builder.AppendLine($"Blueprint: {session.Profile.ActiveBlueprintId}");
        builder.AppendLine($"Posture: {session.SelectedTeamPosture}");
        builder.AppendLine($"Permanent: {FormatAugmentName(equippedPermanentId)}");
        builder.AppendLine($"Bench: {FormatAugmentList(benchAugmentIds)}");
        builder.AppendLine($"Thesis: {session.SelectedTeamPosture} / {FormatAugmentName(equippedPermanentId)} / Squad {session.ExpeditionSquadHeroIds.Count}");
        return builder.ToString();
    }

    public string BuildSelectedHeroSummary(
        GameSessionState session,
        HeroInstanceRecord? hero,
        InventoryItemRecord? selectedItem,
        string selectedNodeId)
    {
        if (hero == null)
        {
            return "No hero selected.";
        }

        var builder = new StringBuilder();
        builder.AppendLine($"{hero.Name}");
        builder.AppendLine($"{_contentText.GetRaceName(hero.RaceId)} / {_contentText.GetClassName(hero.ClassId)}");
        builder.AppendLine($"Flex Active: {_contentText.GetSkillName(hero.FlexActiveId)}");
        builder.AppendLine($"Flex Passive: {_contentText.GetSkillName(hero.FlexPassiveId)}");
        builder.AppendLine($"Selected Item: {FormatItem(selectedItem)}");
        builder.AppendLine($"Equipped Items: {FormatEquippedItems(session, hero)}");

        var loadout = session.Profile.HeroLoadouts.FirstOrDefault(record =>
            string.Equals(record.HeroId, hero.HeroId, StringComparison.Ordinal));
        var boardId = loadout?.PassiveBoardId ?? string.Empty;
        builder.AppendLine($"Board: {FormatBoardName(boardId)}");
        builder.AppendLine($"Selected Nodes: {FormatSelectedNodes(loadout?.SelectedPassiveNodeIds ?? Array.Empty<string>())}");
        builder.AppendLine($"Current Node: {FormatPassiveNodeName(selectedNodeId)}");

        return builder.ToString();
    }

    private string FormatEquippedItems(GameSessionState session, HeroInstanceRecord hero)
    {
        var equippedItems = session.Profile.Inventory
            .Where(item => string.Equals(item.EquippedHeroId, hero.HeroId, StringComparison.Ordinal)
                           || hero.EquippedItemIds.Contains(item.ItemInstanceId, StringComparer.Ordinal))
            .ToList();
        if (equippedItems.Count == 0)
        {
            return "None";
        }

        return string.Join(", ", equippedItems.Select(FormatItem));
    }

    private string FormatItem(InventoryItemRecord? item)
    {
        if (item == null)
        {
            return "None";
        }

        var affixNames = item.AffixIds
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Take(3)
            .Select(_contentText.GetAffixName)
            .ToList();
        return affixNames.Count == 0
            ? _contentText.GetItemName(item.ItemBaseId)
            : $"{_contentText.GetItemName(item.ItemBaseId)} [{string.Join(", ", affixNames)}]";
    }

    private string FormatSelectedNodes(IReadOnlyCollection<string> selectedNodeIds)
    {
        if (selectedNodeIds.Count == 0)
        {
            return "None";
        }

        return string.Join(", ", selectedNodeIds.Select(FormatPassiveNodeName));
    }

    private string FormatPassiveNodeName(string nodeId)
    {
        if (string.IsNullOrWhiteSpace(nodeId))
        {
            return "None";
        }

        return _contentText.GetPassiveNodeName(nodeId);
    }

    private string FormatBoardName(string boardId)
    {
        if (string.IsNullOrWhiteSpace(boardId))
        {
            return "None";
        }

        return _contentText.GetPassiveBoardName(boardId);
    }

    private string FormatAugmentName(string augmentId)
    {
        if (string.IsNullOrWhiteSpace(augmentId))
        {
            return "None";
        }

        return _contentText.GetAugmentName(augmentId);
    }

    private string FormatAugmentList(IReadOnlyCollection<string> augmentIds)
    {
        if (augmentIds.Count == 0)
        {
            return "None";
        }

        return string.Join(", ", augmentIds.Select(FormatAugmentName));
    }
}
