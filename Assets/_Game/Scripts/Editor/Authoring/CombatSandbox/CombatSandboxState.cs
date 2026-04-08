using UnityEngine;
using SM.Unity.Sandbox;

namespace SM.Editor.Authoring.CombatSandbox;

public sealed class CombatSandboxState : ScriptableObject
{
    public CombatSandboxConfig Config;
    public CombatSandboxScenarioAsset SelectedScenario;
    public int Seed = 0;
    public int BatchCount = 0;
    public string SearchText = string.Empty;
    public string TagFilter = string.Empty;
    public bool FavoritesOnly = false;
    public string RecentScenarioIdsCsv = string.Empty;
    [TextArea] public string ScenarioSummary = string.Empty;
    [TextArea] public string LeftTeamPreview = string.Empty;
    [TextArea] public string RightTeamPreview = string.Empty;
    [TextArea] public string PreviewBreakpointSummary = string.Empty;
    [TextArea] public string PreviewBaselineDriftSummary = string.Empty;
    [TextArea] public string PreviewMembershipWarning = string.Empty;
    public string InspectUnitId = string.Empty;
    public string LastCompileHash = string.Empty;
    public string LastReplayHash = string.Empty;
    [TextArea] public string LastBreakpointSummary = string.Empty;
    [TextArea] public string LastBaselineDriftSummary = string.Empty;
    [TextArea] public string LastMembershipWarning = string.Empty;
    [TextArea] public string LastMetricsSummary = string.Empty;
    [TextArea] public string LastCounterCoverageSummary = string.Empty;
    [TextArea] public string LastGovernanceSummary = string.Empty;
    [TextArea] public string LastReadabilitySummary = string.Empty;
    [TextArea] public string LastExplanationSummary = string.Empty;
    [TextArea] public string LastProvenanceSummary = string.Empty;
    [TextArea] public string LastValidationMessage = string.Empty;
    public string LayoutSourceLabel = string.Empty;
}
