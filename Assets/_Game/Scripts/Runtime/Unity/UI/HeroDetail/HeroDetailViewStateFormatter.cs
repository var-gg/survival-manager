using System;
using System.Collections.Generic;
using System.Linq;
using SM.Content.Definitions;
using SM.Core.Content;
using SM.Core.Contracts;
using SM.Persistence.Abstractions.Models;

namespace SM.Unity.UI.HeroDetail;

/// <summary>
/// 재사용 HeroDetail 공통 상세 surface의 UI read model 빌더.
/// 기존 <see cref="Town.TownCharacterSheetFormatter"/>와 동일 소스(GameSessionState /
/// HeroInstanceRecord / ICombatContentLookup / LaunchCoreRosterBaselineCatalog)를 읽되,
/// v0.5 3-rail + 4-slot 위계 형태로 재구성한다.
/// 범위 밖(런타임 데이터 부재): per-hero stance, xpRatio 곡선, 레벨/장비/affix 누적 최종 스탯
/// (stat은 archetype base, tier는 RecruitTier 표기). 상세는 [[hero-detail-uitk-adaptation]] 참조.
/// </summary>
public sealed class HeroDetailViewStateFormatter
{
    private readonly ContentTextResolver _contentText;
    private readonly ICombatContentLookup _lookup;
    private readonly LaunchCoreRosterBaselineCatalog _baselineCatalog;

    public HeroDetailViewStateFormatter(ContentTextResolver contentText, ICombatContentLookup lookup)
    {
        _contentText = contentText ?? throw new ArgumentNullException(nameof(contentText));
        _lookup = lookup ?? throw new ArgumentNullException(nameof(lookup));
        _baselineCatalog = new LaunchCoreRosterBaselineCatalog(lookup);
    }

    public HeroDetailViewState Build(GameSessionState session, HeroInstanceRecord? hero)
    {
        if (session == null || hero == null)
        {
            return HeroDetailViewState.Empty;
        }

        var progression = session.Profile.HeroProgressions.FirstOrDefault(record =>
            string.Equals(record.HeroId, hero.HeroId, StringComparison.Ordinal));
        var archetype = _lookup.TryGetArchetype(hero.ArchetypeId, out var resolvedArchetype) ? resolvedArchetype : null;
        var baseline = _baselineCatalog.TryGetUnitBaseline(hero.ArchetypeId, out var resolvedBaseline) ? resolvedBaseline : null;
        IReadOnlyList<string> unlockedSkillIds = progression?.UnlockedSkillIds ?? (IReadOnlyList<string>)Array.Empty<string>();

        return new HeroDetailViewState(
            HeroId: hero.HeroId,
            DisplayName: _contentText.GetCharacterName(hero.CharacterId, hero.ArchetypeId),
            ArchetypeLabel: _contentText.GetArchetypeName(hero.ArchetypeId),
            RoleLabel: _contentText.GetRoleName(string.Empty, archetype?.RoleTag ?? string.Empty),
            FamilyKey: hero.ClassId,
            TierLabel: hero.RecruitTier.ToString(),
            LevelLabel: $"Lv. {progression?.Level ?? 1}",
            PortraitSprite: null,
            Stats: BuildStats(archetype),
            SkillSlots: BuildSkillSlots(hero, baseline, unlockedSkillIds),
            Equipment: BuildEquipment(session, hero),
            Traits: BuildTraits(hero));
    }

    private static IReadOnlyList<HeroDetailStatViewState> BuildStats(UnitArchetypeDefinition? archetype)
    {
        if (archetype == null)
        {
            return Array.Empty<HeroDetailStatViewState>();
        }

        return new[]
        {
            Stat("hp", "HP", archetype.BaseMaxHealth, "vital"),
            Stat("armor", "ARM", archetype.BaseArmor, "guard"),
            Stat("resist", "RES", archetype.BaseResist, "guard"),
            Stat("phys", "PWR", archetype.BasePhysPower, "attack"),
            Stat("magic", "MAG", archetype.BaseMagPower, "magic"),
            Stat("speed", "SPD", archetype.BaseSpeed, "tempo"),
            Stat("range", "RNG", archetype.BaseAttackRange, "tempo"),
            Stat("haste", "HST", archetype.BaseSkillHaste, "magic"),
        };
    }

    private IReadOnlyList<HeroDetailSkillSlotViewState> BuildSkillSlots(
        HeroInstanceRecord hero,
        LaunchCoreUnitBaseline? baseline,
        IReadOnlyList<string> unlockedSkillIds)
    {
        return new[]
        {
            BuildSlot(
                ActionSlotKind.SignatureActive,
                baseline?.SignatureActiveId ?? string.Empty,
                heroHasExplicitFlexChoice: false,
                unlockedSkillIds),
            BuildSlot(
                ActionSlotKind.FlexActive,
                FirstNonEmpty(hero.FlexActiveId, baseline?.FlexActiveId),
                heroHasExplicitFlexChoice: !string.IsNullOrWhiteSpace(hero.FlexActiveId),
                unlockedSkillIds),
            BuildSlot(
                ActionSlotKind.SignaturePassive,
                baseline?.SignaturePassiveId ?? string.Empty,
                heroHasExplicitFlexChoice: false,
                unlockedSkillIds),
            BuildSlot(
                ActionSlotKind.FlexPassive,
                FirstNonEmpty(hero.FlexPassiveId, baseline?.FlexPassiveId),
                heroHasExplicitFlexChoice: !string.IsNullOrWhiteSpace(hero.FlexPassiveId),
                unlockedSkillIds),
        };
    }

    private HeroDetailSkillSlotViewState BuildSlot(
        ActionSlotKind slotKind,
        string skillId,
        bool heroHasExplicitFlexChoice,
        IReadOnlyList<string> unlockedSkillIds)
    {
        var isUnlocked = !string.IsNullOrWhiteSpace(skillId)
                         && unlockedSkillIds.Contains(skillId, StringComparer.Ordinal);
        var classified = HeroDetailSkillSlotClassifier.Classify(slotKind, heroHasExplicitFlexChoice, isUnlocked);
        var isPassive = slotKind is ActionSlotKind.SignaturePassive or ActionSlotKind.FlexPassive;
        var metaLabel = SlotMetaLabel(slotKind);

        if (string.IsNullOrWhiteSpace(skillId))
        {
            return new HeroDetailSkillSlotViewState(
                SlotKind: classified,
                SkillId: string.Empty,
                Name: "—",
                MetaLabel: metaLabel,
                Description: string.Empty,
                IconKey: string.Empty,
                IconState: HeroDetailIconState.Missing,
                CooldownText: string.Empty,
                IsPassive: isPassive);
        }

        var iconKey = skillId;
        var iconState = HeroDetailIconState.Fallback;
        var cooldownText = isPassive ? "Passive" : "—";
        if (_lookup.TryGetSkillDefinition(skillId, out var skill))
        {
            var hasIcon = !string.IsNullOrWhiteSpace(skill.IconId);
            iconKey = hasIcon ? skill.IconId : skillId;
            iconState = hasIcon ? HeroDetailIconState.Present : HeroDetailIconState.Fallback;
            if (skill.BaseCooldownSeconds > 0.01f)
            {
                cooldownText = $"{skill.BaseCooldownSeconds:0.#}s";
            }
        }

        return new HeroDetailSkillSlotViewState(
            SlotKind: classified,
            SkillId: skillId,
            Name: _contentText.GetSkillName(skillId),
            MetaLabel: metaLabel,
            Description: _contentText.GetSkillDescription(skillId),
            IconKey: iconKey,
            IconState: iconState,
            CooldownText: cooldownText,
            IsPassive: isPassive);
    }

    private IReadOnlyList<HeroDetailEquipSlotViewState> BuildEquipment(GameSessionState session, HeroInstanceRecord hero)
    {
        var equipped = session.Profile.Inventory
            .Where(item => string.Equals(item.EquippedHeroId, hero.HeroId, StringComparison.Ordinal)
                           || hero.EquippedItemIds.Contains(item.ItemInstanceId, StringComparer.Ordinal))
            .ToList();

        return new[]
        {
            EquipSlot(equipped, ItemSlotType.Weapon, "Weapon"),
            EquipSlot(equipped, ItemSlotType.Armor, "Armor"),
            EquipSlot(equipped, ItemSlotType.Accessory, "Accessory"),
        };
    }

    private HeroDetailEquipSlotViewState EquipSlot(
        IReadOnlyList<InventoryItemRecord> equipped,
        ItemSlotType slotType,
        string slotLabel)
    {
        var slotKey = slotType.ToString().ToLowerInvariant();
        var item = equipped.FirstOrDefault(candidate =>
            _lookup.TryGetItemDefinition(candidate.ItemBaseId, out var definition) && definition.SlotType == slotType);
        if (item == null)
        {
            return new HeroDetailEquipSlotViewState(slotKey, slotLabel, "—", "Empty", slotKey, IsFilled: false);
        }

        var iconKey = item.ItemBaseId;
        var meta = $"{item.AffixIds?.Count ?? 0} affixes";
        if (_lookup.TryGetItemDefinition(item.ItemBaseId, out var itemDefinition))
        {
            iconKey = string.IsNullOrWhiteSpace(itemDefinition.IconId) ? item.ItemBaseId : itemDefinition.IconId;
            meta = $"{itemDefinition.RarityTier} / {meta}";
        }

        return new HeroDetailEquipSlotViewState(
            slotKey,
            slotLabel,
            _contentText.GetItemName(item.ItemBaseId),
            meta,
            iconKey,
            IsFilled: true);
    }

    private IReadOnlyList<HeroDetailTraitViewState> BuildTraits(HeroInstanceRecord hero)
    {
        var traits = new List<HeroDetailTraitViewState>(2);
        if (!string.IsNullOrWhiteSpace(hero.PositiveTraitId))
        {
            traits.Add(new HeroDetailTraitViewState(
                "boon", _contentText.GetTraitName(hero.ArchetypeId, hero.PositiveTraitId), string.Empty));
        }

        if (!string.IsNullOrWhiteSpace(hero.NegativeTraitId))
        {
            traits.Add(new HeroDetailTraitViewState(
                "bane", _contentText.GetTraitName(hero.ArchetypeId, hero.NegativeTraitId), string.Empty));
        }

        return traits;
    }

    private static HeroDetailStatViewState Stat(string key, string label, float value, string tone)
    {
        var text = Math.Abs(value % 1f) < 0.001f ? value.ToString("0") : value.ToString("0.#");
        return new HeroDetailStatViewState(key, label, text, tone);
    }

    private static string SlotMetaLabel(ActionSlotKind slotKind)
    {
        return slotKind switch
        {
            ActionSlotKind.SignatureActive => "SIG · ACTIVE",
            ActionSlotKind.SignaturePassive => "SIG · PASSIVE",
            ActionSlotKind.FlexActive => "FLEX · ACTIVE",
            ActionSlotKind.FlexPassive => "FLEX · PASSIVE",
            _ => slotKind.ToString(),
        };
    }

    private static string FirstNonEmpty(params string?[] values)
    {
        return values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? string.Empty;
    }
}
