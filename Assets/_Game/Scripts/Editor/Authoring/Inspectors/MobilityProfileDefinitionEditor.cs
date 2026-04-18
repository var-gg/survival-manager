using System.Text;
using SM.Content.Definitions;
using SM.Core.Content;
using UnityEditor;

namespace SM.Editor.Authoring.Inspectors;

[CustomEditor(typeof(MobilityProfileDefinition))]
public sealed class MobilityProfileDefinitionEditor : UnityEditor.Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        var profile = (MobilityProfileDefinition)target;
        EditorGUILayout.Space(8);
        EditorGUILayout.LabelField("Mobility Preview", EditorStyles.boldLabel);

        using (new EditorGUI.DisabledScope(true))
        {
            var builder = new StringBuilder();
            builder.AppendLine($"Style: {profile.Style}  Purpose: {profile.Purpose}");
            builder.AppendLine($"Distance: {profile.Distance}m  Lateral Bias: {profile.LateralBias}");
            builder.AppendLine($"Cooldown: {profile.Cooldown}s  Cast: {profile.CastTime}s  Recovery: {profile.Recovery}s");

            var totalTime = profile.CastTime + profile.Recovery;
            builder.AppendLine($"Total Commit: {totalTime:0.##}s");

            if (profile.TriggerMinDistance > 0f || profile.TriggerMaxDistance > 0f)
                builder.Append($"Trigger Range: {profile.TriggerMinDistance} – {profile.TriggerMaxDistance}m");

            EditorGUILayout.TextArea(builder.ToString().TrimEnd(), EditorStyles.helpBox);
        }
    }
}
