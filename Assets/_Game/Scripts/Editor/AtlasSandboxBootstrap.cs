using SM.Editor.Authoring.Atlas;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace SM.Editor;

public static class AtlasSandboxBootstrap
{
    private const string AtlasScenePath = "Assets/_Game/Scenes/Atlas.unity";
    private const string AtlasLegacyScenePath = "Assets/_Game/Scenes/AtlasLegacy19.unity";

    [MenuItem("SM/Atlas테스트", false, 4)]
    public static void PlayAtlasGraybox()
    {
        PlayAtlasGraybox(legacy: false);
    }

    [MenuItem("SM/Atlas테스트 (V1 19hex)", false, 5)]
    public static void PlayAtlasLegacyGraybox()
    {
        PlayAtlasGraybox(legacy: true);
    }

    private static void PlayAtlasGraybox(bool legacy)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.LogWarning("[AtlasGraybox] 이미 Play 중입니다.");
            return;
        }

        try
        {
            if (legacy)
            {
                AtlasGrayboxAuthoringAssetUtility.EnsureLegacyAtlasScene();
                EditorSceneManager.OpenScene(AtlasLegacyScenePath, OpenSceneMode.Single);
            }
            else
            {
                AtlasGrayboxAuthoringAssetUtility.EnsureAtlasScene();
                EditorSceneManager.OpenScene(AtlasScenePath, OpenSceneMode.Single);
            }

            EditorApplication.EnterPlaymode();
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[AtlasGraybox] Failed: {ex.Message}\n{ex}");
            throw;
        }
    }
}
