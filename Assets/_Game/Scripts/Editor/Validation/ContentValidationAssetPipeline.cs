using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using SM.Content.Definitions;
using SM.Core.Content;
using SM.Editor.SeedData;
using SM.Unity;
using UnityEditor;
using UnityEngine;

namespace SM.Editor.Validation;

internal enum ValidationAssetSourceKind
{
    Explicit = 0,
    Resources = 1,
    AssetDatabaseTyped = 2,
    AssetDatabaseGeneric = 3,
    FileSystem = 4,
    Fallback = 5,
}

internal sealed record ValidationAssetDescriptor(
    ScriptableObject Asset,
    string AssetPath,
    ValidationAssetSourceKind SourceKind,
    Type AssetType);

internal sealed class ValidationAssetCatalog
{
    private readonly IReadOnlyList<ValidationAssetDescriptor> _descriptors;
    private readonly IReadOnlyDictionary<int, ValidationAssetDescriptor> _descriptorsByInstanceId;

    internal ValidationAssetCatalog(IReadOnlyList<ValidationAssetDescriptor> descriptors)
    {
        _descriptors = descriptors;
        _descriptorsByInstanceId = descriptors.ToDictionary(descriptor => descriptor.Asset.GetInstanceID());
    }

    internal IReadOnlyList<ValidationAssetDescriptor> Descriptors => _descriptors;

    internal IReadOnlyList<ScriptableObject> Assets => _descriptors.Select(descriptor => descriptor.Asset).ToList();

    internal IEnumerable<TAsset> OfType<TAsset>() where TAsset : ScriptableObject
    {
        return _descriptors
            .Where(descriptor => descriptor.Asset is TAsset)
            .Select(descriptor => (TAsset)descriptor.Asset);
    }

    internal string GetPath(UnityEngine.Object asset)
    {
        if (asset == null)
        {
            return string.Empty;
        }

        return _descriptorsByInstanceId.TryGetValue(asset.GetInstanceID(), out var descriptor)
            ? descriptor.AssetPath
            : asset.name ?? string.Empty;
    }
}

internal interface IUnityAssetGateway
{
    void RefreshAssets();
    string ResolveAssetPath(UnityEngine.Object asset);
    void RegisterResolvedAssetPath(UnityEngine.Object asset, string assetPath);
    bool IsValidFolder(string path);
    string GetProjectRoot();
    IReadOnlyList<ValidationAssetDescriptor> LoadResourcesAssets(string resourcesPath);
    IReadOnlyList<ValidationAssetDescriptor> LoadTypedAssets(string root, Type definitionType);
    IReadOnlyList<ValidationAssetDescriptor> LoadGenericAssets(string root);
    IReadOnlyList<ValidationAssetDescriptor> LoadDiskAssets(string root);
}

internal sealed class UnityAssetGateway : IUnityAssetGateway
{
    private readonly Dictionary<int, string> _fallbackResolvedAssetPaths = new();

    public void RefreshAssets()
    {
        AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
    }

    public string ResolveAssetPath(UnityEngine.Object asset)
    {
        if (asset == null)
        {
            return string.Empty;
        }

        var assetPath = AssetDatabase.GetAssetPath(asset);
        if (!string.IsNullOrWhiteSpace(assetPath))
        {
            return assetPath;
        }

        return _fallbackResolvedAssetPaths.TryGetValue(asset.GetInstanceID(), out var fallbackPath)
            ? fallbackPath
            : asset.name ?? string.Empty;
    }

    public void RegisterResolvedAssetPath(UnityEngine.Object asset, string assetPath)
    {
        if (asset == null || string.IsNullOrWhiteSpace(assetPath))
        {
            return;
        }

        _fallbackResolvedAssetPaths[asset.GetInstanceID()] = assetPath;
    }

    public bool IsValidFolder(string path)
    {
        return AssetDatabase.IsValidFolder(path);
    }

    public string GetProjectRoot()
    {
        return Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
    }

    public IReadOnlyList<ValidationAssetDescriptor> LoadResourcesAssets(string resourcesPath)
    {
        return Resources.LoadAll<ScriptableObject>(resourcesPath)
            .Where(asset => asset != null)
            .Select(asset => new ValidationAssetDescriptor(asset, ResolveAssetPath(asset), ValidationAssetSourceKind.Resources, asset.GetType()))
            .ToList();
    }

    public IReadOnlyList<ValidationAssetDescriptor> LoadTypedAssets(string root, Type definitionType)
    {
        if (!IsValidFolder(root))
        {
            return Array.Empty<ValidationAssetDescriptor>();
        }

        return AssetDatabase.FindAssets($"t:{definitionType.Name}", new[] { root })
            .Select(AssetDatabase.GUIDToAssetPath)
            .Where(path => path.EndsWith(".asset", StringComparison.OrdinalIgnoreCase))
            .Select(path => (Asset: LoadDefinitionAssetAtPath(path, definitionType), Path: path))
            .Where(candidate => candidate.Asset != null)
            .Select(candidate => new ValidationAssetDescriptor(candidate.Asset!, candidate.Path, ValidationAssetSourceKind.AssetDatabaseTyped, candidate.Asset!.GetType()))
            .ToList();
    }

    public IReadOnlyList<ValidationAssetDescriptor> LoadGenericAssets(string root)
    {
        if (!IsValidFolder(root))
        {
            return Array.Empty<ValidationAssetDescriptor>();
        }

        return AssetDatabase.FindAssets(string.Empty, new[] { root })
            .Select(AssetDatabase.GUIDToAssetPath)
            .Where(path => path.EndsWith(".asset", StringComparison.OrdinalIgnoreCase))
            .Select(path => (Asset: LoadDefinitionAssetAtPath(path), Path: path))
            .Where(candidate => candidate.Asset != null)
            .Select(candidate => new ValidationAssetDescriptor(candidate.Asset!, candidate.Path, ValidationAssetSourceKind.AssetDatabaseGeneric, candidate.Asset!.GetType()))
            .ToList();
    }

    public IReadOnlyList<ValidationAssetDescriptor> LoadDiskAssets(string root)
    {
        var projectRoot = GetProjectRoot().Replace('\\', '/');
        var diskRoot = Path.Combine(GetProjectRoot(), root.Replace('/', Path.DirectorySeparatorChar));
        if (!Directory.Exists(diskRoot))
        {
            return Array.Empty<ValidationAssetDescriptor>();
        }

        return Directory.EnumerateFiles(diskRoot, "*.asset", SearchOption.AllDirectories)
            .Select(path => path.Replace('\\', '/'))
            .Select(path => path.StartsWith(projectRoot + "/", StringComparison.Ordinal)
                ? path[(projectRoot.Length + 1)..]
                : path)
            .Select(path => (Asset: LoadDefinitionAssetAtPath(path), Path: path))
            .Where(candidate => candidate.Asset != null)
            .Select(candidate => new ValidationAssetDescriptor(candidate.Asset!, candidate.Path, ValidationAssetSourceKind.FileSystem, candidate.Asset!.GetType()))
            .ToList();
    }

    private static ScriptableObject? LoadDefinitionAssetAtPath(string path)
    {
        return LoadDefinitionAssetAtPath(path, typeof(ScriptableObject));
    }

    private static ScriptableObject? LoadDefinitionAssetAtPath(string path, Type definitionType)
    {
        var typedAsset = AssetDatabase.LoadAssetAtPath(path, definitionType) as ScriptableObject;
        if (typedAsset != null)
        {
            return typedAsset;
        }

        var mainAsset = AssetDatabase.LoadMainAssetAtPath(path) as ScriptableObject;
        if (mainAsset != null)
        {
            return mainAsset;
        }

        return AssetDatabase.LoadAllAssetsAtPath(path)
            .OfType<ScriptableObject>()
            .FirstOrDefault(asset => definitionType == typeof(ScriptableObject) || definitionType.IsInstanceOfType(asset));
    }
}

internal interface IAssetLoader
{
    IReadOnlyList<ValidationAssetDescriptor> LoadAssets();
}

internal sealed class ResourcesAssetLoader : IAssetLoader
{
    private readonly IUnityAssetGateway _gateway;

    public ResourcesAssetLoader(IUnityAssetGateway gateway)
    {
        _gateway = gateway;
    }

    public IReadOnlyList<ValidationAssetDescriptor> LoadAssets()
    {
        return _gateway.LoadResourcesAssets("_Game/Content/Definitions");
    }
}

internal sealed class AssetDatabaseTypedAssetLoader : IAssetLoader
{
    private readonly IUnityAssetGateway _gateway;
    private readonly IReadOnlyList<Type> _definitionTypes;
    private readonly string _root;

    public AssetDatabaseTypedAssetLoader(IUnityAssetGateway gateway, IReadOnlyList<Type> definitionTypes, string root)
    {
        _gateway = gateway;
        _definitionTypes = definitionTypes;
        _root = root;
    }

    public IReadOnlyList<ValidationAssetDescriptor> LoadAssets()
    {
        return _definitionTypes
            .SelectMany(definitionType => _gateway.LoadTypedAssets(_root, definitionType))
            .ToList();
    }
}

internal sealed class AssetDatabaseGenericAssetLoader : IAssetLoader
{
    private readonly IUnityAssetGateway _gateway;
    private readonly string _root;

    public AssetDatabaseGenericAssetLoader(IUnityAssetGateway gateway, string root)
    {
        _gateway = gateway;
        _root = root;
    }

    public IReadOnlyList<ValidationAssetDescriptor> LoadAssets()
    {
        return _gateway.LoadGenericAssets(_root);
    }
}

internal sealed class FileSystemAssetLoader : IAssetLoader
{
    private readonly IUnityAssetGateway _gateway;
    private readonly string _root;

    public FileSystemAssetLoader(IUnityAssetGateway gateway, string root)
    {
        _gateway = gateway;
        _root = root;
    }

    public IReadOnlyList<ValidationAssetDescriptor> LoadAssets()
    {
        return _gateway.LoadDiskAssets(_root);
    }
}

internal sealed class FallbackDefinitionFileLoader : IAssetLoader
{
    private readonly IUnityAssetGateway _gateway;

    public FallbackDefinitionFileLoader(IUnityAssetGateway gateway)
    {
        _gateway = gateway;
    }

    public IReadOnlyList<ValidationAssetDescriptor> LoadAssets()
    {
        var fallback = ContentDefinitionFileFallbackLoader.Load();
        return fallback.Assets
            .Select(asset =>
            {
                var assetPath = fallback.AssetPaths.TryGetValue(asset.GetInstanceID(), out var resolvedPath)
                    ? resolvedPath
                    : asset.name;
                _gateway.RegisterResolvedAssetPath(asset, assetPath);
                return new ValidationAssetDescriptor(asset, assetPath, ValidationAssetSourceKind.Fallback, asset.GetType());
            })
            .ToList();
    }
}

internal sealed class CompositeAssetLoader : IAssetLoader
{
    private readonly IAssetLoader _resourcesLoader;
    private readonly IAssetLoader _typedLoader;
    private readonly IAssetLoader _genericLoader;
    private readonly IAssetLoader _fileSystemLoader;
    private readonly IAssetLoader _fallbackLoader;

    public CompositeAssetLoader(
        IAssetLoader resourcesLoader,
        IAssetLoader typedLoader,
        IAssetLoader genericLoader,
        IAssetLoader fileSystemLoader,
        IAssetLoader fallbackLoader)
    {
        _resourcesLoader = resourcesLoader;
        _typedLoader = typedLoader;
        _genericLoader = genericLoader;
        _fileSystemLoader = fileSystemLoader;
        _fallbackLoader = fallbackLoader;
    }

    public IReadOnlyList<ValidationAssetDescriptor> LoadAssets()
    {
        var candidates = new List<ValidationAssetDescriptor>();
        candidates.AddRange(_resourcesLoader.LoadAssets());
        candidates.AddRange(_typedLoader.LoadAssets());
        candidates.AddRange(_genericLoader.LoadAssets());

        if (candidates.Count == 0)
        {
            candidates.AddRange(_fileSystemLoader.LoadAssets());
        }

        if (candidates.Count == 0)
        {
            candidates.AddRange(_fallbackLoader.LoadAssets());
        }

        return candidates;
    }
}

internal interface IValidationAssetSelectionPolicy
{
    IReadOnlyList<ValidationAssetDescriptor> Select(IReadOnlyList<ValidationAssetDescriptor> candidates);
}

internal interface IValidationAssetSkipPolicy
{
    bool ShouldSkip(ValidationAssetDescriptor descriptor);
}

internal sealed class DefaultValidationAssetSkipPolicy : IValidationAssetSkipPolicy
{
    public bool ShouldSkip(ValidationAssetDescriptor descriptor)
    {
        return false;
    }
}

internal interface IValidationAssetIdentityResolver
{
    string ResolveCanonicalId(ScriptableObject asset);
}

internal sealed class DefaultValidationAssetIdentityResolver : IValidationAssetIdentityResolver
{
    public string ResolveCanonicalId(ScriptableObject asset)
    {
        return asset switch
        {
            StatDefinition stat => stat.Id,
            RaceDefinition race => race.Id,
            ClassDefinition @class => @class.Id,
            CharacterDefinition character => character.Id,
            TraitPoolDefinition traitPool => traitPool.Id,
            UnitArchetypeDefinition archetype => archetype.Id,
            SkillDefinitionAsset skill => skill.Id,
            AugmentDefinition augment => augment.Id,
            ItemBaseDefinition item => item.Id,
            AffixDefinition affix => affix.Id,
            StableTagDefinition tag => tag.Id,
            TeamTacticDefinition tactic => tactic.Id,
            RoleInstructionDefinition role => role.Id,
            PassiveBoardDefinition board => board.Id,
            PassiveNodeDefinition node => node.Id,
            SynergyDefinition synergy => synergy.Id,
            SynergyTierDefinition tier => tier.Id,
            ExpeditionDefinition expedition => expedition.Id,
            RewardTableDefinition rewardTable => rewardTable.Id,
            CampaignChapterDefinition chapter => chapter.Id,
            ExpeditionSiteDefinition site => site.Id,
            EncounterDefinition encounter => encounter.Id,
            EnemySquadTemplateDefinition squad => squad.Id,
            BossOverlayDefinition overlay => overlay.Id,
            StatusFamilyDefinition status => status.Id,
            CleanseProfileDefinition cleanse => cleanse.Id,
            ControlDiminishingRuleDefinition controlRule => controlRule.Id,
            RewardSourceDefinition rewardSource => rewardSource.Id,
            DropTableDefinition dropTable => dropTable.Id,
            LootBundleDefinition lootBundle => lootBundle.Id,
            TraitTokenDefinition traitToken => traitToken.Id,
            FirstPlayableSliceDefinitionAsset firstPlayable => firstPlayable.name,
            _ => string.Empty,
        };
    }
}

internal interface IValidationAssetCompletenessScorer
{
    int Score(ScriptableObject asset);
}

internal sealed class DefaultValidationAssetCompletenessScorer : IValidationAssetCompletenessScorer
{
    private readonly IValidationAssetIdentityResolver _identityResolver;
    private readonly IReadOnlyDictionary<Type, Func<ScriptableObject, int>> _typedScorers;

    public DefaultValidationAssetCompletenessScorer(IValidationAssetIdentityResolver identityResolver)
    {
        _identityResolver = identityResolver;
        _typedScorers = new Dictionary<Type, Func<ScriptableObject, int>>
        {
            [typeof(UnitArchetypeDefinition)] = asset => ScoreArchetype((UnitArchetypeDefinition)asset),
            [typeof(SkillDefinitionAsset)] = asset => ScoreSkill((SkillDefinitionAsset)asset),
            [typeof(AugmentDefinition)] = asset => ScoreAugment((AugmentDefinition)asset),
            [typeof(SynergyTierDefinition)] = asset => ScoreSynergyTier((SynergyTierDefinition)asset),
            [typeof(StatusFamilyDefinition)] = asset => ScoreStatusFamily((StatusFamilyDefinition)asset),
            [typeof(CleanseProfileDefinition)] = asset => ScoreCleanseProfile((CleanseProfileDefinition)asset),
        };
    }

    public int Score(ScriptableObject asset)
    {
        if (asset == null)
        {
            return 0;
        }

        return _typedScorers.TryGetValue(asset.GetType(), out var scorer)
            ? scorer(asset)
            : ScoreFallback(asset);
    }

    private int ScoreFallback(ScriptableObject asset)
    {
        return string.IsNullOrWhiteSpace(_identityResolver.ResolveCanonicalId(asset)) ? 0 : 10;
    }

    private static int ScoreArchetype(UnitArchetypeDefinition archetype)
    {
        var score = string.IsNullOrWhiteSpace(archetype.Id) ? 0 : 10;
        score += archetype.Race != null ? 5 : 0;
        score += archetype.Class != null ? 5 : 0;
        score += archetype.TraitPool != null ? 5 : 0;
        score += archetype.Skills?.Count ?? 0;
        score += archetype.TacticPreset?.Count ?? 0;
        score += archetype.BudgetCard?.Vector?.FinalScore ?? 0;
        return score;
    }

    private static int ScoreSkill(SkillDefinitionAsset skill)
    {
        var score = string.IsNullOrWhiteSpace(skill.Id) ? 0 : 10;
        score += skill.BudgetCard?.Vector?.FinalScore ?? 0;
        score += skill.CompileTags?.Count ?? 0;
        return score;
    }

    private static int ScoreAugment(AugmentDefinition augment)
    {
        var score = string.IsNullOrWhiteSpace(augment.Id) ? 0 : 10;
        score += augment.BudgetCard?.Vector?.FinalScore ?? 0;
        score += augment.Modifiers?.Count ?? 0;
        return score;
    }

    private static int ScoreSynergyTier(SynergyTierDefinition tier)
    {
        var score = string.IsNullOrWhiteSpace(tier.Id) ? 0 : 10;
        score += tier.BudgetCard?.Vector?.FinalScore ?? 0;
        score += tier.Threshold;
        return score;
    }

    private static int ScoreStatusFamily(StatusFamilyDefinition status)
    {
        var score = string.IsNullOrWhiteSpace(status.Id) ? 0 : 10;
        score += status.BudgetCard?.Vector?.FinalScore ?? 0;
        score += status.DefaultStackCap;
        return score;
    }

    private static int ScoreCleanseProfile(CleanseProfileDefinition cleanse)
    {
        var score = string.IsNullOrWhiteSpace(cleanse.Id) ? 0 : 10;
        score += cleanse.RemovesStatusIds?.Count ?? 0;
        score += cleanse.GrantsUnstoppable ? 2 : 0;
        return score;
    }
}

internal interface IValidationAssetDeduper
{
    IReadOnlyList<ValidationAssetDescriptor> Deduplicate(IReadOnlyList<ValidationAssetDescriptor> descriptors);
}

internal sealed class DefaultValidationAssetDeduper : IValidationAssetDeduper
{
    private readonly IValidationAssetCompletenessScorer _scorer;
    private readonly IValidationAssetIdentityResolver _identityResolver;

    public DefaultValidationAssetDeduper(
        IValidationAssetCompletenessScorer scorer,
        IValidationAssetIdentityResolver identityResolver)
    {
        _scorer = scorer;
        _identityResolver = identityResolver;
    }

    public IReadOnlyList<ValidationAssetDescriptor> Deduplicate(IReadOnlyList<ValidationAssetDescriptor> descriptors)
    {
        var assetsByPath = new Dictionary<string, ValidationAssetDescriptor>(StringComparer.Ordinal);
        var looseAssetsByIdentity = new Dictionary<string, ValidationAssetDescriptor>(StringComparer.Ordinal);
        var looseAssetsByInstanceId = new Dictionary<int, ValidationAssetDescriptor>();

        foreach (var descriptor in descriptors)
        {
            if (!string.IsNullOrWhiteSpace(descriptor.AssetPath))
            {
                PromoteBestCandidate(assetsByPath, descriptor.AssetPath, descriptor);
                continue;
            }

            var canonicalId = _identityResolver.ResolveCanonicalId(descriptor.Asset);
            if (!string.IsNullOrWhiteSpace(canonicalId))
            {
                PromoteBestCandidate(looseAssetsByIdentity, BuildLooseIdentityKey(descriptor.AssetType, canonicalId), descriptor);
                continue;
            }

            PromoteBestInstance(looseAssetsByInstanceId, descriptor);
        }

        return assetsByPath.Values
            .Concat(looseAssetsByIdentity.Values)
            .Concat(looseAssetsByInstanceId.Values)
            .ToList();
    }

    private static string BuildLooseIdentityKey(Type assetType, string canonicalId)
    {
        return $"{assetType.FullName}:{canonicalId}";
    }

    private void PromoteBestCandidate(
        IDictionary<string, ValidationAssetDescriptor> selected,
        string key,
        ValidationAssetDescriptor candidate)
    {
        if (!selected.TryGetValue(key, out var existing) || IsBetterCandidate(candidate, existing))
        {
            selected[key] = candidate;
        }
    }

    private void PromoteBestInstance(
        IDictionary<int, ValidationAssetDescriptor> selected,
        ValidationAssetDescriptor candidate)
    {
        var instanceId = candidate.Asset.GetInstanceID();
        if (!selected.TryGetValue(instanceId, out var existing) || IsBetterCandidate(candidate, existing))
        {
            selected[instanceId] = candidate;
        }
    }

    private bool IsBetterCandidate(ValidationAssetDescriptor candidate, ValidationAssetDescriptor existing)
    {
        var candidateScore = _scorer.Score(candidate.Asset);
        var existingScore = _scorer.Score(existing.Asset);
        if (candidateScore != existingScore)
        {
            return candidateScore > existingScore;
        }

        if (candidate.SourceKind != existing.SourceKind)
        {
            return candidate.SourceKind > existing.SourceKind;
        }

        return candidate.Asset.GetInstanceID() >= existing.Asset.GetInstanceID();
    }
}

internal sealed class ValidationAssetSelectionPolicy : IValidationAssetSelectionPolicy
{
    private readonly IValidationAssetSkipPolicy _skipPolicy;
    private readonly IValidationAssetDeduper _deduper;

    public ValidationAssetSelectionPolicy()
        : this(
            new DefaultValidationAssetSkipPolicy(),
            new DefaultValidationAssetDeduper(
                new DefaultValidationAssetCompletenessScorer(new DefaultValidationAssetIdentityResolver()),
                new DefaultValidationAssetIdentityResolver()))
    {
    }

    public ValidationAssetSelectionPolicy(
        IValidationAssetSkipPolicy skipPolicy,
        IValidationAssetDeduper deduper)
    {
        _skipPolicy = skipPolicy;
        _deduper = deduper;
    }

    public IReadOnlyList<ValidationAssetDescriptor> Select(IReadOnlyList<ValidationAssetDescriptor> candidates)
    {
        var filtered = candidates
            .Where(descriptor => descriptor.Asset != null && !_skipPolicy.ShouldSkip(descriptor))
            .ToList();

        return _deduper.Deduplicate(filtered);
    }
}

internal static class ValidationKnownDefinitionTypes
{
    internal static IReadOnlyList<Type> All { get; } = new[]
    {
        typeof(StatDefinition),
        typeof(RaceDefinition),
        typeof(ClassDefinition),
        typeof(CharacterDefinition),
        typeof(TraitPoolDefinition),
        typeof(UnitArchetypeDefinition),
        typeof(SkillDefinitionAsset),
        typeof(AugmentDefinition),
        typeof(ItemBaseDefinition),
        typeof(AffixDefinition),
        typeof(StableTagDefinition),
        typeof(TeamTacticDefinition),
        typeof(RoleInstructionDefinition),
        typeof(PassiveBoardDefinition),
        typeof(PassiveNodeDefinition),
        typeof(SynergyDefinition),
        typeof(SynergyTierDefinition),
        typeof(ExpeditionDefinition),
        typeof(RewardTableDefinition),
        typeof(CampaignChapterDefinition),
        typeof(ExpeditionSiteDefinition),
        typeof(EncounterDefinition),
        typeof(EnemySquadTemplateDefinition),
        typeof(BossOverlayDefinition),
        typeof(StatusFamilyDefinition),
        typeof(CleanseProfileDefinition),
        typeof(ControlDiminishingRuleDefinition),
        typeof(RewardSourceDefinition),
        typeof(DropTableDefinition),
        typeof(LootBundleDefinition),
        typeof(TraitTokenDefinition),
        typeof(FirstPlayableSliceDefinitionAsset),
    };
}

internal static class ValidationAssetPipelineDefaults
{
    internal static string Root => SampleSeedGenerator.ResourcesRoot.Replace('\\', '/');
}
