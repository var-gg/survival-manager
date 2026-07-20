using System;
using SM.Content.Definitions;
using SM.Meta.Model;

namespace SM.Unity.ContentConversion;

internal static class WarWoundConverter
{
    internal static WarWoundSpec Build(WarWoundBalanceDefinition definition)
    {
        if (definition == null) throw new ArgumentNullException(nameof(definition));

        return new WarWoundSpec(
            definition.WoundTriggerHpRatio,
            definition.WoundAbilityScalar,
            definition.MaxWoundsAppliedPerBattle,
            definition.MaxActiveWounds,
            definition.WoundStacksPerUnitMax,
            definition.ApplyWoundOnLoss);
    }
}
