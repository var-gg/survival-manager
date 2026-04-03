using System.Linq;
using SM.Combat.Model;
using SM.Content.Definitions;
using SM.Core.Contracts;
using SM.Core.Stats;
using SM.Meta.Model;
using static SM.Unity.ContentConversion.ContentConversionShared;

namespace SM.Unity.ContentConversion;

internal static class ModifierPackageConverter
{
    internal static CombatModifierPackage BuildTraitPackage(TraitEntry trait)
    {
        return new CombatModifierPackage(
            trait.Id,
            ModifierSource.Trait,
            Enumerate(trait.Modifiers).Select(modifier => BuildStatModifier(modifier, ModifierSource.Trait, trait.Id)).ToList());
    }

    internal static CombatModifierPackage BuildItemPackage(ItemBaseDefinition item)
    {
        return new CombatModifierPackage(
            item.Id,
            ModifierSource.Item,
            Enumerate(item.BaseModifiers).Select(modifier => BuildStatModifier(modifier, ModifierSource.Item, item.Id)).ToList());
    }

    internal static CombatModifierPackage BuildAffixPackage(AffixDefinition affix)
    {
        return new CombatModifierPackage(
            affix.Id,
            ModifierSource.Item,
            Enumerate(affix.Modifiers).Select(modifier => BuildStatModifier(modifier, ModifierSource.Item, affix.Id)).ToList());
    }

    internal static CombatModifierPackage BuildAugmentPackage(AugmentDefinition augment)
    {
        return new CombatModifierPackage(
            augment.Id,
            ModifierSource.Augment,
            Enumerate(augment.Modifiers).Select(modifier => BuildStatModifier(modifier, ModifierSource.Augment, augment.Id)).ToList());
    }
}
