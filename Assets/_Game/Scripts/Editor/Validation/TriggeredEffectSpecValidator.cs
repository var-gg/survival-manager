using System.Collections.Generic;
using SM.Content.Definitions;
using SM.Core.Contracts;

namespace SM.Editor.Validation;

internal static class TriggeredEffectSpecValidator
{
    internal static void Validate(
        string ownerLabel,
        string codePrefix,
        IReadOnlyList<TriggeredEffectSpec>? effects,
        string assetPath,
        ICollection<ContentValidationIssue> issues)
    {
        if (effects == null)
        {
            return;
        }

        for (var index = 0; index < effects.Count; index++)
        {
            var effect = effects[index];
            if (effect == null)
            {
                ContentValidationIssueFactory.AddError(
                    issues,
                    $"{codePrefix}.trigger_null",
                    $"{ownerLabel} TriggeredEffects[{index}] is null.",
                    assetPath);
                continue;
            }

            var label = $"{ownerLabel} TriggeredEffects[{index}]";
            ContentDefinitionSchemaRuleSupport.ValidateDefinedEnum(effect.Trigger, $"{label} trigger", assetPath, issues);
            ContentDefinitionSchemaRuleSupport.ValidateDefinedEnum(effect.Op, $"{label} op", assetPath, issues);
            ContentDefinitionSchemaRuleSupport.ValidateDefinedEnum(effect.Scope, $"{label} scope", assetPath, issues);

            if (effect.Scope is not (EffectScope.Self
                or EffectScope.CurrentTarget
                or EffectScope.AlliedCombatants
                or EffectScope.EnemyCombatants))
            {
                ContentValidationIssueFactory.AddError(
                    issues,
                    $"{codePrefix}.trigger_scope_unsupported",
                    $"{label} scope '{effect.Scope}' is not handled by CombatTriggerEngine. Use Self, CurrentTarget, AlliedCombatants, or EnemyCombatants.",
                    assetPath);
            }

            switch (effect.Op)
            {
                case TriggeredEffectOp.ApplyStatus:
                    if (string.IsNullOrWhiteSpace(effect.StatusId))
                    {
                        ContentValidationIssueFactory.AddError(
                            issues,
                            $"{codePrefix}.trigger_status_id",
                            $"{label} ApplyStatus requires a non-empty StatusId.",
                            assetPath);
                    }

                    if (effect.DurationSeconds <= 0f)
                    {
                        ContentValidationIssueFactory.AddError(
                            issues,
                            $"{codePrefix}.trigger_status_duration",
                            $"{label} ApplyStatus requires DurationSeconds greater than 0.",
                            assetPath);
                    }

                    break;
                case TriggeredEffectOp.Heal:
                case TriggeredEffectOp.Barrier:
                case TriggeredEffectOp.GainEnergy:
                    if (effect.Magnitude <= 0f)
                    {
                        ContentValidationIssueFactory.AddError(
                            issues,
                            $"{codePrefix}.trigger_magnitude",
                            $"{label} {effect.Op} requires Magnitude greater than 0.",
                            assetPath);
                    }

                    break;
            }

            if (effect.Trigger == CombatTriggerKind.OnHpBelow
                && (effect.ThresholdRatio <= 0f || effect.ThresholdRatio > 1f))
            {
                ContentValidationIssueFactory.AddError(
                    issues,
                    $"{codePrefix}.trigger_threshold",
                    $"{label} OnHpBelow requires ThresholdRatio in the range (0, 1].",
                    assetPath);
            }
        }
    }
}
