using SM.Unity.Sandbox;
using SM.Editor.Authoring.CombatSandbox;
using SM.Editor.Bootstrap;
using UnityEditor;
using UnityEngine;
using System.Collections.Generic;

namespace SM.Editor.Authoring.Inspectors;

[CustomEditor(typeof(CombatSandboxConfig))]
public sealed class CombatSandboxConfigEditor : UnityEditor.Editor
{
    private static readonly Dictionary<int, CombatSandboxEditorPreviewResult> PreviewCache = new();
    private static readonly Dictionary<int, CombatSandboxEditorRunSummary> RunCache = new();

    private SerializedProperty _id = null!;
    private SerializedProperty _displayName = null!;
    private SerializedProperty _useScenarioAuthoring = null!;
    private SerializedProperty _defaultLaneKind = null!;
    private SerializedProperty _sceneLayout = null!;
    private SerializedProperty _previewSettings = null!;
    private SerializedProperty _scenario = null!;
    private SerializedProperty _leftTeam = null!;
    private SerializedProperty _rightTeam = null!;
    private SerializedProperty _execution = null!;
    private SerializedProperty _allyPosture = null!;
    private SerializedProperty _teamTacticId = null!;
    private SerializedProperty _enemyPosture = null!;
    private SerializedProperty _seed = null!;
    private SerializedProperty _batchCount = null!;
    private SerializedProperty _allySlots = null!;
    private SerializedProperty _enemySlots = null!;
    private bool _showLegacyMirror = true;
    private string _inspectUnitId = string.Empty;
    private Vector2 _previewScroll;
    private Vector2 _resultsScroll;

    private void OnEnable()
    {
        _id = serializedObject.FindProperty(nameof(CombatSandboxConfig.Id));
        _displayName = serializedObject.FindProperty(nameof(CombatSandboxConfig.DisplayName));
        _useScenarioAuthoring = serializedObject.FindProperty(nameof(CombatSandboxConfig.UseScenarioAuthoring));
        _defaultLaneKind = serializedObject.FindProperty(nameof(CombatSandboxConfig.DefaultLaneKind));
        _sceneLayout = serializedObject.FindProperty(nameof(CombatSandboxConfig.SceneLayout));
        _previewSettings = serializedObject.FindProperty(nameof(CombatSandboxConfig.PreviewSettings));
        _scenario = serializedObject.FindProperty(nameof(CombatSandboxConfig.Scenario));
        _leftTeam = serializedObject.FindProperty(nameof(CombatSandboxConfig.LeftTeam));
        _rightTeam = serializedObject.FindProperty(nameof(CombatSandboxConfig.RightTeam));
        _execution = serializedObject.FindProperty(nameof(CombatSandboxConfig.Execution));
        _allyPosture = serializedObject.FindProperty(nameof(CombatSandboxConfig.AllyPosture));
        _teamTacticId = serializedObject.FindProperty(nameof(CombatSandboxConfig.TeamTacticId));
        _enemyPosture = serializedObject.FindProperty(nameof(CombatSandboxConfig.EnemyPosture));
        _seed = serializedObject.FindProperty(nameof(CombatSandboxConfig.Seed));
        _batchCount = serializedObject.FindProperty(nameof(CombatSandboxConfig.BatchCount));
        _allySlots = serializedObject.FindProperty(nameof(CombatSandboxConfig.AllySlots));
        _enemySlots = serializedObject.FindProperty(nameof(CombatSandboxConfig.EnemySlots));
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        DrawGeneralSection();
        EditorGUILayout.Space(8f);
        DrawAuthoringSection();
        EditorGUILayout.Space(8f);
        DrawActionSection();
        EditorGUILayout.Space(8f);
        DrawPreviewSection();
        EditorGUILayout.Space(8f);
        DrawResultsSection();
        EditorGUILayout.Space(8f);
        _showLegacyMirror = EditorGUILayout.Foldout(_showLegacyMirror, EditorLocalizedTextResolver.Label("Legacy Mirror / Compatibility", "Legacy Mirror / Compatibility"), true);
        if (_showLegacyMirror)
        {
            DrawAllySlots();
            EditorGUILayout.Space(8f);
            DrawEnemySlots();
        }

        serializedObject.ApplyModifiedProperties();
    }

    private void DrawGeneralSection()
    {
        EditorGUILayout.LabelField(EditorLocalizedTextResolver.Label("Combat Sandbox Active Handoff", "Combat Sandbox Active Handoff"), EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            EditorLocalizedTextResolver.Label(
                "이 asset은 runtime direct sandbox가 읽는 active handoff이자 주 authoring surface다. preset library와 history/result 비교는 Window/SM/Combat Sandbox에서 보조로 다룬다.",
                "This asset is the runtime active handoff and the primary authoring surface for direct Combat Sandbox play. Use Window/SM/Combat Sandbox as the secondary library, history, and result surface."),
            MessageType.Info);
        EditorGUILayout.PropertyField(_id, new GUIContent(EditorLocalizedTextResolver.Label("구성 ID", "Config Id")));
        EditorGUILayout.PropertyField(_displayName, new GUIContent(EditorLocalizedTextResolver.Label("표시 이름", "Display Name")));
        EditorGUILayout.PropertyField(_useScenarioAuthoring, new GUIContent(EditorLocalizedTextResolver.Label("Scenario Authoring 사용", "Use Scenario Authoring")));
        EditorGUILayout.PropertyField(_defaultLaneKind, new GUIContent(EditorLocalizedTextResolver.Label("기본 lane", "Default Lane")));
        EditorGUILayout.PropertyField(_sceneLayout, new GUIContent(EditorLocalizedTextResolver.Label("씬 레이아웃", "Scene Layout")));
        EditorGUILayout.PropertyField(_previewSettings, new GUIContent(EditorLocalizedTextResolver.Label("미리보기 설정", "Preview Settings")));
    }

    private void DrawAuthoringSection()
    {
        EditorGUILayout.LabelField(EditorLocalizedTextResolver.Label("Scenario Handoff", "Scenario Handoff"), EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(_scenario, new GUIContent(EditorLocalizedTextResolver.Label("시나리오 메타데이터", "Scenario Metadata")), true);
        EditorGUILayout.PropertyField(_leftTeam, new GUIContent(EditorLocalizedTextResolver.Label("왼쪽 팀", "Left Team")), true);
        EditorGUILayout.PropertyField(_rightTeam, new GUIContent(EditorLocalizedTextResolver.Label("오른쪽 팀", "Right Team")), true);
        EditorGUILayout.PropertyField(_execution, new GUIContent(EditorLocalizedTextResolver.Label("실행 프리셋", "Execution Preset")), true);
        EditorGUILayout.Space(4f);
        EditorGUILayout.LabelField(EditorLocalizedTextResolver.Label("Legacy Fallback Values", "Legacy Fallback Values"), EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(_allyPosture, new GUIContent(EditorLocalizedTextResolver.Label("아군 태세", "Ally Posture")));
        EditorGUILayout.PropertyField(_teamTacticId, new GUIContent(EditorLocalizedTextResolver.Label("팀 전술 ID", "Team Tactic Id")));
        EditorGUILayout.PropertyField(_enemyPosture, new GUIContent(EditorLocalizedTextResolver.Label("적군 태세", "Enemy Posture")));
        EditorGUILayout.PropertyField(_seed, new GUIContent(EditorLocalizedTextResolver.Label("시드", "Seed")));
        EditorGUILayout.PropertyField(_batchCount, new GUIContent(EditorLocalizedTextResolver.Label("배치 횟수", "Batch Count")));
    }

    private void DrawActionSection()
    {
        EditorGUILayout.LabelField(EditorLocalizedTextResolver.Label("Inspector Actions", "Inspector Actions"), EditorStyles.boldLabel);
        _inspectUnitId = EditorGUILayout.TextField(EditorLocalizedTextResolver.Label("검사 유닛 ID", "Inspect Unit Id"), _inspectUnitId);

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button(EditorLocalizedTextResolver.Label("미리보기 컴파일", "Compile Preview")))
            {
                CompilePreview();
            }

            if (GUILayout.Button(EditorLocalizedTextResolver.Label("액티브로 밀기", "Push Active")))
            {
                PushActive();
            }

            if (GUILayout.Button(EditorLocalizedTextResolver.Label("라이브러리 열기", "Open Library")))
            {
                CombatSandboxWindow.OpenWindow();
            }
        }

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button(EditorLocalizedTextResolver.Label("단일 실행", "Run Single")))
            {
                Run(false, false);
            }

            if (GUILayout.Button(EditorLocalizedTextResolver.Label("배치 실행", "Run Batch")))
            {
                Run(true, false);
            }

            if (GUILayout.Button(EditorLocalizedTextResolver.Label("사이드 스왑", "Run Side Swap")))
            {
                Run(true, true);
            }

            if (GUILayout.Button(EditorLocalizedTextResolver.Label("밀고 실행", "Push Active + Play")))
            {
                if (PushActive())
                {
                    FirstPlayableBootstrap.PlayCombatSandbox();
                }
            }
        }
    }

    private void DrawPreviewSection()
    {
        EditorGUILayout.LabelField(EditorLocalizedTextResolver.Label("Preview", "Preview"), EditorStyles.boldLabel);
        if (!PreviewCache.TryGetValue(target.GetInstanceID(), out var preview))
        {
            EditorGUILayout.HelpBox(EditorLocalizedTextResolver.Label("아직 컴파일한 미리보기가 없습니다.", "No compiled preview yet."), MessageType.None);
            return;
        }

        _previewScroll = EditorGUILayout.BeginScrollView(_previewScroll, GUILayout.MinHeight(220f));
        DrawReadOnlyBlock("Scenario Summary", preview.ScenarioSummary);
        DrawReadOnlyBlock("Left Team Preview", preview.LeftTeamPreview);
        DrawReadOnlyBlock("Right Team Preview", preview.RightTeamPreview);
        DrawReadOnlyBlock("Breakpoint Summary", preview.LaunchTruth.BreakpointSummary);
        DrawReadOnlyBlock("Baseline Drift", preview.LaunchTruth.DriftSummary);
        DrawReadOnlyBlock("Slice Membership", preview.LaunchTruth.MembershipWarning);
        DrawReadOnlyBlock("Validation", preview.ValidationMessage);
        EditorGUILayout.EndScrollView();
    }

    private void DrawResultsSection()
    {
        EditorGUILayout.LabelField(EditorLocalizedTextResolver.Label("Results", "Results"), EditorStyles.boldLabel);
        if (!RunCache.TryGetValue(target.GetInstanceID(), out var run))
        {
            EditorGUILayout.HelpBox(EditorLocalizedTextResolver.Label("아직 실행 결과가 없습니다.", "No run results yet."), MessageType.None);
            return;
        }

        _resultsScroll = EditorGUILayout.BeginScrollView(_resultsScroll, GUILayout.MinHeight(220f));
        EditorGUILayout.LabelField($"Compile Hash: {run.PrimaryResult.PlayerSnapshot.CompileHash}");
        EditorGUILayout.LabelField($"Replay Hash: {run.PrimaryResult.ReplayHash}");
        EditorGUILayout.LabelField($"Layout Source: {run.LayoutSourceLabel}");
        DrawReadOnlyBlock("Metrics", BuildMetricsSummary(run.PrimaryResult.Metrics, run.SideSwapResult));
        DrawReadOnlyBlock("Counter Coverage", CombatSandboxExecutionService.BuildCounterCoverageSummary(run.PrimaryResult.PlayerSnapshot.TeamCounterCoverage, SM.Combat.Services.CounterCoverageAggregationService.AggregateFromLoadouts(run.PrimaryResult.EnemyLoadout)));
        DrawReadOnlyBlock("Governance", CombatSandboxExecutionService.BuildGovernanceSummary(run.PrimaryResult.PlayerSnapshot, _inspectUnitId));
        DrawReadOnlyBlock("Readability", CombatSandboxExecutionService.BuildReadabilitySummary(run.PrimaryResult.LastReplay.Readability));
        DrawReadOnlyBlock("Explanation", CombatSandboxExecutionService.BuildExplanationSummary(run.PrimaryResult.LastReplay.BattleSummary));
        DrawReadOnlyBlock("Provenance", CombatSandboxExecutionService.BuildProvenanceSummary(run.PrimaryResult.Provenance));
        EditorGUILayout.EndScrollView();
    }

    private void DrawAllySlots()
    {
        DrawArraySizeToolbar(_allySlots, EditorLocalizedTextResolver.Label("아군 슬롯", "Ally Slots"));

        for (var index = 0; index < _allySlots.arraySize; index++)
        {
            var slot = _allySlots.GetArrayElementAtIndex(index);
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                DrawSlotHeader(EditorLocalizedTextResolver.Label($"아군 슬롯 {index + 1}", $"Ally Slot {index + 1}"), _allySlots, index);
                EditorGUILayout.PropertyField(slot.FindPropertyRelative(nameof(CombatSandboxAllySlot.HeroId)), new GUIContent(EditorLocalizedTextResolver.Label("영웅 ID", "Hero Id")));
                EditorGUILayout.PropertyField(slot.FindPropertyRelative(nameof(CombatSandboxAllySlot.Anchor)), new GUIContent(EditorLocalizedTextResolver.Label("배치", "Anchor")));
                EditorGUILayout.PropertyField(slot.FindPropertyRelative(nameof(CombatSandboxAllySlot.RoleInstructionIdOverride)), new GUIContent(EditorLocalizedTextResolver.Label("역할 Override ID", "Role Override Id")));

                var preview = CombatSandboxPreviewResolver.ResolveAlly(new CombatSandboxAllySlot
                {
                    HeroId = slot.FindPropertyRelative(nameof(CombatSandboxAllySlot.HeroId)).stringValue,
                    Anchor = (SM.Combat.Model.DeploymentAnchorId)slot.FindPropertyRelative(nameof(CombatSandboxAllySlot.Anchor)).enumValueIndex,
                    RoleInstructionIdOverride = slot.FindPropertyRelative(nameof(CombatSandboxAllySlot.RoleInstructionIdOverride)).stringValue,
                });
                DrawPreview(preview);
            }
        }
    }

    private void DrawEnemySlots()
    {
        DrawArraySizeToolbar(_enemySlots, EditorLocalizedTextResolver.Label("적군 슬롯", "Enemy Slots"));

        for (var index = 0; index < _enemySlots.arraySize; index++)
        {
            var slot = _enemySlots.GetArrayElementAtIndex(index);
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                DrawSlotHeader(EditorLocalizedTextResolver.Label($"적군 슬롯 {index + 1}", $"Enemy Slot {index + 1}"), _enemySlots, index);
                EditorGUILayout.PropertyField(slot.FindPropertyRelative(nameof(CombatSandboxEnemySlot.ParticipantId)), new GUIContent(EditorLocalizedTextResolver.Label("참가자 ID", "Participant Id")));
                EditorGUILayout.PropertyField(slot.FindPropertyRelative(nameof(CombatSandboxEnemySlot.DisplayName)), new GUIContent(EditorLocalizedTextResolver.Label("표시 이름", "Display Name")));
                EditorGUILayout.PropertyField(slot.FindPropertyRelative(nameof(CombatSandboxEnemySlot.CharacterId)), new GUIContent(EditorLocalizedTextResolver.Label("캐릭터 ID", "Character Id")));
                EditorGUILayout.PropertyField(slot.FindPropertyRelative(nameof(CombatSandboxEnemySlot.ArchetypeIdOverride)), new GUIContent(EditorLocalizedTextResolver.Label("전투 원형 Override ID", "Archetype Override Id")));
                EditorGUILayout.PropertyField(slot.FindPropertyRelative(nameof(CombatSandboxEnemySlot.RoleInstructionId)), new GUIContent(EditorLocalizedTextResolver.Label("역할 ID", "Role Id")));
                EditorGUILayout.PropertyField(slot.FindPropertyRelative(nameof(CombatSandboxEnemySlot.Anchor)), new GUIContent(EditorLocalizedTextResolver.Label("배치", "Anchor")));
                EditorGUILayout.PropertyField(slot.FindPropertyRelative(nameof(CombatSandboxEnemySlot.PositiveTraitId)), new GUIContent(EditorLocalizedTextResolver.Label("긍정 특성 ID", "Positive Trait Id")));
                EditorGUILayout.PropertyField(slot.FindPropertyRelative(nameof(CombatSandboxEnemySlot.NegativeTraitId)), new GUIContent(EditorLocalizedTextResolver.Label("부정 특성 ID", "Negative Trait Id")));
                EditorGUILayout.PropertyField(slot.FindPropertyRelative(nameof(CombatSandboxEnemySlot.TemporaryAugmentIds)), new GUIContent(EditorLocalizedTextResolver.Label("임시 증강 ID", "Temporary Augment Ids")), true);

                var preview = CombatSandboxPreviewResolver.ResolveEnemy(new CombatSandboxEnemySlot
                {
                    ParticipantId = slot.FindPropertyRelative(nameof(CombatSandboxEnemySlot.ParticipantId)).stringValue,
                    DisplayName = slot.FindPropertyRelative(nameof(CombatSandboxEnemySlot.DisplayName)).stringValue,
                    CharacterId = slot.FindPropertyRelative(nameof(CombatSandboxEnemySlot.CharacterId)).stringValue,
                    ArchetypeIdOverride = slot.FindPropertyRelative(nameof(CombatSandboxEnemySlot.ArchetypeIdOverride)).stringValue,
                    RoleInstructionId = slot.FindPropertyRelative(nameof(CombatSandboxEnemySlot.RoleInstructionId)).stringValue,
                    Anchor = (SM.Combat.Model.DeploymentAnchorId)slot.FindPropertyRelative(nameof(CombatSandboxEnemySlot.Anchor)).enumValueIndex,
                    PositiveTraitId = slot.FindPropertyRelative(nameof(CombatSandboxEnemySlot.PositiveTraitId)).stringValue,
                    NegativeTraitId = slot.FindPropertyRelative(nameof(CombatSandboxEnemySlot.NegativeTraitId)).stringValue,
                });
                DrawPreview(preview);
            }
        }
    }

    private static void DrawPreview(CombatSandboxAxisPreview preview)
    {
        EditorGUILayout.LabelField(EditorLocalizedTextResolver.Label("축 미리보기", "Axis Preview"), EditorStyles.miniBoldLabel);
        EditorGUILayout.HelpBox(preview.BuildSummary(), preview.IsResolved ? MessageType.None : MessageType.Warning);
    }

    private void CompilePreview()
    {
        ApplyPendingChanges();
        try
        {
            PreviewCache[target.GetInstanceID()] = CombatSandboxEditorSession.Shared.BuildPreview((CombatSandboxConfig)target);
        }
        catch (System.Exception ex)
        {
            EditorUtility.DisplayDialog("Combat Sandbox", ex.Message, "OK");
        }
    }

    private bool PushActive()
    {
        ApplyPendingChanges();
        if (CombatSandboxAuthoringAssetUtility.IsActiveConfigAsset((CombatSandboxConfig)target))
        {
            EditorUtility.SetDirty(target);
            AssetDatabase.SaveAssets();
            return true;
        }

        if (CombatSandboxAuthoringAssetUtility.TryPushConfigToActiveConfig((CombatSandboxConfig)target, out var message))
        {
            if (!string.IsNullOrWhiteSpace(message))
            {
                Debug.Log(message);
            }

            return true;
        }

        EditorUtility.DisplayDialog("Combat Sandbox", message, "OK");
        return false;
    }

    private void Run(bool runAsBatch, bool runSideSwap)
    {
        ApplyPendingChanges();
        try
        {
            var sceneController = FindAnyObjectByType<CombatSandboxSceneController>();
            var session = CombatSandboxEditorSession.Shared;
            var config = (CombatSandboxConfig)target;
            var runSummary = session.Run(
                config,
                config.Seed,
                config.BatchCount,
                runAsBatch,
                runSideSwap,
                session.ResolveLayout(config, sceneController));
            PreviewCache[target.GetInstanceID()] = runSummary.Preview;
            RunCache[target.GetInstanceID()] = runSummary;
        }
        catch (System.Exception ex)
        {
            EditorUtility.DisplayDialog("Combat Sandbox", ex.Message, "OK");
        }
    }

    private void ApplyPendingChanges()
    {
        serializedObject.ApplyModifiedProperties();
        EditorUtility.SetDirty(target);
        AssetDatabase.SaveAssets();
    }

    private static void DrawReadOnlyBlock(string label, string value)
    {
        EditorGUILayout.LabelField(label, EditorStyles.miniBoldLabel);
        using (new EditorGUI.DisabledScope(true))
        {
            EditorGUILayout.TextArea(string.IsNullOrWhiteSpace(value) ? "none" : value, GUILayout.MinHeight(44f));
        }
    }

    private static string BuildMetricsSummary(CombatSandboxMetrics metrics, CombatSandboxRunResult? sideSwapResult)
    {
        var summary =
            $"win_rate={metrics.WinRate:0.###}\navg_duration={metrics.AverageDurationSeconds:0.###}\navg_events={metrics.AverageEventCount:0.###}\nfirst_action={metrics.AverageFirstActionSeconds:0.###}";
        if (sideSwapResult == null)
        {
            return summary;
        }

        return summary +
               $"\n--- side_swap ---\nwin_rate={sideSwapResult.Metrics.WinRate:0.###}\navg_duration={sideSwapResult.Metrics.AverageDurationSeconds:0.###}\navg_events={sideSwapResult.Metrics.AverageEventCount:0.###}\nfirst_action={sideSwapResult.Metrics.AverageFirstActionSeconds:0.###}";
    }

    private static void DrawArraySizeToolbar(SerializedProperty arrayProperty, string label)
    {
        using (new EditorGUILayout.HorizontalScope())
        {
            EditorGUILayout.LabelField(label, EditorStyles.boldLabel);
            var newSize = Mathf.Max(0, EditorGUILayout.IntField(arrayProperty.arraySize, GUILayout.Width(56f)));
            if (newSize != arrayProperty.arraySize)
            {
                arrayProperty.arraySize = newSize;
            }

            if (GUILayout.Button(EditorLocalizedTextResolver.Label("추가", "Add"), GUILayout.Width(60f)))
            {
                arrayProperty.InsertArrayElementAtIndex(arrayProperty.arraySize);
            }
        }
    }

    private static void DrawSlotHeader(string label, SerializedProperty arrayProperty, int index)
    {
        using (new EditorGUILayout.HorizontalScope())
        {
            EditorGUILayout.LabelField(label, EditorStyles.boldLabel);
            if (GUILayout.Button(EditorLocalizedTextResolver.Label("삭제", "Remove"), GUILayout.Width(72f)))
            {
                arrayProperty.DeleteArrayElementAtIndex(index);
            }
        }
    }
}
