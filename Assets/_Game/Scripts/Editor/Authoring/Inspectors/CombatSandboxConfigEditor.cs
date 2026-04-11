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
        _showLegacyMirror = EditorGUILayout.Foldout(_showLegacyMirror, EditorLocalizedTextResolver.Label("레거시 미러 / 호환", "Legacy Mirror / Compatibility"), true);
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
        EditorGUILayout.LabelField(EditorLocalizedTextResolver.Label("Combat Sandbox 액티브 핸드오프", "Combat Sandbox Active Handoff"), EditorStyles.boldLabel);
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
        EditorGUILayout.LabelField(EditorLocalizedTextResolver.Label("시나리오 핸드오프", "Scenario Handoff"), EditorStyles.boldLabel);
        DrawScenarioMetadataProperty(_scenario, "시나리오 메타데이터", "Scenario Metadata");
        DrawTeamDefinitionProperty(_leftTeam, "왼쪽 팀", "Left Team");
        DrawTeamDefinitionProperty(_rightTeam, "오른쪽 팀", "Right Team");
        DrawExecutionSettingsProperty(_execution, "실행 프리셋", "Execution Preset");
        EditorGUILayout.Space(4f);
        EditorGUILayout.LabelField(EditorLocalizedTextResolver.Label("레거시 fallback 값", "Legacy Fallback Values"), EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(_allyPosture, new GUIContent(EditorLocalizedTextResolver.Label("아군 태세", "Ally Posture")));
        EditorGUILayout.PropertyField(_teamTacticId, new GUIContent(EditorLocalizedTextResolver.Label("팀 전술 ID", "Team Tactic Id")));
        EditorGUILayout.PropertyField(_enemyPosture, new GUIContent(EditorLocalizedTextResolver.Label("적군 태세", "Enemy Posture")));
        EditorGUILayout.PropertyField(_seed, new GUIContent(EditorLocalizedTextResolver.Label("시드", "Seed")));
        EditorGUILayout.PropertyField(_batchCount, new GUIContent(EditorLocalizedTextResolver.Label("배치 횟수", "Batch Count")));
    }

    private static void DrawScenarioMetadataProperty(SerializedProperty property, string koLabel, string enLabel)
    {
        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            DrawFoldout(property, koLabel, enLabel);
            if (!property.isExpanded)
            {
                return;
            }

            using (new EditorGUI.IndentLevelScope())
            {
                DrawLocalizedProperty(property.FindPropertyRelative(nameof(CombatSandboxScenarioMetadata.ScenarioId)), "시나리오 ID", "Scenario Id");
                DrawLocalizedProperty(property.FindPropertyRelative(nameof(CombatSandboxScenarioMetadata.DisplayName)), "표시 이름", "Display Name");
                DrawStringListProperty(property.FindPropertyRelative(nameof(CombatSandboxScenarioMetadata.Tags)), "태그", "Tags", "태그", "Tag");
                DrawLocalizedProperty(property.FindPropertyRelative(nameof(CombatSandboxScenarioMetadata.Notes)), "노트", "Notes");
                DrawLocalizedProperty(property.FindPropertyRelative(nameof(CombatSandboxScenarioMetadata.ExpectedOutcome)), "기대 결과", "Expected Outcome");
            }
        }
    }

    private static void DrawTeamDefinitionProperty(SerializedProperty property, string koLabel, string enLabel)
    {
        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            DrawFoldout(property, koLabel, enLabel);
            if (!property.isExpanded)
            {
                return;
            }

            using (new EditorGUI.IndentLevelScope())
            {
                DrawLocalizedProperty(property.FindPropertyRelative(nameof(CombatSandboxTeamDefinition.TeamId)), "팀 ID", "Team Id");
                DrawLocalizedProperty(property.FindPropertyRelative(nameof(CombatSandboxTeamDefinition.DisplayName)), "표시 이름", "Display Name");
                DrawLocalizedProperty(property.FindPropertyRelative(nameof(CombatSandboxTeamDefinition.SourceMode)), "소스 모드", "Source Mode");
                DrawLocalizedProperty(property.FindPropertyRelative(nameof(CombatSandboxTeamDefinition.TeamPosture)), "팀 태세", "Team Posture");
                DrawLocalizedProperty(property.FindPropertyRelative(nameof(CombatSandboxTeamDefinition.TeamTacticId)), "팀 전술 ID", "Team Tactic Id");
                DrawStringListProperty(property.FindPropertyRelative(nameof(CombatSandboxTeamDefinition.Tags)), "태그", "Tags", "태그", "Tag");
                DrawTeamMembersProperty(property.FindPropertyRelative(nameof(CombatSandboxTeamDefinition.Members)));
                DrawStringListProperty(property.FindPropertyRelative(nameof(CombatSandboxTeamDefinition.TeamTemporaryAugmentIds)), "팀 임시 증강 ID", "Team Temporary Augment Ids", "임시 증강", "Temp Augment");
                DrawStringListProperty(property.FindPropertyRelative(nameof(CombatSandboxTeamDefinition.TeamPermanentAugmentIds)), "팀 영구 증강 ID", "Team Permanent Augment Ids", "영구 증강", "Permanent Augment");
                DrawRemoteDeckProperty(property.FindPropertyRelative(nameof(CombatSandboxTeamDefinition.RemoteDeck)), "원격 덱 참조", "Remote Deck Reference");
                DrawLocalizedProperty(property.FindPropertyRelative(nameof(CombatSandboxTeamDefinition.ProvenanceLabel)), "출처 라벨", "Provenance Label");
                DrawLocalizedProperty(property.FindPropertyRelative(nameof(CombatSandboxTeamDefinition.Notes)), "노트", "Notes");
            }
        }
    }

    private static void DrawExecutionSettingsProperty(SerializedProperty property, string koLabel, string enLabel)
    {
        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            DrawFoldout(property, koLabel, enLabel);
            if (!property.isExpanded)
            {
                return;
            }

            using (new EditorGUI.IndentLevelScope())
            {
                DrawLocalizedProperty(property.FindPropertyRelative(nameof(CombatSandboxExecutionSettings.PresetId)), "프리셋 ID", "Preset Id");
                DrawLocalizedProperty(property.FindPropertyRelative(nameof(CombatSandboxExecutionSettings.DisplayName)), "표시 이름", "Display Name");
                DrawLocalizedProperty(property.FindPropertyRelative(nameof(CombatSandboxExecutionSettings.SeedMode)), "시드 모드", "Seed Mode");
                DrawLocalizedProperty(property.FindPropertyRelative(nameof(CombatSandboxExecutionSettings.Seed)), "시드", "Seed");
                DrawLocalizedProperty(property.FindPropertyRelative(nameof(CombatSandboxExecutionSettings.BatchCount)), "배치 횟수", "Batch Count");
                DrawLocalizedProperty(property.FindPropertyRelative(nameof(CombatSandboxExecutionSettings.RunSideSwap)), "사이드 스왑 실행", "Run Side Swap");
                DrawLocalizedProperty(property.FindPropertyRelative(nameof(CombatSandboxExecutionSettings.RecordReplay)), "리플레이 기록", "Record Replay");
                DrawLocalizedProperty(property.FindPropertyRelative(nameof(CombatSandboxExecutionSettings.StopOnMismatch)), "불일치 시 중단", "Stop On Mismatch");
                DrawLocalizedProperty(property.FindPropertyRelative(nameof(CombatSandboxExecutionSettings.StopOnReadabilityViolation)), "가독성 위반 시 중단", "Stop On Readability Violation");
                DrawLocalizedProperty(property.FindPropertyRelative(nameof(CombatSandboxExecutionSettings.Notes)), "노트", "Notes");
            }
        }
    }

    private static void DrawTeamMembersProperty(SerializedProperty arrayProperty)
    {
        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            DrawArraySizeToolbar(arrayProperty, EditorLocalizedTextResolver.Label("팀 멤버", "Team Members"));
            for (var index = 0; index < arrayProperty.arraySize; index++)
            {
                var member = arrayProperty.GetArrayElementAtIndex(index);
                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    DrawSlotHeader(EditorLocalizedTextResolver.Label($"멤버 {index + 1}", $"Member {index + 1}"), arrayProperty, index);
                    DrawLocalizedProperty(member.FindPropertyRelative(nameof(CombatSandboxTeamMemberDefinition.MemberId)), "멤버 ID", "Member Id");
                    DrawLocalizedProperty(member.FindPropertyRelative(nameof(CombatSandboxTeamMemberDefinition.SourceKind)), "유닛 소스", "Unit Source");
                    DrawLocalizedProperty(member.FindPropertyRelative(nameof(CombatSandboxTeamMemberDefinition.HeroId)), "영웅 ID", "Hero Id");
                    DrawLocalizedProperty(member.FindPropertyRelative(nameof(CombatSandboxTeamMemberDefinition.DisplayName)), "표시 이름", "Display Name");
                    DrawLocalizedProperty(member.FindPropertyRelative(nameof(CombatSandboxTeamMemberDefinition.ArchetypeId)), "전투 원형 ID", "Archetype Id");
                    DrawLocalizedProperty(member.FindPropertyRelative(nameof(CombatSandboxTeamMemberDefinition.CharacterId)), "캐릭터 ID", "Character Id");
                    DrawLocalizedProperty(member.FindPropertyRelative(nameof(CombatSandboxTeamMemberDefinition.Anchor)), "배치", "Anchor");
                    DrawLocalizedProperty(member.FindPropertyRelative(nameof(CombatSandboxTeamMemberDefinition.RoleInstructionId)), "역할 ID", "Role Id");
                    DrawBuildOverrideProperty(member.FindPropertyRelative(nameof(CombatSandboxTeamMemberDefinition.BuildOverride)), "빌드 Override", "Build Override");
                    DrawLocalizedProperty(member.FindPropertyRelative(nameof(CombatSandboxTeamMemberDefinition.Notes)), "노트", "Notes");
                }
            }
        }
    }

    private static void DrawBuildOverrideProperty(SerializedProperty property, string koLabel, string enLabel)
    {
        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            DrawFoldout(property, koLabel, enLabel);
            if (!property.isExpanded)
            {
                return;
            }

            using (new EditorGUI.IndentLevelScope())
            {
                DrawLocalizedProperty(property.FindPropertyRelative(nameof(CombatSandboxBuildOverrideData.OverrideId)), "오버라이드 ID", "Override Id");
                DrawLocalizedProperty(property.FindPropertyRelative(nameof(CombatSandboxBuildOverrideData.DisplayName)), "표시 이름", "Display Name");
                DrawStringListProperty(property.FindPropertyRelative(nameof(CombatSandboxBuildOverrideData.Tags)), "태그", "Tags", "태그", "Tag");
                DrawItemOverridesProperty(property.FindPropertyRelative(nameof(CombatSandboxBuildOverrideData.EquippedItems)));
                DrawLocalizedProperty(property.FindPropertyRelative(nameof(CombatSandboxBuildOverrideData.PassiveBoardId)), "패시브 보드 ID", "Passive Board Id");
                DrawStringListProperty(property.FindPropertyRelative(nameof(CombatSandboxBuildOverrideData.PassiveNodeIds)), "패시브 노드 ID", "Passive Node Ids", "패시브 노드", "Passive Node");
                DrawStringListProperty(property.FindPropertyRelative(nameof(CombatSandboxBuildOverrideData.TemporaryAugmentIds)), "임시 증강 ID", "Temporary Augment Ids", "임시 증강", "Temp Augment");
                DrawStringListProperty(property.FindPropertyRelative(nameof(CombatSandboxBuildOverrideData.PermanentAugmentIds)), "영구 증강 ID", "Permanent Augment Ids", "영구 증강", "Permanent Augment");
                DrawLocalizedProperty(property.FindPropertyRelative(nameof(CombatSandboxBuildOverrideData.FlexActiveSkillId)), "유연 액티브 스킬 ID", "Flex Active Skill Id");
                DrawLocalizedProperty(property.FindPropertyRelative(nameof(CombatSandboxBuildOverrideData.FlexPassiveSkillId)), "유연 패시브 스킬 ID", "Flex Passive Skill Id");
                DrawLocalizedProperty(property.FindPropertyRelative(nameof(CombatSandboxBuildOverrideData.PositiveTraitId)), "긍정 특성 ID", "Positive Trait Id");
                DrawLocalizedProperty(property.FindPropertyRelative(nameof(CombatSandboxBuildOverrideData.NegativeTraitId)), "부정 특성 ID", "Negative Trait Id");
                DrawLocalizedProperty(property.FindPropertyRelative(nameof(CombatSandboxBuildOverrideData.RoleInstructionIdOverride)), "역할 Override ID", "Role Override Id");
                DrawLocalizedProperty(property.FindPropertyRelative(nameof(CombatSandboxBuildOverrideData.RetrainCount)), "재훈련 횟수", "Retrain Count");
                DrawLocalizedProperty(property.FindPropertyRelative(nameof(CombatSandboxBuildOverrideData.Notes)), "노트", "Notes");
            }
        }
    }

    private static void DrawRemoteDeckProperty(SerializedProperty property, string koLabel, string enLabel)
    {
        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            DrawFoldout(property, koLabel, enLabel);
            if (!property.isExpanded)
            {
                return;
            }

            using (new EditorGUI.IndentLevelScope())
            {
                DrawLocalizedProperty(property.FindPropertyRelative(nameof(CombatSandboxRemoteDeckReference.UserId)), "사용자 ID", "User Id");
                DrawLocalizedProperty(property.FindPropertyRelative(nameof(CombatSandboxRemoteDeckReference.DeckId)), "덱 ID", "Deck Id");
                DrawLocalizedProperty(property.FindPropertyRelative(nameof(CombatSandboxRemoteDeckReference.Revision)), "리비전", "Revision");
            }
        }
    }

    private static void DrawItemOverridesProperty(SerializedProperty arrayProperty)
    {
        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            DrawArraySizeToolbar(arrayProperty, EditorLocalizedTextResolver.Label("장착 아이템", "Equipped Items"));
            for (var index = 0; index < arrayProperty.arraySize; index++)
            {
                var item = arrayProperty.GetArrayElementAtIndex(index);
                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    DrawSlotHeader(EditorLocalizedTextResolver.Label($"아이템 {index + 1}", $"Item {index + 1}"), arrayProperty, index);
                    DrawLocalizedProperty(item.FindPropertyRelative(nameof(CombatSandboxItemOverrideData.ItemId)), "아이템 ID", "Item Id");
                    DrawStringListProperty(item.FindPropertyRelative(nameof(CombatSandboxItemOverrideData.AffixIds)), "접두사 ID", "Affix Ids", "접두사", "Affix");
                }
            }
        }
    }

    private static void DrawStringListProperty(SerializedProperty arrayProperty, string koLabel, string enLabel, string itemKoPrefix, string itemEnPrefix)
    {
        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            DrawArraySizeToolbar(arrayProperty, EditorLocalizedTextResolver.Label(koLabel, enLabel));
            for (var index = 0; index < arrayProperty.arraySize; index++)
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.PropertyField(
                        arrayProperty.GetArrayElementAtIndex(index),
                        new GUIContent(EditorLocalizedTextResolver.Label($"{itemKoPrefix} {index + 1}", $"{itemEnPrefix} {index + 1}")));

                    if (GUILayout.Button(EditorLocalizedTextResolver.Label("삭제", "Remove"), GUILayout.Width(72f)))
                    {
                        arrayProperty.DeleteArrayElementAtIndex(index);
                        break;
                    }
                }
            }
        }
    }

    private static void DrawLocalizedProperty(SerializedProperty? property, string koLabel, string enLabel, bool includeChildren = false)
    {
        if (property == null)
        {
            return;
        }

        EditorGUILayout.PropertyField(property, new GUIContent(EditorLocalizedTextResolver.Label(koLabel, enLabel)), includeChildren);
    }

    private static void DrawFoldout(SerializedProperty property, string koLabel, string enLabel)
    {
        property.isExpanded = EditorGUILayout.Foldout(
            property.isExpanded,
            EditorLocalizedTextResolver.Label(koLabel, enLabel),
            true);
    }

    private void DrawActionSection()
    {
        EditorGUILayout.LabelField(EditorLocalizedTextResolver.Label("Inspector 작업", "Inspector Actions"), EditorStyles.boldLabel);
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
        EditorGUILayout.LabelField(EditorLocalizedTextResolver.Label("미리보기", "Preview"), EditorStyles.boldLabel);
        if (!PreviewCache.TryGetValue(target.GetInstanceID(), out var preview))
        {
            EditorGUILayout.HelpBox(EditorLocalizedTextResolver.Label("아직 컴파일한 미리보기가 없습니다.", "No compiled preview yet."), MessageType.None);
            return;
        }

        _previewScroll = EditorGUILayout.BeginScrollView(_previewScroll, GUILayout.MinHeight(220f));
        DrawReadOnlyBlock("시나리오 요약", "Scenario Summary", preview.ScenarioSummary);
        DrawReadOnlyBlock("왼쪽 팀 미리보기", "Left Team Preview", preview.LeftTeamPreview);
        DrawReadOnlyBlock("오른쪽 팀 미리보기", "Right Team Preview", preview.RightTeamPreview);
        DrawReadOnlyBlock("브레이크포인트 요약", "Breakpoint Summary", preview.LaunchTruth.BreakpointSummary);
        DrawReadOnlyBlock("기준선 드리프트", "Baseline Drift", preview.LaunchTruth.DriftSummary);
        DrawReadOnlyBlock("슬라이스 포함 여부", "Slice Membership", preview.LaunchTruth.MembershipWarning);
        DrawReadOnlyBlock("검증", "Validation", preview.ValidationMessage);
        EditorGUILayout.EndScrollView();
    }

    private void DrawResultsSection()
    {
        EditorGUILayout.LabelField(EditorLocalizedTextResolver.Label("결과", "Results"), EditorStyles.boldLabel);
        if (!RunCache.TryGetValue(target.GetInstanceID(), out var run))
        {
            EditorGUILayout.HelpBox(EditorLocalizedTextResolver.Label("아직 실행 결과가 없습니다.", "No run results yet."), MessageType.None);
            return;
        }

        _resultsScroll = EditorGUILayout.BeginScrollView(_resultsScroll, GUILayout.MinHeight(220f));
        EditorGUILayout.LabelField($"{EditorLocalizedTextResolver.Label("컴파일 해시", "Compile Hash")}: {run.PrimaryResult.PlayerSnapshot.CompileHash}");
        EditorGUILayout.LabelField($"{EditorLocalizedTextResolver.Label("리플레이 해시", "Replay Hash")}: {run.PrimaryResult.ReplayHash}");
        EditorGUILayout.LabelField($"{EditorLocalizedTextResolver.Label("레이아웃 소스", "Layout Source")}: {run.LayoutSourceLabel}");
        DrawReadOnlyBlock("지표", "Metrics", BuildMetricsSummary(run.PrimaryResult.Metrics, run.SideSwapResult));
        DrawReadOnlyBlock("카운터 커버리지", "Counter Coverage", CombatSandboxExecutionService.BuildCounterCoverageSummary(run.PrimaryResult.PlayerSnapshot.TeamCounterCoverage, SM.Combat.Services.CounterCoverageAggregationService.AggregateFromLoadouts(run.PrimaryResult.EnemyLoadout)));
        DrawReadOnlyBlock("거버넌스", "Governance", CombatSandboxExecutionService.BuildGovernanceSummary(run.PrimaryResult.PlayerSnapshot, _inspectUnitId));
        DrawReadOnlyBlock("가독성", "Readability", CombatSandboxExecutionService.BuildReadabilitySummary(run.PrimaryResult.LastReplay.Readability));
        DrawReadOnlyBlock("설명", "Explanation", CombatSandboxExecutionService.BuildExplanationSummary(run.PrimaryResult.LastReplay.BattleSummary));
        DrawReadOnlyBlock("출처", "Provenance", CombatSandboxExecutionService.BuildProvenanceSummary(run.PrimaryResult.Provenance));
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
        var config = (CombatSandboxConfig)target;
        if (CombatSandboxAuthoringAssetUtility.IsActiveConfigAsset(config))
        {
            ApplyPendingChanges(saveAsset: true);
            return true;
        }

        ApplyPendingChanges();
        if (CombatSandboxAuthoringAssetUtility.TryPushConfigToActiveConfig(config, out var message))
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

    private void ApplyPendingChanges(bool saveAsset = false)
    {
        serializedObject.ApplyModifiedProperties();
        EditorUtility.SetDirty(target);
        if (saveAsset)
        {
            AssetDatabase.SaveAssetIfDirty(target);
        }
    }

    private static void DrawReadOnlyBlock(string koLabel, string enLabel, string value)
    {
        EditorGUILayout.LabelField(EditorLocalizedTextResolver.Label(koLabel, enLabel), EditorStyles.miniBoldLabel);
        using (new EditorGUI.DisabledScope(true))
        {
            EditorGUILayout.TextArea(
                string.IsNullOrWhiteSpace(value)
                    ? EditorLocalizedTextResolver.Label("없음", "none")
                    : value,
                GUILayout.MinHeight(44f));
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
