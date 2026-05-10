using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using SM.Atlas.Model;

namespace SM.Atlas.Services;

public static class AtlasSpineProgressionService
{
    public const int BossStageIndex = 4;
    public const int ExtractStageIndex = 5;

    public static AtlasStageCandidate? FindCandidate(AtlasRegionDefinition region, string hexId)
    {
        if (region == null)
        {
            throw new ArgumentNullException(nameof(region));
        }

        return region.StageCandidates.FirstOrDefault(candidate => string.Equals(candidate.HexId, hexId, StringComparison.Ordinal));
    }

    public static bool CanEnterStoryCandidate(
        AtlasRegionDefinition region,
        string hexId,
        int siteSpineIndex,
        bool bossResolved)
    {
        var candidate = FindCandidate(region, hexId);
        if (candidate == null)
        {
            return false;
        }

        if (candidate.SiteStageIndex == BossStageIndex)
        {
            return siteSpineIndex >= 3;
        }

        if (candidate.SiteStageIndex == ExtractStageIndex)
        {
            return bossResolved;
        }

        return candidate.SiteStageIndex == siteSpineIndex + 1;
    }

    public static string ResolveLockReason(
        AtlasRegionDefinition region,
        string hexId,
        int siteSpineIndex,
        bool bossResolved)
    {
        var candidate = FindCandidate(region, hexId);
        if (candidate == null || CanEnterStoryCandidate(region, hexId, siteSpineIndex, bossResolved))
        {
            return string.Empty;
        }

        return candidate.SiteStageIndex switch
        {
            BossStageIndex => "단서 단계까지 해결해야 보스가 열립니다.",
            ExtractStageIndex => "보스를 해결해야 추출로가 열립니다.",
            _ => $"{candidate.SiteStageIndex.ToString(CultureInfo.InvariantCulture)}단계 후보는 현재 단계가 아닙니다.",
        };
    }

    public static int AdvanceSpineIndex(AtlasStageCandidate candidate, int siteSpineIndex)
    {
        if (candidate.SiteStageIndex is >= 1 and <= 3)
        {
            return Math.Max(siteSpineIndex, candidate.SiteStageIndex);
        }

        if (candidate.SiteStageIndex == BossStageIndex)
        {
            return Math.Max(siteSpineIndex, BossStageIndex);
        }

        return siteSpineIndex;
    }

    public static IReadOnlyList<AtlasStageCandidate> CurrentCandidates(AtlasRegionDefinition region, int siteSpineIndex, bool bossResolved)
    {
        var targetStage = bossResolved ? ExtractStageIndex : Math.Min(siteSpineIndex + 1, BossStageIndex);
        return region.StageCandidates
            .Where(candidate => candidate.SiteStageIndex == targetStage)
            .OrderBy(candidate => candidate.CandidateBadge, StringComparer.Ordinal)
            .ToArray();
    }

    public static IReadOnlyList<string> StageCandidatePath(
        IReadOnlyList<string> selectedStageHexes,
        AtlasStageCandidate? selectedCandidate)
    {
        var path = new List<string>(selectedStageHexes ?? Array.Empty<string>());
        if (selectedCandidate != null
            && selectedCandidate.SiteStageIndex is >= 1 and <= 3
            && !path.Contains(selectedCandidate.HexId, StringComparer.Ordinal))
        {
            path.Add(selectedCandidate.HexId);
        }

        return path;
    }
}
