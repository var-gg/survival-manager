using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using SM.Combat.Model;
using SM.Combat.Services;
using SM.Content.Definitions;
using SM.Core.Content;
using SM.Meta;
using SM.Meta.Model;
using SM.Meta.Services;
using SM.Unity;
using UnityEditor;

namespace SM.Editor.Validation;

public sealed record CampaignHeadlessBattleParityOutcome(
    string CellId,
    string Winner,
    string FinalBattleStateHash,
    string EncounterId,
    int BattleSeed,
    int StepCount,
    CampaignHeadlessBattleSetupCheckpoint Setup,
    IReadOnlyList<string> StepBattleStateHashes);

public sealed record CampaignHeadlessBattleSetupCheckpoint(
    IReadOnlyList<CampaignHeadlessUnitCheckpoint> CompiledAllyLoadout,
    string CompiledAllyLoadoutCanonicalJson,
    IReadOnlyList<CampaignHeadlessItemCheckpoint> GeneratedAffixAssignments,
    CampaignHeadlessEncounterCheckpoint ResolvedEncounter,
    string ResolvedEncounterCanonicalJson,
    string InitialBattleStateHash);

public sealed record CampaignHeadlessUnitCheckpoint(
    string UnitId,
    string ArchetypeId,
    string PreferredAnchor,
    string RoleTag,
    IReadOnlyList<CampaignHeadlessStatCheckpoint> Stats,
    IReadOnlyList<CampaignHeadlessItemCheckpoint> Equipment);

public sealed record CampaignHeadlessStatCheckpoint(string Key, string Value);

public sealed record CampaignHeadlessItemCheckpoint(
    int AcquisitionIndex,
    string ItemInstanceId,
    string ItemBaseId,
    string SlotType,
    string EquippedHeroId,
    IReadOnlyList<string> AffixIds);

public sealed record CampaignHeadlessEncounterCheckpoint(
    string EncounterId,
    int BattleSeed,
    string BattleContextHash,
    string BossOverlayId,
    string EnemyPosture,
    IReadOnlyList<CampaignHeadlessEnemyCheckpoint> Enemies);

public sealed record CampaignHeadlessEnemyCheckpoint(
    string UnitId,
    string ArchetypeId,
    string PreferredAnchor,
    IReadOnlyList<CampaignHeadlessStatCheckpoint> Stats,
    IReadOnlyList<string> PackageIds,
    IReadOnlyList<string> RulePackageIds,
    IReadOnlyList<string> CompileTags);

public sealed record CampaignHeadlessBattleParityReport(
    string SchemaVersion,
    IReadOnlyList<string> PureLookupMethods,
    IReadOnlyList<string> ScriptableObjectLookupMethods,
    IReadOnlyList<CampaignHeadlessBattleParityOutcome> Outcomes);

internal static partial class CampaignTwoArmSweepRunner
{
    internal static CampaignHeadlessBattleParityReport RunFirstBattleParityWitness(
        IReadOnlyList<string> cellIds)
    {
        var config = CampaignBalanceSweepConfig.Default;
        config.Validate();
        SM.Editor.SeedData.SampleSeedGenerator.RequireCanonicalSampleContentReady(nameof(RunFirstBattleParityWitness));

        var runtimeLookup = new RuntimeCombatContentLookup(allowEditorRecoveryFallback: true);
        if (!runtimeLookup.TryGetCombatSnapshot(out var content, out var contentError))
        {
            throw new InvalidOperationException($"campaign parity content unavailable: {contentError}");
        }

        var lookup = new SessionBattlePathLookupGuard(content);
        var itemIndex = CampaignBalanceSweepRunner.BuildItemMetaIndex(content);
        var order = CampaignContentOrderIndex.Build(content);
        var arm = config.Arms.Single(candidate => string.Equals(candidate.ArmId, "naive", StringComparison.Ordinal));
        var cellsById = config.BuildGrid().ToDictionary(cell => cell.CellId, StringComparer.Ordinal);
        var firstEncounterId = ResolveFirstEncounterId(content);
        var outcomes = new List<CampaignHeadlessBattleParityOutcome>(cellIds.Count);

        foreach (var cellId in cellIds)
        {
            if (!cellsById.TryGetValue(cellId, out var cell))
            {
                throw new InvalidOperationException($"Unknown canonical campaign balance cell: {cellId}");
            }

            CampaignHeadlessBattleParityOutcome? captured = null;
            CampaignHeadlessBattleSetupCheckpoint? setup = null;
            var accumulator = new CampaignTwoArmSweepAccumulator(config);
            RunCell(
                lookup,
                itemIndex,
                order,
                config,
                arm,
                cell,
                accumulator,
                firstEncounterId,
                (state, encounter) =>
                {
                    var stepHashes = new List<string>();
                    var result = BattleResolver.Run(
                        state,
                        BattleSimulator.DefaultMaxSteps,
                        _ =>
                        {
                            stepHashes.Add(BattleStateCanonicalHash.Compute(state));
                        });
                    captured = new CampaignHeadlessBattleParityOutcome(
                        cell.CellId,
                        result.Winner.ToString(),
                        BattleStateCanonicalHash.Compute(state),
                        encounter.Context.EncounterId,
                        encounter.Context.BattleSeed,
                        result.StepCount,
                        setup ?? throw new InvalidOperationException("Campaign parity setup observer did not run."),
                        stepHashes);
                    return result;
                },
                (session, allySnapshot, state, encounter) =>
                    setup = BuildSetupCheckpoint(content, session, allySnapshot, encounter, state));

            outcomes.Add(captured ?? throw new InvalidOperationException(
                $"Campaign parity witness did not reach the first battle: {cell.CellId}"));
        }

        return new CampaignHeadlessBattleParityReport(
            "campaign-headless-battle-parity-v1",
            lookup.PureMethods,
            lookup.ScriptableObjectMethods,
            outcomes);
    }

    private static string ResolveFirstEncounterId(CombatContentSnapshot content)
    {
        var resolver = new EncounterResolutionService(content);
        var progress = resolver.NormalizeCampaignProgress(new CampaignProgressState(
            string.Empty,
            string.Empty,
            Array.Empty<string>(),
            Array.Empty<string>(),
            false,
            false));
        if (content.ExpeditionSites is not { } sites
            || !sites.TryGetValue(progress.SelectedSiteId, out var site))
        {
            throw new InvalidOperationException("Campaign parity could not resolve the first authored site.");
        }

        return site.EncounterIds.First(id => content.Encounters?.ContainsKey(id) == true);
    }

    private static CampaignHeadlessBattleSetupCheckpoint BuildSetupCheckpoint(
        CombatContentSnapshot content,
        GameSessionState session,
        BattleLoadoutSnapshot allySnapshot,
        ResolvedEncounterContext encounter,
        BattleState state)
    {
        var inventory = session.Profile.Inventory
            .Select((item, index) => BuildItemCheckpoint(content, item.ItemInstanceId, item.ItemBaseId, item.EquippedHeroId, item.AffixIds, index))
            .ToArray();
        var inventoryByInstanceId = inventory.ToDictionary(item => item.ItemInstanceId, StringComparer.Ordinal);
        var heroesById = session.Profile.Heroes.ToDictionary(hero => hero.HeroId, StringComparer.Ordinal);
        var allies = allySnapshot.Allies
            .Select(unit =>
            {
                heroesById.TryGetValue(unit.Id, out var hero);
                var equippedIds = new HashSet<string>(
                    hero != null ? hero.EquippedItemIds : Array.Empty<string>(),
                    StringComparer.Ordinal);
                foreach (var item in inventory.Where(item => string.Equals(item.EquippedHeroId, unit.Id, StringComparison.Ordinal)))
                {
                    equippedIds.Add(item.ItemInstanceId);
                }

                return new CampaignHeadlessUnitCheckpoint(
                    unit.Id,
                    unit.ArchetypeId,
                    unit.PreferredAnchor.ToString(),
                    unit.RoleTag,
                    BuildStats(unit),
                    inventory
                        .Where(item => equippedIds.Contains(item.ItemInstanceId) && inventoryByInstanceId.ContainsKey(item.ItemInstanceId))
                        .OrderBy(item => item.AcquisitionIndex)
                        .ToArray());
            })
            .ToArray();

        return new CampaignHeadlessBattleSetupCheckpoint(
            allies,
            SerializeCanonical(allySnapshot),
            inventory,
            BuildEncounterCheckpoint(encounter),
            SerializeCanonical(encounter),
            BattleStateCanonicalHash.Compute(state));
    }

    private static CampaignHeadlessItemCheckpoint BuildItemCheckpoint(
        CombatContentSnapshot content,
        string itemInstanceId,
        string itemBaseId,
        string equippedHeroId,
        IReadOnlyList<string> affixIds,
        int acquisitionIndex)
    {
        var slotType = content.ItemCatalog is { } catalog && catalog.TryGetValue(itemBaseId, out var template)
            ? template.SlotType
            : string.Empty;
        return new CampaignHeadlessItemCheckpoint(
            acquisitionIndex,
            itemInstanceId,
            itemBaseId,
            slotType,
            equippedHeroId ?? string.Empty,
            (affixIds ?? Array.Empty<string>()).ToArray());
    }

    private static CampaignHeadlessEncounterCheckpoint BuildEncounterCheckpoint(ResolvedEncounterContext encounter)
        => new(
            encounter.Context.EncounterId,
            encounter.Context.BattleSeed,
            encounter.Context.BattleContextHash,
            encounter.Context.BossOverlayId,
            encounter.EnemyPosture.ToString(),
            encounter.Enemies.Select(unit => new CampaignHeadlessEnemyCheckpoint(
                    unit.Id,
                    unit.ArchetypeId,
                    unit.PreferredAnchor.ToString(),
                    BuildStats(unit),
                    (unit.Packages ?? Array.Empty<CombatModifierPackage>())
                        .Select(package => package.SourceId)
                        .ToArray(),
                    (unit.RulePackages ?? Array.Empty<CombatRuleModifierPackage>())
                        .Select(package => package.SourceId)
                        .ToArray(),
                    (unit.CompileTags ?? Array.Empty<string>()).ToArray()))
                .ToArray());

    private static IReadOnlyList<CampaignHeadlessStatCheckpoint> BuildStats(BattleUnitLoadout unit)
        => unit.BaseStats
            .OrderBy(pair => pair.Key.ToString(), StringComparer.Ordinal)
            .Select(pair => new CampaignHeadlessStatCheckpoint(
                pair.Key.ToString(),
                pair.Value.ToString("R", CultureInfo.InvariantCulture)))
            .ToArray();

    private static string SerializeCanonical(object value)
        => JsonConvert.SerializeObject(
            value,
            Formatting.None,
            new JsonSerializerSettings
            {
                Culture = CultureInfo.InvariantCulture,
                ReferenceLoopHandling = ReferenceLoopHandling.Ignore,
            });

}

public static class CampaignHeadlessBattleParityExporter
{
    private const string CellSelectionPath = "tools/HeadlessSweep/campaign-battle-cells.json";
    private const string DefaultOutputPath = "Temp/HeadlessSweep/unity-campaign-battles.json";

    [MenuItem("Tools/SM/Validation/Export Headless Battle Parity")]
    public static void Export()
    {
        var root = Directory.GetCurrentDirectory();
        var selectionPath = Path.Combine(root, CellSelectionPath.Replace('/', Path.DirectorySeparatorChar));
        var selection = JsonConvert.DeserializeObject<CampaignHeadlessBattleCellSelection>(
                            File.ReadAllText(selectionPath))
                        ?? throw new InvalidOperationException("Failed to load headless battle cell selection.");
        if (selection.CellIds.Count == 0)
        {
            throw new InvalidOperationException("Headless battle cell selection is empty.");
        }

        var report = CampaignTwoArmSweepRunner.RunFirstBattleParityWitness(selection.CellIds);
        if (report.ScriptableObjectLookupMethods.Count > 0)
        {
            throw new InvalidOperationException(
                $"Campaign battle path touched ScriptableObject lookup methods: {string.Join(",", report.ScriptableObjectLookupMethods)}");
        }

        var configuredOutput = Environment.GetEnvironmentVariable("SM_HEADLESS_BATTLE_UNITY_OUTPUT");
        var outputPath = string.IsNullOrWhiteSpace(configuredOutput)
            ? Path.Combine(root, DefaultOutputPath.Replace('/', Path.DirectorySeparatorChar))
            : Path.GetFullPath(configuredOutput);
        Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
        File.WriteAllText(outputPath, JsonConvert.SerializeObject(report, Formatting.Indented));
        UnityEngine.Debug.Log($"[CampaignHeadlessBattleParity] cells={report.Outcomes.Count} output={outputPath}");
    }

    private sealed class CampaignHeadlessBattleCellSelection
    {
        public List<string> CellIds { get; set; } = new();
    }
}

/// <summary>
/// GameSessionState의 legacy 생성자에는 ICombatContentLookup이 필요하지만 parity battle path에는
/// pure session port만 허용한다. SO surface 호출은 즉시 실패시켜 scope 회귀를 runtime으로 잡는다.
/// </summary>
internal sealed class SessionBattlePathLookupGuard : ICombatContentLookup
{
    private readonly SnapshotSessionContentLookup _inner;
    private readonly HashSet<string> _pureMethods = new(StringComparer.Ordinal);
    private readonly HashSet<string> _scriptableObjectMethods = new(StringComparer.Ordinal);

    internal SessionBattlePathLookupGuard(CombatContentSnapshot snapshot)
    {
        _inner = new SnapshotSessionContentLookup(snapshot);
    }

    public IReadOnlyList<string> PureMethods => _pureMethods.OrderBy(value => value, StringComparer.Ordinal).ToArray();
    public IReadOnlyList<string> ScriptableObjectMethods =>
        _scriptableObjectMethods.OrderBy(value => value, StringComparer.Ordinal).ToArray();

    public CombatContentSnapshot Snapshot
    {
        get
        {
            RecordPure(nameof(Snapshot));
            return _inner.Snapshot;
        }
    }

    public bool TryGetCombatSnapshot(out CombatContentSnapshot snapshot, out string error)
    {
        RecordPure(nameof(TryGetCombatSnapshot));
        return _inner.TryGetCombatSnapshot(out snapshot, out error);
    }

    public IReadOnlyList<string> GetCanonicalArchetypeIds() => Pure(nameof(GetCanonicalArchetypeIds), _inner.GetCanonicalArchetypeIds);
    public IReadOnlyList<string> GetCanonicalItemIds() => Pure(nameof(GetCanonicalItemIds), _inner.GetCanonicalItemIds);
    public IReadOnlyList<string> GetCanonicalAffixIds() => Pure(nameof(GetCanonicalAffixIds), _inner.GetCanonicalAffixIds);
    public IReadOnlyList<string> GetCanonicalTemporaryAugmentIds() => Pure(nameof(GetCanonicalTemporaryAugmentIds), _inner.GetCanonicalTemporaryAugmentIds);
    public IReadOnlyList<string> GetCanonicalPermanentAugmentIds() => Pure(nameof(GetCanonicalPermanentAugmentIds), _inner.GetCanonicalPermanentAugmentIds);
    public IReadOnlyList<string> GetCanonicalPassiveBoardIds() => Pure(nameof(GetCanonicalPassiveBoardIds), _inner.GetCanonicalPassiveBoardIds);
    public IReadOnlyList<string> GetCanonicalSynergyFamilyIds() => Pure(nameof(GetCanonicalSynergyFamilyIds), _inner.GetCanonicalSynergyFamilyIds);
    public FirstPlayableSliceDefinition? GetFirstPlayableSlice() => Pure(nameof(GetFirstPlayableSlice), _inner.GetFirstPlayableSlice);

    public bool TryGetTraitIds(string archetypeId, out IReadOnlyList<string> positiveTraitIds, out IReadOnlyList<string> negativeTraitIds)
    {
        RecordPure(nameof(TryGetTraitIds));
        return _inner.TryGetTraitIds(archetypeId, out positiveTraitIds, out negativeTraitIds);
    }

    public string NormalizeArchetypeId(string archetypeId, string raceId, string classId, int fallbackIndex) =>
        Pure(nameof(NormalizeArchetypeId), () => _inner.NormalizeArchetypeId(archetypeId, raceId, classId, fallbackIndex));
    public string NormalizePositiveTraitId(string archetypeId, string traitId, int fallbackIndex) =>
        Pure(nameof(NormalizePositiveTraitId), () => _inner.NormalizePositiveTraitId(archetypeId, traitId, fallbackIndex));
    public string NormalizeNegativeTraitId(string archetypeId, string traitId, int fallbackIndex) =>
        Pure(nameof(NormalizeNegativeTraitId), () => _inner.NormalizeNegativeTraitId(archetypeId, traitId, fallbackIndex));
    public string NormalizeItemBaseId(string itemBaseId, int fallbackIndex) =>
        Pure(nameof(NormalizeItemBaseId), () => _inner.NormalizeItemBaseId(itemBaseId, fallbackIndex));
    public string NormalizeAffixId(string affixId, int fallbackIndex) =>
        Pure(nameof(NormalizeAffixId), () => _inner.NormalizeAffixId(affixId, fallbackIndex));
    public string NormalizeTemporaryAugmentId(string augmentId, int fallbackIndex) =>
        Pure(nameof(NormalizeTemporaryAugmentId), () => _inner.NormalizeTemporaryAugmentId(augmentId, fallbackIndex));

    public IReadOnlyDictionary<string, UnitArchetypeDefinition> ArchetypeDefinitions =>
        Reject<IReadOnlyDictionary<string, UnitArchetypeDefinition>>(nameof(ArchetypeDefinitions));
    public bool TryGetArchetype(string archetypeId, out UnitArchetypeDefinition archetype) => Reject(nameof(TryGetArchetype), out archetype);
    public bool TryGetItemDefinition(string itemId, out ItemBaseDefinition item) => Reject(nameof(TryGetItemDefinition), out item);
    public bool TryGetRaceDefinition(string raceId, out RaceDefinition race) => Reject(nameof(TryGetRaceDefinition), out race);
    public bool TryGetClassDefinition(string classId, out ClassDefinition @class) => Reject(nameof(TryGetClassDefinition), out @class);
    public bool TryGetCharacterDefinition(string characterId, out CharacterDefinition character) => Reject(nameof(TryGetCharacterDefinition), out character);
    public bool TryGetAugmentDefinition(string augmentId, out AugmentDefinition augment) => Reject(nameof(TryGetAugmentDefinition), out augment);
    public bool TryGetSkillDefinition(string skillId, out SkillDefinitionAsset skill) => Reject(nameof(TryGetSkillDefinition), out skill);
    public bool TryGetAffixDefinition(string affixId, out AffixDefinition affix) => Reject(nameof(TryGetAffixDefinition), out affix);
    public bool TryGetPassiveBoardDefinition(string boardId, out PassiveBoardDefinition board) => Reject(nameof(TryGetPassiveBoardDefinition), out board);
    public bool TryGetPassiveNodeDefinition(string nodeId, out PassiveNodeDefinition node) => Reject(nameof(TryGetPassiveNodeDefinition), out node);
    public bool TryGetTeamTacticDefinition(string teamTacticId, out TeamTacticDefinition teamTactic) => Reject(nameof(TryGetTeamTacticDefinition), out teamTactic);
    public bool TryGetSynergyDefinition(string synergyId, out SynergyDefinition synergy) => Reject(nameof(TryGetSynergyDefinition), out synergy);
    public bool TryGetRoleInstructionDefinition(string roleInstructionId, out RoleInstructionDefinition roleInstruction) => Reject(nameof(TryGetRoleInstructionDefinition), out roleInstruction);
    public bool TryGetCampaignChapterDefinition(string chapterId, out CampaignChapterDefinition chapter) => Reject(nameof(TryGetCampaignChapterDefinition), out chapter);
    public bool TryGetExpeditionSiteDefinition(string siteId, out ExpeditionSiteDefinition site) => Reject(nameof(TryGetExpeditionSiteDefinition), out site);
    public bool TryGetEncounterDefinition(string encounterId, out EncounterDefinition encounter) => Reject(nameof(TryGetEncounterDefinition), out encounter);
    public bool TryGetSiteEventChoiceIconId(string eventId, string choiceId, out string iconId) => Reject(nameof(TryGetSiteEventChoiceIconId), out iconId);
    public bool TryGetTraitEntry(string archetypeId, string traitId, out TraitEntry trait) => Reject(nameof(TryGetTraitEntry), out trait);
    public IReadOnlyList<CampaignChapterDefinition> GetOrderedCampaignChapters() =>
        Reject<IReadOnlyList<CampaignChapterDefinition>>(nameof(GetOrderedCampaignChapters));

    private T Pure<T>(string method, Func<T> action)
    {
        RecordPure(method);
        return action();
    }

    private void RecordPure(string method) => _pureMethods.Add(method);

    private bool Reject<T>(string method, out T value) where T : class
    {
        value = null!;
        Reject<T>(method);
        return false;
    }

    private T Reject<T>(string method)
    {
        _scriptableObjectMethods.Add(method);
        throw new InvalidOperationException($"ScriptableObject lookup is forbidden on campaign battle path: {method}");
    }
}
