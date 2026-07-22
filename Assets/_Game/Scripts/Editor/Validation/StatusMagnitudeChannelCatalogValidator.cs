using System;
using System.Collections.Generic;
using SM.Content.Definitions;
using SM.Core.Contracts;

namespace SM.Editor.Validation;

/// <summary>
/// Fractional status channels must be authored as rates, while instant channels must clear their
/// runtime floor. Runtime safeguards are not a substitute for valid content authoring.
/// </summary>
internal sealed class StatusMagnitudeChannelCatalogValidator : ICatalogValidationRule
{
    private const float MinimumRuntimeMultiplier = 0.10f;
    private const float MinimumIncomingDamageMultiplier = 0.25f;
    private const float MinimumInstantMagnitude = 1f;

    public void Validate(CatalogValidationContext context, ICollection<ContentValidationIssue> issues)
    {
        ValidateFamilyChannels(context, issues);

        foreach (var skill in context.Skills)
        {
            var assetPath = context.GetPath(skill);
            for (var index = 0; index < skill.AppliedStatuses.Count; index++)
            {
                ValidateMagnitude(context, skill.AppliedStatuses[index], assetPath, $"SkillDefinition.AppliedStatuses[{index}]", issues);
            }

            for (var index = 0; index < skill.SupportModifier.AddedStatuses.Count; index++)
            {
                ValidateMagnitude(
                    context,
                    skill.SupportModifier.AddedStatuses[index],
                    assetPath,
                    $"SkillDefinition.SupportModifier.AddedStatuses[{index}]",
                    reportMissingReference: true,
                    issues);
            }

            for (var index = 0; index < skill.TriggeredEffects.Count; index++)
            {
                var effect = skill.TriggeredEffects[index];
                if (effect.Op == TriggeredEffectOp.ApplyStatus)
                {
                    ValidateMagnitude(
                        context,
                        effect.StatusId,
                        effect.Magnitude,
                        assetPath,
                        $"SkillDefinition.TriggeredEffects[{index}]",
                        reportMissingReference: true,
                        issues);
                }
            }
        }

        foreach (var augment in context.Catalog.OfType<AugmentDefinition>())
        {
            var assetPath = context.GetPath(augment);
            for (var index = 0; index < augment.TriggeredEffects.Count; index++)
            {
                var effect = augment.TriggeredEffects[index];
                if (effect.Op == TriggeredEffectOp.ApplyStatus)
                {
                    ValidateMagnitude(
                        context,
                        effect.StatusId,
                        effect.Magnitude,
                        assetPath,
                        $"AugmentDefinition.TriggeredEffects[{index}]",
                        reportMissingReference: true,
                        issues);
                }
            }
        }

        foreach (var overlay in context.Overlays.Values)
        {
            var assetPath = context.GetPath(overlay);
            for (var index = 0; index < overlay.AppliedStatuses.Count; index++)
            {
                ValidateMagnitude(context, overlay.AppliedStatuses[index], assetPath, $"BossOverlayDefinition.AppliedStatuses[{index}]", issues);
            }
        }
    }

    private static void ValidateFamilyChannels(CatalogValidationContext context, ICollection<ContentValidationIssue> issues)
    {
        foreach (var family in context.Statuses.Values)
        {
            var assetPath = context.GetPath(family);
            if (ConsumesApplicationMagnitude(family)
                && (!IsFinite(family.MagnitudeScale) || family.MagnitudeScale <= 0f))
            {
                ContentValidationIssueFactory.AddError(
                    issues,
                    "status.channel_scale_range",
                    $"Status family '{family.Id}' consumes magnitude and requires a finite positive MagnitudeScale.",
                    assetPath,
                    "StatusFamilyDefinition.MagnitudeScale");
            }

            if (family.GrantsGuardedDefense
                && (!IsFinite(family.IncomingDamageDelta)
                    || family.IncomingDamageDelta > 0f
                    || family.IncomingDamageDelta < -(1f - MinimumIncomingDamageMultiplier)))
            {
                ContentValidationIssueFactory.AddError(
                    issues,
                    "status.incoming_damage_delta_range",
                    $"Guarded family '{family.Id}' IncomingDamageDelta must stay within [-0.75, 0] so the 0.25 runtime multiplier clamp is not crossed.",
                    assetPath,
                    "StatusFamilyDefinition.IncomingDamageDelta");
            }
        }
    }

    private static void ValidateMagnitude(
        CatalogValidationContext context,
        StatusApplicationRule application,
        string assetPath,
        string scope,
        bool reportMissingReference,
        ICollection<ContentValidationIssue> issues)
    {
        if (application != null)
        {
            ValidateMagnitude(
                context,
                application.StatusId,
                application.Magnitude,
                assetPath,
                scope,
                reportMissingReference,
                issues);
        }
    }

    private static void ValidateMagnitude(
        CatalogValidationContext context,
        StatusApplicationRule application,
        string assetPath,
        string scope,
        ICollection<ContentValidationIssue> issues)
    {
        ValidateMagnitude(context, application, assetPath, scope, reportMissingReference: false, issues);
    }

    private static void ValidateMagnitude(
        CatalogValidationContext context,
        string statusId,
        float magnitude,
        string assetPath,
        string scope,
        bool reportMissingReference,
        ICollection<ContentValidationIssue> issues)
    {
        if (string.IsNullOrWhiteSpace(statusId) || !context.Statuses.TryGetValue(statusId, out var family))
        {
            if (reportMissingReference)
            {
                var missingStatusId = string.IsNullOrWhiteSpace(statusId) ? "<empty>" : statusId;
                ContentValidationIssueFactory.AddError(
                    issues,
                    "status.application_status_ref",
                    $"Status application references missing status '{missingStatusId}'.",
                    assetPath,
                    scope);
            }

            return;
        }

        if (!ConsumesApplicationMagnitude(family))
        {
            return;
        }

        if (!IsFinite(magnitude) || magnitude < 0f || !IsFinite(family.MagnitudeScale) || family.MagnitudeScale <= 0f)
        {
            AddMagnitudeError(issues, assetPath, scope, statusId, magnitude, "a finite non-negative magnitude and finite positive scale");
            return;
        }

        var effective = magnitude * family.MagnitudeScale;
        if (family.DampensTempo || family.ReducesHealing)
        {
            var maximumReduction = 1f - MinimumRuntimeMultiplier;
            if (effective > maximumReduction)
            {
                AddMagnitudeError(issues, assetPath, scope, statusId, magnitude, $"an effective reduction no greater than {maximumReduction:0.##}");
                return;
            }
        }

        // Incoming-damage amplification has no runtime upper clamp. This limit enforces
        // fractional rate-shaped authoring; it is not justified as a clamp boundary.
        if (family.AmplifiesIncomingDamage && effective >= 1f)
        {
            AddMagnitudeError(issues, assetPath, scope, statusId, magnitude, "a fractional incoming-damage amplification below 1.0");
            return;
        }

        if ((family.AppliesPeriodicDamage || family.GrantsBarrierOnApply) && effective < MinimumInstantMagnitude)
        {
            var channel = family.AppliesPeriodicDamage ? "periodic damage" : "instant barrier";
            AddMagnitudeError(issues, assetPath, scope, statusId, magnitude, $"effective {channel} of at least {MinimumInstantMagnitude:0.##}");
        }
    }

    private static bool ConsumesApplicationMagnitude(StatusFamilyDefinition family)
    {
        // ShredsDefense is flat armor subtraction, not a fractional channel. It is intentionally
        // out of scope for this guard and needs a separately ratified sunder authoring pass.
        return family.AmplifiesIncomingDamage
               || family.ReducesHealing
               || family.DampensTempo
               || family.AppliesPeriodicDamage
               || family.GrantsBarrierOnApply;
    }

    private static void AddMagnitudeError(
        ICollection<ContentValidationIssue> issues,
        string assetPath,
        string scope,
        string statusId,
        float magnitude,
        string expectation)
    {
        ContentValidationIssueFactory.AddError(
            issues,
            "status.channel_magnitude_range",
            $"Status '{statusId}' magnitude {magnitude:R} must represent channel semantics: expected {expectation}; runtime behavior must not be relied on to repair authored values.",
            assetPath,
            scope);
    }

    private static bool IsFinite(float value)
    {
        return !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
