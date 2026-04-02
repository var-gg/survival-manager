using System.Collections.Generic;
using SM.Content.Definitions;
using SM.Meta.Model;

namespace SM.Unity;

/// <summary>
/// RuntimeCombatContentLookup의 소비측 추상화.
/// GameSessionState, ContentTextResolver 등 핵심 로직은 이 인터페이스에만 의존한다.
/// production 구현(RuntimeCombatContentLookup)만 Resources.LoadAll을 사용하고,
/// 테스트에서는 FakeCombatContentLookup으로 교체하여 Unity asset-pipeline 호출을 피한다.
/// </summary>
public interface ICombatContentLookup
{
    // ── Snapshot ──

    CombatContentSnapshot Snapshot { get; }
    IReadOnlyDictionary<string, UnitArchetypeDefinition> ArchetypeDefinitions { get; }
    bool TryGetCombatSnapshot(out CombatContentSnapshot snapshot, out string error);

    // ── Canonical ID lists ──

    IReadOnlyList<string> GetCanonicalArchetypeIds();
    IReadOnlyList<string> GetCanonicalItemIds();
    IReadOnlyList<string> GetCanonicalAffixIds();
    IReadOnlyList<string> GetCanonicalTemporaryAugmentIds();
    IReadOnlyList<string> GetCanonicalSynergyFamilyIds();

    // ── Single-definition lookup ──

    FirstPlayableSliceDefinition? GetFirstPlayableSlice();
    bool TryGetArchetype(string archetypeId, out UnitArchetypeDefinition archetype);
    bool TryGetItemDefinition(string itemId, out ItemBaseDefinition item);
    bool TryGetRaceDefinition(string raceId, out RaceDefinition race);
    bool TryGetClassDefinition(string classId, out ClassDefinition @class);
    bool TryGetAugmentDefinition(string augmentId, out AugmentDefinition augment);
    bool TryGetSkillDefinition(string skillId, out SkillDefinitionAsset skill);
    bool TryGetAffixDefinition(string affixId, out AffixDefinition affix);
    bool TryGetCampaignChapterDefinition(string chapterId, out CampaignChapterDefinition chapter);
    bool TryGetExpeditionSiteDefinition(string siteId, out ExpeditionSiteDefinition site);
    bool TryGetEncounterDefinition(string encounterId, out EncounterDefinition encounter);

    // ── Trait ──

    bool TryGetTraitEntry(string archetypeId, string traitId, out TraitEntry trait);
    bool TryGetTraitIds(string archetypeId, out IReadOnlyList<string> positiveTraitIds, out IReadOnlyList<string> negativeTraitIds);

    // ── Ordered collections ──

    IReadOnlyList<CampaignChapterDefinition> GetOrderedCampaignChapters();

    // ── Normalization ──

    string NormalizeArchetypeId(string archetypeId, string raceId, string classId, int fallbackIndex);
    string NormalizePositiveTraitId(string archetypeId, string traitId, int fallbackIndex);
    string NormalizeNegativeTraitId(string archetypeId, string traitId, int fallbackIndex);
    string NormalizeItemBaseId(string itemBaseId, int fallbackIndex);
    string NormalizeAffixId(string affixId, int fallbackIndex);
    string NormalizeTemporaryAugmentId(string augmentId, int fallbackIndex);
}
