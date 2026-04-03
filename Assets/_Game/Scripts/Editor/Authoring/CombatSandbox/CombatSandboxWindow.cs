using System;
using System.Linq;
using System.Text;
using SM.Combat.Model;
using SM.Combat.Services;
using SM.Unity.Sandbox;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace SM.Editor.Authoring.CombatSandbox;

public sealed class CombatSandboxWindow : EditorWindow
{
    private const string MenuPath = "SM/Authoring/Combat Sandbox";

    private CombatSandboxState _state = null!;
    private SerializedObject _serializedState = null!;

    public CombatSandboxState State => _state;

    [MenuItem(MenuPath)]
    public static void OpenWindow()
    {
        var window = GetWindow<CombatSandboxWindow>();
        window.titleContent = new GUIContent("Combat Sandbox");
        window.minSize = new Vector2(420f, 480f);
        window.Show();
    }

    private void OnEnable()
    {
        _state = CreateInstance<CombatSandboxState>();
        _state.hideFlags = HideFlags.HideAndDontSave;
        _serializedState = new SerializedObject(_state);
        BuildUi();
    }

    private void OnDisable()
    {
        if (_state != null)
        {
            DestroyImmediate(_state);
        }
    }

    private void BuildUi()
    {
        rootVisualElement.Clear();
        rootVisualElement.style.paddingLeft = 8;
        rootVisualElement.style.paddingRight = 8;
        rootVisualElement.style.paddingTop = 8;
        rootVisualElement.style.paddingBottom = 8;

        rootVisualElement.Add(BuildSection("Squad Setup", section =>
        {
            section.Add(new PropertyField(_serializedState.FindProperty(nameof(CombatSandboxState.Config))));
        }));

        rootVisualElement.Add(BuildSection("Overrides", section =>
        {
            section.Add(new PropertyField(_serializedState.FindProperty(nameof(CombatSandboxState.Seed))));
            section.Add(new PropertyField(_serializedState.FindProperty(nameof(CombatSandboxState.BatchCount))));
            section.Add(new PropertyField(_serializedState.FindProperty(nameof(CombatSandboxState.InspectUnitId))));
        }));

        rootVisualElement.Add(BuildSection("Execution", section =>
        {
            var singleRunButton = new Button(RunSingle) { text = "Single Run" };
            var batchRunButton = new Button(RunBatch) { text = "Batch Run" };
            section.Add(singleRunButton);
            section.Add(batchRunButton);
        }));

        rootVisualElement.Add(BuildSection("Results", section =>
        {
            section.Add(CreateReadOnlyField(nameof(CombatSandboxState.LastCompileHash)));
            section.Add(CreateReadOnlyField(nameof(CombatSandboxState.LastReplayHash)));
            section.Add(CreateReadOnlyField(nameof(CombatSandboxState.LastMetricsSummary), 72f));
            section.Add(CreateReadOnlyField(nameof(CombatSandboxState.LastCounterCoverageSummary), 120f));
            section.Add(CreateReadOnlyField(nameof(CombatSandboxState.LastGovernanceSummary), 120f));
            section.Add(CreateReadOnlyField(nameof(CombatSandboxState.LastReadabilitySummary), 120f));
            section.Add(CreateReadOnlyField(nameof(CombatSandboxState.LastExplanationSummary), 120f));
            section.Add(CreateReadOnlyField(nameof(CombatSandboxState.LastProvenanceSummary), 120f));
            section.Add(CreateReadOnlyField(nameof(CombatSandboxState.LastValidationMessage), 72f));
            section.Add(CreateReadOnlyField(nameof(CombatSandboxState.LayoutSourceLabel)));
        }));

        rootVisualElement.Bind(_serializedState);
    }

    private VisualElement BuildSection(string title, Action<VisualElement> build)
    {
        var container = new VisualElement();
        container.style.marginBottom = 8;
        container.style.paddingLeft = 8;
        container.style.paddingRight = 8;
        container.style.paddingTop = 8;
        container.style.paddingBottom = 8;
        container.style.borderBottomWidth = 1;
        container.style.borderTopWidth = 1;
        container.style.borderLeftWidth = 1;
        container.style.borderRightWidth = 1;
        container.style.borderBottomColor = new Color(0.22f, 0.22f, 0.22f);
        container.style.borderTopColor = new Color(0.22f, 0.22f, 0.22f);
        container.style.borderLeftColor = new Color(0.22f, 0.22f, 0.22f);
        container.style.borderRightColor = new Color(0.22f, 0.22f, 0.22f);

        var titleLabel = new Label(title);
        titleLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
        titleLabel.style.marginBottom = 6;
        container.Add(titleLabel);
        build(container);
        return container;
    }

    private PropertyField CreateReadOnlyField(string propertyName, float minHeight = 0f)
    {
        var field = new PropertyField(_serializedState.FindProperty(propertyName));
        field.SetEnabled(false);
        if (minHeight > 0f)
        {
            field.style.minHeight = minHeight;
        }

        return field;
    }

    private void RunSingle()
    {
        Execute(runAsBatch: false);
    }

    private void RunBatch()
    {
        Execute(runAsBatch: true);
    }

    private void Execute(bool runAsBatch)
    {
        _serializedState.ApplyModifiedPropertiesWithoutUndo();

        try
        {
            var sceneController = FindAnyObjectByType<CombatSandboxSceneController>();
            var sceneLayout = sceneController != null ? sceneController.ExportSceneLayout() : null;
            var request = CombatSandboxExecutionService.BuildRequest(_state, sceneLayout);
            var effectiveRequest = runAsBatch ? request : request with { BatchCount = 1 };
            var result = CombatSandboxSceneController.Execute(effectiveRequest);
            _state.LayoutSourceLabel = sceneLayout != null ? "Scene" : "Default";
            _state.LastCompileHash = result.PlayerSnapshot.CompileHash;
            _state.LastReplayHash = result.ReplayHash;
            _state.LastMetricsSummary =
                $"win_rate={result.Metrics.WinRate:0.###}\navg_duration={result.Metrics.AverageDurationSeconds:0.###}\navg_events={result.Metrics.AverageEventCount:0.###}\nfirst_action={result.Metrics.AverageFirstActionSeconds:0.###}";
            _state.LastCounterCoverageSummary = BuildCounterCoverageSummary(
                result.PlayerSnapshot.TeamCounterCoverage,
                CounterCoverageAggregationService.AggregateFromLoadouts(result.EnemyLoadout));
            _state.LastGovernanceSummary = BuildGovernanceSummary(result.PlayerSnapshot, _state.InspectUnitId);
            _state.LastReadabilitySummary = BuildReadabilitySummary(result.LastReplay.Readability);
            _state.LastExplanationSummary = BuildExplanationSummary(result.LastReplay.BattleSummary);
            _state.LastProvenanceSummary = BuildProvenanceSummary(result.Provenance);
            _state.LastValidationMessage =
                $"config={effectiveRequest.RequestedConfigId}\nprovenance={result.Provenance.Count}\nteam_tags={string.Join(", ", result.PlayerSnapshot.TeamTags)}";
        }
        catch (Exception ex)
        {
            _state.LastValidationMessage = ex.Message;
        }

        _serializedState.Update();
        rootVisualElement.Bind(_serializedState);
        Repaint();
    }

    private static string BuildCounterCoverageSummary(TeamCounterCoverageReport? ally, TeamCounterCoverageReport? enemy)
    {
        var builder = new StringBuilder();
        builder.AppendLine("Ally coverage");
        AppendCoverage(builder, ally);
        builder.AppendLine();
        builder.AppendLine("Enemy coverage");
        AppendCoverage(builder, enemy);
        return builder.ToString().TrimEnd();
    }

    private static void AppendCoverage(StringBuilder builder, TeamCounterCoverageReport? report)
    {
        if (report == null)
        {
            builder.AppendLine("- missing");
            return;
        }

        foreach (var lane in new[]
                 {
                     ("ArmorShred", report.ArmorShred),
                     ("Exposure", report.Exposure),
                     ("GuardBreakMultiHit", report.GuardBreakMultiHit),
                     ("TrackingArea", report.TrackingArea),
                     ("TenacityStability", report.TenacityStability),
                     ("AntiHealShatter", report.AntiHealShatter),
                     ("InterceptPeel", report.InterceptPeel),
                     ("CleaveWaveclear", report.CleaveWaveclear),
                 })
        {
            var warning = lane.Item2 is CounterCoverageLevelValue.None or CounterCoverageLevelValue.Light ? " !weak" : string.Empty;
            builder.AppendLine($"- {lane.Item1}: {lane.Item2}{warning}");
        }
    }

    private static string BuildGovernanceSummary(BattleLoadoutSnapshot snapshot, string inspectUnitId)
    {
        var unit = !string.IsNullOrWhiteSpace(inspectUnitId)
            ? snapshot.Allies.FirstOrDefault(candidate => string.Equals(candidate.Id, inspectUnitId, StringComparison.Ordinal))
            : snapshot.Allies.FirstOrDefault();
        if (unit?.Governance == null)
        {
            return "Selected unit governance unavailable.";
        }

        var builder = new StringBuilder();
        builder.AppendLine($"unit={unit.Id}");
        builder.AppendLine($"rarity={unit.Governance.Rarity} role={unit.Governance.RoleProfile} budget={unit.Governance.BudgetFinalScore}");
        builder.AppendLine($"threats=[{string.Join(", ", unit.Governance.DeclaredThreatPatterns)}]");
        builder.AppendLine($"counters=[{string.Join(", ", unit.Governance.DeclaredCounterTools.Select(tool => $"{tool.Tool}:{tool.Strength}"))}]");
        builder.AppendLine($"flags={unit.Governance.DeclaredFeatureFlags}");
        return builder.ToString().TrimEnd();
    }

    private static string BuildReadabilitySummary(ReadabilityReport? report)
    {
        if (report == null)
        {
            return "Readability report unavailable.";
        }

        var builder = new StringBuilder();
        builder.AppendLine($"salience_p95={report.SalienceWeightPer1sP95:0.###}");
        builder.AppendLine($"unexplained_damage={report.UnexplainedDamageRatio:0.###}");
        builder.AppendLine($"target_switch_p95={report.TargetSwitchesPer10sP95:0.###}");
        builder.AppendLine($"violations=[{string.Join(", ", report.Violations ?? Array.Empty<ReadabilityViolationKind>())}]");
        return builder.ToString().TrimEnd();
    }

    private static string BuildExplanationSummary(BattleSummaryReport? report)
    {
        if (report == null)
        {
            return "Battle summary unavailable.";
        }

        var builder = new StringBuilder();
        builder.AppendLine($"top_damage=[{string.Join(", ", report.TopDamageSources ?? Array.Empty<string>())}]");
        builder.AppendLine($"top_reasons=[{string.Join(", ", report.TopDecisionReasons ?? Array.Empty<string>())}]");
        builder.AppendLine($"decisive=[{string.Join(", ", report.DecisiveMoments ?? Array.Empty<string>())}]");
        return builder.ToString().TrimEnd();
    }

    private static string BuildProvenanceSummary(System.Collections.Generic.IReadOnlyList<CompileProvenanceEntry> provenance)
    {
        if (provenance == null || provenance.Count == 0)
        {
            return "Provenance unavailable.";
        }

        var builder = new StringBuilder();
        var grouped = provenance
            .GroupBy(entry => entry.SubjectId)
            .OrderBy(group => group.Key);

        foreach (var group in grouped)
        {
            builder.AppendLine($"[{group.Key}]");
            foreach (var entry in group)
            {
                builder.AppendLine($"  {entry.ArtifactKind} source={entry.SourceId}");
                if (entry.Details != null && entry.Details.Count > 0)
                {
                    foreach (var detail in entry.Details)
                    {
                        builder.AppendLine($"    {detail}");
                    }
                }
            }
        }

        return builder.ToString().TrimEnd();
    }
}
