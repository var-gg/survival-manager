using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using SM.Combat.Model;
using SM.Content.Definitions;
using SM.Core.Content;
using SM.Core.Contracts;
using SM.Meta.Model;
using SM.Meta.Services;
using SM.Persistence.Abstractions.Models;

namespace SM.Unity.UI.Town;

public sealed class TownCharacterSheetFormatter
{
    private readonly GameLocalizationController _localization;
    private readonly ContentTextResolver _contentText;
    private readonly ICombatContentLookup _lookup;
    private readonly LaunchCoreRosterBaselineCatalog _baselineCatalog;

    public TownCharacterSheetFormatter(
        GameLocalizationController localization,
        ContentTextResolver contentText,
        ICombatContentLookup lookup)
    {
        _localization = localization;
        _contentText = contentText;
        _lookup = lookup;
        _baselineCatalog = new LaunchCoreRosterBaselineCatalog(lookup);
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
            var emptyBody = LocalizeTown(
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
        var baseline = ResolveBaseline(hero.ArchetypeId);
        var board = ResolvePassiveBoard(loadout?.PassiveBoardId ?? string.Empty, baseline, hero.ClassId);

        return new TownCharacterSheetViewState(
            new TownCharacterSheetPanelViewState(titles.Overview, BuildOverviewBody(session, hero, archetype)),
            new TownCharacterSheetPanelViewState(titles.Loadout, BuildLoadoutBody(session, hero, baseline)),
            new TownCharacterSheetPanelViewState(titles.Passives, BuildPassivesBody(loadout, board, selectedNode)),
            new TownCharacterSheetPanelViewState(titles.Synergy, BuildSynergyBody(session, hero, archetype, baseline)),
            new TownCharacterSheetPanelViewState(
                titles.Progression,
                BuildProgressionBody(session, hero, loadout, progression, selectedItem, retrainActiveCost, retrainPassiveCost, fullRetrainCost, dismissRefund)));
    }

    private (string Overview, string Loadout, string Passives, string Synergy, string Progression) BuildPanelTitles()
    {
        return (
            LocalizeTown("ui.town.sheet.overview", "Overview"),
            LocalizeTown("ui.town.sheet.loadout", "Loadout"),
            LocalizeTown("ui.town.sheet.passives", "Passives"),
            LocalizeTown("ui.town.sheet.synergy", "Synergy"),
            LocalizeTown("ui.town.sheet.progression", "Progression"));
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
        AppendLabeledLine(
            builder,
            "ui.town.sheet.race_class",
            "Race / Class",
            $"{_contentText.GetRaceName(hero.RaceId)} / {_contentText.GetClassName(hero.ClassId)}");
        AppendLabeledLine(
            builder,
            "ui.town.sheet.role",
            "Role",
            _contentText.GetRoleName(string.Empty, archetype?.RoleTag ?? string.Empty));
        AppendLabeledLine(
            builder,
            "ui.town.sheet.role_family",
            "Role Family",
            _contentText.GetRoleFamilyName(hero.ClassId));
        AppendLabeledLine(
            builder,
            "ui.town.sheet.traits",
            "Traits",
            $"+{FormatTraitName(hero.ArchetypeId, hero.PositiveTraitId)} / -{FormatTraitName(hero.ArchetypeId, hero.NegativeTraitId)}");
        AppendLabeledLine(builder, "ui.town.sheet.posture", "Posture", LocalizePosture(session.SelectedTeamPosture));
        AppendLabeledLine(
            builder,
            "ui.town.sheet.tactic",
            "Tactic",
            _contentText.GetTeamTacticName(ResolveCurrentTeamTacticId(session)));
        return builder.ToString().TrimEnd();
    }

    private string BuildLoadoutBody(
        GameSessionState session,
        HeroInstanceRecord hero,
        LaunchCoreUnitBaseline? baseline)
    {
        var builder = new StringBuilder();
        var equippedItems = GetEquippedItems(session, hero);

        AppendLabeledLine(builder, "ui.town.sheet.weapon", "Weapon", FormatItemBySlot(equippedItems, ItemSlotType.Weapon));
        AppendLabeledLine(builder, "ui.town.sheet.armor", "Armor", FormatItemBySlot(equippedItems, ItemSlotType.Armor));
        AppendLabeledLine(builder, "ui.town.sheet.accessory", "Accessory", FormatItemBySlot(equippedItems, ItemSlotType.Accessory));
        AppendLabeledLine(builder, "ui.town.sheet.basic_attack", "Basic Attack", ResolveBasicAttackName());
        AppendLabeledLine(builder, "ui.town.sheet.signature_active", "Signature Active", ResolveSkillName(baseline?.SignatureActiveId ?? string.Empty));
        AppendLabeledLine(builder, "ui.town.sheet.signature_passive", "Signature Passive", ResolveSkillName(baseline?.SignaturePassiveId ?? string.Empty));
        AppendLabeledLine(builder, "ui.town.sheet.flex_active", "Flex Active", ResolveSkillName(hero.FlexActiveId));
        AppendLabeledLine(builder, "ui.town.sheet.flex_passive", "Flex Passive", ResolveSkillName(hero.FlexPassiveId));
        return builder.ToString().TrimEnd();
    }

    private string BuildPassivesBody(
        HeroLoadoutRecord? loadout,
        PassiveBoardDefinition? board,
        PassiveNodeDefinition? selectedNode)
    {
        IReadOnlyList<string> selectedNodeIds = loadout == null
            ? Array.Empty<string>()
            : loadout.SelectedPassiveNodeIds;
        var builder = new StringBuilder();

        AppendLabeledLine(builder, "ui.town.sheet.board", "Board", FormatPassiveBoardName(board?.Id ?? string.Empty));
        AppendLabeledLine(builder, "ui.town.sheet.active_nodes", "Active Nodes", FormatNodeList(selectedNodeIds));
        AppendLabeledLine(builder, "ui.town.sheet.highlighted_node", "Highlighted Node", FormatPassiveNodeName(selectedNode?.Id ?? string.Empty));
        AppendLabeledLine(builder, "ui.town.sheet.node_count", "Node Count", $"{selectedNodeIds.Count}/{PassiveBoardSelectionValidator.MaxActiveNodeCount}");
        AppendLabeledLine(
            builder,
            "ui.town.sheet.keystone",
            "Keystone",
            selectedNodeIds.Any(id => id.Contains("_keystone_", StringComparison.Ordinal))
                ? LocalizeTown("ui.town.sheet.state.active", "Active")
                : LocalizeTown("ui.town.sheet.state.inactive", "Inactive"));
        return builder.ToString().TrimEnd();
    }

    private string BuildSynergyBody(
        GameSessionState session,
        HeroInstanceRecord hero,
        UnitArchetypeDefinition? archetype,
        LaunchCoreUnitBaseline? baseline)
    {
        var builder = new StringBuilder();
        var squadHeroes = session.Profile.Heroes
            .Where(candidate => session.ExpeditionSquadHeroIds.Contains(candidate.HeroId, StringComparer.Ordinal))
            .ToList();

        AppendLabeledLine(
            builder,
            "ui.town.sheet.squad",
            "Squad",
            squadHeroes.Count == 0
                ? LocalizeTown("ui.town.sheet.state.empty", "Empty")
                : $"{squadHeroes.Count} {LocalizeTown("ui.town.sheet.members", "members")}");

        AppendSynergyLine(builder, squadHeroes, BuildSynergyId(hero.RaceId), hero.RaceId, isClassFamily: false);
        AppendSynergyLine(builder, squadHeroes, BuildSynergyId(hero.ClassId), hero.ClassId, isClassFamily: true);
        AppendLabeledLine(
            builder,
            "ui.town.sheet.expected_families",
            "Expected Families",
            string.Join(", ", ResolveExpectedSynergies(hero, baseline)));
        AppendLabeledLine(builder, "ui.town.sheet.counter_hints", "Counter Hints", FormatCounterTools(archetype));
        AppendLabeledLine(builder, "ui.town.sheet.soft_weakness", "Soft Weakness", FormatWeakness(hero.ClassId));
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

        AppendLabeledLine(
            builder,
            "ui.town.sheet.recruit",
            "Recruit",
            $"{LocalizeRecruitTier(hero.RecruitTier)} / {LocalizeRecruitSource(hero.RecruitSource)}");
        AppendLabeledLine(builder, "ui.town.sheet.retrain_state", "Retrain State", FormatRetrainState(hero.RetrainState));
        AppendLabeledLine(
            builder,
            "ui.town.sheet.retrain_costs",
            "Retrain Costs",
            $"{LocalizeTown("ui.town.sheet.cost.active", "active")} {retrainActiveCost}, " +
            $"{LocalizeTown("ui.town.sheet.cost.passive", "passive")} {retrainPassiveCost}, " +
            $"{LocalizeTown("ui.town.sheet.cost.full", "full")} {fullRetrainCost} {LocalizeTown("ui.town.sheet.currency.echo", "Echo")}");
        AppendLabeledLine(
            builder,
            "ui.town.sheet.dismiss_refund",
            "Dismiss Refund",
            $"+{dismissRefund.GoldRefund} {LocalizeTown("ui.town.sheet.currency.gold", "Gold")} / " +
            $"+{dismissRefund.EchoRefund} {LocalizeTown("ui.town.sheet.currency.echo", "Echo")}");
        AppendLabeledLine(
            builder,
            "ui.town.sheet.refit_preview",
            "Refit Preview",
            selectedItem == null
                ? CommonNone()
                : $"{MetaBalanceDefaults.RefitEchoCost} {LocalizeTown("ui.town.sheet.currency.echo", "Echo")} / {FormatItem(selectedItem)}");
        AppendLabeledLine(builder, "ui.town.sheet.blueprint_permanent", "Blueprint Permanent", FormatAugmentName(GetEquippedPermanentAugmentId(session)));
        AppendLabeledLine(builder, "ui.town.sheet.unlocked_permanents", "Unlocked Permanents", FormatAugmentList(session.Profile.UnlockedPermanentAugmentIds));
        AppendLabeledLine(builder, "ui.town.sheet.passive_progress", "Passive Progress", FormatPassiveProgress(loadout, progression));
        return builder.ToString().TrimEnd();
    }

    private void AppendSynergyLine(
        StringBuilder builder,
        IReadOnlyList<HeroInstanceRecord> squadHeroes,
        string synergyId,
        string countedId,
        bool isClassFamily)
    {
        if (string.IsNullOrWhiteSpace(synergyId) || string.IsNullOrWhiteSpace(countedId))
        {
            return;
        }

        var count = squadHeroes.Count(hero =>
            string.Equals(isClassFamily ? hero.ClassId : hero.RaceId, countedId, StringComparison.Ordinal));
        var (minor, major) = _baselineCatalog.ResolveSynergyThresholds(synergyId, isClassFamily);
        var reachedText = count >= major
            ? $"{minor}/{major} {LocalizeTown("ui.town.sheet.reached", "reached")}"
            : count >= minor
                ? $"{minor} {LocalizeTown("ui.town.sheet.reached", "reached")}, {LocalizeTown("ui.town.sheet.next", "next")} {major}"
                : $"{LocalizeTown("ui.town.sheet.next", "next")} {minor}";
        builder.AppendLine($"{_contentText.GetSynergyName(synergyId)}: {count} {LocalizeTown("ui.town.sheet.units", "units")} ({minor}/{major}) {reachedText}");
    }

    private IReadOnlyList<string> ResolveExpectedSynergies(HeroInstanceRecord hero, LaunchCoreUnitBaseline? baseline)
    {
        var ids = baseline?.ExpectedSynergyIds ?? _baselineCatalog.BuildExpectedSynergyIds(hero.RaceId, hero.ClassId);
        return ids
            .Select(_contentText.GetSynergyName)
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

    private LaunchCoreUnitBaseline? ResolveBaseline(string archetypeId)
    {
        return _baselineCatalog.TryGetUnitBaseline(archetypeId, out var baseline) ? baseline : null;
    }

    private PassiveBoardDefinition? ResolvePassiveBoard(string boardId, LaunchCoreUnitBaseline? baseline, string classId)
    {
        if (!string.IsNullOrWhiteSpace(boardId) && _lookup.TryGetPassiveBoardDefinition(boardId, out var board))
        {
            return board;
        }

        var baselineBoardId = baseline?.DefaultPassiveBoardId ?? string.Empty;
        if (!string.IsNullOrWhiteSpace(baselineBoardId) && _lookup.TryGetPassiveBoardDefinition(baselineBoardId, out var baselineBoard))
        {
            return baselineBoard;
        }

        var classBoardId = string.IsNullOrWhiteSpace(classId) ? string.Empty : $"board_{classId}";
        return !string.IsNullOrWhiteSpace(classBoardId) && _lookup.TryGetPassiveBoardDefinition(classBoardId, out var classBoard)
            ? classBoard
            : null;
    }

    private string ResolveCurrentTeamTacticId(GameSessionState session)
    {
        if (!string.IsNullOrWhiteSpace(session.SelectedTeamTacticId))
        {
            return session.SelectedTeamTacticId;
        }

        return LaunchCoreRosterBaselineCatalog.ResolveRecommendedTeamTacticId(session.SelectedTeamPosture);
    }

    private string ResolveBasicAttackName()
    {
        return LocalizeTown("ui.town.sheet.value.basic_attack", "Basic Attack");
    }

    private string ResolveSkillName(string skillId)
    {
        return string.IsNullOrWhiteSpace(skillId) ? CommonNone() : _contentText.GetSkillName(skillId);
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

        return CommonNone();
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
        return string.IsNullOrWhiteSpace(traitId) ? CommonNone() : _contentText.GetTraitName(archetypeId, traitId);
    }

    private string FormatPassiveBoardName(string boardId)
    {
        return string.IsNullOrWhiteSpace(boardId) ? CommonNone() : _contentText.GetPassiveBoardName(boardId);
    }

    private string FormatPassiveNodeName(string nodeId)
    {
        return string.IsNullOrWhiteSpace(nodeId) ? CommonNone() : _contentText.GetPassiveNodeName(nodeId);
    }

    private string FormatNodeList(IReadOnlyList<string> nodeIds)
    {
        return nodeIds.Count == 0
            ? CommonNone()
            : string.Join(", ", nodeIds.Select(FormatPassiveNodeName));
    }

    private string FormatCounterTools(UnitArchetypeDefinition? archetype)
    {
        var tools = archetype?.BudgetCard?.DeclaredCounterTools?
            .Select(tool => $"{LocalizeCounterTool(tool.Tool)}({LocalizeCounterStrength(tool.Strength)})")
            .Where(text => !string.IsNullOrWhiteSpace(text))
            .ToList() ?? new List<string>();
        return tools.Count == 0 ? CommonNone() : string.Join(", ", tools);
    }

    private string FormatWeakness(string classId)
    {
        return classId switch
        {
            "vanguard" => LocalizeTown("ui.town.sheet.weakness.vanguard", "armor shred / dot / ignore-front"),
            "duelist" => LocalizeTown("ui.town.sheet.weakness.duelist", "peel / redirect / hard CC"),
            "ranger" => LocalizeTown("ui.town.sheet.weakness.ranger", "dive / backline pressure"),
            "mystic" => LocalizeTown("ui.town.sheet.weakness.mystic", "silence / fast dive"),
            _ => CommonNone(),
        };
    }

    private string FormatRetrainState(UnitRetrainState? state)
    {
        var effective = state ?? new UnitRetrainState();
        return $"{LocalizeTown("ui.town.sheet.count", "count")} {effective.RetrainCount}, " +
               $"{LocalizeTown("ui.town.sheet.currency.echo", "Echo")} {effective.TotalEchoSpent}, " +
               $"{LocalizeTown("ui.town.sheet.incoherent", "incoherent")} {effective.ConsecutivePlanIncoherentRetrains}";
    }

    private string GetEquippedPermanentAugmentId(GameSessionState session)
    {
        return session.Profile.PermanentAugmentLoadouts
            .FirstOrDefault(record => string.Equals(record.BlueprintId, session.Profile.ActiveBlueprintId, StringComparison.Ordinal))
            ?.EquippedAugmentIds.FirstOrDefault() ?? string.Empty;
    }

    private string FormatAugmentName(string augmentId)
    {
        return string.IsNullOrWhiteSpace(augmentId) ? CommonNone() : _contentText.GetAugmentName(augmentId);
    }

    private string FormatAugmentList(IEnumerable<string> augmentIds)
    {
        var resolved = augmentIds
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Distinct(StringComparer.Ordinal)
            .Select(FormatAugmentName)
            .ToList();
        return resolved.Count == 0 ? CommonNone() : string.Join(", ", resolved);
    }

    private string FormatPassiveProgress(HeroLoadoutRecord? loadout, HeroProgressionRecord? progression)
    {
        var selected = loadout?.SelectedPassiveNodeIds?.Count ?? 0;
        var unlocked = progression?.UnlockedPassiveNodeIds?.Count ?? 0;
        return $"{selected} {LocalizeTown("ui.town.sheet.state.active", "active")} / " +
               $"{PassiveBoardSelectionValidator.MaxActiveNodeCount} {LocalizeTown("ui.town.sheet.max", "max")} / " +
               $"{unlocked} {LocalizeTown("ui.town.sheet.unlocked", "unlocked")}";
    }

    private string LocalizeRecruitTier(RecruitTier tier)
    {
        return tier switch
        {
            RecruitTier.Rare => LocalizeTown("ui.town.recruit.tier.rare", "Rare"),
            RecruitTier.Epic => LocalizeTown("ui.town.recruit.tier.epic", "Epic"),
            _ => LocalizeTown("ui.town.recruit.tier.common", "Common"),
        };
    }

    private string LocalizeRecruitSource(RecruitOfferSource source)
    {
        return source switch
        {
            RecruitOfferSource.CombatReward => LocalizeTown("ui.town.sheet.recruit_source.combat_reward", "Combat Reward"),
            RecruitOfferSource.EventReward => LocalizeTown("ui.town.sheet.recruit_source.event_reward", "Event Reward"),
            RecruitOfferSource.DirectGrant => LocalizeTown("ui.town.sheet.recruit_source.direct_grant", "Direct Grant"),
            _ => LocalizeTown("ui.town.sheet.recruit_source.recruit_phase", "Recruit Phase"),
        };
    }

    private string LocalizePosture(TeamPostureType posture)
    {
        return posture switch
        {
            TeamPostureType.HoldLine => LocalizeTown("ui.town.sheet.posture.hold_line", "Hold Line"),
            TeamPostureType.ProtectCarry => LocalizeTown("ui.town.sheet.posture.protect_carry", "Protect Carry"),
            TeamPostureType.CollapseWeakSide => LocalizeTown("ui.town.sheet.posture.collapse_weak_side", "Collapse Weak Side"),
            TeamPostureType.AllInBackline => LocalizeTown("ui.town.sheet.posture.all_in_backline", "All In Backline"),
            _ => LocalizeTown("ui.town.sheet.posture.standard_advance", "Standard Advance"),
        };
    }

    private string LocalizeCounterTool(CounterTool tool)
    {
        return tool switch
        {
            CounterTool.ArmorShred => LocalizeTown("ui.town.sheet.counter_tool.armor_shred", "Armor Shred"),
            CounterTool.Exposure => LocalizeTown("ui.town.sheet.counter_tool.exposure", "Exposure"),
            CounterTool.GuardBreakMultiHit => LocalizeTown("ui.town.sheet.counter_tool.guard_break_multi_hit", "Guard Break"),
            CounterTool.TrackingArea => LocalizeTown("ui.town.sheet.counter_tool.tracking_area", "Tracking Area"),
            CounterTool.TenacityStability => LocalizeTown("ui.town.sheet.counter_tool.tenacity_stability", "Tenacity Stability"),
            CounterTool.AntiHealShatter => LocalizeTown("ui.town.sheet.counter_tool.anti_heal_shatter", "Anti-Heal"),
            CounterTool.InterceptPeel => LocalizeTown("ui.town.sheet.counter_tool.intercept_peel", "Intercept Peel"),
            CounterTool.CleaveWaveclear => LocalizeTown("ui.town.sheet.counter_tool.cleave_waveclear", "Cleave Waveclear"),
            _ => CommonNone(),
        };
    }

    private string LocalizeCounterStrength(CounterCoverageStrength strength)
    {
        return strength switch
        {
            CounterCoverageStrength.Strong => LocalizeTown("ui.town.sheet.counter_strength.strong", "Strong"),
            CounterCoverageStrength.Standard => LocalizeTown("ui.town.sheet.counter_strength.standard", "Standard"),
            _ => LocalizeTown("ui.town.sheet.counter_strength.light", "Light"),
        };
    }

    private void AppendLabeledLine(StringBuilder builder, string key, string fallbackLabel, string value)
    {
        builder.AppendLine($"{LocalizeTown(key, fallbackLabel)}: {value}");
    }

    private string LocalizeTown(string key, string fallback, params object[] args)
    {
        return _localization.LocalizeOrFallback(GameLocalizationTables.UITown, key, fallback, args);
    }

    private string LocalizeCommon(string key, string fallback, params object[] args)
    {
        return _localization.LocalizeOrFallback(GameLocalizationTables.UICommon, key, fallback, args);
    }

    private string CommonNone()
    {
        return LocalizeCommon("ui.common.none", "None");
    }

    private static string BuildSynergyId(string familyId)
    {
        return string.IsNullOrWhiteSpace(familyId)
            ? string.Empty
            : $"synergy_{familyId}";
    }
}
