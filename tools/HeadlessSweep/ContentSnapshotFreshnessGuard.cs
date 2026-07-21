internal static class ContentSnapshotFreshnessGuard
{
    private const string SnapshotRelativePath = "Assets/Resources/_Game/Content/content-snapshot.json";
    private const string DefinitionsRelativePath = "Assets/Resources/_Game/Content/Definitions";
    private const string ReExportInstruction =
        "Re-export it with Unity menu SM/Internal/Content/Export Content Snapshot, then rerun the sweep.";

    internal static void EnsureFresh(string repositoryRoot)
    {
        var snapshotPath = Resolve(repositoryRoot, SnapshotRelativePath);
        if (!File.Exists(snapshotPath))
        {
            throw new FileNotFoundException(
                $"Content snapshot is missing: {snapshotPath}. {ReExportInstruction}",
                snapshotPath);
        }

        var definitionsPath = Resolve(repositoryRoot, DefinitionsRelativePath);
        if (!Directory.Exists(definitionsPath))
        {
            throw new DirectoryNotFoundException(
                $"Content definitions directory is missing: {definitionsPath}. Snapshot freshness cannot be verified.");
        }

        var latestDefinition = Directory
            .EnumerateFiles(definitionsPath, "*", SearchOption.AllDirectories)
            .Select(path => new FileInfo(path))
            .OrderByDescending(file => file.LastWriteTimeUtc)
            .FirstOrDefault();
        if (latestDefinition is null)
        {
            throw new InvalidDataException(
                $"Content definitions directory contains no files: {definitionsPath}. Snapshot freshness cannot be verified.");
        }

        var snapshot = new FileInfo(snapshotPath);
        if (snapshot.LastWriteTimeUtc < latestDefinition.LastWriteTimeUtc)
        {
            throw new InvalidDataException(
                $"Content snapshot is stale: {snapshotPath} was written at {snapshot.LastWriteTimeUtc:O}, "
                + $"but {latestDefinition.FullName} is newer ({latestDefinition.LastWriteTimeUtc:O}). "
                + ReExportInstruction);
        }
    }

    private static string Resolve(string repositoryRoot, string relativePath)
        => Path.Combine(repositoryRoot, relativePath.Replace('/', Path.DirectorySeparatorChar));
}
