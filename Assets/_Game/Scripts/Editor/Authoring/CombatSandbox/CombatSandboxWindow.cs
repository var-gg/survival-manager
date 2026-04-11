using System;
using System.Collections.Generic;
using System.Linq;
using SM.Editor.Bootstrap;
using SM.Combat.Services;
using SM.Unity;
using SM.Unity.Sandbox;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace SM.Editor.Authoring.CombatSandbox;

public sealed class CombatSandboxWindow : EditorWindow
{
    private const string MenuPath = "Window/SM/Combat Sandbox";

    private CombatSandboxState _state = null!;
    private Vector2 _libraryScroll;
    private Vector2 _detailsScroll;
    private GUIStyle? _wrappedTextAreaStyle;

    public CombatSandboxState State => _state;

    [MenuItem(MenuPath)]
    public static void OpenWindow()
    {
        var window = GetWindow<CombatSandboxWindow>();
        window.titleContent = new GUIContent("Combat Sandbox");
        window.minSize = new Vector2(960f, 620f);
        window.Show();
    }

    private void OnEnable()
    {
        CombatSandboxAuthoringAssetUtility.EnsureStarterLibrary();
        _state = CreateInstance<CombatSandboxState>();
        _state.hideFlags = HideFlags.HideAndDontSave;
        _state.Config = CombatSandboxAuthoringAssetUtility.EnsureActiveConfig();
        rootVisualElement.Clear();
        rootVisualElement.Add(new IMGUIContainer(DrawGui));
        RefreshPreview();
    }

    private void OnDisable()
    {
        if (_state != null)
        {
            DestroyImmediate(_state);
        }
    }

    private void DrawGui()
    {
        EnsureState();
        _wrappedTextAreaStyle ??= new GUIStyle(EditorStyles.textArea) { wordWrap = true };

        DrawHeader();
        DrawToolbar();

        using (new EditorGUILayout.HorizontalScope())
        {
            DrawLibraryPane();
            GUILayout.Space(8f);
            DrawDetailsPane();
        }

        DrawResultsPane();
    }

    private void DrawHeader()
    {
        EditorGUILayout.HelpBox(
            "Daily edit/compile/play starts at SM/Authoring/Combat Sandbox. Use SM/Play/Combat Sandbox for direct runtime verification, and use this window as the secondary library/history/results surface.",
            MessageType.Info);
        using (new EditorGUILayout.HorizontalScope())
        {
            using (new EditorGUI.DisabledScope(true))
            {
                _state.Config = (CombatSandboxConfig)EditorGUILayout.ObjectField("Active Handoff", _state.Config, typeof(CombatSandboxConfig), false);
            }

            if (GUILayout.Button("Open Active Config", GUILayout.Width(150f)))
            {
                CombatSandboxAuthoringAssetUtility.OpenActiveConfig();
                _state.Config = CombatSandboxAuthoringAssetUtility.EnsureActiveConfig();
            }

            if (GUILayout.Button("Ensure Starter Library", GUILayout.Width(160f)))
            {
                CombatSandboxAuthoringAssetUtility.EnsureStarterLibrary();
                _state.Config = CombatSandboxAuthoringAssetUtility.EnsureActiveConfig();
            }
        }
    }

    private void DrawToolbar()
    {
        using (new EditorGUILayout.HorizontalScope())
        {
            _state.SearchText = EditorGUILayout.TextField("Search", _state.SearchText);
            _state.FavoritesOnly = GUILayout.Toggle(_state.FavoritesOnly, "Favorites Only", GUILayout.Width(120f));

            var tags = BuildTagOptions();
            var tagIndex = Array.IndexOf(tags, string.IsNullOrWhiteSpace(_state.TagFilter) ? "All Tags" : _state.TagFilter);
            var selectedTag = EditorGUILayout.Popup("Tag", Mathf.Max(0, tagIndex), tags);
            _state.TagFilter = selectedTag <= 0 ? string.Empty : tags[selectedTag];
        }

        using (new EditorGUILayout.HorizontalScope())
        {
            _state.Seed = EditorGUILayout.IntField("Seed Override", _state.Seed);
            _state.BatchCount = Mathf.Max(0, EditorGUILayout.IntField("Batch Count", _state.BatchCount));
            _state.InspectUnitId = EditorGUILayout.TextField("Inspect Unit", _state.InspectUnitId);
        }

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Set Active", GUILayout.Width(120f)))
            {
                SetActiveFromSelection();
            }

            if (GUILayout.Button("Run Single", GUILayout.Width(120f)))
            {
                Execute(runAsBatch: false, runSideSwap: false);
            }

            if (GUILayout.Button("Run Batch", GUILayout.Width(120f)))
            {
                Execute(runAsBatch: true, runSideSwap: false);
            }

            if (GUILayout.Button("Run Side Swap", GUILayout.Width(140f)))
            {
                Execute(runAsBatch: true, runSideSwap: true);
            }

            if (GUILayout.Button("Push Active + Play", GUILayout.Width(160f)))
            {
                if (SetActiveFromSelection())
                {
                    FirstPlayableBootstrap.PlayCombatSandbox();
                }
            }

            if (GUILayout.Button("Refresh Preview", GUILayout.Width(140f)))
            {
                RefreshPreview();
            }
        }
    }

    private void DrawLibraryPane()
    {
        using (new EditorGUILayout.VerticalScope(GUILayout.Width(position.width * 0.38f)))
        {
            EditorGUILayout.LabelField("Preset Library", EditorStyles.boldLabel);
            _libraryScroll = EditorGUILayout.BeginScrollView(_libraryScroll, GUILayout.ExpandHeight(true));
            foreach (var scenario in FilterScenarios(CombatSandboxAuthoringAssetUtility.FindScenarioAssets()))
            {
                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        if (GUILayout.Button(scenario.DisplayName, EditorStyles.linkLabel))
                        {
                            SelectScenario(scenario);
                        }

                        GUILayout.FlexibleSpace();
                        GUILayout.Label(scenario.IsFavorite ? "★" : "·", GUILayout.Width(18f));
                    }

                    EditorGUILayout.LabelField($"id={scenario.ScenarioId}");
                    EditorGUILayout.LabelField($"tags={FormatTags(scenario.Tags)}");
                    EditorGUILayout.LabelField($"left={scenario.LeftTeam?.DisplayName ?? "none"}");
                    EditorGUILayout.LabelField($"right={scenario.RightTeam?.DisplayName ?? "none"}");
                    if (GUILayout.Button("Select", GUILayout.Width(90f)))
                    {
                        SelectScenario(scenario);
                    }
                }
            }

            EditorGUILayout.EndScrollView();
        }
    }

    private void DrawDetailsPane()
    {
        using (new EditorGUILayout.VerticalScope(GUILayout.ExpandWidth(true)))
        {
            if (_state.SelectedScenario == null)
            {
                EditorGUILayout.HelpBox("Select a scenario from the library to inspect its notes, preview, and authoring actions.", MessageType.None);
                return;
            }

            var scenario = _state.SelectedScenario;
            EditorGUILayout.LabelField(scenario.DisplayName, EditorStyles.boldLabel);
            EditorGUILayout.LabelField($"Scenario Id: {scenario.ScenarioId}");
            EditorGUILayout.LabelField($"Tags: {FormatTags(scenario.Tags)}");
            EditorGUILayout.LabelField($"Execution: {scenario.ExecutionPreset?.Settings.DisplayName ?? "none"}");
            EditorGUILayout.LabelField($"Left Team: {scenario.LeftTeam?.DisplayName ?? "none"}");
            EditorGUILayout.LabelField($"Right Team: {scenario.RightTeam?.DisplayName ?? "none"}");

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button(scenario.IsFavorite ? "Unset Favorite" : "Mark Favorite", GUILayout.Width(120f)))
                {
                    scenario.IsFavorite = !scenario.IsFavorite;
                    EditorUtility.SetDirty(scenario);
                    AssetDatabase.SaveAssets();
                }

                if (GUILayout.Button("Save As New", GUILayout.Width(120f)))
                {
                    var clone = CombatSandboxAuthoringAssetUtility.DuplicateScenario(scenario);
                    if (clone != null)
                    {
                        SelectScenario(clone);
                    }
                }
            }

            _detailsScroll = EditorGUILayout.BeginScrollView(_detailsScroll, GUILayout.ExpandHeight(true));
            DrawReadOnlyBlock("Expected Outcome", string.IsNullOrWhiteSpace(scenario.ExpectedOutcome) ? "none" : scenario.ExpectedOutcome);
            DrawReadOnlyBlock("Notes", string.IsNullOrWhiteSpace(scenario.Notes) ? "none" : scenario.Notes);
            DrawReadOnlyBlock("Scenario Summary", string.IsNullOrWhiteSpace(_state.ScenarioSummary) ? "Preview not generated yet." : _state.ScenarioSummary);
            DrawReadOnlyBlock("Left Team Preview", string.IsNullOrWhiteSpace(_state.LeftTeamPreview) ? "Preview not generated yet." : _state.LeftTeamPreview);
            DrawReadOnlyBlock("Right Team Preview", string.IsNullOrWhiteSpace(_state.RightTeamPreview) ? "Preview not generated yet." : _state.RightTeamPreview);
            DrawReadOnlyBlock("Breakpoint Summary", _state.PreviewBreakpointSummary);
            DrawReadOnlyBlock("Baseline Drift", _state.PreviewBaselineDriftSummary);
            DrawReadOnlyBlock("Slice Membership", _state.PreviewMembershipWarning);
            DrawReadOnlyBlock("Recent", string.IsNullOrWhiteSpace(_state.RecentScenarioIdsCsv) ? "none" : _state.RecentScenarioIdsCsv.Replace(";", "\n"));
            EditorGUILayout.EndScrollView();
        }
    }

    private void DrawResultsPane()
    {
        EditorGUILayout.Space(8f);
        EditorGUILayout.LabelField("Results", EditorStyles.boldLabel);
        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            EditorGUILayout.LabelField($"Compile Hash: {_state.LastCompileHash}");
            EditorGUILayout.LabelField($"Replay Hash: {_state.LastReplayHash}");
            EditorGUILayout.LabelField($"Layout Source: {(string.IsNullOrWhiteSpace(_state.LayoutSourceLabel) ? "Default" : _state.LayoutSourceLabel)}");
            DrawReadOnlyBlock("Breakpoint Summary", _state.LastBreakpointSummary);
            DrawReadOnlyBlock("Baseline Drift", _state.LastBaselineDriftSummary);
            DrawReadOnlyBlock("Slice Membership", _state.LastMembershipWarning);
            DrawReadOnlyBlock("Metrics", _state.LastMetricsSummary);
            DrawReadOnlyBlock("Counter Coverage", _state.LastCounterCoverageSummary);
            DrawReadOnlyBlock("Governance", _state.LastGovernanceSummary);
            DrawReadOnlyBlock("Readability", _state.LastReadabilitySummary);
            DrawReadOnlyBlock("Explanation", _state.LastExplanationSummary);
            DrawReadOnlyBlock("Provenance", _state.LastProvenanceSummary);
            DrawReadOnlyBlock("Validation", _state.LastValidationMessage);
        }
    }

    private void Execute(bool runAsBatch, bool runSideSwap)
    {
        var config = BuildPreviewConfig();
        if (config == null)
        {
            _state.LastValidationMessage = "Unable to resolve a preview config.";
            return;
        }

        try
        {
            var sceneController = FindAnyObjectByType<CombatSandboxSceneController>();
            var session = CombatSandboxEditorSession.Shared;
            var runSummary = session.Run(
                config,
                _state.Seed,
                _state.BatchCount,
                runAsBatch,
                runSideSwap,
                session.ResolveLayout(config, sceneController));
            _state.LayoutSourceLabel = runSummary.LayoutSourceLabel;
            _state.LastCompileHash = runSummary.PrimaryResult.PlayerSnapshot.CompileHash;
            _state.LastReplayHash = runSummary.PrimaryResult.ReplayHash;
            _state.LastBreakpointSummary = runSummary.Preview.LaunchTruth.BreakpointSummary;
            _state.LastBaselineDriftSummary = runSummary.Preview.LaunchTruth.DriftSummary;
            _state.LastMembershipWarning = runSummary.Preview.LaunchTruth.MembershipWarning;
            _state.LastMetricsSummary = BuildMetricsSummary(runSummary.PrimaryResult.Metrics);
            _state.LastCounterCoverageSummary = CombatSandboxExecutionService.BuildCounterCoverageSummary(runSummary.PrimaryResult.PlayerSnapshot.TeamCounterCoverage, CounterCoverageAggregationService.AggregateFromLoadouts(runSummary.PrimaryResult.EnemyLoadout));
            _state.LastGovernanceSummary = CombatSandboxExecutionService.BuildGovernanceSummary(runSummary.PrimaryResult.PlayerSnapshot, _state.InspectUnitId);
            _state.LastReadabilitySummary = CombatSandboxExecutionService.BuildReadabilitySummary(runSummary.PrimaryResult.LastReplay.Readability);
            _state.LastExplanationSummary = CombatSandboxExecutionService.BuildExplanationSummary(runSummary.PrimaryResult.LastReplay.BattleSummary);
            _state.LastProvenanceSummary = CombatSandboxExecutionService.BuildProvenanceSummary(runSummary.PrimaryResult.Provenance);
            _state.LastValidationMessage = runSummary.Preview.ValidationMessage;

            if (runSummary.SideSwapResult != null)
            {
                _state.LastMetricsSummary += $"\n--- side_swap ---\n{BuildMetricsSummary(runSummary.SideSwapResult.Metrics)}";
            }
        }
        catch (Exception ex)
        {
            _state.LastValidationMessage = ex.Message;
            _state.LastBreakpointSummary = string.Empty;
            _state.LastBaselineDriftSummary = string.Empty;
            _state.LastMembershipWarning = string.Empty;
        }
        finally
        {
            DestroyImmediate(config);
        }

        Repaint();
    }

    private bool SetActiveFromSelection()
    {
        EnsureState();
        if (_state.Config == null || _state.SelectedScenario == null)
        {
            _state.LastValidationMessage = "Select a scenario and ensure the active handoff asset exists first.";
            return false;
        }

        if (!CombatSandboxAuthoringAssetUtility.TryPushScenarioToActiveConfig(_state.SelectedScenario, _state.Config, out var message))
        {
            _state.LastValidationMessage = message;
            return false;
        }

        if (!string.IsNullOrWhiteSpace(message))
        {
            _state.LastValidationMessage = message;
        }

        RefreshPreview();
        Selection.activeObject = _state.Config;
        EditorGUIUtility.PingObject(_state.Config);
        return true;
    }

    private void SelectScenario(CombatSandboxScenarioAsset scenario)
    {
        _state.SelectedScenario = scenario;
        AddRecentScenario(scenario.ScenarioId);
        RefreshPreview();
    }

    private void RefreshPreview()
    {
        var config = BuildPreviewConfig();
        if (config == null)
        {
            return;
        }

        try
        {
            var preview = CombatSandboxEditorSession.Shared.BuildPreview(config, _state.Seed);
            _state.ScenarioSummary = preview.ScenarioSummary;
            _state.LeftTeamPreview = preview.LeftTeamPreview;
            _state.RightTeamPreview = preview.RightTeamPreview;
            _state.PreviewBreakpointSummary = preview.LaunchTruth.BreakpointSummary;
            _state.PreviewBaselineDriftSummary = preview.LaunchTruth.DriftSummary;
            _state.PreviewMembershipWarning = preview.LaunchTruth.MembershipWarning;
            _state.LastValidationMessage = preview.ValidationMessage;
        }
        catch (Exception ex)
        {
            _state.ScenarioSummary = string.Empty;
            _state.LeftTeamPreview = string.Empty;
            _state.RightTeamPreview = string.Empty;
            _state.PreviewBreakpointSummary = string.Empty;
            _state.PreviewBaselineDriftSummary = string.Empty;
            _state.PreviewMembershipWarning = string.Empty;
            _state.LastValidationMessage = ex.Message;
        }
        finally
        {
            DestroyImmediate(config);
        }
    }

    private CombatSandboxConfig? BuildPreviewConfig()
    {
        EnsureState();
        var config = CreateInstance<CombatSandboxConfig>();
        config.hideFlags = HideFlags.HideAndDontSave;
        if (_state.SelectedScenario == null)
        {
            if (_state.Config == null)
            {
                DestroyImmediate(config);
                return null;
            }

            DestroyImmediate(config);
            return CombatSandboxAuthoringAssetUtility.CreateTransientConfigCopy(_state.Config);
        }

        if (!CombatSandboxAuthoringAssetUtility.TryPushScenarioToActiveConfig(_state.SelectedScenario, config, out var message))
        {
            _state.LastValidationMessage = message;
            DestroyImmediate(config);
            return null;
        }

        if (!string.IsNullOrWhiteSpace(message))
        {
            _state.LastValidationMessage = message;
        }

        return config;
    }

    private void EnsureState()
    {
        if (_state != null)
        {
            _state.Config ??= CombatSandboxAuthoringAssetUtility.EnsureActiveConfig();
            return;
        }

        _state = CreateInstance<CombatSandboxState>();
        _state.hideFlags = HideFlags.HideAndDontSave;
        _state.Config = CombatSandboxAuthoringAssetUtility.EnsureActiveConfig();
    }

    private string[] BuildTagOptions()
    {
        var tags = CombatSandboxAuthoringAssetUtility.FindScenarioAssets()
            .SelectMany(asset => asset.Tags ?? new List<string>())
            .Where(tag => !string.IsNullOrWhiteSpace(tag))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(tag => tag, StringComparer.Ordinal)
            .ToList();
        tags.Insert(0, "All Tags");
        return tags.ToArray();
    }

    private IEnumerable<CombatSandboxScenarioAsset> FilterScenarios(IReadOnlyList<CombatSandboxScenarioAsset> scenarios)
    {
        return scenarios.Where(asset =>
            (!_state.FavoritesOnly || asset.IsFavorite)
            && (string.IsNullOrWhiteSpace(_state.TagFilter) || (asset.Tags?.Any(tag => string.Equals(tag, _state.TagFilter, StringComparison.Ordinal)) ?? false))
            && (string.IsNullOrWhiteSpace(_state.SearchText)
                || asset.DisplayName.IndexOf(_state.SearchText, StringComparison.OrdinalIgnoreCase) >= 0
                || asset.ScenarioId.IndexOf(_state.SearchText, StringComparison.OrdinalIgnoreCase) >= 0));
    }

    private void AddRecentScenario(string scenarioId)
    {
        var recent = _state.RecentScenarioIdsCsv?
            .Split(';', StringSplitOptions.RemoveEmptyEntries)
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .ToList() ?? new List<string>();
        recent.RemoveAll(id => string.Equals(id, scenarioId, StringComparison.Ordinal));
        recent.Insert(0, scenarioId);
        _state.RecentScenarioIdsCsv = string.Join(";", recent.Take(6));
    }

    private void DrawReadOnlyBlock(string label, string value)
    {
        EditorGUILayout.LabelField(label, EditorStyles.miniBoldLabel);
        using (new EditorGUI.DisabledScope(true))
        {
            EditorGUILayout.TextArea(string.IsNullOrWhiteSpace(value) ? "none" : value, _wrappedTextAreaStyle, GUILayout.MinHeight(44f));
        }
    }

    private static string BuildMetricsSummary(CombatSandboxMetrics metrics)
    {
        return
            $"win_rate={metrics.WinRate:0.###}\navg_duration={metrics.AverageDurationSeconds:0.###}\navg_events={metrics.AverageEventCount:0.###}\nfirst_action={metrics.AverageFirstActionSeconds:0.###}";
    }

    private static string FormatTags(IEnumerable<string> tags)
    {
        var materialized = tags?.Where(tag => !string.IsNullOrWhiteSpace(tag)).ToList() ?? new List<string>();
        return materialized.Count == 0 ? "none" : string.Join(", ", materialized);
    }
}
