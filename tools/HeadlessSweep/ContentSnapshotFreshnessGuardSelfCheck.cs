internal static class ContentSnapshotFreshnessGuardSelfCheck
{
    internal static int Run()
    {
        var root = Path.Combine(Path.GetTempPath(), $"sm-snapshot-freshness-{Guid.NewGuid():N}");
        try
        {
            var definitionsPath = Path.Combine(root, "Assets", "Resources", "_Game", "Content", "Definitions");
            var snapshotPath = Path.Combine(root, "Assets", "Resources", "_Game", "Content", "content-snapshot.json");
            Directory.CreateDirectory(definitionsPath);
            Directory.CreateDirectory(Path.GetDirectoryName(snapshotPath)!);

            var definitionPath = Path.Combine(definitionsPath, "probe.asset");
            File.WriteAllText(definitionPath, "definition");
            File.WriteAllText(snapshotPath, "snapshot");

            var now = DateTime.UtcNow;
            File.SetLastWriteTimeUtc(definitionPath, now.AddMinutes(-2));
            File.SetLastWriteTimeUtc(snapshotPath, now.AddMinutes(-1));
            ContentSnapshotFreshnessGuard.EnsureFresh(root);

            File.SetLastWriteTimeUtc(definitionPath, now);
            try
            {
                ContentSnapshotFreshnessGuard.EnsureFresh(root);
                throw new InvalidOperationException("Stale snapshot unexpectedly passed the freshness guard.");
            }
            catch (InvalidDataException exception)
                when (exception.Message.Contains("Content snapshot is stale", StringComparison.Ordinal)
                      && exception.Message.Contains("Re-export", StringComparison.Ordinal))
            {
            }

            Console.WriteLine("content-snapshot-freshness SELF-CHECK fresh=PASS stale=PASS");
            return 0;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine($"content-snapshot-freshness SELF-CHECK ERROR: {exception}");
            return 2;
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }
}
