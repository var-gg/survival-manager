using System.Text;
using SM.Combat.Services;
using SM.Meta.Serialization;

const string goldenRelativePath = "Assets/Tests/EditMode/FastUnit/Golden/battle-hash-corpus.golden.txt";
const string snapshotRelativePath = "Assets/Resources/_Game/Content/content-snapshot.json";

if (args.Length == 0)
{
    return RunDeterminismGate();
}

if (args.Length == 1 && string.Equals(args[0], "snapshot-load", StringComparison.Ordinal))
{
    return RunSnapshotLoad();
}

if (args.Length >= 1 && string.Equals(args[0], "campaign-battle", StringComparison.Ordinal))
{
    string? unityReportPath = null;
    string? outputPath = null;
    for (var index = 1; index < args.Length; index++)
    {
        if (string.Equals(args[index], "--unity", StringComparison.Ordinal) && index + 1 < args.Length)
        {
            unityReportPath = args[++index];
        }
        else if (string.Equals(args[index], "--output", StringComparison.Ordinal) && index + 1 < args.Length)
        {
            outputPath = args[++index];
        }
        else
        {
            Console.Error.WriteLine($"Unknown campaign-battle argument: {args[index]}");
            return 2;
        }
    }

    return CampaignCellBattleRunner.Run(FindRepositoryRoot(), unityReportPath, outputPath);
}

if (args.Length >= 1 && string.Equals(args[0], "campaign-sweep", StringComparison.Ordinal))
{
    return HeadlessCampaignSweepRunner.Run(FindRepositoryRoot(), args.Skip(1).ToArray());
}

Console.Error.WriteLine(
    "Usage: HeadlessSweep [snapshot-load | campaign-battle [--unity <report>] [--output <path>] "
    + "| campaign-sweep [--cells <1-480>] [--degree <n>] [--stop-after <encounter>] "
    + "[--output <path>] [--verify]]");
return 2;

static int RunDeterminismGate()
{
    try
    {
        var repositoryRoot = FindRepositoryRoot();
        var goldenPath = Path.Combine(repositoryRoot, goldenRelativePath.Replace('/', Path.DirectorySeparatorChar));
        var golden = NormalizeLineEndings(File.ReadAllText(goldenPath));
        var net8 = NormalizeLineEndings(BattleHashCorpus.Generate());

        var goldenBytes = Encoding.UTF8.GetBytes(golden);
        var net8Bytes = Encoding.UTF8.GetBytes(net8);
        if (goldenBytes.AsSpan().SequenceEqual(net8Bytes))
        {
            Console.WriteLine($"headless-sweep MATCH ({CountLines(golden)} lines): .NET 8 == Unity golden");
            return 0;
        }

        var divergence = FindFirstDivergence(golden, net8);
        Console.Error.WriteLine("== headless-sweep DIVERGENCE ==");
        Console.Error.WriteLine($"  context : {divergence.SeedContext}");
        Console.Error.WriteLine($"  line    : {divergence.LineNumber}");
        Console.Error.WriteLine($"  golden  : {divergence.Golden}");
        Console.Error.WriteLine($"  net8    : {divergence.Net8}");
        return 1;
    }
    catch (Exception exception)
    {
        Console.Error.WriteLine($"headless-sweep ERROR: {exception.Message}");
        return 2;
    }
}

static int RunSnapshotLoad()
{
    try
    {
        var repositoryRoot = FindRepositoryRoot();
        var snapshotPath = Path.Combine(repositoryRoot, snapshotRelativePath.Replace('/', Path.DirectorySeparatorChar));
        if (!File.Exists(snapshotPath))
        {
            throw new FileNotFoundException(
                $"Content snapshot not found. Export it with SM.Editor.Validation.ContentSnapshotExporter.ExportSnapshot: {snapshotPath}",
                snapshotPath);
        }

        var snapshot = ContentSnapshotJsonSerializer.Deserialize(File.ReadAllText(snapshotPath));
        var itemCatalog = snapshot.ItemCatalog?.Values ?? Array.Empty<SM.Meta.Model.ItemTemplate>();
        var affixCatalog = snapshot.AffixCatalog?.Values ?? Array.Empty<SM.Meta.Model.AffixTemplate>();
        var itemWithSlotTypeCount = itemCatalog.Count(item => !string.IsNullOrWhiteSpace(item.SlotType));
        var itemWithAllowedClassCount = itemCatalog.Count(item => item.AllowedClassIds is { Count: > 0 });
        var affixWithAllowedSlotCount = affixCatalog.Count(affix => affix.AllowedSlotTypes is { Count: > 0 });
        var affixWithBudgetCount = affixCatalog.Count(affix => affix.BudgetScore != 0f);
        var affixWithSpawnWeightCount = affixCatalog.Count(affix => affix.SpawnWeight > 0f);

        if (snapshot.Archetypes.Count == 0
            || itemWithSlotTypeCount == 0
            || itemWithAllowedClassCount == 0
            || affixWithAllowedSlotCount == 0
            || affixWithBudgetCount == 0
            || affixWithSpawnWeightCount == 0)
        {
            throw new InvalidDataException(
                "Content snapshot loaded, but one or more required headless sweep metadata fields were empty.");
        }

        Console.WriteLine(
            $"headless-snapshot LOAD archetypes={snapshot.Archetypes.Count} "
            + $"items_with_slot_type={itemWithSlotTypeCount} "
            + $"affixes_with_budget={affixWithBudgetCount}");
        return 0;
    }
    catch (Exception exception)
    {
        Console.Error.WriteLine($"headless-snapshot ERROR: {exception.Message}");
        return 2;
    }
}

static string FindRepositoryRoot()
{
    foreach (var startPath in new[] { Directory.GetCurrentDirectory(), AppContext.BaseDirectory })
    {
        for (var directory = new DirectoryInfo(startPath); directory != null; directory = directory.Parent)
        {
            var goldenPath = Path.Combine(
                directory.FullName,
                goldenRelativePath.Replace('/', Path.DirectorySeparatorChar));
            if (File.Exists(goldenPath))
            {
                return directory.FullName;
            }
        }
    }

    throw new DirectoryNotFoundException(
        $"Repository root not found; expected to locate {goldenRelativePath} above the current directory or executable.");
}

static string NormalizeLineEndings(string text) => text.Replace("\r\n", "\n", StringComparison.Ordinal);

static int CountLines(string text)
{
    if (text.Length == 0)
    {
        return 0;
    }

    var lineCount = 0;
    foreach (var character in text)
    {
        if (character == '\n')
        {
            lineCount++;
        }
    }

    return text.EndsWith('\n') ? lineCount : lineCount + 1;
}

static Divergence FindFirstDivergence(string golden, string net8)
{
    var goldenLines = golden.Split('\n');
    var net8Lines = net8.Split('\n');
    var lineCount = Math.Max(goldenLines.Length, net8Lines.Length);
    var seedContext = "(pre-seed)";

    for (var index = 0; index < lineCount; index++)
    {
        var goldenLine = index < goldenLines.Length ? goldenLines[index] : "(line missing)";
        var net8Line = index < net8Lines.Length ? net8Lines[index] : "(line missing)";
        if (goldenLine.StartsWith("seed=", StringComparison.Ordinal))
        {
            seedContext = goldenLine;
        }

        if (!string.Equals(goldenLine, net8Line, StringComparison.Ordinal))
        {
            return new Divergence(seedContext, index + 1, goldenLine, net8Line);
        }
    }

    throw new InvalidOperationException("Corpus bytes differed, but no divergent line was found.");
}

internal readonly record struct Divergence(string SeedContext, int LineNumber, string Golden, string Net8);
