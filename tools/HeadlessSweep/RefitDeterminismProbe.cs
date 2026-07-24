using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;
using SM.Core.Content;
using SM.Meta.Model;
using SM.Meta.Serialization;
using SM.Meta.Services;

internal static class RefitDeterminismProbe
{
    private const string SnapshotRelativePath = "Assets/Resources/_Game/Content/content-snapshot.json";
    private const ulong StableCommandSeed = 0xA2C0FFEEUL;

    public static int Run(string repositoryRoot)
    {
        try
        {
            var snapshotPath = Path.Combine(
                repositoryRoot,
                SnapshotRelativePath.Replace('/', Path.DirectorySeparatorChar));
            var snapshot = ContentSnapshotJsonSerializer.Deserialize(File.ReadAllText(snapshotPath));
            var balance = snapshot.RefitBalance
                ?? throw new InvalidDataException("Content snapshot has no serialized Refit balance.");
            var gradeStepBudgetScore = snapshot.DropTables?.Values
                .Where(table => table.GradeStepBudgetScore > 0f)
                .OrderBy(table => table.Id, StringComparer.Ordinal)
                .Select(table => table.GradeStepBudgetScore)
                .FirstOrDefault() ?? 0f;
            if (gradeStepBudgetScore <= 0f)
            {
                throw new InvalidDataException("Content snapshot has no positive GradeStepBudgetScore.");
            }

            var chapterEconomy = ResolveChapterEconomy(snapshot);
            var lookup = new SnapshotSessionContentLookup(snapshot);
            var selector = new AffixQualityConditionedSelector();
            var compiler = new AffixQualityProfileCompiler();
            var service = new RefitService(lookup, balance, gradeStepBudgetScore);

            foreach (var itemId in lookup.GetCanonicalItemIds().OrderBy(value => value, StringComparer.Ordinal))
            {
                foreach (var grade in new[] { ItemRarityTierValue.Epic, ItemRarityTierValue.Legendary })
                {
                    AffixQualityProfile profile;
                    try
                    {
                        profile = compiler.Compile(
                            lookup,
                            itemId,
                            grade,
                            gradeStepBudgetScore,
                            balance.AffixCatalogVersion,
                            out _);
                    }
                    catch (Exception)
                    {
                        continue;
                    }

                    if (profile.SupportScoreQ.Count < 2)
                    {
                        continue;
                    }

                    var initialAffixes = selector.SelectBudgetWeightedConditioned(
                        profile,
                        profile.SupportScoreQ[0],
                        seed: 1701);
                    var item = new RefitItemState(
                        itemId,
                        "refit-cross-process-item-0",
                        grade,
                        initialAffixes,
                        RefitLevel: 0);
                    var result = service.RefitNextEffective(item, chapterEconomy, StableCommandSeed);
                    if (!result.Applied)
                    {
                        continue;
                    }

                    var affixBytes = EncodeAffixIds(result.AffixIds);
                    var output = new RefitDeterminismOutput(
                        itemId,
                        grade.ToString(),
                        result.Quote.TargetRefitLevel,
                        result.Quote.TargetScoreQ,
                        Convert.ToHexString(SHA256.HashData(affixBytes)).ToLowerInvariant(),
                        result.AffixIds.ToArray());
                    Console.WriteLine(JsonConvert.SerializeObject(output, Formatting.None));
                    return 0;
                }
            }

            throw new InvalidOperationException("No shipped item/grade profile could execute a Refit.");
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine($"refit-determinism ERROR: {exception.Message}");
            return 2;
        }
    }

    private static RefitChapterEconomy ResolveChapterEconomy(CombatContentSnapshot snapshot)
    {
        var chapters = snapshot.CampaignChapters?.Values
            .OrderBy(chapter => chapter.StoryOrder)
            .ThenBy(chapter => chapter.Id, StringComparer.Ordinal)
            ?? Enumerable.Empty<CampaignChapterTemplate>();
        RefitChapterEconomy? selected = null;
        foreach (var chapter in chapters)
        {
            var firstFarmEcho = CampaignRecoveryRewardPolicy.ResolveFirstFarmRunEcho(snapshot, chapter.Id);
            var meanGrade = CampaignRecoveryRewardPolicy.ResolveFirstFarmRunMeanGrade(snapshot, chapter.Id);
            if (firstFarmEcho <= 0 || !double.IsFinite(meanGrade))
            {
                throw new InvalidDataException(
                    $"Campaign chapter '{chapter.Id}' has no derivable first-farm Refit economy.");
            }

            selected ??= new RefitChapterEconomy(chapter.Id, firstFarmEcho, meanGrade);
        }

        return selected
               ?? throw new InvalidDataException("Content snapshot has no campaign chapters.");
    }

    private static byte[] EncodeAffixIds(IReadOnlyList<string> affixIds)
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true);
        writer.Write(affixIds.Count);
        foreach (var affixId in affixIds)
        {
            var bytes = Encoding.UTF8.GetBytes(affixId);
            writer.Write(bytes.Length);
            writer.Write(bytes);
        }

        writer.Flush();
        return stream.ToArray();
    }

    private sealed record RefitDeterminismOutput(
        string ItemId,
        string Grade,
        int TargetLevel,
        int ScoreQ,
        string AffixHash,
        IReadOnlyList<string> AffixIds);
}
