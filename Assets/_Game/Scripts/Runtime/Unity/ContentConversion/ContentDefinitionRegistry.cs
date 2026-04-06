using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using SM.Combat.Model;
using SM.Content.Definitions;
using SM.Core.Contracts;
using SM.Meta.Model;
using UnityEngine;
using static SM.Unity.ContentConversion.ContentConversionShared;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace SM.Unity.ContentConversion;

internal sealed class ContentDefinitionRegistry
{
    private readonly Dictionary<string, UnitArchetypeDefinition> _archetypeDefinitions = new(StringComparer.Ordinal);
    private readonly Dictionary<string, TraitPoolDefinition> _traitPools = new(StringComparer.Ordinal);
    private readonly Dictionary<string, RaceDefinition> _raceDefinitions = new(StringComparer.Ordinal);
    private readonly Dictionary<string, ClassDefinition> _classDefinitions = new(StringComparer.Ordinal);
    private readonly Dictionary<string, CharacterDefinition> _characterDefinitions = new(StringComparer.Ordinal);
    private readonly Dictionary<string, ItemBaseDefinition> _itemDefinitions = new(StringComparer.Ordinal);
    private readonly Dictionary<string, AffixDefinition> _affixDefinitions = new(StringComparer.Ordinal);
    private readonly Dictionary<string, AugmentDefinition> _augmentDefinitions = new(StringComparer.Ordinal);
    private readonly Dictionary<string, SkillDefinitionAsset> _skillDefinitions = new(StringComparer.Ordinal);
    private readonly Dictionary<string, TeamTacticDefinition> _teamTacticDefinitions = new(StringComparer.Ordinal);
    private readonly Dictionary<string, RoleInstructionDefinition> _roleInstructionDefinitions = new(StringComparer.Ordinal);
    private readonly Dictionary<string, PassiveNodeDefinition> _passiveNodeDefinitions = new(StringComparer.Ordinal);
    private readonly Dictionary<string, SynergyDefinition> _synergyDefinitions = new(StringComparer.Ordinal);
    private readonly Dictionary<string, CampaignChapterDefinition> _campaignChapterDefinitions = new(StringComparer.Ordinal);
    private readonly Dictionary<string, ExpeditionSiteDefinition> _expeditionSiteDefinitions = new(StringComparer.Ordinal);
    private readonly Dictionary<string, EncounterDefinition> _encounterDefinitions = new(StringComparer.Ordinal);
    private readonly Dictionary<string, EnemySquadTemplateDefinition> _enemySquadDefinitions = new(StringComparer.Ordinal);
    private readonly Dictionary<string, BossOverlayDefinition> _bossOverlayDefinitions = new(StringComparer.Ordinal);
    private readonly Dictionary<string, StatusFamilyDefinition> _statusFamilyDefinitions = new(StringComparer.Ordinal);
    private readonly Dictionary<string, CleanseProfileDefinition> _cleanseProfileDefinitions = new(StringComparer.Ordinal);
    private readonly Dictionary<string, ControlDiminishingRuleDefinition> _controlDiminishingDefinitions = new(StringComparer.Ordinal);
    private readonly Dictionary<string, RewardSourceDefinition> _rewardSourceDefinitions = new(StringComparer.Ordinal);
    private readonly Dictionary<string, DropTableDefinition> _dropTableDefinitions = new(StringComparer.Ordinal);
    private readonly Dictionary<string, LootBundleDefinition> _lootBundleDefinitions = new(StringComparer.Ordinal);
    private readonly Dictionary<string, TraitTokenDefinition> _traitTokenDefinitions = new(StringComparer.Ordinal);
    private FirstPlayableSliceDefinition? _firstPlayableSlice;
    private bool _loaded;

    internal IReadOnlyDictionary<string, UnitArchetypeDefinition> ArchetypeDefinitions => _archetypeDefinitions;
    internal IReadOnlyDictionary<string, TraitPoolDefinition> TraitPools => _traitPools;
    internal IReadOnlyDictionary<string, RaceDefinition> RaceDefinitions => _raceDefinitions;
    internal IReadOnlyDictionary<string, ClassDefinition> ClassDefinitions => _classDefinitions;
    internal IReadOnlyDictionary<string, CharacterDefinition> CharacterDefinitions => _characterDefinitions;
    internal IReadOnlyDictionary<string, ItemBaseDefinition> ItemDefinitions => _itemDefinitions;
    internal IReadOnlyDictionary<string, AffixDefinition> AffixDefinitions => _affixDefinitions;
    internal IReadOnlyDictionary<string, AugmentDefinition> AugmentDefinitions => _augmentDefinitions;
    internal IReadOnlyDictionary<string, SkillDefinitionAsset> SkillDefinitions => _skillDefinitions;
    internal IReadOnlyDictionary<string, TeamTacticDefinition> TeamTacticDefinitions => _teamTacticDefinitions;
    internal IReadOnlyDictionary<string, RoleInstructionDefinition> RoleInstructionDefinitions => _roleInstructionDefinitions;
    internal IReadOnlyDictionary<string, PassiveNodeDefinition> PassiveNodeDefinitions => _passiveNodeDefinitions;
    internal IReadOnlyDictionary<string, SynergyDefinition> SynergyDefinitions => _synergyDefinitions;
    internal IReadOnlyDictionary<string, CampaignChapterDefinition> CampaignChapterDefinitions => _campaignChapterDefinitions;
    internal IReadOnlyDictionary<string, ExpeditionSiteDefinition> ExpeditionSiteDefinitions => _expeditionSiteDefinitions;
    internal IReadOnlyDictionary<string, EncounterDefinition> EncounterDefinitions => _encounterDefinitions;
    internal IReadOnlyDictionary<string, EnemySquadTemplateDefinition> EnemySquadDefinitions => _enemySquadDefinitions;
    internal IReadOnlyDictionary<string, BossOverlayDefinition> BossOverlayDefinitions => _bossOverlayDefinitions;
    internal IReadOnlyDictionary<string, StatusFamilyDefinition> StatusFamilyDefinitions => _statusFamilyDefinitions;
    internal IReadOnlyDictionary<string, CleanseProfileDefinition> CleanseProfileDefinitions => _cleanseProfileDefinitions;
    internal IReadOnlyDictionary<string, ControlDiminishingRuleDefinition> ControlDiminishingDefinitions => _controlDiminishingDefinitions;
    internal IReadOnlyDictionary<string, RewardSourceDefinition> RewardSourceDefinitions => _rewardSourceDefinitions;
    internal IReadOnlyDictionary<string, DropTableDefinition> DropTableDefinitions => _dropTableDefinitions;
    internal IReadOnlyDictionary<string, LootBundleDefinition> LootBundleDefinitions => _lootBundleDefinitions;
    internal IReadOnlyDictionary<string, TraitTokenDefinition> TraitTokenDefinitions => _traitTokenDefinitions;
    internal FirstPlayableSliceDefinition? FirstPlayableSlice => _firstPlayableSlice;

    internal void EnsureLoaded()
    {
        if (_loaded)
        {
            return;
        }

        LoadContent();
        _loaded = true;
    }

    private void LoadContent()
    {
#if UNITY_EDITOR
        RequireEditorCanonicalSampleContentReady();
#endif
        var archetypes = LoadDefinitions<UnitArchetypeDefinition>("_Game/Content/Definitions/Archetypes", "Assets/Resources/_Game/Content/Definitions/Archetypes");
        var traitPools = LoadDefinitions<TraitPoolDefinition>("_Game/Content/Definitions/Traits", "Assets/Resources/_Game/Content/Definitions/Traits");
        var races = LoadDefinitions<RaceDefinition>("_Game/Content/Definitions/Races", "Assets/Resources/_Game/Content/Definitions/Races");
        var classes = LoadDefinitions<ClassDefinition>("_Game/Content/Definitions/Classes", "Assets/Resources/_Game/Content/Definitions/Classes");
        var characters = LoadDefinitions<CharacterDefinition>("_Game/Content/Definitions/Characters", "Assets/Resources/_Game/Content/Definitions/Characters");
        var items = LoadDefinitions<ItemBaseDefinition>("_Game/Content/Definitions/Items", "Assets/Resources/_Game/Content/Definitions/Items");
        var affixes = LoadDefinitions<AffixDefinition>("_Game/Content/Definitions/Affixes", "Assets/Resources/_Game/Content/Definitions/Affixes");
        var augments = LoadDefinitions<AugmentDefinition>("_Game/Content/Definitions/Augments", "Assets/Resources/_Game/Content/Definitions/Augments");
        var skills = LoadDefinitions<SkillDefinitionAsset>("_Game/Content/Definitions/Skills", "Assets/Resources/_Game/Content/Definitions/Skills");
        var teamTactics = LoadDefinitions<TeamTacticDefinition>("_Game/Content/Definitions/TeamTactics", "Assets/Resources/_Game/Content/Definitions/TeamTactics");
        var roleInstructions = LoadDefinitions<RoleInstructionDefinition>("_Game/Content/Definitions/RoleInstructions", "Assets/Resources/_Game/Content/Definitions/RoleInstructions");
        var passiveNodes = LoadDefinitions<PassiveNodeDefinition>("_Game/Content/Definitions/PassiveNodes", "Assets/Resources/_Game/Content/Definitions/PassiveNodes");
        var synergies = LoadDefinitions<SynergyDefinition>("_Game/Content/Definitions/Synergies", "Assets/Resources/_Game/Content/Definitions/Synergies");
        var campaignChapters = LoadDefinitions<CampaignChapterDefinition>("_Game/Content/Definitions/CampaignChapters", "Assets/Resources/_Game/Content/Definitions/CampaignChapters");
        var expeditionSites = LoadDefinitions<ExpeditionSiteDefinition>("_Game/Content/Definitions/ExpeditionSites", "Assets/Resources/_Game/Content/Definitions/ExpeditionSites");
        var encounters = LoadDefinitions<EncounterDefinition>("_Game/Content/Definitions/Encounters", "Assets/Resources/_Game/Content/Definitions/Encounters");
        var enemySquads = LoadDefinitions<EnemySquadTemplateDefinition>("_Game/Content/Definitions/EnemySquads", "Assets/Resources/_Game/Content/Definitions/EnemySquads");
        var bossOverlays = LoadDefinitions<BossOverlayDefinition>("_Game/Content/Definitions/BossOverlays", "Assets/Resources/_Game/Content/Definitions/BossOverlays");
        var statusFamilies = LoadDefinitions<StatusFamilyDefinition>("_Game/Content/Definitions/StatusFamilies", "Assets/Resources/_Game/Content/Definitions/StatusFamilies");
        var cleanseProfiles = LoadDefinitions<CleanseProfileDefinition>("_Game/Content/Definitions/CleanseProfiles", "Assets/Resources/_Game/Content/Definitions/CleanseProfiles");
        var controlDiminishingRules = LoadDefinitions<ControlDiminishingRuleDefinition>("_Game/Content/Definitions/ControlDiminishingRules", "Assets/Resources/_Game/Content/Definitions/ControlDiminishingRules");
        var rewardSources = LoadDefinitions<RewardSourceDefinition>("_Game/Content/Definitions/RewardSources", "Assets/Resources/_Game/Content/Definitions/RewardSources");
        var dropTables = LoadDefinitions<DropTableDefinition>("_Game/Content/Definitions/DropTables", "Assets/Resources/_Game/Content/Definitions/DropTables");
        var lootBundles = LoadDefinitions<LootBundleDefinition>("_Game/Content/Definitions/LootBundles", "Assets/Resources/_Game/Content/Definitions/LootBundles");
        var traitTokens = LoadDefinitions<TraitTokenDefinition>("_Game/Content/Definitions/TraitTokens", "Assets/Resources/_Game/Content/Definitions/TraitTokens");
        var firstPlayableSliceAssets = LoadDefinitions<FirstPlayableSliceDefinitionAsset>("_Game/Content/Definitions/FirstPlayable", "Assets/Resources/_Game/Content/Definitions/FirstPlayable");

        var requiresFileFallback =
            characters.Length == 0 ||
            archetypes.Length == 0 ||
            skills.Length == 0 ||
            campaignChapters.Length == 0 ||
            expeditionSites.Length == 0 ||
            encounters.Length == 0 ||
            enemySquads.Length == 0 ||
            bossOverlays.Length == 0 ||
            rewardSources.Length == 0 ||
            dropTables.Length == 0 ||
            lootBundles.Length == 0;

        if (requiresFileFallback)
        {
            if (!RuntimeCombatContentFileParser.TryLoad(out var parsed, out var parseError))
            {
                throw new InvalidOperationException($"전투 archetype 정의를 Resources에서 찾을 수 없습니다. {parseError}");
            }

            if (traitPools.Length == 0) traitPools = parsed.TraitPools.ToArray();
            if (races.Length == 0) races = parsed.Races.ToArray();
            if (classes.Length == 0) classes = parsed.Classes.ToArray();
            if (characters.Length == 0) characters = parsed.Characters.ToArray();
            if (archetypes.Length == 0) archetypes = parsed.Archetypes.ToArray();
            if (skills.Length == 0) skills = parsed.Skills.ToArray();
            if (items.Length == 0) items = parsed.Items.ToArray();
            if (affixes.Length == 0) affixes = parsed.Affixes.ToArray();
            if (augments.Length == 0) augments = parsed.Augments.ToArray();
            if (teamTactics.Length == 0) teamTactics = parsed.TeamTactics.ToArray();
            if (roleInstructions.Length == 0) roleInstructions = parsed.RoleInstructions.ToArray();
            if (passiveNodes.Length == 0) passiveNodes = parsed.PassiveNodes.ToArray();
            if (synergies.Length == 0) synergies = parsed.Synergies.ToArray();
            if (campaignChapters.Length == 0) campaignChapters = parsed.CampaignChapters.ToArray();
            if (expeditionSites.Length == 0) expeditionSites = parsed.ExpeditionSites.ToArray();
            if (encounters.Length == 0) encounters = parsed.Encounters.ToArray();
            if (enemySquads.Length == 0) enemySquads = parsed.EnemySquads.ToArray();
            if (bossOverlays.Length == 0) bossOverlays = parsed.BossOverlays.ToArray();
            if (statusFamilies.Length == 0) statusFamilies = parsed.StatusFamilies.ToArray();
            if (cleanseProfiles.Length == 0) cleanseProfiles = parsed.CleanseProfiles.ToArray();
            if (controlDiminishingRules.Length == 0) controlDiminishingRules = parsed.ControlDiminishingRules.ToArray();
            if (rewardSources.Length == 0) rewardSources = parsed.RewardSources.ToArray();
            if (dropTables.Length == 0) dropTables = parsed.DropTables.ToArray();
            if (lootBundles.Length == 0) lootBundles = parsed.LootBundles.ToArray();
            if (traitTokens.Length == 0) traitTokens = parsed.TraitTokens.ToArray();
        }

        ClearAll();

        foreach (var race in races.Where(d => d != null && !string.IsNullOrWhiteSpace(d.Id)))
            _raceDefinitions[race.Id] = race;
        foreach (var @class in classes.Where(d => d != null && !string.IsNullOrWhiteSpace(d.Id)))
            _classDefinitions[@class.Id] = @class;
        foreach (var character in characters.Where(d => d != null && !string.IsNullOrWhiteSpace(d.Id)))
            _characterDefinitions[character.Id] = character;
        foreach (var archetype in archetypes.Where(d => d != null && !string.IsNullOrWhiteSpace(d.Id)))
            _archetypeDefinitions[archetype.Id] = archetype;

        foreach (var traitPool in traitPools.Where(definition => definition != null))
        {
            var archetypeId = ResolveTraitPoolArchetypeId(traitPool);
            if (!string.IsNullOrWhiteSpace(archetypeId))
                _traitPools[archetypeId] = traitPool;
        }

        foreach (var archetype in archetypes.Where(definition => definition != null && definition.TraitPool != null))
        {
            var archetypeId = ResolveTraitPoolArchetypeId(archetype.TraitPool);
            if (!string.IsNullOrWhiteSpace(archetypeId))
                _traitPools[archetypeId] = archetype.TraitPool;
        }

        foreach (var item in items.Where(d => d != null && !string.IsNullOrWhiteSpace(d.Id)))
            _itemDefinitions[item.Id] = item;
        foreach (var affix in affixes.Where(d => d != null && !string.IsNullOrWhiteSpace(d.Id)))
            _affixDefinitions[affix.Id] = affix;
        foreach (var augment in augments.Where(d => d != null && !string.IsNullOrWhiteSpace(d.Id)))
            _augmentDefinitions[augment.Id] = augment;
        foreach (var skill in skills.Where(d => d != null && !string.IsNullOrWhiteSpace(d.Id)))
            _skillDefinitions[skill.Id] = skill;
        foreach (var teamTactic in teamTactics.Where(d => d != null && !string.IsNullOrWhiteSpace(d.Id)))
            _teamTacticDefinitions[teamTactic.Id] = teamTactic;
        foreach (var roleInstruction in roleInstructions.Where(d => d != null && !string.IsNullOrWhiteSpace(d.Id)))
            _roleInstructionDefinitions[roleInstruction.Id] = roleInstruction;
        foreach (var passiveNode in passiveNodes.Where(d => d != null && !string.IsNullOrWhiteSpace(d.Id)))
            _passiveNodeDefinitions[passiveNode.Id] = passiveNode;
        foreach (var synergy in synergies.Where(d => d != null && !string.IsNullOrWhiteSpace(d.Id)))
            _synergyDefinitions[synergy.Id] = synergy;
        foreach (var chapter in campaignChapters.Where(d => d != null && !string.IsNullOrWhiteSpace(d.Id)))
            _campaignChapterDefinitions[chapter.Id] = chapter;
        foreach (var site in expeditionSites.Where(d => d != null && !string.IsNullOrWhiteSpace(d.Id)))
            _expeditionSiteDefinitions[site.Id] = site;
        foreach (var encounter in encounters.Where(d => d != null && !string.IsNullOrWhiteSpace(d.Id)))
            _encounterDefinitions[encounter.Id] = encounter;
        foreach (var squad in enemySquads.Where(d => d != null && !string.IsNullOrWhiteSpace(d.Id)))
            _enemySquadDefinitions[squad.Id] = squad;
        foreach (var bossOverlay in bossOverlays.Where(d => d != null && !string.IsNullOrWhiteSpace(d.Id)))
            _bossOverlayDefinitions[bossOverlay.Id] = bossOverlay;
        foreach (var statusFamily in statusFamilies.Where(d => d != null && !string.IsNullOrWhiteSpace(d.Id)))
            _statusFamilyDefinitions[statusFamily.Id] = statusFamily;
        foreach (var cleanseProfile in cleanseProfiles.Where(d => d != null && !string.IsNullOrWhiteSpace(d.Id)))
            _cleanseProfileDefinitions[cleanseProfile.Id] = cleanseProfile;
        foreach (var controlDiminishing in controlDiminishingRules.Where(d => d != null && !string.IsNullOrWhiteSpace(d.Id)))
            _controlDiminishingDefinitions[controlDiminishing.Id] = controlDiminishing;
        foreach (var rewardSource in rewardSources.Where(d => d != null && !string.IsNullOrWhiteSpace(d.Id)))
            _rewardSourceDefinitions[rewardSource.Id] = rewardSource;
        foreach (var dropTable in dropTables.Where(d => d != null && !string.IsNullOrWhiteSpace(d.Id)))
            _dropTableDefinitions[dropTable.Id] = dropTable;
        foreach (var lootBundle in lootBundles.Where(d => d != null && !string.IsNullOrWhiteSpace(d.Id)))
            _lootBundleDefinitions[lootBundle.Id] = lootBundle;
        foreach (var traitToken in traitTokens.Where(d => d != null && !string.IsNullOrWhiteSpace(d.Id)))
            _traitTokenDefinitions[traitToken.Id] = traitToken;

        _firstPlayableSlice = NormalizeFirstPlayableSlice(
            firstPlayableSliceAssets
                .FirstOrDefault(asset => asset != null)?
                .ToRuntime());
    }

    private void ClearAll()
    {
        _archetypeDefinitions.Clear();
        _traitPools.Clear();
        _raceDefinitions.Clear();
        _classDefinitions.Clear();
        _characterDefinitions.Clear();
        _itemDefinitions.Clear();
        _affixDefinitions.Clear();
        _augmentDefinitions.Clear();
        _skillDefinitions.Clear();
        _teamTacticDefinitions.Clear();
        _roleInstructionDefinitions.Clear();
        _passiveNodeDefinitions.Clear();
        _synergyDefinitions.Clear();
        _campaignChapterDefinitions.Clear();
        _expeditionSiteDefinitions.Clear();
        _encounterDefinitions.Clear();
        _enemySquadDefinitions.Clear();
        _bossOverlayDefinitions.Clear();
        _statusFamilyDefinitions.Clear();
        _cleanseProfileDefinitions.Clear();
        _controlDiminishingDefinitions.Clear();
        _rewardSourceDefinitions.Clear();
        _dropTableDefinitions.Clear();
        _lootBundleDefinitions.Clear();
        _traitTokenDefinitions.Clear();
    }

    private FirstPlayableSliceDefinition? NormalizeFirstPlayableSlice(FirstPlayableSliceDefinition? authored)
    {
        if (authored == null
            && _archetypeDefinitions.Count == 0
            && _skillDefinitions.Count == 0
            && _affixDefinitions.Count == 0
            && _augmentDefinitions.Count == 0
            && _synergyDefinitions.Count == 0)
        {
            return null;
        }

        var slice = authored ?? new FirstPlayableSliceDefinition();
        var selectedUnits = SelectUnitBlueprintIds(slice);
        var selectedSignatures = SelectSignatureSkillIds(selectedUnits, slice.SignatureActiveIds, slice.SignaturePassiveIds, slice);
        var selectedFlex = SelectFlexSkillIds(selectedUnits, slice.FlexActiveIds, slice.FlexPassiveIds, slice);
        var selectedAffixes = NormalizeIds(
            slice.AffixIds,
            _affixDefinitions.Keys.OrderBy(id => id, StringComparer.Ordinal),
            slice.AffixCap);
        var selectedTempAugments = NormalizeIds(
            slice.TemporaryAugmentIds,
            _augmentDefinitions.Values
                .Where(definition => !definition.IsPermanent && !string.IsNullOrWhiteSpace(definition.Id))
                .Select(definition => definition.Id)
                .OrderBy(id => id, StringComparer.Ordinal),
            slice.TemporaryAugmentCap);
        var selectedPermAugments = NormalizeIds(
            slice.PermanentAugmentIds,
            _augmentDefinitions.Values
                .Where(definition => definition.IsPermanent && !string.IsNullOrWhiteSpace(definition.Id))
                .Select(definition => definition.Id)
                .OrderBy(id => id, StringComparer.Ordinal),
            slice.PermanentAugmentCap);
        var selectedSynergies = NormalizeIds(
            slice.SynergyFamilyIds,
            _synergyDefinitions.Keys.OrderBy(id => id, StringComparer.Ordinal),
            slice.SynergyFamilyCap);

        var parkingLot = new HashSet<string>(slice.ParkingLotContentIds.Where(id => !string.IsNullOrWhiteSpace(id)), StringComparer.Ordinal);
        AddParkingLot(parkingLot, _archetypeDefinitions.Keys, selectedUnits);
        AddParkingLot(parkingLot, _affixDefinitions.Keys, selectedAffixes);
        AddParkingLot(parkingLot, _skillDefinitions.Keys, selectedSignatures.SignatureActiveIds);
        AddParkingLot(parkingLot, _skillDefinitions.Keys, selectedSignatures.SignaturePassiveIds);
        AddParkingLot(parkingLot, _skillDefinitions.Keys, selectedFlex.FlexActiveIds);
        AddParkingLot(parkingLot, _skillDefinitions.Keys, selectedFlex.FlexPassiveIds);
        AddParkingLot(parkingLot, _augmentDefinitions.Keys.Where(id => !string.IsNullOrWhiteSpace(id)), selectedTempAugments);
        AddParkingLot(parkingLot, _augmentDefinitions.Keys.Where(id => !string.IsNullOrWhiteSpace(id)), selectedPermAugments);
        AddParkingLot(parkingLot, _synergyDefinitions.Keys.Where(id => !string.IsNullOrWhiteSpace(id)), selectedSynergies);

        return new FirstPlayableSliceDefinition
        {
            UnitBlueprintCap = slice.UnitBlueprintCap,
            SignatureActiveCap = slice.SignatureActiveCap,
            SignaturePassiveCap = slice.SignaturePassiveCap,
            FlexActiveCap = slice.FlexActiveCap,
            FlexPassiveCap = slice.FlexPassiveCap,
            AffixCap = slice.AffixCap,
            SynergyFamilyCap = slice.SynergyFamilyCap,
            TemporaryAugmentCap = slice.TemporaryAugmentCap,
            PermanentAugmentCap = slice.PermanentAugmentCap,
            RequireAllThreatPatternsCovered = slice.RequireAllThreatPatternsCovered,
            RequireAllCounterToolsCovered = slice.RequireAllCounterToolsCovered,
            CoverageQuotas = (slice.CoverageQuotas ?? Array.Empty<SliceCoverageQuota>())
                .Select(quota => new SliceCoverageQuota
                {
                    Kind = quota.Kind,
                    MinimumCount = quota.MinimumCount,
                })
                .ToList(),
            UnitBlueprintIds = selectedUnits,
            SignatureActiveIds = selectedSignatures.SignatureActiveIds,
            SignaturePassiveIds = selectedSignatures.SignaturePassiveIds,
            FlexActiveIds = selectedFlex.FlexActiveIds,
            FlexPassiveIds = selectedFlex.FlexPassiveIds,
            AffixIds = selectedAffixes,
            SynergyFamilyIds = selectedSynergies,
            TemporaryAugmentIds = selectedTempAugments,
            PermanentAugmentIds = selectedPermAugments,
            ParkingLotContentIds = parkingLot
                .OrderBy(id => id, StringComparer.Ordinal)
                .ToList(),
        };
    }

    private IReadOnlyList<string> SelectUnitBlueprintIds(FirstPlayableSliceDefinition slice)
    {
        var canonical = ContentFallbackData.CanonicalArchetypeOrder
            .Where(id => _archetypeDefinitions.ContainsKey(id)).ToList();
        if (canonical.Count == 0)
        {
            canonical = _archetypeDefinitions.Keys.OrderBy(id => id, StringComparer.Ordinal).ToList();
        }

        return NormalizeIds(slice.UnitBlueprintIds, canonical, slice.UnitBlueprintCap);
    }

    private (IReadOnlyList<string> SignatureActiveIds, IReadOnlyList<string> SignaturePassiveIds) SelectSignatureSkillIds(
        IReadOnlyList<string> selectedUnits,
        IReadOnlyList<string> authoredSignatureActives,
        IReadOnlyList<string> authoredSignaturePassives,
        FirstPlayableSliceDefinition slice)
    {
        var archetypeConverter = new ArchetypeConverter(_skillDefinitions, null);
        var signatureActiveCandidates = new List<string>();
        var signaturePassiveCandidates = new List<string>();

        foreach (var unitId in selectedUnits)
        {
            if (!_archetypeDefinitions.TryGetValue(unitId, out var definition)) continue;
            var template = archetypeConverter.BuildArchetypeTemplate(definition);
            if (template.SignatureActive != null) signatureActiveCandidates.Add(template.SignatureActive.Id);
            if (template.SignaturePassive != null) signaturePassiveCandidates.Add(template.SignaturePassive.Id);
        }

        signatureActiveCandidates.AddRange(_skillDefinitions.Values
            .Select(SkillConverter.BuildSkillSpec)
            .Where(skill => skill.EffectiveSlotKind == ActionSlotKind.SignatureActive)
            .Select(skill => skill.Id));
        signaturePassiveCandidates.AddRange(_skillDefinitions.Values
            .Select(SkillConverter.BuildSkillSpec)
            .Where(skill => skill.EffectiveSlotKind == ActionSlotKind.SignaturePassive)
            .Select(skill => skill.Id));

        return (
            NormalizeIds(authoredSignatureActives, signatureActiveCandidates, slice.SignatureActiveCap),
            NormalizeIds(authoredSignaturePassives, signaturePassiveCandidates, slice.SignaturePassiveCap));
    }

    private (IReadOnlyList<string> FlexActiveIds, IReadOnlyList<string> FlexPassiveIds) SelectFlexSkillIds(
        IReadOnlyList<string> selectedUnits,
        IReadOnlyList<string> authoredFlexActives,
        IReadOnlyList<string> authoredFlexPassives,
        FirstPlayableSliceDefinition slice)
    {
        var archetypeConverter = new ArchetypeConverter(_skillDefinitions, null);
        var flexActiveCandidates = new List<string>();
        var flexPassiveCandidates = new List<string>();

        foreach (var unitId in selectedUnits)
        {
            if (!_archetypeDefinitions.TryGetValue(unitId, out var definition)) continue;
            var template = archetypeConverter.BuildArchetypeTemplate(definition);
            flexActiveCandidates.AddRange(template.RecruitFlexActivePool.Select(s => s.Id));
            flexPassiveCandidates.AddRange(template.RecruitFlexPassivePool.Select(s => s.Id));
        }

        flexActiveCandidates.AddRange(_skillDefinitions.Values
            .Select(SkillConverter.BuildSkillSpec)
            .Where(skill => skill.EffectiveSlotKind == ActionSlotKind.FlexActive)
            .Select(skill => skill.Id));
        flexPassiveCandidates.AddRange(_skillDefinitions.Values
            .Select(SkillConverter.BuildSkillSpec)
            .Where(skill => skill.EffectiveSlotKind == ActionSlotKind.FlexPassive)
            .Select(skill => skill.Id));

        return (
            NormalizeIds(authoredFlexActives, flexActiveCandidates, slice.FlexActiveCap),
            NormalizeIds(authoredFlexPassives, flexPassiveCandidates, slice.FlexPassiveCap));
    }

    private static IReadOnlyList<string> NormalizeIds(
        IReadOnlyList<string>? authoredIds,
        IEnumerable<string> candidates,
        int cap)
    {
        var resolved = new List<string>();
        var seen = new HashSet<string>(StringComparer.Ordinal);

        void AddRange(IEnumerable<string> ids)
        {
            foreach (var id in ids)
            {
                if (string.IsNullOrWhiteSpace(id) || !seen.Add(id)) continue;
                resolved.Add(id);
                if (cap > 0 && resolved.Count >= cap) break;
            }
        }

        if (authoredIds != null) AddRange(authoredIds);
        if (cap <= 0 || resolved.Count < cap) AddRange(candidates);

        return cap > 0 ? resolved.Take(cap).ToList() : resolved;
    }

    private static void AddParkingLot(HashSet<string> parkingLot, IEnumerable<string> allIds, IEnumerable<string> selectedIds)
    {
        var selected = selectedIds
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .ToHashSet(StringComparer.Ordinal);
        foreach (var id in allIds.Where(id => !string.IsNullOrWhiteSpace(id)))
        {
            if (!selected.Contains(id))
            {
                parkingLot.Add(id);
            }
        }
    }

    internal static T[] LoadDefinitions<T>(string resourcesPath, string editorFolderPath) where T : UnityEngine.Object
    {
        var results = new List<T>();
#if UNITY_EDITOR
        var seenPaths = new HashSet<string>(StringComparer.Ordinal);
#endif

        foreach (var asset in Resources.LoadAll<T>(resourcesPath))
        {
            if (asset == null) continue;
            results.Add(asset);
#if UNITY_EDITOR
            var assetPath = AssetDatabase.GetAssetPath(asset);
            if (!string.IsNullOrWhiteSpace(assetPath))
            {
                seenPaths.Add(assetPath);
            }
#endif
        }

#if UNITY_EDITOR
        if (!AssetDatabase.IsValidFolder(editorFolderPath))
        {
            return results.ToArray();
        }

        foreach (var path in AssetDatabase.FindAssets($"t:{typeof(T).Name}", new[] { editorFolderPath })
                     .Select(AssetDatabase.GUIDToAssetPath)
                     .Where(path => !string.IsNullOrWhiteSpace(path) && seenPaths.Add(path)))
        {
            var asset = LoadEditorAssetAtPath<T>(path);
            if (asset != null)
            {
                results.Add(asset);
            }
        }

        foreach (var path in AssetDatabase.FindAssets(string.Empty, new[] { editorFolderPath })
                     .Select(AssetDatabase.GUIDToAssetPath)
                     .Where(path => path.EndsWith(".asset", StringComparison.OrdinalIgnoreCase) && seenPaths.Add(path)))
        {
            var asset = LoadEditorAssetAtPath<T>(path);
            if (asset != null)
            {
                results.Add(asset);
            }
        }

        return results.ToArray();
#else
        return results.ToArray();
#endif
    }

#if UNITY_EDITOR
    private static T? LoadEditorAssetAtPath<T>(string path) where T : UnityEngine.Object
    {
        var mainAsset = AssetDatabase.LoadMainAssetAtPath(path) as T;
        if (mainAsset != null)
        {
            return mainAsset;
        }

        return AssetDatabase.LoadAllAssetsAtPath(path).OfType<T>().FirstOrDefault();
    }

    private static void RequireEditorCanonicalSampleContentReady()
    {
        var generatorType = Type.GetType("SM.Editor.SeedData.SampleSeedGenerator, SM.Editor");
        if (generatorType == null)
        {
            return;
        }

        var requireMethod = generatorType.GetMethod("RequireCanonicalSampleContentReady", BindingFlags.Public | BindingFlags.Static);
        if (requireMethod != null)
        {
            try
            {
                requireMethod.Invoke(null, new object[] { nameof(ContentDefinitionRegistry) });
                return;
            }
            catch (TargetInvocationException ex) when (ex.InnerException != null)
            {
                throw ex.InnerException;
            }
        }

        var hasMinimumMethod = generatorType.GetMethod("HasCanonicalMinimumContent", BindingFlags.Public | BindingFlags.Static);
        if (hasMinimumMethod == null)
        {
            return;
        }

        if (hasMinimumMethod.Invoke(null, null) is true)
        {
            return;
        }

        throw new InvalidOperationException(
            "SM canonical sample content is not preflight-ready for ContentDefinitionRegistry. " +
            "Run 'pwsh -File tools/unity-bridge.ps1 seed-content' or Unity menu 'SM/Seed/Generate Sample Content' before using editor-side runtime lookup.");
    }
#endif
}
