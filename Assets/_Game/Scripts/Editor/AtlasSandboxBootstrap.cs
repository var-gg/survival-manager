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

    /// <summary>
    /// Edit-time 비주얼 셋업 — Play 진입 없이 Atlas.unity를 열고 graybox 자산을 보장한다.
    /// hex grid / leyline / sigil 머티리얼 + 카메라 framing을 Scene 뷰에서 직접 튠할 때 사용.
    /// </summary>
    [MenuItem("SM/Atlas 미리보기 셋업", false, 7)]
    public static void SetupAtlasPreview()
    {
        try
        {
            AtlasGrayboxAuthoringAssetUtility.EnsureAtlasScene();
            EditorSceneManager.OpenScene(AtlasScenePath, OpenSceneMode.Single);
            Debug.Log("[AtlasGraybox] Edit-time 미리보기 셋업 완료. Scene 뷰에서 hex/leyline/sigil 검토하세요.");
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[AtlasGraybox] Preview setup failed: {ex.Message}\n{ex}");
            throw;
        }
    }
}
