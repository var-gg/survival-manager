using SM.Unity.Sandbox;
using UnityEditor;
using UnityEngine;

namespace SM.Editor.Authoring.Inspectors;

[CustomEditor(typeof(CombatSandboxConfig))]
public sealed class CombatSandboxConfigEditor : UnityEditor.Editor
{
    private SerializedProperty _id = null!;
    private SerializedProperty _displayName = null!;
    private SerializedProperty _useScenarioAuthoring = null!;
    private SerializedProperty _defaultLaneKind = null!;
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

    private void OnEnable()
    {
        _id = serializedObject.FindProperty(nameof(CombatSandboxConfig.Id));
        _displayName = serializedObject.FindProperty(nameof(CombatSandboxConfig.DisplayName));
        _useScenarioAuthoring = serializedObject.FindProperty(nameof(CombatSandboxConfig.UseScenarioAuthoring));
        _defaultLaneKind = serializedObject.FindProperty(nameof(CombatSandboxConfig.DefaultLaneKind));
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
                "이 asset은 runtime direct sandbox가 읽는 active handoff이다. 무거운 프리셋 authoring과 library 관리는 Combat Sandbox window에서 수행하고, 여기서는 sync 결과와 compatibility mirror를 확인한다.",
                "This asset is the runtime active handoff for direct Combat Sandbox play. Use the Combat Sandbox window for preset authoring and library management, and use this inspector to verify the synced handoff plus compatibility mirrors."),
            MessageType.Info);
        EditorGUILayout.PropertyField(_id, new GUIContent(EditorLocalizedTextResolver.Label("구성 ID", "Config Id")));
        EditorGUILayout.PropertyField(_displayName, new GUIContent(EditorLocalizedTextResolver.Label("표시 이름", "Display Name")));
        EditorGUILayout.PropertyField(_useScenarioAuthoring, new GUIContent(EditorLocalizedTextResolver.Label("Scenario Authoring 사용", "Use Scenario Authoring")));
        EditorGUILayout.PropertyField(_defaultLaneKind, new GUIContent(EditorLocalizedTextResolver.Label("기본 lane", "Default Lane")));
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
