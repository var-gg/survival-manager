using System;
using System.Collections.Generic;
using System.Linq;
using SM.Content.Definitions;
using SM.Core.Content;
using UnityEngine;

namespace SM.Editor.Validation;

internal sealed class ValidationReportBuilder
{
    private readonly Dictionary<string, List<string>> _canonicalIds = new(StringComparer.Ordinal);
    private readonly Dictionary<string, List<string>> _localizationKeys = new(StringComparer.Ordinal);
    private readonly Dictionary<string, List<string>> _passiveBoardsByClassId = new(StringComparer.Ordinal);
    private readonly List<ContentValidationIssue> _issues = new();

    internal ICollection<ContentValidationIssue> Issues => _issues;
    internal LaunchScopeCountReport LaunchScope { get; set; } = new();
    internal IReadOnlyList<string> FloorGaps { get; set; } = Array.Empty<string>();
    internal IReadOnlyList<string> SafeTargetGaps { get; set; } = Array.Empty<string>();
    internal IReadOnlyList<PassiveBoardShapeReport> PassiveBoards { get; set; } = Array.Empty<PassiveBoardShapeReport>();
    internal LoopCValidationSummary LoopC { get; set; } = new();

    internal void RegisterCanonicalId(string kind, string id, string assetPath)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            return;
        }

        var composite = $"{kind}:{id}";
        if (!_canonicalIds.TryGetValue(composite, out var paths))
        {
            paths = new List<string>();
            _canonicalIds[composite] = paths;
        }

        paths.Add(assetPath);
    }

    internal void RegisterLocalizationKey(string tableName, string key, string owner)
    {
        var composite = $"{tableName}:{key}";
        if (!_localizationKeys.TryGetValue(composite, out var owners))
        {
            owners = new List<string>();
            _localizationKeys[composite] = owners;
        }

        owners.Add(owner);
    }

    internal void RegisterPassiveBoardClass(string classId, string assetPath)
    {
        if (string.IsNullOrWhiteSpace(classId))
        {
            return;
        }

        if (!_passiveBoardsByClassId.TryGetValue(classId, out var paths))
        {
            paths = new List<string>();
            _passiveBoardsByClassId[classId] = paths;
        }

        paths.Add(assetPath);
    }

    internal void EmitAggregateIssues()
    {
        foreach (var pair in _canonicalIds.Where(entry => entry.Value.Count > 1))
        {
            ContentValidationIssueFactory.AddError(_issues, "id.duplicate", $"Duplicate id '{pair.Key}': {string.Join(", ", pair.Value)}", string.Join(", ", pair.Value));
        }

        foreach (var pair in _passiveBoardsByClassId.Where(entry => entry.Value.Count > 1))
        {
            ContentValidationIssueFactory.AddError(_issues, "passive_board.duplicate_class", $"Duplicate passive board class '{pair.Key}': {string.Join(", ", pair.Value)}", string.Join(", ", pair.Value));
        }

        foreach (var pair in _localizationKeys.Where(entry => entry.Value.Count > 1))
        {
            ContentValidationIssueFactory.AddError(_issues, "localization.duplicate_key", $"Duplicate localization key '{pair.Key}': {string.Join(", ", pair.Value)}", string.Join(", ", pair.Value));
        }
    }
}

internal sealed class ValidationReportAssembler
{
    internal ContentValidationReport Assemble(ValidationReportBuilder builder)
    {
        return new ContentValidationReport
        {
            ValidationPhase = ContentLocalizationPolicy.CurrentPhase,
            LaunchScope = builder.LaunchScope,
            FloorGaps = builder.FloorGaps,
            SafeTargetGaps = builder.SafeTargetGaps,
            PassiveBoards = builder.PassiveBoards,
            LoopC = builder.LoopC,
            Issues = builder.Issues
                .OrderByDescending(issue => issue.Severity)
                .ThenBy(issue => issue.Code, StringComparer.Ordinal)
                .ThenBy(issue => issue.AssetPath, StringComparer.Ordinal)
                .ToList(),
        };
    }
}

internal interface IValidationPass
{
    void Execute(ValidationAssetCatalog catalog, ValidationReportBuilder builder);
}

internal sealed class LocalizationValidationPass : IValidationPass
{
    private readonly ILocalizationInspector _inspector;

    public LocalizationValidationPass(ILocalizationInspector inspector)
    {
        _inspector = inspector;
    }

    public void Execute(ValidationAssetCatalog catalog, ValidationReportBuilder builder)
    {
        foreach (var descriptor in catalog.Descriptors)
        {
            _inspector.InspectAsset(descriptor, builder);
        }
    }
}

internal sealed class DefinitionSchemaValidationPass : IValidationPass
{
    private readonly DefinitionSchemaRuleRegistry _registry;

    public DefinitionSchemaValidationPass(DefinitionSchemaRuleRegistry registry)
    {
        _registry = registry;
    }

    public void Execute(ValidationAssetCatalog catalog, ValidationReportBuilder builder)
    {
        foreach (var descriptor in catalog.Descriptors)
        {
            if (!_registry.TryGetRule(descriptor.AssetType, out var rule))
            {
                continue;
            }

            var canonicalId = rule.GetCanonicalId(descriptor.Asset);
            builder.RegisterCanonicalId(rule.Kind, canonicalId, descriptor.AssetPath);
            ContentDefinitionSchemaRuleSupport.ValidateCanonicalId(canonicalId, descriptor.AssetPath, $"{rule.Kind}.Id", builder.Issues);
            rule.Validate(descriptor, catalog, builder.Issues);
        }
    }
}

internal sealed class PassiveBoardShapeValidationPass : IValidationPass
{
    public void Execute(ValidationAssetCatalog catalog, ValidationReportBuilder builder)
    {
        foreach (var board in catalog.OfType<PassiveBoardDefinition>())
        {
            builder.RegisterPassiveBoardClass(board.ClassId, catalog.GetPath(board));
        }

        var reports = LaunchScopeGapEvaluator.BuildPassiveBoardReports(catalog.Assets);
        builder.PassiveBoards = reports;
        foreach (var report in reports)
        {
            LaunchScopeGapEvaluator.ValidatePassiveBoardShape(report, builder.Issues);
        }
    }
}

internal sealed class LaunchScopeValidationPass : IValidationPass
{
    public void Execute(ValidationAssetCatalog catalog, ValidationReportBuilder builder)
    {
        var launchScope = LaunchScopeGapEvaluator.BuildCountReport(catalog.Assets);
        builder.LaunchScope = launchScope;
        builder.FloorGaps = LaunchScopeGapEvaluator.BuildGapList(launchScope, ContentValidationPolicyCatalog.PaidLaunchFloor);
        builder.SafeTargetGaps = LaunchScopeGapEvaluator.BuildGapList(launchScope, ContentValidationPolicyCatalog.PaidLaunchSafeTarget);

        foreach (var gap in builder.FloorGaps)
        {
            ContentValidationIssueFactory.AddError(builder.Issues, "launch_floor.gap", $"Paid launch floor requirement not met: {gap}", ContentValidationPolicyCatalog.ReportFolderName);
        }

        foreach (var gap in LaunchScopeGapEvaluator.BuildGapList(launchScope, ContentValidationPolicyCatalog.CurrentMvpMinimum))
        {
            ContentValidationIssueFactory.AddError(builder.Issues, "launch_scope.mvp_gap", $"Current MVP minimum requirement not met: {gap}", ContentValidationPolicyCatalog.ReportFolderName);
        }
    }
}

internal sealed class CatalogConsistencyValidationPass : IValidationPass
{
    private readonly CatalogValidationRuleRegistry _registry;

    public CatalogConsistencyValidationPass(CatalogValidationRuleRegistry registry)
    {
        _registry = registry;
    }

    public void Execute(ValidationAssetCatalog catalog, ValidationReportBuilder builder)
    {
        _registry.Validate(catalog, builder.Issues);
    }
}

internal sealed class LoopCGovernanceValidationPass : IValidationPass
{
    private readonly LoopCGovernanceOrchestrator _orchestrator;

    public LoopCGovernanceValidationPass(LoopCGovernanceOrchestrator orchestrator)
    {
        _orchestrator = orchestrator;
    }

    public void Execute(ValidationAssetCatalog catalog, ValidationReportBuilder builder)
    {
        builder.LoopC = _orchestrator.Validate(catalog, builder.Issues);
    }
}

internal sealed class ValidationPassRegistry
{
    private readonly IReadOnlyList<IValidationPass> _passes;

    public ValidationPassRegistry(IReadOnlyList<IValidationPass> passes)
    {
        _passes = passes;
    }

    internal IReadOnlyList<IValidationPass> Passes => _passes;
}

internal sealed class ContentValidationOrchestrator
{
    private readonly IAssetLoader _assetLoader;
    private readonly IValidationAssetSelectionPolicy _selectionPolicy;
    private readonly ValidationPassRegistry _passRegistry;
    private readonly ValidationReportAssembler _assembler;
    private readonly IUnityAssetGateway _gateway;

    public ContentValidationOrchestrator(
        IAssetLoader assetLoader,
        IValidationAssetSelectionPolicy selectionPolicy,
        ValidationPassRegistry passRegistry,
        ValidationReportAssembler assembler,
        IUnityAssetGateway gateway)
    {
        _assetLoader = assetLoader;
        _selectionPolicy = selectionPolicy;
        _passRegistry = passRegistry;
        _assembler = assembler;
        _gateway = gateway;
    }

    internal ValidationAssetCatalog LoadCatalog()
    {
        _gateway.RefreshAssets();
        var descriptors = _selectionPolicy.Select(_assetLoader.LoadAssets());
        return new ValidationAssetCatalog(descriptors);
    }

    internal ValidationAssetCatalog CreateExplicitCatalog(IEnumerable<ScriptableObject> assets)
    {
        return new ValidationAssetCatalog(assets
            .Where(asset => asset != null)
            .Select(asset => new ValidationAssetDescriptor(asset, _gateway.ResolveAssetPath(asset), ValidationAssetSourceKind.Explicit, asset.GetType()))
            .ToList());
    }

    internal LaunchScopeCountReport BuildLaunchScopeCountReport()
    {
        return LaunchScopeGapEvaluator.BuildCountReport(LoadCatalog().Assets);
    }

    internal LaunchScopeCountReport BuildLaunchScopeCountReport(IEnumerable<ScriptableObject> assets)
    {
        return LaunchScopeGapEvaluator.BuildCountReport(CreateExplicitCatalog(assets).Assets);
    }

    internal ContentValidationReport BuildReport()
    {
        return Execute(LoadCatalog());
    }

    internal ContentValidationReport BuildReport(IEnumerable<ScriptableObject> assets)
    {
        return Execute(CreateExplicitCatalog(assets));
    }

    private ContentValidationReport Execute(ValidationAssetCatalog catalog)
    {
        var builder = new ValidationReportBuilder();
        foreach (var pass in _passRegistry.Passes)
        {
            pass.Execute(catalog, builder);
        }

        builder.EmitAggregateIssues();
        return _assembler.Assemble(builder);
    }
}

internal sealed class ValidationServiceBundle
{
    public ValidationServiceBundle(
        ContentValidationOrchestrator validator,
        IReportWriter reportWriter,
        IUnityAssetGateway unityGateway,
        IValidationReportPathProvider reportPaths)
    {
        Validator = validator;
        ReportWriter = reportWriter;
        UnityGateway = unityGateway;
        ReportPaths = reportPaths;
    }

    internal ContentValidationOrchestrator Validator { get; }
    internal IReportWriter ReportWriter { get; }
    internal IUnityAssetGateway UnityGateway { get; }
    internal IValidationReportPathProvider ReportPaths { get; }
}

internal static class ContentValidationRuntimeFactory
{
    internal static ValidationServiceBundle CreateDefault()
    {
        var unityGateway = new UnityAssetGateway();
        var identityResolver = new DefaultValidationAssetIdentityResolver();
        var completenessScorer = new DefaultValidationAssetCompletenessScorer(identityResolver);
        var selectionPolicy = new ValidationAssetSelectionPolicy(
            new DefaultValidationAssetSkipPolicy(),
            new DefaultValidationAssetDeduper(completenessScorer, identityResolver));

        var schemaRegistry = DefinitionSchemaRuleRegistry.CreateDefault();
        var catalogRegistry = CatalogValidationRuleRegistry.CreateDefault();
        var localizationInspector = new DescriptorDrivenLocalizationInspector(
            new CompositeLocalizationShapeProvider(
                new DefaultLocalizationShapeProvider(),
                new ReflectionFallbackLocalizationShapeProvider()),
            new UnityLocalizationEntryLookup());

        var assetLoader = new CompositeAssetLoader(
            new ResourcesAssetLoader(unityGateway),
            new AssetDatabaseTypedAssetLoader(unityGateway, ValidationKnownDefinitionTypes.All, ValidationAssetPipelineDefaults.Root),
            new AssetDatabaseGenericAssetLoader(unityGateway, ValidationAssetPipelineDefaults.Root),
            new FileSystemAssetLoader(unityGateway, ValidationAssetPipelineDefaults.Root),
            new FallbackDefinitionFileLoader(unityGateway));

        var loopCGovernance = LoopCGovernanceOrchestrator.CreateDefault();
        var passRegistry = new ValidationPassRegistry(new IValidationPass[]
        {
            new LocalizationValidationPass(localizationInspector),
            new DefinitionSchemaValidationPass(schemaRegistry),
            new PassiveBoardShapeValidationPass(),
            new LaunchScopeValidationPass(),
            new CatalogConsistencyValidationPass(catalogRegistry),
            new LoopCGovernanceValidationPass(loopCGovernance),
        });

        var assembler = new ValidationReportAssembler();
        var validator = new ContentValidationOrchestrator(
            assetLoader,
            selectionPolicy,
            passRegistry,
            assembler,
            unityGateway);

        var reportPaths = new ContentValidationReportPathProvider(unityGateway);
        var reportWriter = new CompositeReportWriter(
            reportPaths,
            new IValidationArtifactRenderer[]
            {
                new JsonValidationReportRenderer(),
                new MarkdownValidationSummaryRenderer(),
                new LoopCArtifactRenderer(),
            },
            new FileArtifactSink());

        return new ValidationServiceBundle(validator, reportWriter, unityGateway, reportPaths);
    }
}

internal static class ContentValidationCompositionRoot
{
    private static readonly Lazy<ValidationServiceBundle> BundleInstance = new(ContentValidationRuntimeFactory.CreateDefault);

    internal static ValidationServiceBundle Services => BundleInstance.Value;
    internal static ContentValidationOrchestrator Validator => Services.Validator;
    internal static IReportWriter ReportWriter => Services.ReportWriter;
    internal static IUnityAssetGateway UnityGateway => Services.UnityGateway;
    internal static IValidationReportPathProvider ReportPaths => Services.ReportPaths;
}
