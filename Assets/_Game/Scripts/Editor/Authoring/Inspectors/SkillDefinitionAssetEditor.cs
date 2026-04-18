using System.Text;
using SM.Content.Definitions;
using SM.Core.Content;
using UnityEditor;

namespace SM.Editor.Authoring.Inspectors;

[CustomEditor(typeof(SkillDefinitionAsset))]
public sealed class SkillDefinitionAssetEditor : UnityEditor.Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        var skill = (SkillDefinitionAsset)target;
        EditorGUILayout.Space(8);
        EditorGUILayout.LabelField("Derived Preview", EditorStyles.boldLabel);

        using (new EditorGUI.DisabledScope(true))
        {
            DrawCadenceSummary(skill);
            DrawScalingSummary(skill);
            DrawTargetingSummary(skill);
        }
    }

    private static void DrawCadenceSummary(SkillDefinitionAsset skill)
    {
        EditorGUILayout.LabelField("Cadence Timeline", EditorStyles.miniBoldLabel);
        var builder = new StringBuilder();

        var effectiveCooldown = skill.CooldownSeconds >= 0f ? skill.CooldownSeconds : skill.BaseCooldownSeconds;
        var effectiveRecovery = skill.RecoverySeconds >= 0f ? skill.RecoverySeconds : 0f;
        var totalCycleTime = effectiveCooldown + skill.CastWindupSeconds + effectiveRecovery;

        builder.AppendLine($"Activation: {skill.ActivationModel}  Lane: {skill.Lane}  Lock: {skill.LockRule}");
        builder.AppendLine($"Cooldown: {effectiveCooldown:0.##}s  Windup: {skill.CastWindupSeconds:0.##}s  Recovery: {effectiveRecovery:0.##}s");
        builder.AppendLine($"Total Cycle: {totalCycleTime:0.##}s");

        if (skill.ManaCost > 0f)
            builder.AppendLine($"Mana Cost: {skill.ManaCost}");
        if (skill.ResourceCost >= 0f)
            builder.AppendLine($"Resource Cost: {skill.ResourceCost}");
        if (skill.InterruptRefundScalar < 1f)
            builder.Append($"Interrupt Refund: {skill.InterruptRefundScalar:0.##}");

        EditorGUILayout.TextArea(builder.ToString().TrimEnd(), EditorStyles.helpBox);
    }

    private static void DrawScalingSummary(SkillDefinitionAsset skill)
    {
        EditorGUILayout.LabelField("Scaling", EditorStyles.miniBoldLabel);
        var builder = new StringBuilder();

        builder.AppendLine($"Kind: {skill.Kind}  Slot: {skill.SlotKind}  DamageType: {skill.DamageType}");
        builder.AppendLine($"Power: {skill.Power}  PowerFlat: {skill.PowerFlat}  Budget: {skill.PowerBudget}");

        var coeffs = new StringBuilder();
        if (skill.PhysCoeff > 0.001f) coeffs.Append($"Phys:{skill.PhysCoeff:0.##} ");
        if (skill.MagCoeff > 0.001f) coeffs.Append($"Mag:{skill.MagCoeff:0.##} ");
        if (skill.HealCoeff > 0.001f) coeffs.Append($"Heal:{skill.HealCoeff:0.##} ");
        if (skill.HealthCoeff > 0.001f) coeffs.Append($"Health:{skill.HealthCoeff:0.##} ");
        builder.AppendLine($"Coefficients: {(coeffs.Length > 0 ? coeffs.ToString().TrimEnd() : "none")}");

        builder.Append($"Crit: {(skill.CanCrit ? "yes" : "no")}  Authority: {skill.AuthorityLayer}");

        EditorGUILayout.TextArea(builder.ToString().TrimEnd(), EditorStyles.helpBox);
    }

    private static void DrawTargetingSummary(SkillDefinitionAsset skill)
    {
        EditorGUILayout.LabelField("Targeting Geometry", EditorStyles.miniBoldLabel);
        var builder = new StringBuilder();

        builder.AppendLine($"Delivery: {skill.Delivery}  TargetRule: {skill.TargetRule}");
        builder.Append($"Range: {skill.Range}");
        if (skill.RangeMin > 0f) builder.Append($"  RangeMin: {skill.RangeMin}");
        if (skill.RangeMax >= 0f) builder.Append($"  RangeMax: {skill.RangeMax}");
        builder.AppendLine();

        if (skill.Radius > 0f) builder.Append($"Radius: {skill.Radius}  ");
        if (skill.Width > 0f) builder.Append($"Width: {skill.Width}  ");
        if (skill.ArcDegrees > 0f) builder.Append($"Arc: {skill.ArcDegrees}°  ");

        var effectCount = skill.Effects?.Count ?? 0;
        var statusCount = skill.AppliedStatuses?.Count ?? 0;
        if (effectCount > 0 || statusCount > 0)
        {
            builder.AppendLine();
            builder.Append($"Effects: {effectCount}  Statuses: {statusCount}");
        }

        EditorGUILayout.TextArea(builder.ToString().TrimEnd(), EditorStyles.helpBox);
    }
}
