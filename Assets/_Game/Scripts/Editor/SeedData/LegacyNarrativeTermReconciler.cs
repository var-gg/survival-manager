using UnityEditor;
using UnityEditor.Localization;
using UnityEngine;
using UnityEngine.Localization.Tables;

namespace SM.Editor.SeedData
{
    /// <summary>
    /// ADR-0024 인간 중심 reskin + 챕터/사이트 재정렬 이후 ko StringTable 표시값이 동기화되지 않아
    /// reskin-이전 지명이 잔존한다(en locale·content id·seed 소스는 갱신됨). 이 도구는 ko 표시값을
    /// 현 narrative SoT(pindoc)로 value 기반 외과 교체한다 — 특정 entry만 손대고 다른 콘텐츠/재생성은 건드리지 않음.
    ///
    /// 범위 한정: SoT가 명확히 확정한 지명만. 모호하거나(변경=frontier/change 중의) 정확한 SoT 표시명 확인이
    /// 필요한 종족/시너지 라벨, narrative 판단이 필요한 산문 flavor(변이/잠식 등)는 제외 — 별도 검토 후 추가.
    /// </summary>
    public static class LegacyNarrativeTermReconciler
    {
        private static readonly (string Stale, string Sot)[] KoReplacements =
        {
            ("침몰 보루", "가라앉은 보루"),
            ("폐허 묘실", "무너진 묘역"),
            ("유리섬", "유리의 숲"),
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

            AssetDatabase.SaveAssets();
            Debug.Log($"[LegacyReconcile] 완료 — ko entry {changed}건 정정.");
        }
    }
}
