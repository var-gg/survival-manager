using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;
using SM.Core.Content;
using SM.Meta.Model;
using SM.Meta.Serialization;
using SM.Meta.Services;

internal static class SealDeterminismProbe
{
    private const string SnapshotRelativePath =
        "Assets/Resources/_Game/Content/content-snapshot.json";
    private const ulong StableCommandSeed = 0x5EA1C0DEUL;
    private const int StableItemSeed = 1701;
    private const int AttemptIndex = 3;

    public static int Run(string repositoryRoot)
    {
        try
        {
            var snapshotPath = Path.Combine(
                repositoryRoot,
                SnapshotRelativePath.Replace('/', Path.DirectorySeparatorChar));
            var snapshot = ContentSnapshotJsonSerializer.Deserialize(
                File.ReadAllText(snapshotPath));
            var balance = snapshot.RefitBalance
                          ?? throw new InvalidDataException(
                              "Content snapshot has no serialized Refit balance.");
            var gradeStepBudgetScore = snapshot.DropTables?.Values
                .Where(table => table.GradeStepBudgetScore > 0f)
                .OrderBy(table => table.Id, StringComparer.Ordinal)
                .Select(table => table.GradeStepBudgetScore)
                .FirstOrDefault() ?? 0f;
            var affixCatalog = snapshot.AffixCatalog
                               ?? throw new InvalidDataException(
                                   "Content snapshot has no affix catalog.");
            var lookup = new SnapshotSessionContentLookup(snapshot);
            var service = new RefitService(lookup, balance);
            var economy = ResolveChapterEconomy(snapshot);

            foreach (var itemId in lookup.GetCanonicalItemIds()
                         .OrderBy(id => id, StringComparer.Ordinal))
            {
                foreach (var grade in Enum.GetValues<ItemRarityTierValue>())
                {
                    var affixIds = GeneratedItemAffixSelector.Select(
                        lookup,
                        itemId,
                        StableItemSeed,
                        grade,
                        gradeStepBudgetScore);
                    if (affixIds.Count < 2)
                    {
                        continue;
                    }

                    var magnitudes = new Dictionary<string, float>(StringComparer.Ordinal);
                    for (var index = 0; index < affixIds.Count; index++)
                    {
                        var affix = affixCatalog[affixIds[index]];
                        magnitudes.Add(
                            affix.Id,
                            AffixMagnitudeRoller.Roll(
                                StableItemSeed,
                                affix.Id,
                                index,
                                affix.ValueMin,
                                affix.ValueMax));
                    }

                    var lockedId = affixIds[0];
                    var state = new RefitItemState(
                        itemId,
                        "seal-cross-process-item-0",
                        grade,
                        affixIds,
                        magnitudes,
                        RefitLevel: 0);
                    var result = service.SealNextEffective(
                        state,
                        economy,
                        new[] { lockedId },
                        AttemptIndex,
                        StableCommandSeed);
                    if (!result.Applied)
                    {
                        continue;
                    }

                    var beforeBits = BitConverter.SingleToInt32Bits(magnitudes[lockedId]);
                    var afterBits = BitConverter.SingleToInt32Bits(
                        result.AffixMagnitudes[lockedId]);
                    var resultBits = result.AffixIds
                        .Select(id => BitConverter.SingleToInt32Bits(
                            result.AffixMagnitudes[id]))
                        .ToArray();
                    var output = new SealDeterminismOutput(
                        itemId,
                        grade.ToString(),
                        AttemptIndex,
                        StableCommandSeed,
                        new[] { lockedId },
                        beforeBits,
                        afterBits,
                        beforeBits == afterBits,
                        result.AffixIds.Skip(1).Any(id =>
                            BitConverter.SingleToInt32Bits(result.AffixMagnitudes[id])
                            != BitConverter.SingleToInt32Bits(magnitudes[id])),
                        result.Quote.TargetRefitLevel,
                        result.Quote.EchoCost,
                        result.ResultPercentileQ64,
                        Hash(Encode(result.AffixIds, resultBits)),
                        result.AffixIds.ToArray(),
                        resultBits);
                    Console.WriteLine(JsonConvert.SerializeObject(output, Formatting.None));
                    return 0;
                }
            }

            throw new InvalidOperationException(
                "No shipped item/grade could execute a one-lock Seal.");
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine($"seal-determinism ERROR: {exception.Message}");
            return 2;
        }
    }

    private static RefitChapterEconomy ResolveChapterEconomy(
        CombatContentSnapshot snapshot)
    {
        foreach (var chapter in snapshot.CampaignChapters?.Values
                     .OrderBy(chapter => chapter.StoryOrder)
                     .ThenBy(chapter => chapter.Id, StringComparer.Ordinal)
                 ?? Enumerable.Empty<CampaignChapterTemplate>())
        {
            return new RefitChapterEconomy(
                chapter.Id,
                CampaignRecoveryRewardPolicy.ResolveFirstFarmRunEcho(
                    snapshot,
                    chapter.Id),
                CampaignRecoveryRewardPolicy.ResolveFirstFarmRunMeanGrade(
                    snapshot,
                    chapter.Id));
        }

        throw new InvalidDataException("Content snapshot has no campaign chapters.");
    }

    private static byte[] Encode(
        IReadOnlyList<string> affixIds,
        IReadOnlyList<int> magnitudeBits)
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true);
        writer.Write(affixIds.Count);
        for (var index = 0; index < affixIds.Count; index++)
        {
            writer.Write(affixIds[index]);
            writer.Write(magnitudeBits[index]);
        }

        writer.Flush();
        return stream.ToArray();
    }

    private static string Hash(byte[] bytes)
        => Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();

    private sealed record SealDeterminismOutput(
        string ItemId,
        string Grade,
        int AttemptIndex,
        ulong StableCommandSeed,
        IReadOnlyList<string> LockedAffixIds,
        int LockedBitsBefore,
        int LockedBitsAfter,
        bool LockedBitsPreserved,
        bool UnlockedMagnitudeChanged,
        int TargetLevel,
        int EchoCost,
        ulong ResultQualityQ64,
        string ResultHash,
        IReadOnlyList<string> AffixIds,
        IReadOnlyList<int> AffixMagnitudeBits);
}
