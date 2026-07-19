using System.Collections.Generic;
using SM.Core.Content;
using SM.Meta.Model;

namespace SM.Meta;

/// <summary>
/// 세션의 전투 준비 경로가 소비하는 engine-free content port.
/// ScriptableObject definition과 presentation metadata는 의도적으로 노출하지 않는다.
/// </summary>
public interface ISessionContentLookup
{
    CombatContentSnapshot Snapshot { get; }
    bool TryGetCombatSnapshot(out CombatContentSnapshot snapshot, out string error);

    IReadOnlyList<string> GetCanonicalArchetypeIds();
    IReadOnlyList<string> GetCanonicalItemIds();
    IReadOnlyList<string> GetCanonicalAffixIds();
    IReadOnlyList<string> GetCanonicalTemporaryAugmentIds();
    IReadOnlyList<string> GetCanonicalPermanentAugmentIds();
    IReadOnlyList<string> GetCanonicalPassiveBoardIds();
    IReadOnlyList<string> GetCanonicalSynergyFamilyIds();

    FirstPlayableSliceDefinition? GetFirstPlayableSlice();
    bool TryGetTraitIds(
        string archetypeId,
        out IReadOnlyList<string> positiveTraitIds,
        out IReadOnlyList<string> negativeTraitIds);

    string NormalizeArchetypeId(string archetypeId, string raceId, string classId, int fallbackIndex);
    string NormalizePositiveTraitId(string archetypeId, string traitId, int fallbackIndex);
    string NormalizeNegativeTraitId(string archetypeId, string traitId, int fallbackIndex);
    string NormalizeItemBaseId(string itemBaseId, int fallbackIndex);
    string NormalizeAffixId(string affixId, int fallbackIndex);
    string NormalizeTemporaryAugmentId(string augmentId, int fallbackIndex);
}
