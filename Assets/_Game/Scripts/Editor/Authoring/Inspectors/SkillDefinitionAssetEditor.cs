using System.Collections.Generic;
using System.Text;
using SM.Content.Definitions;
using SM.Core.Content;
using UnityEditor;
using UnityEngine;

namespace SM.Editor.Authoring.Inspectors;

[CustomEditor(typeof(SkillDefinitionAsset))]
public sealed class SkillDefinitionAssetEditor : UnityEditor.Editor
{
    private bool _showMechanics = true;
    private bool _showEffects = true;
    private bool _showTargeting = true;
    private bool _showTagsCompatibility = true;
    private bool _showBudget = true;
    private bool _showPresentation = true;
    private bool _showRawDiagnostics;

    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        var skill = (SkillDefinitionAsset)target;

        DrawHealthCard(skill);
        EditorGUILayout.Space(8);

        DrawFoldout(ref _showMechanics, "Mechanics", new[]
        {
            nameof(SkillDefinitionAsset.Id),
            nameof(SkillDefinitionAsset.NameKey),
            nameof(SkillDefinitionAsset.DescriptionKey),
            nameof(SkillDefinitionAsset.TemplateType),
            nameof(SkillDefinitionAsset.Kind),
            nameof(SkillDefinitionAsset.SlotKind),
            nameof(SkillDefinitionAsset.LearnSource),
            nameof(SkillDefinitionAsset.ActivationModel),
            nameof(SkillDefinitionAsset.Lane),
            nameof(SkillDefinitionAsset.LockRule),
            nameof(SkillDefinitionAsset.AuthorityLayer),
            nameof(SkillDefinitionAsset.AiIntents),
            nameof(SkillDefinitionAsset.AiScoreHints),
        });
        DrawFoldout(ref _showEffects, "Effects", new[]
        {
            nameof(SkillDefinitionAsset.Effects),
            nameof(SkillDefinitionAsset.AppliedStatuses),
            nameof(SkillDefinitionAsset.CleanseProfileId),
            nameof(SkillDefinitionAsset.DamageType),
            nameof(SkillDefinitionAsset.AreaEffectFamily),
            nameof(SkillDefinitionAsset.DisplacementKind),
            nameof(SkillDefinitionAsset.DisplacementDistance),
            nameof(SkillDefinitionAsset.SummonProfile),
            nameof(SkillDefinitionAsset.Power),
            nameof(SkillDefinitionAsset.PowerFlat),
            nameof(SkillDefinitionAsset.PhysCoeff),
            nameof(SkillDefinitionAsset.MagCoeff),
            nameof(SkillDefinitionAsset.HealCoeff),
            nameof(SkillDefinitionAsset.HealthCoeff),
            nameof(SkillDefinitionAsset.CanCrit),
        });
        DrawFoldout(ref _showTargeting, "Targeting", new[]
        {
            nameof(SkillDefinitionAsset.Delivery),
            nameof(SkillDefinitionAsset.TargetRule),
            nameof(SkillDefinitionAsset.TargetRuleData),
            nameof(SkillDefinitionAsset.Range),
            nameof(SkillDefinitionAsset.RangeMin),
            nameof(SkillDefinitionAsset.RangeMax),
            nameof(SkillDefinitionAsset.Radius),
            nameof(SkillDefinitionAsset.Width),
            nameof(SkillDefinitionAsset.ArcDegrees),
            nameof(SkillDefinitionAsset.PunishCluster),
        });
        DrawFoldout(ref _showTagsCompatibility, "Tags / Compatibility", new[]
        {
            nameof(SkillDefinitionAsset.CompileTags),
            nameof(SkillDefinitionAsset.RuleModifierTags),
            nameof(SkillDefinitionAsset.SupportAllowedTags),
            nameof(SkillDefinitionAsset.SupportBlockedTags),
            nameof(SkillDefinitionAsset.RequiredWeaponTags),
            nameof(SkillDefinitionAsset.RequiredClassTags),
            nameof(SkillDefinitionAsset.RecruitNativeTags),
            nameof(SkillDefinitionAsset.RecruitPlanTags),
            nameof(SkillDefinitionAsset.RecruitScoutTags),
            nameof(SkillDefinitionAsset.EffectFamilyId),
            nameof(SkillDefinitionAsset.MutuallyExclusiveGroupId),
        });
        DrawFoldout(ref _showBudget, "Budget", new[]
        {
            nameof(SkillDefinitionAsset.BudgetCard),
            nameof(SkillDefinitionAsset.PowerBudget),
            nameof(SkillDefinitionAsset.ManaCost),
            nameof(SkillDefinitionAsset.ResourceCost),
            nameof(SkillDefinitionAsset.BaseCooldownSeconds),
            nameof(SkillDefinitionAsset.StartsOnCooldown),
            nameof(SkillDefinitionAsset.OpeningLockSeconds),
            nameof(SkillDefinitionAsset.CooldownSeconds),
            nameof(SkillDefinitionAsset.CastWindupSeconds),
            nameof(SkillDefinitionAsset.RecoverySeconds),
            nameof(SkillDefinitionAsset.InterruptRefundScalar),
        });
        DrawFoldout(ref _showPresentation, "Presentation", new[]
        {
            nameof(SkillDefinitionAsset.IconId),
            nameof(SkillDefinitionAsset.AnimationHookId),
            nameof(SkillDefinitionAsset.VfxHookId),
            nameof(SkillDefinitionAsset.SfxHookId),
        });

        EditorGUILayout.Space(8);
        _showRawDiagnostics = EditorGUILayout.Foldout(_showRawDiagnostics, "Derived Diagnostics", true);
        if (_showRawDiagnostics)
        {
            using (new EditorGUI.DisabledScope(true))
            {
                DrawCadenceSummary(skill);
                DrawScalingSummary(skill);
                DrawTargetingSummary(skill);
            }
        }

        serializedObject.ApplyModifiedProperties();
    }

    private void DrawFoldout(ref bool isExpanded, string label, IReadOnlyList<string> propertyNames)
    {
        isExpanded = EditorGUILayout.Foldout(isExpanded, label, true);
        if (!isExpanded)
        {
            return;
        }

        EditorGUI.indentLevel++;
        foreach (var propertyName in propertyNames)
        {
            var property = serializedObject.FindProperty(propertyName);
            if (property == null)
            {
                EditorGUILayout.HelpBox($"Missing serialized property: {propertyName}", MessageType.Warning);
                continue;
            }

            EditorGUILayout.PropertyField(property, true);
        }

        EditorGUI.indentLevel--;
        EditorGUILayout.Space(4);
    }

    private static void DrawHealthCard(SkillDefinitionAsset skill)
    {
        var snapshot = SkillDefinitionInspectorHealthResolver.Resolve(skill);
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.LabelField("Authoring Health", EditorStyles.boldLabel);

        EditorGUILayout.BeginHorizontal();
        DrawIconPreview(snapshot);
        EditorGUILayout.BeginVertical();
        EditorGUILayout.LabelField(snapshot.LocalizedName, EditorStyles.boldLabel);
        DrawReadOnlyRow("SkillId", skill.Id);
        DrawReadOnlyRow("Slot / Kind", $"{skill.SlotKind} / {skill.Kind}");
        DrawReadOnlyRow("Live Slice", snapshot.SliceExposure);
        DrawReadOnlyRow("Localized Name", snapshot.NameFallbackState);
        DrawReadOnlyRow("Icon", snapshot.IconState);
        EditorGUILayout.EndVertical();
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.HelpBox(
            $"{snapshot.Health}: template={skill.TemplateType}, effects={snapshot.EffectsState}, localization={snapshot.LocalizationState}, tags={snapshot.CompileTagsState}",
            ToMessageType(snapshot.Health));

        foreach (var finding in snapshot.Findings)
        {
            EditorGUILayout.HelpBox($"{finding.Label}: {finding.Detail}", ToMessageType(finding.Level));
        }

        if (snapshot.Findings.Count == 0)
        {
            EditorGUILayout.HelpBox("No red/yellow authoring health findings.", MessageType.Info);
        }

        DrawReadOnlyRow("Support compatibility", snapshot.SupportCompatibilityState);
        DrawReadOnlyRow("Required weapons", snapshot.RequiredWeaponTagsState);
        DrawReadOnlyRow("Required classes", snapshot.RequiredClassTagsState);
        if (!string.IsNullOrWhiteSpace(snapshot.IconAssetPath))
        {
            DrawReadOnlyRow("Icon asset", snapshot.IconAssetPath);
        }

        EditorGUILayout.EndVertical();
    }

    private static void DrawIconPreview(SkillDefinitionInspectorHealthSnapshot snapshot)
    {
        const float iconSize = 64f;
        var rect = GUILayoutUtility.GetRect(iconSize, iconSize, GUILayout.Width(iconSize), GUILayout.Height(iconSize));
        GUI.Box(rect, GUIContent.none);
        if (snapshot.IconTexture == null)
        {
            GUI.Label(rect, "No Icon", EditorStyles.centeredGreyMiniLabel);
            return;
        }

        GUI.DrawTexture(rect, snapshot.IconTexture, ScaleMode.ScaleToFit, true);
    }

    private static void DrawReadOnlyRow(string label, string value)
    {
        using (new EditorGUI.DisabledScope(true))
        {
            EditorGUILayout.TextField(label, string.IsNullOrWhiteSpace(value) ? "-" : value);
        }
    }

    private static MessageType ToMessageType(SkillInspectorHealthLevel level)
    {
        return level switch
        {
            SkillInspectorHealthLevel.Red => MessageType.Error,
            SkillInspectorHealthLevel.Yellow => MessageType.Warning,
            _ => MessageType.Info,
        };
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
        if (skill.StartsOnCooldown)
            builder.AppendLine($"Opening Lock: {skill.OpeningLockSeconds:0.##}s");
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
