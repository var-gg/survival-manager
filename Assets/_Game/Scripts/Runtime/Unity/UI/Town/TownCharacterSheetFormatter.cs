using System;
using System.Collections.Generic;
using System.Linq;
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
            // 모든 패널이 같은 note row 리스트(동일 인스턴스)를 공유 — empty-state 일관성 보장.
            IReadOnlyList<TownCharacterSheetPanelRowViewState> emptyRows = new[]
            {
                Note(LocalizeTown(
                    "ui.town.sheet.empty",
                    "Select a hero to inspect town loadout, passives, synergy, and progression.")),
            };
            return new TownCharacterSheetViewState(
                HeroId: string.Empty,
                DisplayName: "No hero selected",
                ArchetypeLabel: string.Empty,
                RoleLabel: string.Empty,
                FamilyKey: string.Empty,
                PortraitSprite: null,
                HeroRail: Array.Empty<TownCharacterSheetHeroRailEntryViewState>(),
                Stats: Array.Empty<TownCharacterSheetStatViewState>(),
                Skills: Array.Empty<TownCharacterSheetSkillCardViewState>(),
                Equipment: Array.Empty<TownCharacterSheetEquipmentSlotViewState>(),
                ProgressionNodes: Array.Empty<TownCharacterSheetProgressionNodeViewState>(),
                LevelProgress: new TownCharacterSheetLevelProgressViewState("Lv. —", string.Empty, 0f),
                PassiveTrackCaption: BuildPassiveTrackCaption(),
                Overview: new TownCharacterSheetPanelViewState(titles.Overview, emptyRows),
                Loadout: new TownCharacterSheetPanelViewState(titles.Loadout, emptyRows),
                Passives: new TownCharacterSheetPanelViewState(titles.Passives, emptyRows),
                Synergy: new TownCharacterSheetPanelViewState(titles.Synergy, emptyRows),
                Progression: new TownCharacterSheetPanelViewState(titles.Progression, emptyRows));
        }

        var loadout = session.Profile.HeroLoadouts.FirstOrDefault(record =>
            string.Equals(record.HeroId, hero.HeroId, StringComparison.Ordinal));
        var progression = session.Profile.HeroProgressions.FirstOrDefault(record =>
            string.Equals(record.HeroId, hero.HeroId, StringComparison.Ordinal));
        var archetype = ResolveArchetype(hero.ArchetypeId);
        var baseline = ResolveBaseline(hero.ArchetypeId);
        var board = ResolvePassiveBoard(loadout?.PassiveBoardId ?? string.Empty, baseline, hero.ClassId);
        var characterName = _contentText.GetCharacterName(hero.CharacterId, hero.ArchetypeId);
        var archetypeName = _contentText.GetArchetypeName(hero.ArchetypeId);
        var roleLabel = _contentText.GetRoleName(string.Empty, archetype?.RoleTag ?? string.Empty);

        return new TownCharacterSheetViewState(
            HeroId: hero.HeroId,
            DisplayName: characterName,
            ArchetypeLabel: $"Lv. {progression?.Level ?? 1} / {archetypeName}",
            RoleLabel: roleLabel,
            FamilyKey: hero.ClassId,
            PortraitSprite: null,
            HeroRail: BuildHeroRail(session, hero.HeroId),
            Stats: BuildStatGrid(archetype),
            Skills: BuildSkillCards(hero, baseline),
            Equipment: BuildEquipmentSlots(session, hero),
            ProgressionNodes: BuildProgressionNodes(loadout, progression),
            LevelProgress: BuildLevelProgress(progression),
            PassiveTrackCaption: BuildPassiveTrackCaption(),
            Overview: new TownCharacterSheetPanelViewState(titles.Overview, BuildOverviewBody(session, hero, archetype)),
            Loadout: new TownCharacterSheetPanelViewState(titles.Loadout, BuildLoadoutBody(session, hero, baseline)),
            Passives: new TownCharacterSheetPanelViewState(titles.Passives, BuildPassivesBody(loadout, board, selectedNode)),
            Synergy: new TownCharacterSheetPanelViewState(titles.Synergy, BuildSynergyBody(session, hero, archetype, baseline)),
            Progression: new TownCharacterSheetPanelViewState(
                titles.Progression,
                BuildProgressionBody(session, hero, loadout, progression, selectedItem, retrainActiveCost, retrainPassiveCost, fullRetrainCost, dismissRefund)));
    }

    private (string Overview, string Loadout, string Passives, string Synergy, string Progression) BuildPanelTitles()
    {
        return (
            LocalizeTown("ui.town.sheet.overview", "Overview"),
            LocalizeTown("ui.town.sheet.loadout", "Loadout"),
            "스킬",
            LocalizeTown("ui.town.sheet.synergy", "Synergy"),
            LocalizeTown("ui.town.sheet.progression", "Progression"));
    }

    private IReadOnlyList<TownCharacterSheetHeroRailEntryViewState> BuildHeroRail(
        GameSessionState session,
        string selectedHeroId)
    {
        return session.Profile.Heroes
            .Take(5)
            .Select(hero => new TownCharacterSheetHeroRailEntryViewState(
                HeroId: hero.HeroId,
                DisplayName: _contentText.GetCharacterName(hero.CharacterId, hero.ArchetypeId),
                MetaLabel: _contentText.GetClassName(hero.ClassId),
                FamilyKey: hero.ClassId,
                PortraitSprite: null,
                IsSelected: string.Equals(hero.HeroId, selectedHeroId, StringComparison.Ordinal)))
            .ToList();
    }

    private static IReadOnlyList<TownCharacterSheetStatViewState> BuildStatGrid(UnitArchetypeDefinition? archetype)
    {
        if (archetype == null)
        {
            return Array.Empty<TownCharacterSheetStatViewState>();
        }

        return new[]
        {
            BuildStat("hp", "HP", archetype.BaseMaxHealth, "vital"),
            BuildStat("armor", "ARM", archetype.BaseArmor, "guard"),
            BuildStat("resist", "RES", archetype.BaseResist, "guard"),
            BuildStat("phys", "PWR", archetype.BasePhysPower, "attack"),
            BuildStat("magic", "MAG", archetype.BaseMagPower, "magic"),
            BuildStat("speed", "SPD", archetype.BaseSpeed, "tempo"),
            BuildStat("range", "RNG", archetype.BaseAttackRange, "tempo"),
            BuildStat("haste", "HST", archetype.BaseSkillHaste, "magic"),
        };
    }

    private IReadOnlyList<TownCharacterSheetSkillCardViewState> BuildSkillCards(
        HeroInstanceRecord hero,
        LaunchCoreUnitBaseline? baseline)
    {
        return new[]
            {
                BuildSkillCard("SIG A", baseline?.SignatureActiveId ?? string.Empty, isSignature: true),
                BuildSkillCard("SIG P", baseline?.SignaturePassiveId ?? string.Empty, isSignature: true),
                BuildSkillCard("FLX A", FirstNonEmpty(hero.FlexActiveId, baseline?.FlexActiveId), isSignature: false),
                BuildSkillCard("FLX P", FirstNonEmpty(hero.FlexPassiveId, baseline?.FlexPassiveId), isSignature: false),
            }
            .ToList();
    }

    private IReadOnlyList<TownCharacterSheetEquipmentSlotViewState> BuildEquipmentSlots(
        GameSessionState session,
        HeroInstanceRecord hero)
    {
        var equippedItems = GetEquippedItems(session, hero);
        return new[]
        {
            BuildEquipmentSlot(equippedItems, ItemSlotType.Weapon, LocalizeTown("ui.town.sheet.weapon", "Weapon"), "weapon"),
            BuildEquipmentSlot(equippedItems, ItemSlotType.Armor, LocalizeTown("ui.town.sheet.armor", "Armor"), "armor"),
            BuildEquipmentSlot(equippedItems, ItemSlotType.Accessory, LocalizeTown("ui.town.sheet.accessory", "Accessory"), "accessory"),
        };
    }

    private IReadOnlyList<TownCharacterSheetProgressionNodeViewState> BuildProgressionNodes(
        HeroLoadoutRecord? loadout,
        HeroProgressionRecord? progression)
    {
        IReadOnlyList<string> selectedIds = loadout?.SelectedPassiveNodeIds != null
            ? loadout.SelectedPassiveNodeIds
            : Array.Empty<string>();
        IReadOnlyList<string> unlockedIds = progression?.UnlockedPassiveNodeIds != null
            ? progression.UnlockedPassiveNodeIds
            : Array.Empty<string>();
        var nodes = new List<TownCharacterSheetProgressionNodeViewState>();
        // 트랙 슬롯 수는 패시브 보드 최대 활성 노드 수와 일치시킨다(하드코딩 6 → 5 정합).
        var slotCount = PassiveBoardSelectionValidator.MaxActiveNodeCount;

        foreach (var nodeId in selectedIds.Where(id => !string.IsNullOrWhiteSpace(id)).Take(slotCount))
        {
            nodes.Add(new TownCharacterSheetProgressionNodeViewState(FormatPassiveNodeName(nodeId), "active"));
        }

        foreach (var nodeId in unlockedIds
                     .Where(id => !string.IsNullOrWhiteSpace(id))
                     .Where(id => !selectedIds.Contains(id, StringComparer.Ordinal))
                     .Take(Math.Max(0, slotCount - nodes.Count)))
        {
            nodes.Add(new TownCharacterSheetProgressionNodeViewState(FormatPassiveNodeName(nodeId), "unlocked"));
        }

        // 빈 잠금 슬롯은 숫자(level/step으로 오해)가 아니라 중립 글리프로 — 미해금 패시브 슬롯임을 표시.
        while (nodes.Count < slotCount)
        {
            nodes.Add(new TownCharacterSheetProgressionNodeViewState("·", "locked"));
        }

        return nodes;
    }

    private TownCharacterSheetSkillCardViewState BuildSkillCard(
        string slotLabel,
        string skillId,
        bool isSignature)
    {
        if (string.IsNullOrWhiteSpace(skillId))
        {
            return new TownCharacterSheetSkillCardViewState(
                SkillId: string.Empty,
                Name: CommonNone(),
                MetaLabel: slotLabel,
                Description: "No skill assigned.",
                IconKey: string.Empty,
                IconSprite: null,
                IsSignature: isSignature);
        }

        var description = _contentText.GetSkillDescription(skillId);
        return new TownCharacterSheetSkillCardViewState(
            SkillId: skillId,
            Name: ResolveSkillName(skillId),
            MetaLabel: FormatSkillMeta(slotLabel, skillId),
            Description: string.IsNullOrWhiteSpace(description) ? FormatSkillMeta(slotLabel, skillId) : description,
            IconKey: ResolveSkillIconKey(skillId),
            IconSprite: null,
            IsSignature: isSignature);
    }

    private TownCharacterSheetEquipmentSlotViewState BuildEquipmentSlot(
        IReadOnlyList<InventoryItemRecord> equippedItems,
        ItemSlotType slotType,
        string slotLabel,
        string fallbackIconKey)
    {
        var item = equippedItems.FirstOrDefault(candidate =>
            _lookup.TryGetItemDefinition(candidate.ItemBaseId, out var definition)
            && definition.SlotType == slotType);
        if (item == null)
        {
            return new TownCharacterSheetEquipmentSlotViewState(
                SlotKey: fallbackIconKey,
                SlotLabel: slotLabel,
                ItemLabel: CommonNone(),
                MetaLabel: LocalizeTown("ui.town.recruit.empty", "Empty Slot"),
                IconKey: fallbackIconKey,
                IconSprite: null,
                IsFilled: false);
        }

        var meta = $"{item.AffixIds?.Count ?? 0} affixes";
        var iconKey = item.ItemBaseId;
        if (_lookup.TryGetItemDefinition(item.ItemBaseId, out var itemDefinition))
        {
            iconKey = string.IsNullOrWhiteSpace(itemDefinition.IconId) ? item.ItemBaseId : itemDefinition.IconId;
            meta = $"{itemDefinition.RarityTier} / {ResolveItemFamilyLabel(itemDefinition)} / {meta}";
        }

        return new TownCharacterSheetEquipmentSlotViewState(
            SlotKey: fallbackIconKey,
            SlotLabel: slotLabel,
            ItemLabel: _contentText.GetItemName(item.ItemBaseId),
            MetaLabel: meta,
            IconKey: iconKey,
            IconSprite: null,
            IsFilled: true);
    }

    private IReadOnlyList<TownCharacterSheetPanelRowViewState> BuildOverviewBody(
        GameSessionState session,
        HeroInstanceRecord hero,
        UnitArchetypeDefinition? archetype)
    {
        // 이름은 상단 타이틀에 이미 있으므로 개요 본문의 중복 헤딩 행은 제거.
        return new[]
        {
            Row(
                "ui.town.sheet.race_class",
                "Race / Class",
                $"{_contentText.GetRaceName(hero.RaceId)} / {_contentText.GetClassName(hero.ClassId)}"),
            Row(
                "ui.town.sheet.role",
                "Role",
                _contentText.GetRoleName(string.Empty, archetype?.RoleTag ?? string.Empty)),
            Row(
                "ui.town.sheet.role_family",
                "Role Family",
                _contentText.GetRoleFamilyName(hero.ClassId)),
            Row(
                "ui.town.sheet.traits",
                "Traits",
                $"+{FormatTraitName(hero.ArchetypeId, hero.PositiveTraitId)} / -{FormatTraitName(hero.ArchetypeId, hero.NegativeTraitId)}"),
            Row("ui.town.sheet.posture", "Posture", LocalizePosture(session.SelectedTeamPosture)),
            Row(
                "ui.town.sheet.tactic",
                "Tactic",
                _contentText.GetTeamTacticName(ResolveCurrentTeamTacticId(session))),
        };
    }

    private IReadOnlyList<TownCharacterSheetPanelRowViewState> BuildLoadoutBody(
        GameSessionState session,
        HeroInstanceRecord hero,
        LaunchCoreUnitBaseline? baseline)
    {
        var equippedItems = GetEquippedItems(session, hero);
        return new[]
        {
            Row("ui.town.sheet.weapon", "Weapon", FormatItemBySlot(equippedItems, ItemSlotType.Weapon)),
            Row("ui.town.sheet.armor", "Armor", FormatItemBySlot(equippedItems, ItemSlotType.Armor)),
            Row("ui.town.sheet.accessory", "Accessory", FormatItemBySlot(equippedItems, ItemSlotType.Accessory)),
            Row("ui.town.sheet.basic_attack", "Basic Attack", ResolveBasicAttackName()),
            Row("ui.town.sheet.signature_active", "Signature Active", ResolveSkillName(baseline?.SignatureActiveId ?? string.Empty)),
            Row("ui.town.sheet.signature_passive", "Signature Passive", ResolveSkillName(baseline?.SignaturePassiveId ?? string.Empty)),
            Row("ui.town.sheet.flex_active", "Flex Active", ResolveSkillName(hero.FlexActiveId)),
            Row("ui.town.sheet.flex_passive", "Flex Passive", ResolveSkillName(hero.FlexPassiveId)),
        };
    }

    private IReadOnlyList<TownCharacterSheetPanelRowViewState> BuildPassivesBody(
        HeroLoadoutRecord? loadout,
        PassiveBoardDefinition? board,
        PassiveNodeDefinition? selectedNode)
    {
        IReadOnlyList<string> selectedNodeIds = loadout == null
            ? Array.Empty<string>()
            : loadout.SelectedPassiveNodeIds;
        return new[]
        {
            Row("ui.town.sheet.board", "Board", FormatPassiveBoardName(board?.Id ?? string.Empty)),
            Row("ui.town.sheet.active_nodes", "Active Nodes", FormatNodeList(selectedNodeIds)),
            Row("ui.town.sheet.highlighted_node", "Highlighted Node", FormatPassiveNodeName(selectedNode?.Id ?? string.Empty)),
            Row("ui.town.sheet.node_count", "Node Count", $"{selectedNodeIds.Count}/{PassiveBoardSelectionValidator.MaxActiveNodeCount}"),
            Row(
                "ui.town.sheet.keystone",
                "Keystone",
                selectedNodeIds.Any(id => id.Contains("_keystone_", StringComparison.Ordinal))
                    ? LocalizeTown("ui.town.sheet.state.active", "Active")
                    : LocalizeTown("ui.town.sheet.state.inactive", "Inactive")),
        };
    }

    private IReadOnlyList<TownCharacterSheetPanelRowViewState> BuildSynergyBody(
        GameSessionState session,
        HeroInstanceRecord hero,
        UnitArchetypeDefinition? archetype,
        LaunchCoreUnitBaseline? baseline)
    {
        var squadHeroes = session.Profile.Heroes
            .Where(candidate => session.ExpeditionSquadHeroIds.Contains(candidate.HeroId, StringComparer.Ordinal))
            .ToList();
        var rows = new List<TownCharacterSheetPanelRowViewState>
        {
            Row(
                "ui.town.sheet.squad",
                "Squad",
                squadHeroes.Count == 0
                    ? LocalizeTown("ui.town.sheet.state.empty", "Empty")
                    : $"{squadHeroes.Count} {LocalizeTown("ui.town.sheet.members", "members")}"),
        };

        AddSynergyRow(rows, squadHeroes, BuildSynergyId(hero.RaceId), hero.RaceId, isClassFamily: false);
        AddSynergyRow(rows, squadHeroes, BuildSynergyId(hero.ClassId), hero.ClassId, isClassFamily: true);
        rows.Add(Row(
            "ui.town.sheet.expected_families",
            "Expected Families",
            string.Join(", ", ResolveExpectedSynergies(hero, baseline))));
        rows.Add(Row("ui.town.sheet.counter_hints", "Counter Hints", FormatCounterTools(archetype)));
        rows.Add(Row("ui.town.sheet.soft_weakness", "Soft Weakness", FormatWeakness(hero.ClassId)));
        return rows;
    }

    private IReadOnlyList<TownCharacterSheetPanelRowViewState> BuildProgressionBody(
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
        return new[]
        {
            Row(
                "ui.town.sheet.recruit",
                "Recruit",
                $"{LocalizeRecruitTier(hero.RecruitTier)} / {LocalizeRecruitSource(hero.RecruitSource)}"),
            Row("ui.town.sheet.retrain_state", "Retrain State", FormatRetrainState(hero.RetrainState)),
            Row(
                "ui.town.sheet.retrain_costs",
                "Retrain Costs",
                $"{LocalizeTown("ui.town.sheet.cost.active", "active")} {retrainActiveCost}, " +
                $"{LocalizeTown("ui.town.sheet.cost.passive", "passive")} {retrainPassiveCost}, " +
                $"{LocalizeTown("ui.town.sheet.cost.full", "full")} {fullRetrainCost} {LocalizeTown("ui.town.sheet.currency.echo", "Echo")}"),
            Row(
                "ui.town.sheet.dismiss_refund",
                "Dismiss Refund",
                $"+{dismissRefund.GoldRefund} {LocalizeTown("ui.town.sheet.currency.gold", "Gold")} / " +
                $"+{dismissRefund.EchoRefund} {LocalizeTown("ui.town.sheet.currency.echo", "Echo")}"),
            Row(
                "ui.town.sheet.refit_preview",
                "Refit Preview",
                selectedItem == null
                    ? CommonNone()
                    : $"{MetaBalanceDefaults.RefitEchoCost} {LocalizeTown("ui.town.sheet.currency.echo", "Echo")} / {FormatItem(selectedItem)}"),
            Row("ui.town.sheet.blueprint_permanent", "Blueprint Permanent", FormatAugmentName(GetEquippedPermanentAugmentId(session))),
            Row("ui.town.sheet.unlocked_permanents", "Unlocked Permanents", FormatAugmentList(session.Profile.UnlockedPermanentAugmentIds)),
            Row("ui.town.sheet.passive_progress", "Passive Progress", FormatPassiveProgress(loadout, progression)),
        };
    }

    private void AddSynergyRow(
        List<TownCharacterSheetPanelRowViewState> rows,
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
        rows.Add(new TownCharacterSheetPanelRowViewState(
            _contentText.GetSynergyName(synergyId),
            $"{count} {LocalizeTown("ui.town.sheet.units", "units")} ({minor}/{major}) {reachedText}"));
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

    private string ResolveSkillIconKey(string skillId)
    {
        if (_lookup.TryGetSkillDefinition(skillId, out var skill) && !string.IsNullOrWhiteSpace(skill.IconId))
        {
            return skill.IconId;
        }

        return skillId;
    }

    private string FormatSkillMeta(string slotLabel, string skillId)
    {
        if (!_lookup.TryGetSkillDefinition(skillId, out var skill))
        {
            return slotLabel;
        }

        var cooldown = skill.BaseCooldownSeconds > 0.01f
            ? $"{skill.BaseCooldownSeconds:0.#}s"
            : "Always";
        return $"{slotLabel} / {skill.Kind} / {skill.TargetRule} / {cooldown}";
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

    private static TownCharacterSheetStatViewState BuildStat(string key, string label, float value, string tone)
    {
        return new TownCharacterSheetStatViewState(
            Key: key,
            Label: label,
            Value: FormatStatValue(value),
            DeltaLabel: string.Empty,
            Tone: tone);
    }

    private static string FormatStatValue(float value)
    {
        return Math.Abs(value % 1f) < 0.001f
            ? value.ToString("0")
            : value.ToString("0.#");
    }

    private static string ResolveItemFamilyLabel(ItemBaseDefinition item)
    {
        if (!string.IsNullOrWhiteSpace(item.WeaponFamilyTag))
        {
            return item.WeaponFamilyTag;
        }

        if (!string.IsNullOrWhiteSpace(item.ItemFamilyTag))
        {
            return item.ItemFamilyTag;
        }

        return item.SlotType.ToString();
    }

    private static string FirstNonEmpty(params string?[] values)
    {
        return values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? string.Empty;
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

    private TownCharacterSheetPanelRowViewState Row(string key, string fallbackLabel, string value, string tone = "")
    {
        return new TownCharacterSheetPanelRowViewState(LocalizeTown(key, fallbackLabel), value, tone);
    }

    private static TownCharacterSheetPanelRowViewState Note(string value)
    {
        return new TownCharacterSheetPanelRowViewState(string.Empty, value, string.Empty);
    }

    private static TownCharacterSheetLevelProgressViewState BuildLevelProgress(HeroProgressionRecord? progression)
    {
        var level = progression?.Level ?? 1;
        var exp = progression?.Experience ?? 0;
        var next = HeroProgressionCurve.ExperienceToNextLevel(level);
        var fraction = next > 0 ? (float)exp / next : 0f;
        fraction = fraction < 0f ? 0f : (fraction > 1f ? 1f : fraction);
        return new TownCharacterSheetLevelProgressViewState($"Lv. {level}", $"{exp} / {next}", fraction);
    }

    private string BuildPassiveTrackCaption()
    {
        return LocalizeTown(
            "ui.town.selected_hero.passive_nodes",
            "Passive Nodes: {0}",
            PassiveBoardSelectionValidator.MaxActiveNodeCount);
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
