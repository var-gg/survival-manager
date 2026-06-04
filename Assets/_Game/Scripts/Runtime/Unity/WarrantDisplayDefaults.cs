using System;
using System.Collections.Generic;
using SM.Meta;

namespace SM.Unity;

/// <summary>
/// 정치 세력/서약의 한국어 표시명 기본값(label layer). ID/label 분리 — 로직(SM.Meta `WarrantCatalog`·
/// `WarrantOptionBuilder` 등)은 lore-free이고 표시명은 여기 격리된다. 정식 다국어는 localization
/// StringTable(`Content_Factions`/`Content_Warrants`)로 가고, 그 entry가 비면 이 기본값이 fallback이 된다.
/// 출처: 세력명은 reskin settled(analysis-narrative-reskin-4-faction-root-draft), warrant/kind 라벨은 authoring.
/// </summary>
public static class WarrantDisplayDefaults
{
    private static readonly IReadOnlyDictionary<string, string> FactionNames = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        [WarrantCatalog.SolarumId] = "솔라룸",
        [WarrantCatalog.WolfpineId] = "이리솔 부족",
        [WarrantCatalog.PaleConclaveId] = "회상 결사",
        [WarrantCatalog.LatticeId] = "그물 결사",
    };

    private static readonly IReadOnlyDictionary<string, string> WarrantNames = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        [WarrantCatalog.SolarumOrderId] = "솔라룸의 질서",
        [WarrantCatalog.WolfpineHuntId] = "이리솔의 사냥",
        [WarrantCatalog.PaleConclaveVigilId] = "회상의 불침번",
        [WarrantCatalog.LatticePrecisionId] = "그물의 정밀",
    };

    /// <summary>세력 표시명(미등록이면 ""). 호출자는 빈 값이면 id로 폴백.</summary>
    public static string FactionName(string factionId) =>
        !string.IsNullOrEmpty(factionId) && FactionNames.TryGetValue(factionId, out var name) ? name : string.Empty;

    /// <summary>서약 표시명(미등록이면 "").</summary>
    public static string WarrantName(string warrantId) =>
        !string.IsNullOrEmpty(warrantId) && WarrantNames.TryGetValue(warrantId, out var name) ? name : string.Empty;

    /// <summary>세력 기준 종류 표시명 — Swift=속전, Intact=온전.</summary>
    public static string KindName(WarrantKind kind) => kind switch
    {
        WarrantKind.Swift => "속전",
        WarrantKind.Intact => "온전",
        _ => string.Empty,
    };
}
