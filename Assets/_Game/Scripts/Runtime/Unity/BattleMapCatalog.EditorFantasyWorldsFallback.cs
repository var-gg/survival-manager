#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace SM.Unity;

public sealed partial class BattleMapCatalog
{
    private const string DefaultForestRuinsMapId = "map_001_forest_ruins";
    private const string DefaultForestRuinsMapPath = "Assets/_Game/Prefabs/Battle/Maps/BattleMap_Forest_Ruins_01.prefab";

    private static BattleMapCatalog? _editorFantasyWorldsFallbackCatalog;

    private static BattleMapCatalog? TryCreateEditorFantasyWorldsFallbackCatalog()
    {
        if (_editorFantasyWorldsFallbackCatalog != null)
        {
            return _editorFantasyWorldsFallbackCatalog;
        }

        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(DefaultForestRuinsMapPath);
        if (prefab == null)
        {
            return null;
        }

        var catalog = CreateInstance<BattleMapCatalog>();
        catalog.hideFlags = HideFlags.DontSave;
        catalog.SetDefaultMapId(DefaultForestRuinsMapId);
        catalog.SetMap(
            DefaultForestRuinsMapId,
            "늑대소나무길",
            prefab,
            Vector3.zero,
            Vector3.zero,
            Vector3.one,
            BattleMapTacticalOverlayMode.None);
        catalog.SetChapterPool("chapter_ashen_gate", new[] { DefaultForestRuinsMapId });
        catalog.SetChapterPool("chapter_sunken_bastion", new[] { DefaultForestRuinsMapId });
        catalog.SetChapterPool("chapter_ruined_crypts", new[] { DefaultForestRuinsMapId });
        catalog.SetChapterPool("chapter_glass_forest", new[] { DefaultForestRuinsMapId });
        catalog.SetChapterPool("chapter_heartforge_descent", new[] { DefaultForestRuinsMapId });

        _editorFantasyWorldsFallbackCatalog = catalog;
        return _editorFantasyWorldsFallbackCatalog;
    }
}
#endif
