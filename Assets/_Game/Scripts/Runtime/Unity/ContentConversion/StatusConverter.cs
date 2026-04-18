using System;
using System.Linq;
using SM.Content.Definitions;
using SM.Core.Content;
using SM.Core.Contracts;
using SM.Meta.Model;
using static SM.Unity.ContentConversion.ContentConversionShared;

namespace SM.Unity.ContentConversion;

internal static class StatusConverter
{
    internal static StatusFamilyTemplate BuildStatusFamilyTemplate(StatusFamilyDefinition definition)
    {
        return new StatusFamilyTemplate(
            definition.Id,
            definition.Group,
            definition.IsHardControl,
            definition.UsesControlDiminishing,
            definition.AffectedByTenacity,
            definition.TenacityScale,
            definition.IsRuleModifierOnly,
            Enumerate(definition.CompileTags)
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .Distinct(StringComparer.Ordinal)
                .ToList(),
            BuildGovernanceSummary(definition.BudgetCard));
    }

    internal static CleanseProfileTemplate BuildCleanseProfileTemplate(CleanseProfileDefinition definition)
    {
        return new CleanseProfileTemplate(
            definition.Id,
            Enumerate(definition.RemovesStatusIds)
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .Distinct(StringComparer.Ordinal)
                .ToList(),
            definition.RemovesOneHardControl,
            definition.GrantsUnstoppable,
            definition.GrantedUnstoppableDurationSeconds);
    }

    internal static ControlDiminishingTemplate BuildControlDiminishingTemplate(ControlDiminishingRuleDefinition definition)
    {
        return new ControlDiminishingTemplate(
            definition.Id,
            definition.ControlResistMultiplier,
            definition.WindowSeconds,
            Enumerate(definition.FullTenacityStatusIds)
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .Distinct(StringComparer.Ordinal)
                .ToList(),
            Enumerate(definition.PartialTenacityStatusIds)
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .Distinct(StringComparer.Ordinal)
                .ToList());
    }
}
