using SM.Editor.SeedData;
using UnityEditor;
using UnityEngine;

namespace SM.Editor.Bootstrap;

public static class FirstPlayableContentBootstrap
{
    [MenuItem("SM/Bootstrap/Ensure Sample Content")]
    public static void EnsureSampleContent()
    {
        SampleSeedGenerator.EnsureCanonicalSampleContent();
        Debug.Log("SM sample content bootstrap ensured the canonical sample content root without rewriting committed authoring.");
    }

    [MenuItem("SM/Bootstrap/Ensure Sample Content", true)]
    public static bool ValidateEnsureSampleContent()
    {
        return !EditorApplication.isPlayingOrWillChangePlaymode;
    }
}
