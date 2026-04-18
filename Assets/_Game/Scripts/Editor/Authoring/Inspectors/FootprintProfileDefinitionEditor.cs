using System.Text;
using SM.Content.Definitions;
using SM.Core.Content;
using UnityEditor;

namespace SM.Editor.Authoring.Inspectors;

[CustomEditor(typeof(FootprintProfileDefinition))]
public sealed class FootprintProfileDefinitionEditor : UnityEditor.Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        var profile = (FootprintProfileDefinition)target;
        EditorGUILayout.Space(8);
        EditorGUILayout.LabelField("Spatial Preview", EditorStyles.boldLabel);

        using (new EditorGUI.DisabledScope(true))
        {
            var builder = new StringBuilder();
            builder.AppendLine($"Body: {profile.BodySizeCategory}  Head: {profile.HeadAnchorHeight}m");
            builder.AppendLine($"Navigation: {profile.NavigationRadius}  Separation: {profile.SeparationRadius}  Reach: {profile.CombatReach}");
            builder.AppendLine($"Preferred Range: {profile.PreferredRangeMin} – {profile.PreferredRangeMax}");
            builder.Append($"Engagement: {profile.EngagementSlotCount} slots @ {profile.EngagementSlotRadius}m radius");

            var reachGap = profile.CombatReach - profile.SeparationRadius;
            if (reachGap < 0.1f)
                builder.Append($"\n⚠ Reach ({profile.CombatReach}) close to separation ({profile.SeparationRadius})");

            EditorGUILayout.TextArea(builder.ToString(), EditorStyles.helpBox);
        }
    }
}
