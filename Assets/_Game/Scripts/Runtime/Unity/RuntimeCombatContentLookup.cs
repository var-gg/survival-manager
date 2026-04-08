using System;
using System.Collections.Generic;
using System.Linq;
using SM.Content.Definitions;
using SM.Meta.Model;
using SM.Unity.ContentConversion;
using UnityEngine;
using Unity.Profiling;

namespace SM.Unity;

public sealed class RuntimeCombatContentLookup : ICombatContentLookup
{
    private static readonly ProfilerMarker EnsureLoadedMarker = new("SM.RuntimeCombatContentLookup.EnsureLoaded");

    private readonly ContentDefinitionRegistry _registry;
    private CombatContentSnapshot? _snapshot;

    public RuntimeCombatContentLookup(bool allowEditorRecoveryFallback = false)
    {
        _registry = new ContentDefinitionRegistry(allowEditorRecoveryFallback);
    }

    internal bool AllowsEditorRecoveryFallback => _registry.AllowsEditorRecoveryFallback;

    public CombatContentSnapshot Snapshot
    {
        get
        {
            EnsureLoaded();
            return _snapshot!;
        }
    }

    public IReadOnlyDictionary<string, UnitArchetypeDefinition> ArchetypeDefinitions
    {
        get
        {
            EnsureLoaded();
            return _registry.ArchetypeDefinitions;
        }
    }

    public bool TryGetCombatSnapshot(out CombatContentSnapshot snapshot, out string error)
    {
        try
        {
            snapshot = Snapshot;
            error = string.Empty;
            return true;
        }
        catch (Exception ex)
        {
            snapshot = null!;
            error = ex.Message;
#if UNITY_EDITOR
            Debug.LogError($"[RuntimeCombatContentLookup] Failed to build combat snapshot: {ex}");
#endif
            return false;
        }
    }

    public IReadOnlyList<string> GetCanonicalArchetypeIds()
    {
        EnsureLoaded();
        var resolved = ContentFallbackData.CanonicalArchetypeOrder.Where(id => _registry.ArchetypeDefinitions.ContainsKey(id)).ToList();
        if (resolved.Count > 0)
        {
            return resolved;
        }

        return _registry.ArchetypeDefinitions.Keys.OrderBy(id => id, StringComparer.Ordinal).ToList();
    }

    public IReadOnlyList<string> GetCanonicalItemIds()
    {
        EnsureLoaded();
        var ordered = ContentFallbackData.LegacyItemFallbackOrder.Where(id => _registry.ItemDefinitions.ContainsKey(id)).ToList();
        ordered.AddRange(_registry.ItemDefinitions.Keys.Where(id => !ordered.Contains(id)).OrderBy(id => id, StringComparer.Ordinal));
        return ordered;
    }

    public IReadOnlyList<string> GetCanonicalAffixIds()
    {
        EnsureLoaded();
        if (_registry.FirstPlayableSlice?.AffixIds.Count > 0)
        {
            return _registry.FirstPlayableSlice.AffixIds
                .Where(id => _registry.AffixDefinitions.ContainsKey(id))
                .OrderBy(id => id, StringComparer.Ordinal)
                .ToList();
        }

        return _registry.AffixDefinitions.Keys.OrderBy(id => id, StringComparer.Ordinal).ToList();
    }

    public IReadOnlyList<string> GetCanonicalTemporaryAugmentIds()
    {
        EnsureLoaded();
        var ordered = ContentFallbackData.LegacyAugmentFallbackOrder
            .Where(id => _registry.AugmentDefinitions.TryGetValue(id, out var augment) && !augment.IsPermanent).ToList();
        ordered.AddRange(_registry.AugmentDefinitions.Values
            .Where(augment => !augment.IsPermanent && !ordered.Contains(augment.Id))
            .OrderBy(augment => augment.Id, StringComparer.Ordinal)
            .Select(augment => augment.Id));
        if (_registry.FirstPlayableSlice?.AugmentIds.Count > 0)
        {
            return ordered
                .Where(id => _registry.FirstPlayableSlice.AugmentIds.Contains(id, StringComparer.Ordinal))
                .ToList();
        }

        return ordered;
    }

    public IReadOnlyList<string> GetCanonicalPermanentAugmentIds()
    {
        EnsureLoaded();
        if (_registry.FirstPlayableSlice?.PermanentAugmentIds.Count > 0)
        {
            return _registry.FirstPlayableSlice.PermanentAugmentIds
                .Where(id => _registry.AugmentDefinitions.TryGetValue(id, out var augment) && augment.IsPermanent)
                .OrderBy(id => id, StringComparer.Ordinal)
                .ToList();
        }

        return _registry.AugmentDefinitions.Values
            .Where(augment => augment.IsPermanent && !string.IsNullOrWhiteSpace(augment.Id))
            .OrderBy(augment => augment.Id, StringComparer.Ordinal)
            .Select(augment => augment.Id)
            .ToList();
    }

    public IReadOnlyList<string> GetCanonicalPassiveBoardIds()
    {
        EnsureLoaded();
        if (_registry.FirstPlayableSlice?.PassiveBoardIds.Count > 0)
        {
            return _registry.FirstPlayableSlice.PassiveBoardIds
                .Where(id => _registry.PassiveBoardDefinitions.ContainsKey(id))
                .OrderBy(id => id, StringComparer.Ordinal)
                .ToList();
        }

        return _registry.PassiveBoardDefinitions.Keys
            .OrderBy(id => id, StringComparer.Ordinal)
            .ToList();
    }

    public IReadOnlyList<string> GetCanonicalSynergyFamilyIds()
    {
        EnsureLoaded();
        if (_registry.FirstPlayableSlice?.SynergyFamilyIds.Count > 0)
        {
            return _registry.FirstPlayableSlice.SynergyFamilyIds
                .Where(id => _registry.SynergyDefinitions.ContainsKey(id))
                .OrderBy(id => id, StringComparer.Ordinal)
                .ToList();
        }

        return _registry.SynergyDefinitions.Keys
            .OrderBy(id => id, StringComparer.Ordinal)
            .ToList();
    }

    public FirstPlayableSliceDefinition? GetFirstPlayableSlice()
    {
        EnsureLoaded();
        return _registry.FirstPlayableSlice;
    }

    public bool TryGetArchetype(string archetypeId, out UnitArchetypeDefinition archetype)
    {
        EnsureLoaded();
        return _registry.ArchetypeDefinitions.TryGetValue(archetypeId, out archetype!);
    }

    public bool TryGetItemDefinition(string itemId, out ItemBaseDefinition item)
    {
        EnsureLoaded();
        return _registry.ItemDefinitions.TryGetValue(itemId, out item!);
    }

    public bool TryGetRaceDefinition(string raceId, out RaceDefinition race)
    {
        EnsureLoaded();
        return _registry.RaceDefinitions.TryGetValue(raceId, out race!);
    }

    public bool TryGetClassDefinition(string classId, out ClassDefinition @class)
    {
        EnsureLoaded();
        return _registry.ClassDefinitions.TryGetValue(classId, out @class!);
    }

    public bool TryGetCharacterDefinition(string characterId, out CharacterDefinition character)
    {
        EnsureLoaded();
        return _registry.CharacterDefinitions.TryGetValue(characterId, out character!);
    }

    public bool TryGetAugmentDefinition(string augmentId, out AugmentDefinition augment)
    {
        EnsureLoaded();
        return _registry.AugmentDefinitions.TryGetValue(augmentId, out augment!);
    }

    public bool TryGetSkillDefinition(string skillId, out SkillDefinitionAsset skill)
    {
        EnsureLoaded();
        return _registry.SkillDefinitions.TryGetValue(skillId, out skill!);
    }

    public bool TryGetAffixDefinition(string affixId, out AffixDefinition affix)
    {
        EnsureLoaded();
        return _registry.AffixDefinitions.TryGetValue(affixId, out affix!);
    }

    public bool TryGetPassiveBoardDefinition(string boardId, out PassiveBoardDefinition board)
    {
        EnsureLoaded();
        return _registry.PassiveBoardDefinitions.TryGetValue(boardId, out board!);
    }

    public bool TryGetPassiveNodeDefinition(string nodeId, out PassiveNodeDefinition node)
    {
        EnsureLoaded();
        return _registry.PassiveNodeDefinitions.TryGetValue(nodeId, out node!);
    }

    public bool TryGetTeamTacticDefinition(string teamTacticId, out TeamTacticDefinition teamTactic)
    {
        EnsureLoaded();
        return _registry.TeamTacticDefinitions.TryGetValue(teamTacticId, out teamTactic!);
    }

    public bool TryGetSynergyDefinition(string synergyId, out SynergyDefinition synergy)
    {
        EnsureLoaded();
        return _registry.SynergyDefinitions.TryGetValue(synergyId, out synergy!);
    }

    public bool TryGetRoleInstructionDefinition(string roleInstructionId, out RoleInstructionDefinition roleInstruction)
    {
        EnsureLoaded();
        return _registry.RoleInstructionDefinitions.TryGetValue(roleInstructionId, out roleInstruction!);
    }

    public bool TryGetCampaignChapterDefinition(string chapterId, out CampaignChapterDefinition chapter)
    {
        EnsureLoaded();
        return _registry.CampaignChapterDefinitions.TryGetValue(chapterId, out chapter!);
    }

    public bool TryGetExpeditionSiteDefinition(string siteId, out ExpeditionSiteDefinition site)
    {
        EnsureLoaded();
        return _registry.ExpeditionSiteDefinitions.TryGetValue(siteId, out site!);
    }

    public bool TryGetEncounterDefinition(string encounterId, out EncounterDefinition encounter)
    {
        EnsureLoaded();
        return _registry.EncounterDefinitions.TryGetValue(encounterId, out encounter!);
    }

    public IReadOnlyList<CampaignChapterDefinition> GetOrderedCampaignChapters()
    {
        EnsureLoaded();
        return _registry.CampaignChapterDefinitions.Values
            .OrderBy(definition => definition.StoryOrder)
            .ThenBy(definition => definition.Id, StringComparer.Ordinal)
            .ToList();
    }

    public bool TryGetTraitEntry(string archetypeId, string traitId, out TraitEntry trait)
    {
        EnsureLoaded();
        trait = null!;

        if (!_registry.TraitPools.TryGetValue(archetypeId, out var pool))
        {
            return false;
        }

        trait = pool.PositiveTraits.Concat(pool.NegativeTraits)
            .FirstOrDefault(entry => entry != null && string.Equals(entry.Id, traitId, StringComparison.Ordinal))!;
        return trait != null;
    }

    public bool TryGetTraitIds(string archetypeId, out IReadOnlyList<string> positiveTraitIds, out IReadOnlyList<string> negativeTraitIds)
    {
        EnsureLoaded();
        positiveTraitIds = Array.Empty<string>();
        negativeTraitIds = Array.Empty<string>();

        if (!_registry.TraitPools.TryGetValue(archetypeId, out var pool))
        {
            return false;
        }

        positiveTraitIds = pool.PositiveTraits
            .Where(entry => !string.IsNullOrWhiteSpace(entry.Id))
            .Select(entry => entry.Id)
            .ToList();
        negativeTraitIds = pool.NegativeTraits
            .Where(entry => !string.IsNullOrWhiteSpace(entry.Id))
            .Select(entry => entry.Id)
            .ToList();
        return positiveTraitIds.Count > 0 && negativeTraitIds.Count > 0;
    }

    public string NormalizeArchetypeId(string archetypeId, string raceId, string classId, int fallbackIndex)
    {
        EnsureLoaded();
        if (!string.IsNullOrWhiteSpace(archetypeId) && _registry.ArchetypeDefinitions.ContainsKey(archetypeId))
        {
            return archetypeId;
        }

        var exactMatch = _registry.ArchetypeDefinitions.Values.FirstOrDefault(definition =>
            string.Equals(definition.Race.Id, raceId, StringComparison.Ordinal) &&
            string.Equals(definition.Class.Id, classId, StringComparison.Ordinal));
        if (exactMatch != null)
        {
            return exactMatch.Id;
        }

        var ordered = GetCanonicalArchetypeIds();
        if (ordered.Count == 0)
        {
            throw new InvalidOperationException("전투 archetype canonical 목록이 비어 있습니다.");
        }

        return ordered[Math.Abs(fallbackIndex) % ordered.Count];
    }

    public string NormalizePositiveTraitId(string archetypeId, string traitId, int fallbackIndex)
    {
        EnsureLoaded();
        if (TryGetTraitIds(archetypeId, out var positives, out _) && positives.Count > 0)
        {
            if (!string.IsNullOrWhiteSpace(traitId) && positives.Contains(traitId))
            {
                return traitId;
            }

            return positives[Math.Abs(fallbackIndex) % positives.Count];
        }

        return string.Empty;
    }

    public string NormalizeNegativeTraitId(string archetypeId, string traitId, int fallbackIndex)
    {
        EnsureLoaded();
        if (TryGetTraitIds(archetypeId, out _, out var negatives) && negatives.Count > 0)
        {
            if (!string.IsNullOrWhiteSpace(traitId) && negatives.Contains(traitId))
            {
                return traitId;
            }

            return negatives[Math.Abs(fallbackIndex) % negatives.Count];
        }

        return string.Empty;
    }

    public string NormalizeItemBaseId(string itemBaseId, int fallbackIndex)
    {
        EnsureLoaded();
        if (!string.IsNullOrWhiteSpace(itemBaseId) && _registry.ItemDefinitions.ContainsKey(itemBaseId))
        {
            return itemBaseId;
        }

        var items = GetCanonicalItemIds();
        if (items.Count == 0)
        {
            return string.Empty;
        }

        return items[Math.Abs(fallbackIndex) % items.Count];
    }

    public string NormalizeAffixId(string affixId, int fallbackIndex)
    {
        EnsureLoaded();
        if (!string.IsNullOrWhiteSpace(affixId) && _registry.AffixDefinitions.ContainsKey(affixId))
        {
            return affixId;
        }

        var affixes = GetCanonicalAffixIds();
        if (affixes.Count == 0)
        {
            return string.Empty;
        }

        return affixes[Math.Abs(fallbackIndex) % affixes.Count];
    }

    public string NormalizeTemporaryAugmentId(string augmentId, int fallbackIndex)
    {
        EnsureLoaded();
        if (!string.IsNullOrWhiteSpace(augmentId)
            && _registry.AugmentDefinitions.TryGetValue(augmentId, out var augment)
            && !augment.IsPermanent)
        {
            return augmentId;
        }

        var augments = GetCanonicalTemporaryAugmentIds();
        if (augments.Count == 0)
        {
            return string.Empty;
        }

        return augments[Math.Abs(fallbackIndex) % augments.Count];
    }

    private void EnsureLoaded()
    {
        if (_snapshot != null)
        {
            return;
        }

        using (EnsureLoadedMarker.Auto())
        {
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            _registry.EnsureLoaded();
            _snapshot = new SnapshotAssembler(_registry).Assemble();
            stopwatch.Stop();
            RuntimeInstrumentation.LogDuration(
                nameof(RuntimeCombatContentLookup) + ".EnsureLoaded",
                stopwatch.Elapsed,
                $"allowEditorRecoveryFallback={AllowsEditorRecoveryFallback}");
        }
    }
}
