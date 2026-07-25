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
    private const int StableItemSeed = 1701;

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

            var affixCatalog = snapshot.AffixCatalog
                               ?? throw new InvalidDataException(
                                   "Content snapshot has no affix catalog.");
            var chapterEconomy = ResolveChapterEconomy(snapshot);
            var lookup = new SnapshotSessionContentLookup(snapshot);
            var service = new RefitService(lookup, balance);

            foreach (var itemId in lookup.GetCanonicalItemIds()
                         .OrderBy(value => value, StringComparer.Ordinal))
            {
                foreach (var grade in Enum.GetValues<ItemRarityTierValue>())
                {
                    IReadOnlyList<string> affixIds;
                    try
                    {
                        affixIds = GeneratedItemAffixSelector.Select(
                            lookup,
                            itemId,
                            StableItemSeed,
                            grade,
                            gradeStepBudgetScore);
                    }
                    catch (Exception)
                    {
                        continue;
                    }

                    if (affixIds.Count == 0)
                    {
                        continue;
                    }

                    var magnitudes = new Dictionary<string, float>(StringComparer.Ordinal);
                    for (var index = 0; index < affixIds.Count; index++)
                    {
                        var affixId = affixIds[index];
                        if (!affixCatalog.TryGetValue(affixId, out var affix))
                        {
                            throw new InvalidDataException(
                                $"Generated affix '{affixId}' was missing from the snapshot.");
                        }

                        magnitudes.Add(
                            affixId,
                            AffixMagnitudeRoller.Roll(
                                StableItemSeed,
                                affixId,
                                index,
                                affix.ValueMin,
                                affix.ValueMax));
                    }

                    var item = new RefitItemState(
                        itemId,
                        "refit-cross-process-item-0",
                        grade,
                        affixIds,
                        magnitudes,
                        RefitLevel: 0);
                    var result = service.RefitNextEffective(
                        item,
                        chapterEconomy,
                        StableCommandSeed);
                    if (!result.Applied)
                    {
                        continue;
                    }

                    var output = new RefitDeterminismOutput(
                        itemId,
                        grade.ToString(),
                        result.Quote.TargetRefitLevel,
                        result.Quote.CurrentPercentileQ64,
                        result.Quote.TargetFloorQ64,
                        result.ResultPercentileQ64,
                        affixIds.SequenceEqual(result.AffixIds, StringComparer.Ordinal),
                        Hash(EncodeAffixIds(result.AffixIds)),
                        Hash(EncodeMagnitudes(result.AffixIds, result.AffixMagnitudes)),
                        result.AffixIds.ToArray(),
                        result.AffixIds.Select(id => result.AffixMagnitudes[id]).ToArray());
                    Console.WriteLine(JsonConvert.SerializeObject(output, Formatting.None));
                    return 0;
                }
            }

            throw new InvalidOperationException("No shipped item/grade could execute a roll-quality Refit.");
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

    private static byte[] EncodeMagnitudes(
        IReadOnlyList<string> affixIds,
        IReadOnlyDictionary<string, float> magnitudes)
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true);
        writer.Write(affixIds.Count);
        foreach (var affixId in affixIds)
        {
            writer.Write(BitConverter.SingleToInt32Bits(magnitudes[affixId]));
        }

        writer.Flush();
        return stream.ToArray();
    }

    private static string Hash(byte[] bytes)
        => Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();

    private sealed record RefitDeterminismOutput(
        string ItemId,
        string Grade,
        int TargetLevel,
        ulong CurrentQualityQ64,
        ulong TargetFloorQ64,
        ulong ResultQualityQ64,
        bool AffixIdsPreserved,
        string AffixHash,
        string PostRefitMagnitudeHash,
        IReadOnlyList<string> AffixIds,
        IReadOnlyList<float> AffixMagnitudes);
}
