using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using SM.Combat.Model;
using SM.Content.Definitions;
using SM.Meta.Model;
using SM.Meta.Services;
using SM.Persistence.Abstractions.Models;

namespace SM.Unity.UI.Town;

public sealed class TownCharacterSheetFormatter
{
    private readonly GameLocalizationController _localization;
    private readonly ContentTextResolver _contentText;
    private readonly ICombatContentLookup _lookup;

    public TownCharacterSheetFormatter(
        GameLocalizationController localization,
        ContentTextResolver contentText,
        ICombatContentLookup lookup)
    {
        _localization = localization;
        _contentText = contentText;
        _lookup = lookup;
    }

    public TownCharacterSheetViewState Build(
        GameSessionState session,
        HeroInstanceRecord? hero,
        InventoryItemRecord? selectedItem,
        PassiveNodeDefinition? selectedNode,
        int retrainActiveCost,
        int retrainPassiveCost,
        int fullRetrainCost,
        DismissRefundResult dismissRefund)
    {
        var titles = BuildPanelTitles();
        if (hero == null)
        {
            var emptyBody = Localize(
                GameLocalizationTables.UITown,
                "ui.town.sheet.empty",
                "Select a hero to inspect town loadout, passives, synergy, and progression.");
            return new TownCharacterSheetViewState(
                new TownCharacterSheetPanelViewState(titles.Overview, emptyBody),
                new TownCharacterSheetPanelViewState(titles.Loadout, emptyBody),
                new TownCharacterSheetPanelViewState(titles.Passives, emptyBody),
                new TownCharacterSheetPanelViewState(titles.Synergy, emptyBody),
                new TownCharacterSheetPanelViewState(titles.Progression, emptyBody));
        }

        var loadout = session.Profile.HeroLoadouts.FirstOrDefault(record =>
            string.Equals(record.HeroId, hero.HeroId, StringComparison.Ordinal));
        var progression = session.Profile.HeroProgressions.FirstOrDefault(record =>
            string.Equals(record.HeroId, hero.HeroId, StringComparison.Ordinal));
        var archetype = ResolveArchetype(hero.ArchetypeId);
        var board = ResolvePassiveBoard(loadout?.PassiveBoardId, hero.ClassId);
        return new TownCharacterSheetViewState(
            new TownCharacterSheetPanelViewState(titles.Overview, BuildOverviewBody(session, hero, archetype)),
            new TownCharacterSheetPanelViewState(titles.Loadout, BuildLoadoutBody(session, hero, archetype)),
            new TownCharacterSheetPanelViewState(titles.Passives, BuildPassivesBody(loadout, board, selectedNode)),
            new TownCharacterSheetPanelViewState(titles.Synergy, BuildSynergyBody(session, hero, archetype)),
            new TownCharacterSheetPanelViewState(
                titles.Progression,
                BuildProgressionBody(session, hero, loadout, progression, selectedItem, retrainActiveCost, retrainPassiveCost, fullRetrainCost, dismissRefund)));
    }

    private (string Overview, string Loadout, string Passives, string Synergy, string Progression) BuildPanelTitles()
    {
        return (
            Localize(GameLocalizationTables.UITown, "ui.town.sheet.overview", "Overview"),
            Localize(GameLocalizationTables.UITown, "ui.town.sheet.loadout", "Loadout"),
            Localize(GameLocalizationTables.UITown, "ui.town.sheet.passives", "Passives"),
            Localize(GameLocalizationTables.UITown, "ui.town.sheet.synergy", "Synergy"),
            Localize(GameLocalizationTables.UITown, "ui.town.sheet.progression", "Progression"));
    }

    private string BuildOverviewBody(
        GameSessionState session,
        HeroInstanceRecord hero,
        UnitArchetypeDefinition? archetype)
    {
        var builder = new StringBuilder();
        var characterName = _contentText.GetCharacterName(hero.CharacterId, hero.ArchetypeId);
        var archetypeName = _contentText.GetArchetypeName(hero.ArchetypeId);
        builder.AppendLine($"{characterName} ({archetypeName})");
        builder.AppendLine($"Race / Class: {_contentText.GetRaceName(hero.RaceId)} / {_contentText.GetClassName(hero.ClassId)}");
        builder.AppendLine($"Role: {_contentText.GetRoleName(string.Empty, archetype?.RoleTag ?? string.Empty)}");
        builder.AppendLine($"Role Family: {_contentText.GetRoleFamilyName(hero.ClassId)}");
        builder.AppendLine($"Traits: +{FormatTraitName(hero.ArchetypeId, hero.PositiveTraitId)} / -{FormatTraitName(hero.ArchetypeId, hero.NegativeTraitId)}");
        builder.AppendLine($"Posture: {session.SelectedTeamPosture}");
        builder.AppendLine($"Tactic: {_contentText.GetTeamTacticName(ResolveCurrentTeamTacticId(session))}");
        return builder.ToString().TrimEnd();
    }

    private string BuildLoadoutBody(
        GameSessionState session,
        HeroInstanceRecord hero,
        UnitArchetypeDefinition? archetype)
    {
        var builder = new StringBuilder();
        var equippedItems = GetEquippedItems(session, hero);
        builder.AppendLine($"Weapon: {FormatItemBySlot(equippedItems, ItemSlotType.Weapon)}");
        builder.AppendLine($"Armor: {FormatItemBySlot(equippedItems, ItemSlotType.Armor)}");
        builder.AppendLine($"Accessory: {FormatItemBySlot(equippedItems, ItemSlotType.Accessory)}");
        builder.AppendLine($"Basic Attack: {ResolveBasicAttackName()}");
        builder.AppendLine($"Signature Active: {ResolveSignatureActiveName(archetype)}");
        builder.AppendLine($"Signature Passive: {ResolveSignaturePassiveName(archetype, hero.ClassId)}");
        builder.AppendLine($"Flex Active: {ResolveSkillName(hero.FlexActiveId)}");
        builder.AppendLine($"Flex Passive: {ResolveSkillName(hero.FlexPassiveId)}");
        return builder.ToString().TrimEnd();
    }

    private string BuildPassivesBody(
        HeroLoadoutRecord? loadout,
        PassiveBoardDefinition? board,
        PassiveNodeDefinition? selectedNode)
    {
        IReadOnlyList<string> selectedNodeIds = loadout?.SelectedPassiveNodeIds != null
            ? loadout.SelectedPassiveNodeIds
            : Array.Empty<string>();
        var builder = new StringBuilder();
        builder.AppendLine($"Board: {FormatPassiveBoardName(board?.Id ?? string.Empty)}");
        builder.AppendLine($"Active Nodes: {FormatNodeList(selectedNodeIds)}");
        builder.AppendLine($"Highlighted Node: {FormatPassiveNodeName(selectedNode?.Id ?? string.Empty)}");
        builder.AppendLine($"Node Count: {selectedNodeIds.Count}/{PassiveBoardSelectionValidator.MaxActiveNodeCount}");
        builder.AppendLine($"Keystone: {(selectedNodeIds.Any(id => id.Contains("_keystone_", StringComparison.Ordinal)) ? "Active" : "Inactive")}");
        return builder.ToString().TrimEnd();
    }

    private string BuildSynergyBody(
        GameSessionState session,
        HeroInstanceRecord hero,
        UnitArchetypeDefinition? archetype)
    {
        var builder = new StringBuilder();
        var squadHeroes = session.Profile.Heroes
            .Where(candidate => session.ExpeditionSquadHeroIds.Contains(candidate.HeroId, StringComparer.Ordinal))
            .ToList();
        if (squadHeroes.Count == 0)
        {
            builder.AppendLine("Squad: empty");
        }
        else
        {
            builder.AppendLine($"Squad: {squadHeroes.Count} members");
        }

        AppendSynergyLine(builder, squadHeroes, $"synergy_{hero.RaceId}", hero.RaceId, isClassFamily: false);
        AppendSynergyLine(builder, squadHeroes, $"synergy_{hero.ClassId}", hero.ClassId, isClassFamily: true);
        builder.AppendLine($"Expected Families: {string.Join(", ", ResolveExpectedSynergies(hero))}");
        builder.AppendLine($"Counter Hints: {FormatCounterTools(archetype)}");
        builder.AppendLine($"Soft Weakness: {FormatWeakness(hero.ClassId)}");
        return builder.ToString().TrimEnd();
    }

    private string BuildProgressionBody(
        GameSessionState session,
        HeroInstanceRecord hero,
        HeroLoadoutRecord? loadout,
        HeroProgressionRecord? progression,
        InventoryItemRecord? selectedItem,
        int retrainActiveCost,
        int retrainPassiveCost,
        int fullRetrainCost,
        DismissRefundResult dismissRefund)
    {
        var builder = new StringBuilder();
        builder.AppendLine($"Recruit: {hero.RecruitTier} / {hero.RecruitSource}");
        builder.AppendLine($"Retrain State: {FormatRetrainState(hero.RetrainState)}");
        builder.AppendLine($"Retrain Costs: active {retrainActiveCost}, passive {retrainPassiveCost}, full {fullRetrainCost} Echo");
        builder.AppendLine($"Dismiss Refund: +{dismissRefund.GoldRefund} Gold / +{dismissRefund.EchoRefund} Echo");
        builder.AppendLine($"Refit Preview: {(selectedItem == null ? "None" : $"{MetaBalanceDefaults.RefitEchoCost} Echo on {FormatItem(selectedItem)}")}");
        builder.AppendLine($"Blueprint Permanent: {FormatAugmentName(GetEquippedPermanentAugmentId(session))}");
        builder.AppendLine($"Unlocked Permanents: {FormatAugmentList(session.Profile.UnlockedPermanentAugmentIds)}");
        builder.AppendLine($"Passive Progress: {FormatPassiveProgress(loadout, progression)}");
        return builder.ToString().TrimEnd();
    }

    private void AppendSynergyLine(
        StringBuilder builder,
        IReadOnlyList<HeroInstanceRecord> squadHeroes,
        string synergyId,
        string countedId,
        bool isClassFamily)
    {
        var count = squadHeroes.Count(hero =>
            string.Equals(isClassFamily ? hero.ClassId : hero.RaceId, countedId, StringComparison.Ordinal));
        var (minor, major) = ResolveSynergyThresholds(synergyId, isClassFamily);
        var reached = count >= major
            ? $"{minor}/{major} reached"
            : count >= minor
                ? $"{minor} reached, next {major}"
                : $"next {minor}";
        builder.AppendLine($"{_contentText.GetSynergyName(synergyId)}: {count} units ({minor}/{major}) {reached}");
    }

    private (int Minor, int Major) ResolveSynergyThresholds(string synergyId, bool isClassFamily)
    {
        var entry = _lookup.GetFirstPlayableSlice()?.SynergyGrammar?
            .FirstOrDefault(candidate => string.Equals(candidate.FamilyId, synergyId, StringComparison.Ordinal));
        if (entry != null)
        {
            return (Math.Max(1, entry.MinorThreshold), Math.Max(entry.MinorThreshold, entry.MajorThreshold));
        }

        return isClassFamily ? (2, 3) : (2, 4);
    }

    private IReadOnlyList<string> ResolveExpectedSynergies(HeroInstanceRecord hero)
    {
        return new[]
            {
                _contentText.GetSynergyName($"synergy_{hero.RaceId}"),
                _contentText.GetSynergyName($"synergy_{hero.ClassId}"),
            }
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Distinct(StringComparer.Ordinal)
            .ToList();
    }

    private IReadOnlyList<InventoryItemRecord> GetEquippedItems(GameSessionState session, HeroInstanceRecord hero)
    {
        return session.Profile.Inventory
            .Where(item => string.Equals(item.EquippedHeroId, hero.HeroId, StringComparison.Ordinal)
                           || hero.EquippedItemIds.Contains(item.ItemInstanceId, StringComparer.Ordinal))
            .ToList();
    }

    private UnitArchetypeDefinition? ResolveArchetype(string archetypeId)
    {
        return _lookup.TryGetArchetype(archetypeId, out var archetype) ? archetype : null;
    }

    private PassiveBoardDefinition? ResolvePassiveBoard(string boardId, string classId)
    {
        if (!string.IsNullOrWhiteSpace(boardId) && _lookup.TryGetPassiveBoardDefinition(boardId, out var board))
        {
            return board;
        }

        var fallbackBoardId = string.IsNullOrWhiteSpace(classId) ? string.Empty : $"board_{classId}";
        return _lookup.TryGetPassiveBoardDefinition(fallbackBoardId, out var fallbackBoard) ? fallbackBoard : null;
    }

    private string ResolveCurrentTeamTacticId(GameSessionState session)
    {
        if (!string.IsNullOrWhiteSpace(session.SelectedTeamTacticId))
        {
            return session.SelectedTeamTacticId;
        }

        return session.SelectedTeamPosture switch
        {
            TeamPostureType.HoldLine => "team_tactic_hold_line",
            TeamPostureType.ProtectCarry => "team_tactic_protect_carry",
            TeamPostureType.CollapseWeakSide => "team_tactic_collapse_weak_side",
            TeamPostureType.AllInBackline => "team_tactic_all_in_backline",
            _ => "team_tactic_standard_advance",
        };
    }

    private string ResolveBasicAttackName() => "Basic Attack";

    private string ResolveSignatureActiveName(UnitArchetypeDefinition? archetype)
    {
        if (archetype?.Loadout?.SignatureActive != null && !string.IsNullOrWhiteSpace(archetype.Loadout.SignatureActive.Id))
        {
            return ResolveSkillName(archetype.Loadout.SignatureActive.Id);
        }

        if (archetype?.LockedSignatureActiveSkill != null && !string.IsNullOrWhiteSpace(archetype.LockedSignatureActiveSkill.Id))
        {
            return ResolveSkillName(archetype.LockedSignatureActiveSkill.Id);
        }

        if (archetype != null && DefaultSignatureActiveIds.TryGetValue(archetype.Id, out var fallbackId))
        {
            return ResolveSkillName(fallbackId);
        }

        return "None";
    }

    private string ResolveSignaturePassiveName(UnitArchetypeDefinition? archetype, string classId)
    {
        if (archetype?.Loadout?.SignaturePassive != null && !string.IsNullOrWhiteSpace(archetype.Loadout.SignaturePassive.Id))
        {
            return ResolveSkillName(archetype.Loadout.SignaturePassive.Id);
        }

        return DefaultSignaturePassiveIds.TryGetValue(classId, out var fallbackId)
            ? ResolveSkillName(fallbackId)
            : "None";
    }

    private string ResolveSkillName(string skillId)
    {
        return string.IsNullOrWhiteSpace(skillId) ? "None" : _contentText.GetSkillName(skillId);
    }

    private string FormatItemBySlot(IEnumerable<InventoryItemRecord> items, ItemSlotType slotType)
    {
        foreach (var item in items)
        {
            if (_lookup.TryGetItemDefinition(item.ItemBaseId, out var definition) && definition.SlotType == slotType)
            {
                return FormatItem(item);
            }
        }

        return "None";
    }

    private string FormatItem(InventoryItemRecord item)
    {
        var affixNames = item.AffixIds
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Take(3)
            .Select(_contentText.GetAffixName)
            .ToList();
        return affixNames.Count == 0
            ? _contentText.GetItemName(item.ItemBaseId)
            : $"{_contentText.GetItemName(item.ItemBaseId)} [{string.Join(", ", affixNames)}]";
    }

    private string FormatTraitName(string archetypeId, string traitId)
    {
        return string.IsNullOrWhiteSpace(traitId) ? "None" : _contentText.GetTraitName(archetypeId, traitId);
    }

    private string FormatPassiveBoardName(string boardId)
    {
        return string.IsNullOrWhiteSpace(boardId) ? "None" : _contentText.GetPassiveBoardName(boardId);
    }

    private string FormatPassiveNodeName(string nodeId)
    {
        return string.IsNullOrWhiteSpace(nodeId) ? "None" : _contentText.GetPassiveNodeName(nodeId);
    }

    private string FormatNodeList(IReadOnlyList<string> nodeIds)
    {
        return nodeIds == null || nodeIds.Count == 0
            ? "None"
            : string.Join(", ", nodeIds.Select(FormatPassiveNodeName));
    }

    private string FormatCounterTools(UnitArchetypeDefinition? archetype)
    {
        var tools = archetype?.BudgetCard?.DeclaredCounterTools?
            .Select(tool => $"{tool.Tool}:{tool.Strength}")
            .Where(text => !string.IsNullOrWhiteSpace(text))
            .ToList() ?? new List<string>();
        return tools.Count == 0 ? "None" : string.Join(", ", tools);
    }

    private string FormatWeakness(string classId)
    {
        return classId switch
        {
            "vanguard" => "armor shred / dot / ignore-front",
            "duelist" => "peel / redirect / hard CC",
            "ranger" => "dive / backline pressure",
            "mystic" => "silence / fast dive",
            _ => "None",
        };
    }

    private string FormatRetrainState(UnitRetrainState? state)
    {
        var effective = state ?? new UnitRetrainState();
        return $"count {effective.RetrainCount}, echo {effective.TotalEchoSpent}, incoherent {effective.ConsecutivePlanIncoherentRetrains}";
    }

    private string GetEquippedPermanentAugmentId(GameSessionState session)
    {
        return session.Profile.PermanentAugmentLoadouts
            .FirstOrDefault(record => string.Equals(record.BlueprintId, session.Profile.ActiveBlueprintId, StringComparison.Ordinal))
            ?.EquippedAugmentIds.FirstOrDefault() ?? string.Empty;
    }

    private string FormatAugmentName(string augmentId)
    {
        return string.IsNullOrWhiteSpace(augmentId) ? "None" : _contentText.GetAugmentName(augmentId);
    }

    private string FormatAugmentList(IEnumerable<string> augmentIds)
    {
        var resolved = augmentIds?
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Distinct(StringComparer.Ordinal)
            .Select(FormatAugmentName)
            .ToList() ?? new List<string>();
        return resolved.Count == 0 ? "None" : string.Join(", ", resolved);
    }

    private string FormatPassiveProgress(HeroLoadoutRecord? loadout, HeroProgressionRecord? progression)
    {
        var selected = loadout?.SelectedPassiveNodeIds?.Count ?? 0;
        var unlocked = progression?.UnlockedPassiveNodeIds?.Count ?? 0;
        return $"{selected} active / {PassiveBoardSelectionValidator.MaxActiveNodeCount} max / {unlocked} unlocked";
    }

    private string Localize(string table, string key, string fallback, params object[] args)
    {
        return _localization.LocalizeOrFallback(table, key, fallback, args);
    }

    private static readonly IReadOnlyDictionary<string, string> DefaultSignatureActiveIds =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["warden"] = "skill_power_strike",
            ["guardian"] = "skill_guardian_core",
            ["bulwark"] = "skill_bulwark_core",
            ["slayer"] = "skill_slayer_core",
            ["raider"] = "skill_raider_core",
            ["reaver"] = "skill_reaver_core",
            ["hunter"] = "skill_precision_shot",
            ["scout"] = "skill_scout_core",
            ["marksman"] = "skill_marksman_core",
            ["priest"] = "skill_priest_core",
            ["hexer"] = "skill_hexer_core",
            ["shaman"] = "skill_shaman_core",
        };

    private static readonly IReadOnlyDictionary<string, string> DefaultSignaturePassiveIds =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["vanguard"] = "skill_vanguard_passive_1",
            ["duelist"] = "skill_duelist_passive_1",
            ["ranger"] = "skill_ranger_passive_1",
            ["mystic"] = "skill_mystic_passive_1",
        };
}
