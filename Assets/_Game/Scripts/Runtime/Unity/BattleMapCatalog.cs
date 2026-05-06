using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace SM.Unity;

public enum BattleMapTacticalOverlayMode
{
    FullArena = 0,
    ReadabilityOnly = 1,
    None = 2,
}

public readonly record struct BattleMapSelectionContext(
    string ChapterId,
    string SiteId,
    string EncounterId,
    int BattleSeed)
{
    public static BattleMapSelectionContext Empty { get; } = new(string.Empty, string.Empty, string.Empty, 0);
}

[CreateAssetMenu(menuName = "SM/Battle/Battle Map Catalog", fileName = "BattleMapCatalog")]
public sealed partial class BattleMapCatalog : ScriptableObject
{
    public const string ResourcesPath = "_Game/Battle/BattleMapCatalog";

    [SerializeField] private string defaultMapId = string.Empty;
    [SerializeField] private List<BattleMapCatalogEntry> maps = new();
    [SerializeField] private List<BattleChapterMapPoolEntry> chapterPools = new();

    public static BattleMapCatalog? ResolveRuntimeCatalog(BattleMapCatalog? configuredCatalog)
    {
        if (configuredCatalog != null)
        {
            return configuredCatalog;
        }

        var loaded = Resources.Load<BattleMapCatalog>(ResourcesPath);
        if (loaded != null && loaded.HasAnyMap())
        {
            return loaded;
        }

#if UNITY_EDITOR
        return TryCreateEditorFantasyWorldsFallbackCatalog();
#else
        return null;
#endif
    }

    public bool TrySelectMap(BattleMapSelectionContext context, out BattleMapCatalogEntry entry)
    {
        var candidates = ResolveCandidates(context.ChapterId);
        if (candidates.Count == 0)
        {
            entry = null!;
            return false;
        }

        var index = candidates.Count == 1
            ? 0
            : PositiveModulo(StableHash($"{context.ChapterId}|{context.SiteId}|{context.EncounterId}|{context.BattleSeed}"), candidates.Count);
        entry = candidates[index];
        return true;
    }

    public void SetDefaultMapId(string mapId)
    {
        defaultMapId = mapId ?? string.Empty;
    }

    public void SetMap(
        string mapId,
        string displayName,
        GameObject prefab,
        Vector3 localPosition,
        Vector3 localEulerAngles,
        Vector3 localScale,
        BattleMapTacticalOverlayMode tacticalOverlayMode)
    {
        var existing = maps.FirstOrDefault(map => string.Equals(map.MapId, mapId, StringComparison.Ordinal));
        if (existing == null)
        {
            maps.Add(new BattleMapCatalogEntry(
                mapId,
                displayName,
                prefab,
                localPosition,
                localEulerAngles,
                localScale,
                tacticalOverlayMode));
            return;
        }

        existing.Configure(displayName, prefab, localPosition, localEulerAngles, localScale, tacticalOverlayMode);
    }

    public void SetChapterPool(string chapterId, IReadOnlyList<string> mapIds)
    {
        var existing = chapterPools.FirstOrDefault(pool => string.Equals(pool.ChapterId, chapterId, StringComparison.Ordinal));
        if (existing == null)
        {
            chapterPools.Add(new BattleChapterMapPoolEntry(chapterId, mapIds));
            return;
        }

        existing.Configure(mapIds);
    }

    private IReadOnlyList<BattleMapCatalogEntry> ResolveCandidates(string chapterId)
    {
        var byId = maps
            .Where(map => map != null && map.Prefab != null && !string.IsNullOrWhiteSpace(map.MapId))
            .GroupBy(map => map.MapId, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);

        if (byId.Count == 0)
        {
            return Array.Empty<BattleMapCatalogEntry>();
        }

        var pool = chapterPools.FirstOrDefault(candidate => string.Equals(candidate.ChapterId, chapterId, StringComparison.Ordinal));
        if (pool != null)
        {
            var pooled = pool.MapIds
                .Where(mapId => byId.ContainsKey(mapId))
                .Select(mapId => byId[mapId])
                .ToList();
            if (pooled.Count > 0)
            {
                return pooled;
            }
        }

        if (!string.IsNullOrWhiteSpace(defaultMapId) && byId.TryGetValue(defaultMapId, out var defaultMap))
        {
            return new[] { defaultMap };
        }

        return byId.Values
            .OrderBy(map => map.MapId, StringComparer.Ordinal)
            .ToList();
    }

    private bool HasAnyMap()
    {
        return maps.Any(map => map != null && map.Prefab != null && !string.IsNullOrWhiteSpace(map.MapId));
    }

    private static int StableHash(string value)
    {
        unchecked
        {
            var hash = 23;
            for (var i = 0; i < value.Length; i++)
            {
                hash = hash * 31 + value[i];
            }

            return hash;
        }
    }

    private static int PositiveModulo(int value, int divisor)
    {
        var remainder = value % divisor;
        return remainder < 0 ? remainder + divisor : remainder;
    }
}

[Serializable]
public sealed class BattleMapCatalogEntry
{
    [SerializeField] private string mapId = string.Empty;
    [SerializeField] private string displayName = string.Empty;
    [SerializeField] private GameObject prefab = null!;
    [SerializeField] private Vector3 localPosition = Vector3.zero;
    [SerializeField] private Vector3 localEulerAngles = Vector3.zero;
    [SerializeField] private Vector3 localScale = Vector3.one;
    [SerializeField] private BattleMapTacticalOverlayMode tacticalOverlayMode = BattleMapTacticalOverlayMode.FullArena;

    public BattleMapCatalogEntry(
        string mapId,
        string displayName,
        GameObject prefab,
        Vector3 localPosition,
        Vector3 localEulerAngles,
        Vector3 localScale,
        BattleMapTacticalOverlayMode tacticalOverlayMode)
    {
        this.mapId = mapId;
        Configure(displayName, prefab, localPosition, localEulerAngles, localScale, tacticalOverlayMode);
    }

    public string MapId => mapId;
    public string DisplayName => displayName;
    public GameObject Prefab => prefab;
    public Vector3 LocalPosition => localPosition;
    public Vector3 LocalEulerAngles => localEulerAngles;
    public Vector3 LocalScale => localScale;
    public BattleMapTacticalOverlayMode TacticalOverlayMode => tacticalOverlayMode;

    public void Configure(
        string configuredDisplayName,
        GameObject configuredPrefab,
        Vector3 configuredLocalPosition,
        Vector3 configuredLocalEulerAngles,
        Vector3 configuredLocalScale,
        BattleMapTacticalOverlayMode configuredTacticalOverlayMode)
    {
        displayName = configuredDisplayName ?? string.Empty;
        prefab = configuredPrefab;
        localPosition = configuredLocalPosition;
        localEulerAngles = configuredLocalEulerAngles;
        localScale = configuredLocalScale == Vector3.zero ? Vector3.one : configuredLocalScale;
        tacticalOverlayMode = configuredTacticalOverlayMode;
    }
}

[Serializable]
public sealed class BattleChapterMapPoolEntry
{
    [SerializeField] private string chapterId = string.Empty;
    [SerializeField] private List<string> mapIds = new();

    public BattleChapterMapPoolEntry(string chapterId, IReadOnlyList<string> mapIds)
    {
        this.chapterId = chapterId ?? string.Empty;
        Configure(mapIds);
    }

    public string ChapterId => chapterId;
    public IReadOnlyList<string> MapIds => mapIds;

    public void Configure(IReadOnlyList<string> configuredMapIds)
    {
        mapIds = configuredMapIds?
            .Where(mapId => !string.IsNullOrWhiteSpace(mapId))
            .Distinct(StringComparer.Ordinal)
            .ToList() ?? new List<string>();
    }
}
