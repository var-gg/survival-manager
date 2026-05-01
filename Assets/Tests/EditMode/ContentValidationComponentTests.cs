using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using SM.Content.Definitions;
using SM.Core.Content;
using SM.Core.Contracts;
using SM.Editor.Validation;
using UnityEngine;

namespace SM.Tests.EditMode;

[Category("BatchOnly")]
public sealed class ContentValidationComponentTests
{
    private readonly List<UnityEngine.Object> _ownedObjects = new();

    [TearDown]
    public void TearDown()
    {
        foreach (var asset in _ownedObjects.Where(asset => asset != null))
        {
            UnityEngine.Object.DestroyImmediate(asset);
        }

        _ownedObjects.Clear();
    }

    [Test]
    public void RuntimeFactory_CreatesExplicitValidationGraph()
    {
        var services = ContentValidationRuntimeFactory.CreateDefault();

        Assert.That(services.Validator, Is.Not.Null);
        Assert.That(services.ReportWriter, Is.Not.Null);
        Assert.That(services.ReportPaths.GetDefaultReportDirectory(), Does.Contain("Logs/content-validation"));
    }

    [Test]
    public void ClassRoleFamilyMapping_RoundTripsDuelistAndStriker()
    {
        Assert.That(ContentValidationPolicyCatalog.TryGetRoleFamilyForCanonicalClassId("duelist", out var roleFamily), Is.True);
        Assert.That(roleFamily, Is.EqualTo("striker"));
        Assert.That(ContentValidationPolicyCatalog.TryGetCanonicalClassIdForRoleFamily("striker", out var canonicalClassId), Is.True);
        Assert.That(canonicalClassId, Is.EqualTo("duelist"));
    }

    [Test]
    public void ArchetypeSchemaRule_FlagsGlossaryRoleFamilyDrift()
    {
        var rule = new ArchetypeSchemaRule();
        var archetype = Own(ScriptableObject.CreateInstance<UnitArchetypeDefinition>());
        archetype.Id = "archetype_glossary_probe";
        archetype.Race = Own(ScriptableObject.CreateInstance<RaceDefinition>());
        archetype.Class = Own(ScriptableObject.CreateInstance<ClassDefinition>());
        archetype.Class.Id = "duelist";
        archetype.TraitPool = Own(ScriptableObject.CreateInstance<TraitPoolDefinition>());
        archetype.RoleFamilyTag = "vanguard";
        archetype.PrimaryWeaponFamilyTag = "blade";
        archetype.TacticPreset = new List<TacticPresetEntry> { new() };
        archetype.IsRecruitable = false;

        var issues = new List<ContentValidationIssue>();
        rule.Validate(new ValidationAssetDescriptor(archetype, "Assets/test_archetype.asset", ValidationAssetSourceKind.Explicit, archetype.GetType()), new ValidationAssetCatalog(new[]
        {
            new ValidationAssetDescriptor(archetype, "Assets/test_archetype.asset", ValidationAssetSourceKind.Explicit, archetype.GetType()),
        }), issues);

        Assert.That(issues.Select(issue => issue.Code), Contains.Item("glossary.duelist_role_family"));
    }

    [Test]
    public void SkillSchemaRule_FlagsRangeAndAiHintDrift()
    {
        var rule = new SkillSchemaRule();
        var skill = Own(ScriptableObject.CreateInstance<SkillDefinitionAsset>());
        skill.Id = "skill_probe";
        skill.RangeMin = 5f;
        skill.RangeMax = 1f;
        skill.AiScoreHints.MinimumTargetHealthRatio = 0.8f;
        skill.AiScoreHints.MaximumTargetHealthRatio = 0.4f;

        var issues = new List<ContentValidationIssue>();
        rule.Validate(new ValidationAssetDescriptor(skill, "Assets/test_skill.asset", ValidationAssetSourceKind.Explicit, skill.GetType()), EmptyCatalog(), issues);

        Assert.That(issues.Select(issue => issue.Code), Contains.Item("skill.range_band"));
        Assert.That(issues.Select(issue => issue.Code), Contains.Item("skill.ai_score_hints"));
    }

    [Test]
    public void SkillCatalogValidator_FlagsMissingStatusReference()
    {
        var skill = Own(ScriptableObject.CreateInstance<SkillDefinitionAsset>());
        skill.Id = "skill_missing_status";
        skill.AppliedStatuses.Add(new StatusApplicationRule { StatusId = "missing_status", MaxStacks = 1 });

        var catalog = new ValidationAssetCatalog(new[]
        {
            new ValidationAssetDescriptor(skill, "Assets/skill_missing_status.asset", ValidationAssetSourceKind.Explicit, skill.GetType()),
        });

        var issues = new List<ContentValidationIssue>();
        new SkillCatalogValidator().Validate(new CatalogValidationContext(catalog), issues);

        Assert.That(issues.Select(issue => issue.Code), Contains.Item("skill.status_ref"));
    }

    [Test]
    public void FactionIsolationValidator_FlagsSynergyLeak()
    {
        var site = Own(ScriptableObject.CreateInstance<ExpeditionSiteDefinition>());
        site.Id = "site_probe";
        site.FactionId = "faction_alpha";

        var synergy = Own(ScriptableObject.CreateInstance<SynergyDefinition>());
        synergy.Id = "synergy_probe";
        synergy.CountedTagId = "faction_alpha";

        var catalog = new ValidationAssetCatalog(new ValidationAssetDescriptor[]
        {
            new(site, "Assets/site_probe.asset", ValidationAssetSourceKind.Explicit, site.GetType()),
            new(synergy, "Assets/synergy_probe.asset", ValidationAssetSourceKind.Explicit, synergy.GetType()),
        });

        var issues = new List<ContentValidationIssue>();
        new FactionIsolationValidator().Validate(new CatalogValidationContext(catalog), issues);

        Assert.That(issues.Select(issue => issue.Code), Contains.Item("faction.synergy_leak"));
    }

    [Test]
    public void CharacterCatalogValidator_LocksExecutableCharacterCoverage()
    {
        var fullCatalog = ContentValidationPolicyCatalog.RequiredExecutableCharacterIdsInRosterOrder
            .Select(id => OwnCharacter(id))
            .ToArray();
        var passIssues = new List<ContentValidationIssue>();
        new CharacterCatalogValidator().Validate(new CatalogValidationContext(ToCatalog(fullCatalog)), passIssues);

        Assert.That(passIssues.Select(issue => issue.Code), Does.Not.Contain("character.executable_catalog_floor"));

        var missingCatalog = fullCatalog
            .Where(character => !string.Equals(character.Id, "mirror_cantor", StringComparison.Ordinal))
            .ToArray();
        var issues = new List<ContentValidationIssue>();
        new CharacterCatalogValidator().Validate(new CatalogValidationContext(ToCatalog(missingCatalog)), issues);

        Assert.That(issues.Select(issue => issue.Code), Contains.Item("character.executable_catalog_floor"));
    }

    [Test]
    public void DefaultLocalizationShapeProvider_ReturnsExpectedFieldsForSkill()
    {
        var provider = new DefaultLocalizationShapeProvider();

        Assert.That(provider.TryGetShape(typeof(SkillDefinitionAsset), out var shape), Is.True);
        Assert.That(shape.Fields.Select(field => field.FieldName), Is.SupersetOf(new[] { "NameKey", "DescriptionKey" }));
    }

    [Test]
    public void CompositeLocalizationShapeProvider_UsesFallbackOnlyForUnknownTypes()
    {
        var primary = new StubShapeProvider();
        var fallback = new TrackingFallbackShapeProvider();
        var provider = new CompositeLocalizationShapeProvider(primary, fallback);

        Assert.That(provider.TryGetShape(typeof(ShapeKnownAsset), out _), Is.True);
        Assert.That(provider.TryGetShape(typeof(ShapeUnknownAsset), out _), Is.True);
        Assert.That(fallback.Calls, Is.EqualTo(1));
    }

    [Test]
    public void DescriptorDrivenLocalizationInspector_DistinguishesMissingCollectionLocaleAndEntry()
    {
        var provider = new StubShapeProvider(typeof(LocalizationProbeAsset), new LocalizationShape("ProbeTable", new[] { new LocalizedFieldDescriptor("NameKey") }));
        var asset = Own(ScriptableObject.CreateInstance<LocalizationProbeAsset>());
        asset.NameKey = "content.probe.name";
        var descriptor = new ValidationAssetDescriptor(asset, "Assets/localization_probe.asset", ValidationAssetSourceKind.Explicit, asset.GetType());

        var missingCollectionBuilder = new ValidationReportBuilder();
        new DescriptorDrivenLocalizationInspector(provider, new StubLocalizationLookup(null))
            .InspectAsset(descriptor, missingCollectionBuilder);
        Assert.That(missingCollectionBuilder.Issues.Select(issue => issue.Code), Contains.Item("localization.missing_collection"));

        var missingLocaleBuilder = new ValidationReportBuilder();
        new DescriptorDrivenLocalizationInspector(provider, new StubLocalizationLookup(new StubLocalizationCollection(hasKoTable: false, hasEntry: true)))
            .InspectAsset(descriptor, missingLocaleBuilder);
        Assert.That(missingLocaleBuilder.Issues.Select(issue => issue.Code), Contains.Item("localization.missing_locale_table"));

        var missingEntryBuilder = new ValidationReportBuilder();
        new DescriptorDrivenLocalizationInspector(provider, new StubLocalizationLookup(new StubLocalizationCollection(hasKoTable: true, hasEntry: false)))
            .InspectAsset(descriptor, missingEntryBuilder);
        Assert.That(missingEntryBuilder.Issues.Select(issue => issue.Code), Contains.Item("localization.missing_entry"));
    }

    [Test]
    public void CompositeReportWriter_RendersArtifactsAndDelegatesPersistenceToSink()
    {
        var sink = new RecordingArtifactSink();
        var writer = new CompositeReportWriter(
            new StubReportPathProvider("A:/reports"),
            new IValidationArtifactRenderer[]
            {
                new JsonValidationReportRenderer(),
                new MarkdownValidationSummaryRenderer(),
                new LoopCArtifactRenderer(),
            },
            sink);

        var report = writer.Write(new ContentValidationReport
        {
            Issues = new[] { new ContentValidationIssue(ContentValidationSeverity.Warning, "probe", "warning", "Assets/probe.asset") },
        });

        Assert.That(report.JsonReportPath, Is.EqualTo("A:/reports/content-validation-report.json"));
        Assert.That(sink.Artifacts.Count, Is.EqualTo(6));
        Assert.That(sink.Artifacts.Any(artifact => artifact.FilePath.EndsWith("content-validation-summary.md", StringComparison.Ordinal)), Is.True);
    }

    [Test]
    public void LoopCGovernanceOrchestrator_PreservesConfiguredRuleOrder()
    {
        var executionOrder = new List<string>();
        var orchestrator = new LoopCGovernanceOrchestrator(
            new FixedSubjectExtractor(Array.Empty<LoopCGovernanceSubject>()),
            new ILoopCGovernanceRule[]
            {
                new RecordingGovernanceRule("first", executionOrder),
                new RecordingGovernanceRule("second", executionOrder),
                new RecordingGovernanceRule("third", executionOrder),
            });

        orchestrator.Validate(EmptyCatalog(), new List<ContentValidationIssue>());

        Assert.That(executionOrder, Is.EqualTo(new[] { "first", "second", "third" }));
    }

    [Test]
    public void DefaultLoopCGovernanceSubjectExtractor_ExtractsNestedGovernedDefinitions()
    {
        var archetype = Own(ScriptableObject.CreateInstance<UnitArchetypeDefinition>());
        archetype.Id = "loopc_unit";
        archetype.BudgetCard = new BudgetCard { Domain = BudgetDomain.UnitBlueprint };
        archetype.Loadout.SignaturePassive = new PassiveDefinition
        {
            Id = "loopc_signature_passive",
            BudgetCard = new BudgetCard { Domain = BudgetDomain.Passive },
        };

        var subjects = new DefaultLoopCGovernanceSubjectExtractor().Extract(new ValidationAssetCatalog(new[]
        {
            new ValidationAssetDescriptor(archetype, "Assets/loopc_unit.asset", ValidationAssetSourceKind.Explicit, archetype.GetType()),
        }));

        Assert.That(subjects.Select(subject => subject.ContentKind), Contains.Item(nameof(UnitArchetypeDefinition)));
        Assert.That(subjects.Select(subject => subject.ContentId), Contains.Item("loopc_signature_passive"));
    }

    private ValidationAssetCatalog EmptyCatalog()
    {
        return new ValidationAssetCatalog(Array.Empty<ValidationAssetDescriptor>());
    }

    private static ValidationAssetCatalog ToCatalog(IEnumerable<ScriptableObject> assets)
    {
        return new ValidationAssetCatalog(assets
            .Select((asset, index) => new ValidationAssetDescriptor(asset, $"Assets/test_asset_{index}.asset", ValidationAssetSourceKind.Explicit, asset.GetType()))
            .ToList());
    }

    private CharacterDefinition OwnCharacter(string id)
    {
        var character = Own(ScriptableObject.CreateInstance<CharacterDefinition>());
        character.Id = id;
        return character;
    }

    private T Own<T>(T asset) where T : UnityEngine.Object
    {
        _ownedObjects.Add(asset);
        return asset;
    }

    private sealed class ShapeKnownAsset : ScriptableObject
    {
        public string NameKey = string.Empty;
    }

    private sealed class ShapeUnknownAsset : ScriptableObject
    {
        public string NameKey = string.Empty;
    }

    private sealed class LocalizationProbeAsset : ScriptableObject
    {
        public string NameKey = string.Empty;
    }

    private sealed class StubShapeProvider : ILocalizationShapeProvider
    {
        private readonly Dictionary<Type, LocalizationShape> _shapes = new();

        public StubShapeProvider()
        {
            _shapes[typeof(ShapeKnownAsset)] = new LocalizationShape("KnownTable", new[] { new LocalizedFieldDescriptor("NameKey") });
        }

        public StubShapeProvider(Type type, LocalizationShape shape)
        {
            _shapes[type] = shape;
        }

        public bool TryGetShape(Type type, out LocalizationShape shape)
        {
            return _shapes.TryGetValue(type, out shape!);
        }
    }

    private sealed class TrackingFallbackShapeProvider : ILocalizationShapeProvider
    {
        public int Calls { get; private set; }

        public bool TryGetShape(Type type, out LocalizationShape shape)
        {
            Calls++;
            shape = new LocalizationShape("FallbackTable", new[] { new LocalizedFieldDescriptor("NameKey") });
            return true;
        }
    }

    private sealed class StubLocalizationLookup : ILocalizationEntryLookup
    {
        private readonly ILocalizationTableCollection? _collection;

        public StubLocalizationLookup(ILocalizationTableCollection? collection)
        {
            _collection = collection;
        }

        public ILocalizationTableCollection? GetCollection(string tableName)
        {
            return _collection;
        }
    }

    private sealed class StubLocalizationCollection : ILocalizationTableCollection
    {
        private readonly bool _hasKoTable;
        private readonly bool _hasEntry;

        public StubLocalizationCollection(bool hasKoTable, bool hasEntry)
        {
            _hasKoTable = hasKoTable;
            _hasEntry = hasEntry;
        }

        public bool HasLocaleTable(string localeCode)
        {
            return localeCode == "en" || _hasKoTable;
        }

        public bool HasEntry(string localeCode, string key)
        {
            return _hasEntry;
        }
    }

    private sealed class StubReportPathProvider : IValidationReportPathProvider
    {
        private readonly string _root;

        public StubReportPathProvider(string root)
        {
            _root = root;
        }

        public string GetDefaultReportDirectory()
        {
            return _root;
        }

        public ValidationReportOutputPaths BuildOutputPaths()
        {
            return new ValidationReportOutputPaths(
                _root,
                $"{_root}/content-validation-report.json",
                $"{_root}/content-validation-summary.md",
                $"{_root}/content_budget_audit.json",
                $"{_root}/content_budget_audit.md",
                $"{_root}/counter_coverage_matrix.md",
                $"{_root}/v1_forbidden_feature_report.md");
        }
    }

    private sealed class RecordingArtifactSink : IArtifactSink
    {
        public List<ValidationArtifact> Artifacts { get; } = new();

        public void Write(IReadOnlyList<ValidationArtifact> artifacts)
        {
            Artifacts.AddRange(artifacts);
        }
    }

    private sealed class FixedSubjectExtractor : ILoopCGovernanceSubjectExtractor
    {
        private readonly IReadOnlyList<LoopCGovernanceSubject> _subjects;

        public FixedSubjectExtractor(IReadOnlyList<LoopCGovernanceSubject> subjects)
        {
            _subjects = subjects;
        }

        public IReadOnlyList<LoopCGovernanceSubject> Extract(ValidationAssetCatalog catalog)
        {
            return _subjects;
        }
    }

    private sealed class RecordingGovernanceRule : ILoopCGovernanceRule
    {
        private readonly string _label;
        private readonly ICollection<string> _executionOrder;

        public RecordingGovernanceRule(string label, ICollection<string> executionOrder)
        {
            _label = label;
            _executionOrder = executionOrder;
        }

        public void Execute(LoopCGovernanceContext context)
        {
            _executionOrder.Add(_label);
        }
    }
}
