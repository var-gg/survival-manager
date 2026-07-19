using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using NUnit.Framework;
using SM.Combat.Model;
using SM.Core.Contracts;
using SM.Core.Stats;
using SM.Meta.Model;
using SM.Meta.Serialization;
using SM.Tests.EditMode.Fakes;
using UnityEngine;

namespace SM.Tests.EditMode;

[Category("FastUnit")]
public sealed class SnapshotBudgetValidationTests
{
    private const string SnapshotRelativePath = "Assets/Resources/_Game/Content/content-snapshot.json";

    private static string ResolveSnapshotPath()
    {
        var projectRoot = Path.GetDirectoryName(Application.dataPath)!;
        return Path.Combine(projectRoot, SnapshotRelativePath);
    }

    [Test]
    public void SnapshotFileExists()
    {
        var path = ResolveSnapshotPath();
        if (!File.Exists(path))
        {
            Assert.Ignore($"content-snapshot.json not found at {path}. Run SM/Internal/Content/Export Content Snapshot to enable budget validation tests.");
            return;
        }

        var json = File.ReadAllText(path);
        Assert.That(json.Length, Is.GreaterThan(100), "content-snapshot.json appears to be empty or corrupt.");
    }

    [Test]
    public void SerializeDeserialize_PreservesSweepItemAndAffixMetadata()
    {
        const string itemId = "item.snapshot-roundtrip";
        const string affixId = "affix.snapshot-roundtrip";
        const string archetypeId = "archetype.snapshot-roundtrip";
        var source = EditorFreeCombatContentFixture.CreateSnapshot() with
        {
            Archetypes = new Dictionary<string, CombatArchetypeTemplate>(StringComparer.Ordinal)
            {
                [archetypeId] = new CombatArchetypeTemplate(
                    archetypeId,
                    "Snapshot Roundtrip",
                    "race:test",
                    "class:test",
                    DeploymentAnchorId.FrontCenter,
                    new Dictionary<StatKey, float> { [StatKey.MaxHealth] = 100f },
                    Array.Empty<TacticRule>(),
                    Array.Empty<BattleSkillSpec>()),
            },
            ItemCatalog = new Dictionary<string, ItemTemplate>(StringComparer.Ordinal)
            {
                [itemId] = new ItemTemplate(
                    itemId,
                    new[] { "item:test" },
                    "blade",
                    "Weapon",
                    new[] { "class:vanguard", "class:ranger" }),
            },
            AffixCatalog = new Dictionary<string, AffixTemplate>(StringComparer.Ordinal)
            {
                [affixId] = new AffixTemplate(
                    affixId,
                    new[] { "affix:test" },
                    new[] { "weapon:blade" },
                    Array.Empty<string>(),
                    null,
                    new[] { "Weapon", "Accessory" },
                    7.25f,
                    0.625f),
            },
        };

        var json = ContentSnapshotJsonSerializer.Serialize(source);
        var restored = ContentSnapshotJsonSerializer.Deserialize(json);
        var archetype = restored.Archetypes[archetypeId];
        var item = restored.ItemCatalog![itemId];
        var affix = restored.AffixCatalog![affixId];

        Assert.That(json, Does.Contain("\"slotType\""));
        Assert.That(json, Does.Contain("\"allowedClassIds\""));
        Assert.That(json, Does.Contain("\"allowedSlotTypes\""));
        Assert.That(json, Does.Contain("\"budgetScore\""));
        Assert.That(json, Does.Contain("\"spawnWeight\""));
        Assert.That(archetype.BaseStats[StatKey.MaxHealth], Is.EqualTo(100f));
        Assert.That(item.SlotType, Is.EqualTo("Weapon"));
        Assert.That(item.AllowedClassIds, Is.EqualTo(new[] { "class:vanguard", "class:ranger" }));
        Assert.That(affix.AllowedSlotTypes, Is.EqualTo(new[] { "Weapon", "Accessory" }));
        Assert.That(affix.BudgetScore, Is.EqualTo(7.25f));
        Assert.That(affix.SpawnWeight, Is.EqualTo(0.625f));
    }

    [Test]
    public void AllArchetypeBudgets_WithinTarget()
    {
        var path = ResolveSnapshotPath();
        if (!File.Exists(path))
        {
            Assert.Ignore("content-snapshot.json not exported yet. Run SM/Internal/Content/Export Content Snapshot.");
            return;
        }

        var json = File.ReadAllText(path);
        var snapshot = ContentSnapshotJsonSerializer.Deserialize(json);
        var violations = SnapshotBudgetValidator.ValidateArchetypes(snapshot);

        if (violations.Count > 0)
        {
            var sb = new StringBuilder($"{violations.Count} archetype budget violations:\n");
            foreach (var v in violations)
            {
                sb.AppendLine($"  {v.SubjectId}: score={v.AuthoredScore} target={v.Target}±{v.Tolerance}");
            }

            Assert.Fail(sb.ToString());
        }
    }

    [Test]
    public void AllAugmentBudgets_WithinTarget()
    {
        var path = ResolveSnapshotPath();
        if (!File.Exists(path))
        {
            Assert.Ignore("content-snapshot.json not exported yet. Run SM/Internal/Content/Export Content Snapshot.");
            return;
        }

        var json = File.ReadAllText(path);
        var snapshot = ContentSnapshotJsonSerializer.Deserialize(json);
        var violations = SnapshotBudgetValidator.ValidateAugments(snapshot);

        if (violations.Count > 0)
        {
            var sb = new StringBuilder($"{violations.Count} augment budget violations:\n");
            foreach (var v in violations)
            {
                sb.AppendLine($"  {v.SubjectId}: score={v.AuthoredScore} target={v.Target}±{v.Tolerance}");
            }

            Assert.Fail(sb.ToString());
        }
    }
}
