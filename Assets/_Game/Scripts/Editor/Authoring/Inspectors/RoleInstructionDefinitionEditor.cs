using SM.Content.Definitions;
using SM.Core.Content;
using UnityEditor;
using UnityEngine;

namespace SM.Editor.Authoring.Inspectors;

[CustomEditor(typeof(RoleInstructionDefinition))]
public sealed class RoleInstructionDefinitionEditor : UnityEditor.Editor
{
    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        DrawProperty(nameof(RoleInstructionDefinition.Id), "ID", "ID");
        DrawProperty(nameof(RoleInstructionDefinition.NameKey), "이름 Key", "Name Key");
        DrawProperty(nameof(RoleInstructionDefinition.Anchor), "배치", "Anchor");
        DrawProperty(nameof(RoleInstructionDefinition.RoleTag), "역할 태그", "Role Tag");
        DrawProperty(nameof(RoleInstructionDefinition.ProtectCarryBias), "Carry 보호 편향", "Protect Carry Bias");
        DrawProperty(nameof(RoleInstructionDefinition.BacklinePressureBias), "후열 압박 편향", "Backline Pressure Bias");
        DrawProperty(nameof(RoleInstructionDefinition.RetreatBias), "후퇴 편향", "Retreat Bias");
        DrawProperty(nameof(RoleInstructionDefinition.CompileTags), "컴파일 태그", "Compile Tags");

        serializedObject.ApplyModifiedProperties();

        var role = (RoleInstructionDefinition)target;
        EditorGUILayout.Space(8f);
        EditorGUILayout.LabelField(EditorLocalizedTextResolver.Label("Localized Preview", "Localized Preview"), EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            $"{EditorLocalizedTextResolver.LocalizeUi("ui.battle.axis.role", "역할", "Role")}: {EditorLocalizedTextResolver.GetRoleName(role, role.RoleTag, role.Id)} [{role.Id}]\n" +
            $"{EditorLocalizedTextResolver.LocalizeUi("ui.battle.axis.anchor", "배치", "Anchor")}: {EditorLocalizedTextResolver.GetAnchorName((SM.Combat.Model.DeploymentAnchorId)(int)role.Anchor)}\n" +
            $"{EditorLocalizedTextResolver.Label("역할 태그", "Role Tag")}: {role.RoleTag}",
            MessageType.None);
    }

    private void DrawProperty(string propertyName, string koLabel, string enLabel)
    {
        var property = serializedObject.FindProperty(propertyName);
        if (property != null)
        {
            EditorGUILayout.PropertyField(property, new GUIContent(EditorLocalizedTextResolver.Label(koLabel, enLabel)), true);
        }
    }
}
