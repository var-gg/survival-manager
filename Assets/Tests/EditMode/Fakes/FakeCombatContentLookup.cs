using System;
using System.Collections.Generic;
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

    public FakeCombatContentLookup(
        CombatContentSnapshot? snapshot = null,
        FirstPlayableSliceDefinition? firstPlayableSlice = null)
    {
        _snapshot = snapshot ?? CreateEmptySnapshot();
        _firstPlayableSlice = firstPlayableSlice;
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
    public IReadOnlyList<string> GetCanonicalItemIds() => Array.Empty<string>();
    public IReadOnlyList<string> GetCanonicalAffixIds() => Array.Empty<string>();
    public IReadOnlyList<string> GetCanonicalTemporaryAugmentIds() => Array.Empty<string>();
    public IReadOnlyList<string> GetCanonicalSynergyFamilyIds() => Array.Empty<string>();

    // ── Single-definition lookup ──

    public FirstPlayableSliceDefinition? GetFirstPlayableSlice() => _firstPlayableSlice;

    public bool TryGetArchetype(string archetypeId, out UnitArchetypeDefinition archetype)
    {
        archetype = null!;
        return false;
    }

    public bool TryGetItemDefinition(string itemId, out ItemBaseDefinition item)
    {
        item = null!;
        return false;
    }

    public bool TryGetRaceDefinition(string raceId, out RaceDefinition race)
    {
        race = null!;
        return false;
    }

    public bool TryGetClassDefinition(string classId, out ClassDefinition @class)
    {
        @class = null!;
        return false;
    }

    public bool TryGetAugmentDefinition(string augmentId, out AugmentDefinition augment)
    {
        augment = null!;
        return false;
    }

    public bool TryGetSkillDefinition(string skillId, out SkillDefinitionAsset skill)
    {
        skill = null!;
        return false;
    }

    public bool TryGetAffixDefinition(string affixId, out AffixDefinition affix)
    {
        affix = null!;
        return false;
    }

    public bool TryGetCampaignChapterDefinition(string chapterId, out CampaignChapterDefinition chapter)
    {
        chapter = null!;
        return false;
    }

    public bool TryGetExpeditionSiteDefinition(string siteId, out ExpeditionSiteDefinition site)
    {
        site = null!;
        return false;
    }

    public bool TryGetEncounterDefinition(string encounterId, out EncounterDefinition encounter)
    {
        encounter = null!;
        return false;
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

    public IReadOnlyList<CampaignChapterDefinition> GetOrderedCampaignChapters() => Array.Empty<CampaignChapterDefinition>();

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
