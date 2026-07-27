using System;
using System.Collections.Generic;
using System.Linq;
using SM.Content.Definitions;
using SM.Core.Content;
using SM.Meta.Model;
using SM.Meta.Services;
using SM.Unity.ContentConversion;
using UnityEngine;
using Unity.Profiling;

namespace SM.Unity;

public sealed class RuntimeCombatContentLookup : ICombatContentLookup
{
    private static readonly ProfilerMarker EnsureLoadedMarker = new("SM.RuntimeCombatContentLookup.EnsureLoaded");

    private readonly ContentDefinitionRegistry _registry;
    private CombatContentSnapshot? _snapshot;
    private SnapshotSessionContentLookup? _sessionContentLookup;

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
        return _sessionContentLookup!.GetCanonicalArchetypeIds();
    }

    public IReadOnlyList<string> GetCanonicalItemIds()
    {
        EnsureLoaded();
        return _sessionContentLookup!.GetCanonicalItemIds();
    }

    public IReadOnlyList<string> GetCanonicalAffixIds()
    {
        EnsureLoaded();
        return _sessionContentLookup!.GetCanonicalAffixIds();
    }

    public IReadOnlyList<string> GetCanonicalTemporaryAugmentIds()
    {
        EnsureLoaded();
        return _sessionContentLookup!.GetCanonicalTemporaryAugmentIds();
    }

    public IReadOnlyList<string> GetCanonicalPermanentAugmentIds()
    {
        EnsureLoaded();
        return _sessionContentLookup!.GetCanonicalPermanentAugmentIds();
    }

    public IReadOnlyList<string> GetCanonicalPassiveBoardIds()
    {
        EnsureLoaded();
        return _sessionContentLookup!.GetCanonicalPassiveBoardIds();
    }

    public IReadOnlyList<string> GetCanonicalSynergyFamilyIds()
    {
        EnsureLoaded();
        return _sessionContentLookup!.GetCanonicalSynergyFamilyIds();
    }

    public FirstPlayableSliceDefinition? GetFirstPlayableSlice()
    {
        EnsureLoaded();
        return _sessionContentLookup!.GetFirstPlayableSlice();
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

    public bool TryGetSiteEventChoiceIconId(string eventId, string choiceId, out string iconId)
    {
        EnsureLoaded();
        iconId = string.Empty;
        if (!_registry.SiteEventDefinitions.TryGetValue(eventId, out var definition))
        {
            return false;
        }

        var choice = definition.Choices.FirstOrDefault(candidate =>
            candidate != null && string.Equals(candidate.Id, choiceId, StringComparison.Ordinal));
        if (choice == null || string.IsNullOrWhiteSpace(choice.IconId))
        {
            return false;
        }

        iconId = choice.IconId;
        return true;
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
        return _sessionContentLookup!.TryGetTraitIds(archetypeId, out positiveTraitIds, out negativeTraitIds);
    }

    public string NormalizeArchetypeId(string archetypeId, string raceId, string classId, int fallbackIndex)
    {
        EnsureLoaded();
        return _sessionContentLookup!.NormalizeArchetypeId(archetypeId, raceId, classId, fallbackIndex);
    }

    public string NormalizePositiveTraitId(string archetypeId, string traitId, int fallbackIndex)
    {
        EnsureLoaded();
        return _sessionContentLookup!.NormalizePositiveTraitId(archetypeId, traitId, fallbackIndex);
    }

    public string NormalizeNegativeTraitId(string archetypeId, string traitId, int fallbackIndex)
    {
        EnsureLoaded();
        return _sessionContentLookup!.NormalizeNegativeTraitId(archetypeId, traitId, fallbackIndex);
    }

    public string NormalizeItemBaseId(string itemBaseId, int fallbackIndex)
    {
        EnsureLoaded();
        return _sessionContentLookup!.NormalizeItemBaseId(itemBaseId, fallbackIndex);
    }

    public string NormalizeAffixId(string affixId, int fallbackIndex)
    {
        EnsureLoaded();
        return _sessionContentLookup!.NormalizeAffixId(affixId, fallbackIndex);
    }

    public string NormalizeTemporaryAugmentId(string augmentId, int fallbackIndex)
    {
        EnsureLoaded();
        return _sessionContentLookup!.NormalizeTemporaryAugmentId(augmentId, fallbackIndex);
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
            _sessionContentLookup = new SnapshotSessionContentLookup(_snapshot);
            stopwatch.Stop();
            RuntimeInstrumentation.LogDuration(
                nameof(RuntimeCombatContentLookup) + ".EnsureLoaded",
                stopwatch.Elapsed,
                $"allowEditorRecoveryFallback={AllowsEditorRecoveryFallback}");
        }
    }
}
