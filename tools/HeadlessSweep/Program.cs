using System.Text;
using SM.Combat.Services;

const string goldenRelativePath = "Assets/Tests/EditMode/FastUnit/Golden/battle-hash-corpus.golden.txt";

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
