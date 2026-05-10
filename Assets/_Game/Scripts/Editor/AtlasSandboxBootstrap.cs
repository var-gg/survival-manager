using SM.Editor.Authoring.Atlas;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace SM.Editor;

public static class AtlasSandboxBootstrap
{
    private const string AtlasScenePath = "Assets/_Game/Scenes/Atlas.unity";

    [MenuItem("SM/Atlas테스트", false, 4)]
    public static void PlayAtlasGraybox()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.LogWarning("[AtlasGraybox] 이미 Play 중입니다.");
            return;
        }

        try
        {
            AtlasGrayboxAuthoringAssetUtility.EnsureAtlasScene();
            EditorSceneManager.OpenScene(AtlasScenePath, OpenSceneMode.Single);
            EditorApplication.EnterPlaymode();
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[AtlasGraybox] Failed: {ex.Message}\n{ex}");
            throw;
        }
    }
}
