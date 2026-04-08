using System;
using System.Collections.Generic;
using System.Linq;
using SM.Combat.Model;
using SM.Content.Definitions;
using SM.Core.Stats;
using SM.Meta.Model;
using SM.Unity;

namespace SM.Tests.EditMode.Fakes;

/// <summary>
/// Unity asset-pipeline(Resources.LoadAll)을 사용하지 않는 테스트 전용 ICombatContentLookup 구현.
/// 빈 Snapshot과 최소 기본값을 반환하여 GameSessionState 등의 로직을 격리 테스트할 수 있다.
/// </summary>
public sealed class FakeCombatContentLookup : ICombatContentLookup
{
    private readonly CombatContentSnapshot _snapshot;
    private readonly FirstPlayableSliceDefinition? _firstPlayableSlice;
    private readonly Dictionary<string, UnitArchetypeDefinition> _archetypes = new(StringComparer.Ordinal);
    private readonly Dictionary<string, ItemBaseDefinition> _items = new(StringComparer.Ordinal);
    private readonly Dictionary<string, AffixDefinition> _affixes = new(StringComparer.Ordinal);
    private readonly Dictionary<string, AugmentDefinition> _augments = new(StringComparer.Ordinal);
    private readonly Dictionary<string, SkillDefinitionAsset> _skills = new(StringComparer.Ordinal);
    private readonly Dictionary<string, RaceDefinition> _races = new(StringComparer.Ordinal);
    private readonly Dictionary<string, ClassDefinition> _classes = new(StringComparer.Ordinal);
    private readonly Dictionary<string, CharacterDefinition> _characters = new(StringComparer.Ordinal);
    private readonly Dictionary<string, TeamTacticDefinition> _teamTactics = new(StringComparer.Ordinal);
    private readonly Dictionary<string, SynergyDefinition> _synergies = new(StringComparer.Ordinal);
    private readonly Dictionary<string, RoleInstructionDefinition> _roleInstructions = new(StringComparer.Ordinal);
    private readonly Dictionary<string, PassiveBoardDefinition> _passiveBoards = new(StringComparer.Ordinal);
    private readonly Dictionary<string, PassiveNodeDefinition> _passiveNodes = new(StringComparer.Ordinal);
    private readonly Dictionary<string, CampaignChapterDefinition> _campaignChapters = new(StringComparer.Ordinal);
    private readonly Dictionary<string, ExpeditionSiteDefinition> _expeditionSites = new(StringComparer.Ordinal);
    private readonly Dictionary<string, EncounterDefinition> _encounters = new(StringComparer.Ordinal);
    private readonly List<CampaignChapterDefinition> _orderedCampaignChapters = new();

    public FakeCombatContentLookup(
        CombatContentSnapshot? snapshot = null,
        FirstPlayableSliceDefinition? firstPlayableSlice = null,
        IReadOnlyDictionary<string, UnitArchetypeDefinition>? archetypes = null,
        IReadOnlyDictionary<string, ItemBaseDefinition>? items = null,
        IReadOnlyDictionary<string, AffixDefinition>? affixes = null,
        IReadOnlyDictionary<string, AugmentDefinition>? augments = null,
        IReadOnlyDictionary<string, SkillDefinitionAsset>? skills = null,
        IReadOnlyDictionary<string, RaceDefinition>? races = null,
        IReadOnlyDictionary<string, ClassDefinition>? classes = null,
        IReadOnlyDictionary<string, CharacterDefinition>? characters = null,
        IReadOnlyDictionary<string, TeamTacticDefinition>? teamTactics = null,
        IReadOnlyDictionary<string, SynergyDefinition>? synergies = null,
        IReadOnlyDictionary<string, RoleInstructionDefinition>? roleInstructions = null,
        IReadOnlyDictionary<string, PassiveBoardDefinition>? passiveBoards = null,
        IReadOnlyDictionary<string, PassiveNodeDefinition>? passiveNodes = null,
        IReadOnlyDictionary<string, CampaignChapterDefinition>? campaignChapters = null,
        IReadOnlyDictionary<string, ExpeditionSiteDefinition>? expeditionSites = null,
        IReadOnlyDictionary<string, EncounterDefinition>? encounters = null,
        IReadOnlyList<CampaignChapterDefinition>? orderedCampaignChapters = null)
    {
        _snapshot = snapshot ?? CreateEmptySnapshot();
        _firstPlayableSlice = firstPlayableSlice;
        if (archetypes != null)
        {
            foreach (var (id, archetype) in archetypes)
            {
                _archetypes[id] = archetype;
            }
        }

        if (items != null)
        {
            foreach (var (id, item) in items)
            {
                _items[id] = item;
            }
        }

        if (affixes != null)
        {
            foreach (var (id, affix) in affixes)
            {
                _affixes[id] = affix;
            }
        }

        if (augments != null)
        {
            foreach (var (id, augment) in augments)
            {
                _augments[id] = augment;
            }
        }

        if (skills != null)
        {
            foreach (var (id, skill) in skills)
            {
                _skills[id] = skill;
            }
        }

        if (races != null)
        {
            foreach (var (id, race) in races)
            {
                _races[id] = race;
            }
        }

        if (classes != null)
        {
            foreach (var (id, @class) in classes)
            {
                _classes[id] = @class;
            }
        }

        if (characters != null)
        {
            foreach (var (id, character) in characters)
            {
                _characters[id] = character;
            }
        }

        if (teamTactics != null)
        {
            foreach (var (id, teamTactic) in teamTactics)
            {
                _teamTactics[id] = teamTactic;
            }
        }

        if (synergies != null)
        {
            foreach (var (id, synergy) in synergies)
            {
                _synergies[id] = synergy;
            }
        }

        if (roleInstructions != null)
        {
            foreach (var (id, roleInstruction) in roleInstructions)
            {
                _roleInstructions[id] = roleInstruction;
            }
        }

        if (passiveBoards != null)
        {
            foreach (var (id, passiveBoard) in passiveBoards)
            {
                _passiveBoards[id] = passiveBoard;
            }
        }

        if (passiveNodes != null)
        {
            foreach (var (id, passiveNode) in passiveNodes)
            {
                _passiveNodes[id] = passiveNode;
            }
        }

        if (campaignChapters != null)
        {
            foreach (var (id, chapter) in campaignChapters)
            {
                _campaignChapters[id] = chapter;
            }
        }

        if (expeditionSites != null)
        {
            foreach (var (id, site) in expeditionSites)
            {
                _expeditionSites[id] = site;
            }
        }

        if (encounters != null)
        {
            foreach (var (id, encounter) in encounters)
            {
                _encounters[id] = encounter;
            }
        }

        if (orderedCampaignChapters != null)
        {
            _orderedCampaignChapters.AddRange(orderedCampaignChapters.Where(chapter => chapter != null));
        }
        else if (_campaignChapters.Count > 0)
        {
            _orderedCampaignChapters.AddRange(_campaignChapters.Values
                .OrderBy(chapter => chapter.StoryOrder)
                .ThenBy(chapter => chapter.Id, StringComparer.Ordinal));
        }
    }

    // ── Snapshot ──

    public CombatContentSnapshot Snapshot => _snapshot;

    public IReadOnlyDictionary<string, UnitArchetypeDefinition> ArchetypeDefinitions => _archetypes;

    public bool TryGetCombatSnapshot(out CombatContentSnapshot snapshot, out string error)
    {
        snapshot = _snapshot;
        error = string.Empty;
        return true;
    }

    // ── Canonical ID lists ──

    public IReadOnlyList<string> GetCanonicalArchetypeIds() => Array.Empty<string>();
    public IReadOnlyList<string> GetCanonicalItemIds() => _items.Keys.OrderBy(id => id, StringComparer.Ordinal).ToArray();
    public IReadOnlyList<string> GetCanonicalAffixIds() => _firstPlayableSlice?.AffixIds?.Count > 0
        ? _firstPlayableSlice.AffixIds.ToArray()
        : _affixes.Keys.OrderBy(id => id, StringComparer.Ordinal).ToArray();
    public IReadOnlyList<string> GetCanonicalTemporaryAugmentIds() => _firstPlayableSlice?.TemporaryAugmentIds?.Count > 0
        ? _firstPlayableSlice.TemporaryAugmentIds.ToArray()
        : _augments.Values.Where(augment => !augment.IsPermanent).OrderBy(augment => augment.Id, StringComparer.Ordinal).Select(augment => augment.Id).ToArray();
    public IReadOnlyList<string> GetCanonicalPermanentAugmentIds() => _firstPlayableSlice?.PermanentAugmentIds?.Count > 0
        ? _firstPlayableSlice.PermanentAugmentIds.ToArray()
        : _augments.Values.Where(augment => augment.IsPermanent).OrderBy(augment => augment.Id, StringComparer.Ordinal).Select(augment => augment.Id).ToArray();
    public IReadOnlyList<string> GetCanonicalPassiveBoardIds() => _firstPlayableSlice?.PassiveBoardIds?.Count > 0
        ? _firstPlayableSlice.PassiveBoardIds.ToArray()
        : _passiveBoards.Keys.OrderBy(id => id, StringComparer.Ordinal).ToArray();
    public IReadOnlyList<string> GetCanonicalSynergyFamilyIds() => _synergies.Keys.OrderBy(id => id, StringComparer.Ordinal).ToArray();

    // ── Single-definition lookup ──

    public FirstPlayableSliceDefinition? GetFirstPlayableSlice() => _firstPlayableSlice;

    public bool TryGetArchetype(string archetypeId, out UnitArchetypeDefinition archetype)
    {
        return _archetypes.TryGetValue(archetypeId, out archetype!);
    }

    public bool TryGetItemDefinition(string itemId, out ItemBaseDefinition item)
    {
        return _items.TryGetValue(itemId, out item!);
    }

    public bool TryGetRaceDefinition(string raceId, out RaceDefinition race)
    {
        return _races.TryGetValue(raceId, out race!);
    }

    public bool TryGetClassDefinition(string classId, out ClassDefinition @class)
    {
        return _classes.TryGetValue(classId, out @class!);
    }

    public bool TryGetCharacterDefinition(string characterId, out CharacterDefinition character)
    {
        return _characters.TryGetValue(characterId, out character!);
    }

    public bool TryGetAugmentDefinition(string augmentId, out AugmentDefinition augment)
    {
        return _augments.TryGetValue(augmentId, out augment!);
    }

    public bool TryGetSkillDefinition(string skillId, out SkillDefinitionAsset skill)
    {
        return _skills.TryGetValue(skillId, out skill!);
    }

    public bool TryGetAffixDefinition(string affixId, out AffixDefinition affix)
    {
        return _affixes.TryGetValue(affixId, out affix!);
    }

    public bool TryGetPassiveBoardDefinition(string boardId, out PassiveBoardDefinition board)
    {
        return _passiveBoards.TryGetValue(boardId, out board!);
    }

    public bool TryGetPassiveNodeDefinition(string nodeId, out PassiveNodeDefinition node)
    {
        return _passiveNodes.TryGetValue(nodeId, out node!);
    }

    public bool TryGetTeamTacticDefinition(string teamTacticId, out TeamTacticDefinition teamTactic)
    {
        return _teamTactics.TryGetValue(teamTacticId, out teamTactic!);
    }

    public bool TryGetSynergyDefinition(string synergyId, out SynergyDefinition synergy)
    {
        return _synergies.TryGetValue(synergyId, out synergy!);
    }

    public bool TryGetRoleInstructionDefinition(string roleInstructionId, out RoleInstructionDefinition roleInstruction)
    {
        return _roleInstructions.TryGetValue(roleInstructionId, out roleInstruction!);
    }

    public bool TryGetCampaignChapterDefinition(string chapterId, out CampaignChapterDefinition chapter)
    {
        return _campaignChapters.TryGetValue(chapterId, out chapter!);
    }

    public bool TryGetExpeditionSiteDefinition(string siteId, out ExpeditionSiteDefinition site)
    {
        return _expeditionSites.TryGetValue(siteId, out site!);
    }

    public bool TryGetEncounterDefinition(string encounterId, out EncounterDefinition encounter)
    {
        return _encounters.TryGetValue(encounterId, out encounter!);
    }

    // ── Trait ──

    public bool TryGetTraitEntry(string archetypeId, string traitId, out TraitEntry trait)
    {
        trait = null!;
        return false;
    }

    public bool TryGetTraitIds(string archetypeId, out IReadOnlyList<string> positiveTraitIds, out IReadOnlyList<string> negativeTraitIds)
    {
        positiveTraitIds = Array.Empty<string>();
        negativeTraitIds = Array.Empty<string>();
        return false;
    }

    // ── Ordered collections ──

    public IReadOnlyList<CampaignChapterDefinition> GetOrderedCampaignChapters() => _orderedCampaignChapters;

    // ── Normalization (identity passthrough) ──

    public string NormalizeArchetypeId(string archetypeId, string raceId, string classId, int fallbackIndex)
        => archetypeId;

    public string NormalizePositiveTraitId(string archetypeId, string traitId, int fallbackIndex)
        => traitId;

    public string NormalizeNegativeTraitId(string archetypeId, string traitId, int fallbackIndex)
        => traitId;

    public string NormalizeItemBaseId(string itemBaseId, int fallbackIndex)
        => itemBaseId;

    public string NormalizeAffixId(string affixId, int fallbackIndex)
        => affixId;

    public string NormalizeTemporaryAugmentId(string augmentId, int fallbackIndex)
        => augmentId;

    // ── Factory ──

    private static CombatContentSnapshot CreateEmptySnapshot()
    {
        var empty = new Dictionary<string, CombatModifierPackage>();
        return new CombatContentSnapshot(
            Archetypes: new Dictionary<string, CombatArchetypeTemplate>(),
            TraitPackages: empty,
            ItemPackages: empty,
            AffixPackages: empty,
            AugmentPackages: empty,
            SkillCatalog: new Dictionary<string, BattleSkillSpec>(),
            TeamTactics: new Dictionary<string, TeamTacticTemplate>(),
            RoleInstructions: new Dictionary<string, RoleInstructionTemplate>(),
            PassiveNodes: new Dictionary<string, PassiveNodeTemplate>(),
            AugmentCatalog: new Dictionary<string, AugmentCatalogEntry>(),
            SynergyCatalog: new Dictionary<string, SynergyTierTemplate>()
        );
    }
}
