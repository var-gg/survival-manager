using SM.Unity;
using UnityEditor;
using UnityEngine;

namespace SM.Editor.Authoring;

/// <summary>
/// 프로덕션 BattleVfxCatalog.asset 빌더 — Epic Toon FX baseline 테이블(에디터 폴백과 공유,
/// BattleVfxCatalog.PopulateEpicToonFxBaseline)로 Resources asset을 생성/갱신한다.
/// 이 asset이 없으면 전투 VFX 전량이 에디터 전용 폴백 코드에 의존해 빌드에서 이펙트가 0이 된다.
/// SetEntry가 (cue, semantic, family, skin) 키로 upsert하므로 재실행은 멱등이다.
/// </summary>
public static class BattleVfxCatalogBuilder
{
    private const string AssetPath = "Assets/Resources/_Game/Battle/BattleVfxCatalog.asset";

    [MenuItem("SM/Internal/Content/Build Battle VFX Catalog")]
    public static void Build()
    {
        var catalog = AssetDatabase.LoadAssetAtPath<BattleVfxCatalog>(AssetPath);
        if (catalog == null)
        {
            catalog = ScriptableObject.CreateInstance<BattleVfxCatalog>();
            AssetDatabase.CreateAsset(catalog, AssetPath);
        }

        BattleVfxCatalog.PopulateEpicToonFxBaseline(catalog);
        EditorUtility.SetDirty(catalog);
        AssetDatabase.SaveAssets();
        Debug.Log($"[BattleVfxCatalogBuilder] 프로덕션 VFX 카탈로그 갱신: {AssetPath}");
    }
}
