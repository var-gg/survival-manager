using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using SM.Content.Definitions;
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
    public void Select_SkipsTier3SynergyAndKeepsHighestCompletenessCandidate()
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

        Assert.That(selected.Count, Is.EqualTo(1));
        Assert.That(selected[0].Asset, Is.SameAs(completeSkill));
        Assert.That(selected.All(descriptor => descriptor.Asset is not SynergyTierDefinition), Is.True);
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
}
