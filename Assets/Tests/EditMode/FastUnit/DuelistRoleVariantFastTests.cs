using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using SM.Combat.Model;
using SM.Core.Contracts;
using SM.Core.Stats;
using SM.Meta.Model;
using SM.Meta.Services;

namespace SM.Tests.EditMode;

[Category("FastUnit")]
public sealed class DuelistRoleVariantFastTests
{
    [Test]
    public void RoleVariant_IsDerivedFromBehaviorTags_WithOrderIndependentPriority()
    {
        var forward = Packages(
            CombatBehaviorTags.DuelistDiveCommit,
            CombatBehaviorTags.ExecuteLowHp,
            CombatBehaviorTags.DuelistHoldBruiser);
        var reverse = Packages(
            CombatBehaviorTags.DuelistHoldBruiser,
            CombatBehaviorTags.ExecuteLowHp,
            CombatBehaviorTags.DuelistDiveCommit);

        Assert.That(Resolve(forward), Is.EqualTo(RoleVariantTag.Peeler));
        Assert.That(Resolve(reverse), Is.EqualTo(RoleVariantTag.Peeler));
        Assert.That(Resolve(Packages(CombatBehaviorTags.DuelistDiveCommit, CombatBehaviorTags.ExecuteLowHp)),
            Is.EqualTo(RoleVariantTag.Executioner));
        Assert.That(Resolve(Packages(CombatBehaviorTags.DuelistDiveCommit)), Is.EqualTo(RoleVariantTag.Diver));
    }

    private static RoleVariantTag Resolve(IReadOnlyList<CombatRuleModifierPackage> packages)
    {
        var method = typeof(LoadoutCompiler).GetMethod(
            "ResolveRoleVariant",
            BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new AssertionException("ResolveRoleVariant was not found.");
        var archetype = new CombatArchetypeTemplate(
            "test_duelist",
            "Test Duelist",
            "human",
            "duelist",
            DeploymentAnchorId.FrontCenter,
            new Dictionary<StatKey, float>(),
            Array.Empty<TacticRule>(),
            Array.Empty<BattleSkillSpec>());
        var role = new SlotRoleInstruction(DeploymentAnchorId.FrontCenter, "bruiser");

        return (RoleVariantTag)(method.Invoke(null, new object[] { archetype, role, packages })
                                ?? throw new AssertionException("ResolveRoleVariant returned null."));
    }

    private static IReadOnlyList<CombatRuleModifierPackage> Packages(params string[] tags)
    {
        var result = new List<CombatRuleModifierPackage>();
        foreach (var tag in tags)
        {
            result.Add(new CombatRuleModifierPackage(
                $"test:{tag}",
                ModifierSource.Other,
                new[] { new RuleModifier(RuleModifierKind.BehaviorTag, tag) }));
        }

        return result;
    }
}
