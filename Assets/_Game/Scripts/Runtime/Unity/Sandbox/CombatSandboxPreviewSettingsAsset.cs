using UnityEngine;

namespace SM.Unity.Sandbox;

[CreateAssetMenu(menuName = "SM/Sandbox/Combat Sandbox Preview Settings", fileName = "combat_sandbox_preview_settings_")]
public sealed class CombatSandboxPreviewSettingsAsset : ScriptableObject
{
    public string SettingsId = "combat_sandbox_preview_default";
    public string DisplayName = "Combat Sandbox Preview";
    public float RangePreviewRadius = 2f;
    public float NavigationPreviewRadius = 0.5f;
    public float SeparationPreviewRadius = 0.75f;
    public float PreferredRangeMinPreview = 1f;
    public float PreferredRangeMaxPreview = 3f;
    public float EngagementSlotRadiusPreview = 1.25f;
    public int EngagementSlotCountPreview = 4;
    public float HeadAnchorHeightPreview = 2f;
    public float FrontlineGuardRadiusPreview = 2.5f;
    public float ClusterRadiusPreview = 2.5f;
}
