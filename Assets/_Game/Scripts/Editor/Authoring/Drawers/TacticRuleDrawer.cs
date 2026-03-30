using SM.Content.Definitions;
using UnityEditor;
using UnityEngine;

namespace SM.Editor.Authoring.Drawers;

[CustomPropertyDrawer(typeof(TacticPresetEntry))]
public sealed class TacticRuleDrawer : PropertyDrawer
{
    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        EditorGUI.BeginProperty(position, label, property);
        var lineHeight = EditorGUIUtility.singleLineHeight;
        var spacing = EditorGUIUtility.standardVerticalSpacing;
        var rowA = new Rect(position.x, position.y, position.width, lineHeight);
        var rowB = new Rect(position.x, position.y + lineHeight + spacing, position.width, lineHeight);

        var priority = property.FindPropertyRelative(nameof(TacticPresetEntry.Priority));
        var condition = property.FindPropertyRelative(nameof(TacticPresetEntry.ConditionType));
        var threshold = property.FindPropertyRelative(nameof(TacticPresetEntry.Threshold));
        var action = property.FindPropertyRelative(nameof(TacticPresetEntry.ActionType));
        var target = property.FindPropertyRelative(nameof(TacticPresetEntry.TargetSelector));
        var skill = property.FindPropertyRelative(nameof(TacticPresetEntry.Skill));

        var leftWidth = rowA.width * 0.2f;
        var middleWidth = rowA.width * 0.5f;
        EditorGUI.PropertyField(new Rect(rowA.x, rowA.y, leftWidth - 4f, rowA.height), priority, GUIContent.none);
        EditorGUI.PropertyField(new Rect(rowA.x + leftWidth, rowA.y, middleWidth - 4f, rowA.height), condition, GUIContent.none);
        EditorGUI.PropertyField(new Rect(rowA.x + leftWidth + middleWidth, rowA.y, rowA.width - leftWidth - middleWidth, rowA.height), threshold, GUIContent.none);

        var thirdWidth = rowB.width / 3f;
        EditorGUI.PropertyField(new Rect(rowB.x, rowB.y, thirdWidth - 4f, rowB.height), action, GUIContent.none);
        EditorGUI.PropertyField(new Rect(rowB.x + thirdWidth, rowB.y, thirdWidth - 4f, rowB.height), target, GUIContent.none);
        EditorGUI.PropertyField(new Rect(rowB.x + (thirdWidth * 2f), rowB.y, thirdWidth, rowB.height), skill, GUIContent.none);
        EditorGUI.EndProperty();
    }

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        return (EditorGUIUtility.singleLineHeight * 2f) + EditorGUIUtility.standardVerticalSpacing;
    }
}
