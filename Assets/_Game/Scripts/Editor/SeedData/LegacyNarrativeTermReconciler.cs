using SM.Content.Definitions;
using UnityEditor;
using UnityEditor.Localization;
using UnityEngine;
using UnityEngine.Localization.Tables;

namespace SM.Editor.SeedData
{
    /// <summary>
    /// ADR-0024 인간 중심 reskin + 챕터/사이트 재정렬 이후 ko StringTable 표시값이 동기화되지 않아
    /// reskin-이전 어휘가 잔존한다(content id·seed 소스는 갱신됨). 이 도구는 ko 표시값을 현 narrative
    /// SoT로 외과 교체한다 — 특정 entry만 손대고 다른 콘텐츠/재생성은 건드리지 않음. 두 메커니즘:
    ///
    /// 1) 지명 value 기반 교체(<see cref="KoReplacements"/>): SoT가 명확히 확정한 고유 지명만. value Contains/Replace.
    /// 2) 종족 라벨 key-targeted 교체(<see cref="KoRaceReskin"/>): Content_Races 표시값이 reskin-이전 종족
    ///    어휘(인간/수인/언데드 + "종족"/"야수형"/"불사")로 남아 SoT(wiki-combat-v1-index settled 표)와 어긋남.
    ///    "인간" 같은 단어는 한국어 도처에 substring으로 박혀 value-blanket 치환이 위험하므로, race name/desc key만
    ///    정확히 교체한다. race id는 보존(id/label 분리).
    ///
    /// 범위 밖(별도 narrative reskin task 6/8): en locale 표시명·설명(settled 영문 SoT 부재), 시너지 family
    /// 라벨(아직 propose 대기), narrative 산문 flavor(hero lore·chapter beat의 변이/잠식/야수족 등 재서술 영역),
    /// "변경↔변방"(settled artifact 간 혼용이라 blanket 치환 보류).
    /// </summary>
    public static class LegacyNarrativeTermReconciler
    {
        private static readonly (string Stale, string Sot)[] KoReplacements =
        {
            ("침몰 보루", "가라앉은 보루"),
            ("폐허 묘실", "무너진 묘역"),
            ("유리섬", "유리의 숲"),
        };

        // race id(보존) → (ko 표시명, ko 설명). SoT: faction_solarum 솔라룸 / faction_wolfpine_tribes
        // 이리솔 부족 / faction_pale_conclave 회상 결사 (wiki-combat-v1-index settled "옛 race → 한국어 표시" 표).
        private static readonly (string RaceId, string Name, string Desc)[] KoRaceReskin =
        {
            ("human", "솔라룸", "질서와 정화를 신봉하는 균형 잡힌 변경 세력."),
            ("beastkin", "이리솔 부족", "씨족 결속으로 공세를 펼치는 강인한 부족 세력."),
            ("undead", "회상 결사", "기억을 지키며 소모전에 강한 끈질긴 결사."),
        };

        [MenuItem("SM/Internal/Content/Reconcile Legacy Narrative Terms")]
        public static void Reconcile()
        {
            var collections = LocalizationEditorSettings.GetStringTableCollections();
            var changed = 0;
            foreach (var collection in collections)
            {
                if (collection.GetTable(new UnityEngine.Localization.LocaleIdentifier("ko")) is not StringTable koTable)
                {
                    continue;
                }

                var tableChanged = false;
                foreach (var entry in koTable.Values)
                {
                    if (entry == null || string.IsNullOrEmpty(entry.Value))
                    {
                        continue;
                    }

                    var updated = entry.Value;
                    foreach (var (stale, sot) in KoReplacements)
                    {
                        if (updated.Contains(stale))
                        {
                            updated = updated.Replace(stale, sot);
                        }
                    }

                    if (updated != entry.Value)
                    {
                        Debug.Log($"[LegacyReconcile] {collection.TableCollectionName} keyId={entry.KeyId}: '{entry.Value}' -> '{updated}'");
                        entry.Value = updated;
                        changed++;
                        tableChanged = true;
                    }
                }

                if (tableChanged)
                {
                    EditorUtility.SetDirty(koTable);
                    EditorUtility.SetDirty(koTable.SharedData);
                }
            }

            changed += ReconcileKoRaceLabels();

            AssetDatabase.SaveAssets();
            Debug.Log($"[LegacyReconcile] 완료 — ko entry {changed}건 정정.");
        }

        /// <summary>Content_Races ko 표시명/설명을 인간 세력 reskin SoT로 key-targeted 교체. 교체 건수 반환.</summary>
        private static int ReconcileKoRaceLabels()
        {
            var collection = LocalizationEditorSettings.GetStringTableCollection(ContentLocalizationTables.Races);
            if (collection?.GetTable(new UnityEngine.Localization.LocaleIdentifier("ko")) is not StringTable koTable)
            {
                return 0;
            }

            var changed = 0;
            foreach (var (raceId, name, desc) in KoRaceReskin)
            {
                changed += SetKoEntry(collection, koTable, ContentLocalizationTables.BuildRaceNameKey(raceId), name);
                changed += SetKoEntry(collection, koTable, ContentLocalizationTables.BuildRaceDescriptionKey(raceId), desc);
            }

            if (changed > 0)
            {
                EditorUtility.SetDirty(koTable);
                EditorUtility.SetDirty(koTable.SharedData);
            }

            return changed;
        }

        private static int SetKoEntry(StringTableCollection collection, StringTable koTable, string key, string value)
        {
            var entry = koTable.GetEntry(key);
            if (entry == null || entry.Value == value)
            {
                return 0;
            }

            Debug.Log($"[LegacyReconcile] {collection.TableCollectionName} key={key}: '{entry.Value}' -> '{value}'");
            entry.Value = value;
            return 1;
        }
    }
}
