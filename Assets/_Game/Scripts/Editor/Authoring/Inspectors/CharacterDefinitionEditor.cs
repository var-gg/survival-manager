using SM.Content.Definitions;
using SM.Core.Content;
using UnityEditor;
using UnityEngine;

namespace SM.Editor.Authoring.Inspectors;

[CustomEditor(typeof(CharacterDefinition))]
public sealed class CharacterDefinitionEditor : UnityEditor.Editor
{
    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        DrawProperty(nameof(CharacterDefinition.Id), "ID", "ID");
        DrawProperty(nameof(CharacterDefinition.NameKey), "이름 Key", "Name Key");
        DrawProperty(nameof(CharacterDefinition.DescriptionKey), "설명 Key", "Description Key");
        DrawProperty(nameof(CharacterDefinition.Race), "종족", "Race");
        DrawProperty(nameof(CharacterDefinition.Class), "직업", "Class");
        DrawProperty(nameof(CharacterDefinition.DefaultArchetype), "기본 전투 원형", "Default Archetype");
        DrawProperty(nameof(CharacterDefinition.DefaultRoleInstruction), "기본 역할", "Default Role");

        serializedObject.ApplyModifiedProperties();

        var character = (CharacterDefinition)target;
        EditorGUILayout.Space(8f);
        EditorGUILayout.LabelField(EditorLocalizedTextResolver.Label("Localized Preview", "Localized Preview"), EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            $"{EditorLocalizedTextResolver.LocalizeUi("ui.battle.axis.character", "캐릭터", "Character")}: {EditorLocalizedTextResolver.GetCharacterName(character, character.Id)} [{character.Id}]\n" +
            $"{EditorLocalizedTextResolver.LocalizeUi("ui.battle.axis.race", "종족", "Race")}: {EditorLocalizedTextResolver.GetRaceName(character.Race, character.Race != null ? character.Race.Id : string.Empty)}\n" +
            $"{EditorLocalizedTextResolver.LocalizeUi("ui.battle.axis.class", "직업", "Class")}: {EditorLocalizedTextResolver.GetClassName(character.Class, character.Class != null ? character.Class.Id : string.Empty)}\n" +
            $"{EditorLocalizedTextResolver.LocalizeUi("ui.battle.axis.archetype", "전투 원형", "Archetype")}: {EditorLocalizedTextResolver.GetArchetypeName(character.DefaultArchetype, character.DefaultArchetype != null ? character.DefaultArchetype.Id : string.Empty)}\n" +
            $"{EditorLocalizedTextResolver.LocalizeUi("ui.battle.axis.role", "역할", "Role")}: {EditorLocalizedTextResolver.GetRoleName(character.DefaultRoleInstruction, character.DefaultRoleInstruction != null ? character.DefaultRoleInstruction.RoleTag : string.Empty, character.DefaultRoleInstruction != null ? character.DefaultRoleInstruction.Id : string.Empty)}",
            MessageType.None);
    }

    private void DrawProperty(string propertyName, string koLabel, string enLabel)
    {
        var property = serializedObject.FindProperty(propertyName);
        if (property != null)
        {
            EditorGUILayout.PropertyField(property, new GUIContent(EditorLocalizedTextResolver.Label(koLabel, enLabel)));
        }
    }
}
