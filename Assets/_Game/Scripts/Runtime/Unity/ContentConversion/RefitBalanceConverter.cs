using System;
using SM.Content.Definitions;
using SM.Meta.Model;
using SM.Meta.Services;

namespace SM.Unity.ContentConversion;

internal static class RefitBalanceConverter
{
    internal static RefitBalanceTemplate Build(RefitBalanceDefinition definition)
    {
        if (definition == null)
        {
            throw new ArgumentNullException(nameof(definition));
        }

        if (definition.RulesVersion <= 0
            || string.IsNullOrWhiteSpace(definition.AffixCatalogVersion)
            || definition.CostBaseFirstFarmEchoMultiplier <= 0f
            || definition.CostGrowthPerLevel <= 1f
            || definition.GradeCostRatio <= 1f
            || definition.SealCostMultiplierPerLockedAffix <= 0f)
        {
            throw new InvalidOperationException(
                $"Refit balance '{definition.Id}' contains invalid rules, catalog, or cost knobs.");
        }

        return new RefitBalanceTemplate(
            definition.RulesVersion,
            definition.AffixCatalogVersion,
            definition.MaximumFloorNumerator,
            definition.MaximumFloorDenominator,
            definition.FloorDecayNumerator,
            definition.FloorDecayDenominator,
            RefitFloorSchedule.Generate(
                definition.MaximumFloorNumerator,
                definition.MaximumFloorDenominator,
                definition.FloorDecayNumerator,
                definition.FloorDecayDenominator),
            definition.CostBaseFirstFarmEchoMultiplier,
            definition.CostGrowthPerLevel,
            definition.GradeCostRatio,
            definition.SealCostMultiplierPerLockedAffix);
    }
}
