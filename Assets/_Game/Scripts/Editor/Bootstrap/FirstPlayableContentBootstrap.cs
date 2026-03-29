using SM.Editor.SeedData;
using UnityEditor;
using UnityEngine;

namespace SM.Editor.Bootstrap;

public static class FirstPlayableContentBootstrap
{
    [MenuItem("SM/Bootstrap/Ensure Sample Content")]
    public static void EnsureSampleContent()
    {
        if (SampleSeedGenerator.HasCanonicalMinimumContent())
        {
            Debug.Log("SM canonical sample content already present. No changes needed.");
            return;
        }

        SampleSeedGenerator.EnsureCanonicalSampleContent();
        Debug.Log("SM sample content bootstrap bridge completed. This bridge remains temporary until block25.");
    }

    [MenuItem("SM/Bootstrap/Ensure Sample Content", true)]
    public static bool ValidateEnsureSampleContent()
    {
        return !EditorApplication.isPlayingOrWillChangePlaymode;
    }
}
