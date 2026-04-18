using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using SM.Content.Definitions;
using SM.Core.Content;
using SM.Editor.Validation;
using UnityEngine;

namespace SM.Tests.EditMode;

public sealed class ValidationAssetSelectionPolicyTests
{
    private readonly List<UnityEngine.Object> _ownedObjects = new();

    [TearDown]
    public void TearDown()
    {
        foreach (var asset in _ownedObjects.Where(asset => asset != null))
        {
            UnityEngine.Object.DestroyImmediate(asset);
        }

        _ownedObjects.Clear();
    }

    [Test]
    public void Select_KeepsTier3SynergyAndKeepsHighestCompletenessCandidate()
    {
        var policy = new ValidationAssetSelectionPolicy();
        var sharedPath = "Assets/Resources/_Game/Content/Definitions/test_skill.asset";

        var incompleteSkill = Own(ScriptableObject.CreateInstance<SkillDefinitionAsset>());
        incompleteSkill.Id = "test_skill";
        incompleteSkill.CompileTags = new List<StableTagDefinition>();

        var completeSkill = Own(ScriptableObject.CreateInstance<SkillDefinitionAsset>());
        completeSkill.Id = "test_skill";
        completeSkill.CompileTags = new List<StableTagDefinition> { Own(CreateTag("test_tag")) };

        var tierThree = Own(ScriptableObject.CreateInstance<SynergyTierDefinition>());
        tierThree.Id = "test_synergy_3";
        tierThree.Threshold = 3;

        var selected = policy.Select(new[]
        {
            new ValidationAssetDescriptor(incompleteSkill, sharedPath, ValidationAssetSourceKind.AssetDatabaseTyped, incompleteSkill.GetType()),
            new ValidationAssetDescriptor(completeSkill, sharedPath, ValidationAssetSourceKind.AssetDatabaseGeneric, completeSkill.GetType()),
            new ValidationAssetDescriptor(tierThree, "Assets/Resources/_Game/Content/Definitions/test_synergy_3.asset", ValidationAssetSourceKind.AssetDatabaseTyped, tierThree.GetType()),
        });

        Assert.That(selected.Count, Is.EqualTo(2));
        Assert.That(selected[0].Asset, Is.SameAs(completeSkill));
        Assert.That(selected.Any(descriptor => descriptor.Asset is SynergyTierDefinition), Is.True);
    }

    [Test]
    public void Select_DedupesLooseAssetsByCanonicalIdAndType()
    {
        var identityResolver = new DefaultValidationAssetIdentityResolver();
        var scorer = new DefaultValidationAssetCompletenessScorer(identityResolver);
        var policy = new ValidationAssetSelectionPolicy(
            new DefaultValidationAssetSkipPolicy(),
            new DefaultValidationAssetDeduper(scorer, identityResolver));

        var firstSkill = Own(ScriptableObject.CreateInstance<SkillDefinitionAsset>());
        firstSkill.Id = "shared_id";

        var betterSkill = Own(ScriptableObject.CreateInstance<SkillDefinitionAsset>());
        betterSkill.Id = "shared_id";
        betterSkill.CompileTags = new List<StableTagDefinition> { Own(CreateTag("complete")) };

        var sameIdDifferentType = Own(ScriptableObject.CreateInstance<AffixDefinition>());
        sameIdDifferentType.Id = "shared_id";

        var selected = policy.Select(new[]
        {
            new ValidationAssetDescriptor(firstSkill, string.Empty, ValidationAssetSourceKind.Resources, firstSkill.GetType()),
            new ValidationAssetDescriptor(betterSkill, string.Empty, ValidationAssetSourceKind.AssetDatabaseGeneric, betterSkill.GetType()),
            new ValidationAssetDescriptor(sameIdDifferentType, string.Empty, ValidationAssetSourceKind.AssetDatabaseTyped, sameIdDifferentType.GetType()),
        });

        Assert.That(selected.Count, Is.EqualTo(2));
        Assert.That(selected.Any(descriptor => ReferenceEquals(descriptor.Asset, betterSkill)), Is.True);
        Assert.That(selected.Any(descriptor => ReferenceEquals(descriptor.Asset, sameIdDifferentType)), Is.True);
    }

    [Test]
    public void IdentityResolver_ReturnsCanonicalIdForSupportedDefinitions()
    {
        var resolver = new DefaultValidationAssetIdentityResolver();
        var skill = Own(ScriptableObject.CreateInstance<SkillDefinitionAsset>());
        skill.Id = "resolver_skill";

        var passiveBoard = Own(ScriptableObject.CreateInstance<PassiveBoardDefinition>());
        passiveBoard.Id = "resolver_board";

        Assert.That(resolver.ResolveCanonicalId(skill), Is.EqualTo("resolver_skill"));
        Assert.That(resolver.ResolveCanonicalId(passiveBoard), Is.EqualTo("resolver_board"));
    }

    [Test]
    public void Select_UsesInjectedSkipPolicyOverride()
    {
        var identityResolver = new DefaultValidationAssetIdentityResolver();
        var scorer = new DefaultValidationAssetCompletenessScorer(identityResolver);
        var policy = new ValidationAssetSelectionPolicy(
            new NeverSkipPolicy(),
            new DefaultValidationAssetDeduper(scorer, identityResolver));

        var tierThree = Own(ScriptableObject.CreateInstance<SynergyTierDefinition>());
        tierThree.Id = "override_synergy_3";
        tierThree.Threshold = 3;

        var selected = policy.Select(new[]
        {
            new ValidationAssetDescriptor(tierThree, "Assets/Resources/_Game/Content/Definitions/override_synergy_3.asset", ValidationAssetSourceKind.AssetDatabaseTyped, tierThree.GetType()),
        });

        Assert.That(selected.Count, Is.EqualTo(1));
        Assert.That(selected[0].Asset, Is.SameAs(tierThree));
    }

    private StableTagDefinition CreateTag(string id)
    {
        var tag = ScriptableObject.CreateInstance<StableTagDefinition>();
        tag.Id = id;
        return tag;
    }

    private T Own<T>(T asset) where T : UnityEngine.Object
    {
        _ownedObjects.Add(asset);
        return asset;
    }

    private sealed class NeverSkipPolicy : IValidationAssetSkipPolicy
    {
        public bool ShouldSkip(ValidationAssetDescriptor descriptor)
        {
            return false;
        }
    }
}
