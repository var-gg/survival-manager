using UnityEngine;
using SM.Unity.Sandbox;

namespace SM.Editor.Authoring.CombatSandbox;

public sealed class CombatSandboxState : ScriptableObject
{
    public CombatSandboxConfig Config;
    public int Seed = 17;
    public int BatchCount = 1;
    public string LastCompileHash = string.Empty;
    public string LastReplayHash = string.Empty;
    [TextArea] public string LastMetricsSummary = string.Empty;
    [TextArea] public string LastValidationMessage = string.Empty;
}
