using SM.Unity;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SM.Editor.Bootstrap;

[InitializeOnLoad]
public static class FirstPlayableEditorSceneBinder
{
    static FirstPlayableEditorSceneBinder()
    {
        EditorSceneManager.sceneOpened -= HandleSceneOpened;
        EditorSceneManager.sceneOpened += HandleSceneOpened;
        EditorApplication.delayCall += BindActiveScene;
    }

    private static void HandleSceneOpened(Scene scene, OpenSceneMode mode)
    {
        if (Application.isPlaying)
        {
            return;
        }

        FirstPlayableRuntimeSceneBinder.EnsureSceneBindings(scene);
    }

    private static void BindActiveScene()
    {
        if (Application.isPlaying)
        {
            return;
        }

        var scene = SceneManager.GetActiveScene();
        if (scene.IsValid() && scene.isLoaded)
        {
            FirstPlayableRuntimeSceneBinder.EnsureSceneBindings(scene);
        }
    }
}
