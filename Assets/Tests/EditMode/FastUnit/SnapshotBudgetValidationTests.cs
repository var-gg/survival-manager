using System.IO;
using System.Text;
using NUnit.Framework;
using SM.Meta.Serialization;
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
