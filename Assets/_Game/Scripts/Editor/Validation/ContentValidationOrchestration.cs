using System;
using System.Collections.Generic;
using System.Linq;
using SM.Content.Definitions;
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
            ContentDefinitionSchemaRules.ValidateCanonicalId(canonicalId, descriptor.AssetPath, $"{rule.Kind}.Id", builder.Issues);
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
    public void Execute(ValidationAssetCatalog catalog, ValidationReportBuilder builder)
    {
        ContentDefinitionCatalogRules.ValidateLaunchFloorCatalogs(catalog, builder.Issues);
    }
}

internal sealed class LoopCGovernanceValidationPass : IValidationPass
{
    public void Execute(ValidationAssetCatalog catalog, ValidationReportBuilder builder)
    {
        builder.LoopC = LoopCContentGovernanceValidator.Validate(catalog, builder.Issues);
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
    private readonly ValidationAssetSelectionPolicy _selectionPolicy;
    private readonly ValidationPassRegistry _passRegistry;
    private readonly ValidationReportAssembler _assembler;
    private readonly IUnityAssetGateway _gateway;

    public ContentValidationOrchestrator(
        IAssetLoader assetLoader,
        ValidationAssetSelectionPolicy selectionPolicy,
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

internal static class ContentValidationCompositionRoot
{
    private static readonly Lazy<IUnityAssetGateway> UnityAssetGatewayInstance = new(() => new UnityAssetGateway());
    private static readonly Lazy<ValidationAssetSelectionPolicy> SelectionPolicyInstance = new(() => new ValidationAssetSelectionPolicy());
    private static readonly Lazy<DefinitionSchemaRuleRegistry> RuleRegistryInstance = new(DefinitionSchemaRuleRegistry.CreateDefault);
    private static readonly Lazy<ILocalizationInspector> LocalizationInspectorInstance = new(() => new ReflectionLocalizationInspector());
    private static readonly Lazy<IAssetLoader> AssetLoaderInstance = new(() => new CompositeAssetLoader(
        new ResourcesAssetLoader(UnityAssetGatewayInstance.Value),
        new AssetDatabaseTypedAssetLoader(UnityAssetGatewayInstance.Value, ValidationKnownDefinitionTypes.All, ValidationAssetPipelineDefaults.Root),
        new AssetDatabaseGenericAssetLoader(UnityAssetGatewayInstance.Value, ValidationAssetPipelineDefaults.Root),
        new FileSystemAssetLoader(UnityAssetGatewayInstance.Value, ValidationAssetPipelineDefaults.Root),
        new FallbackDefinitionFileLoader(UnityAssetGatewayInstance.Value)));
    private static readonly Lazy<ValidationPassRegistry> PassRegistryInstance = new(() => new ValidationPassRegistry(new IValidationPass[]
    {
        new LocalizationValidationPass(LocalizationInspectorInstance.Value),
        new DefinitionSchemaValidationPass(RuleRegistryInstance.Value),
        new PassiveBoardShapeValidationPass(),
        new LaunchScopeValidationPass(),
        new CatalogConsistencyValidationPass(),
        new LoopCGovernanceValidationPass(),
    }));
    private static readonly Lazy<ValidationReportAssembler> ReportAssemblerInstance = new(() => new ValidationReportAssembler());
    private static readonly Lazy<ContentValidationOrchestrator> OrchestratorInstance = new(() => new ContentValidationOrchestrator(
        AssetLoaderInstance.Value,
        SelectionPolicyInstance.Value,
        PassRegistryInstance.Value,
        ReportAssemblerInstance.Value,
        UnityAssetGatewayInstance.Value));
    private static readonly Lazy<IReportWriter> ReportWriterInstance = new(() => new CompositeReportWriter(
        UnityAssetGatewayInstance.Value,
        new JsonReportWriter(),
        new MarkdownSummaryWriter(),
        new LoopCArtifactWriter()));

    internal static ContentValidationOrchestrator Validator => OrchestratorInstance.Value;
    internal static IReportWriter ReportWriter => ReportWriterInstance.Value;
    internal static IUnityAssetGateway UnityGateway => UnityAssetGatewayInstance.Value;
}
