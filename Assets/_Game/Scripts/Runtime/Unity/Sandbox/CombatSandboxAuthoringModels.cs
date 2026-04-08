using System;
using System.Collections.Generic;
using SM.Combat.Model;
using UnityEngine;

namespace SM.Unity.Sandbox;

public enum CombatSandboxLaneKind
{
    None = 0,
    DirectCombatSandbox = 1,
    TownIntegrationSmoke = 2,
}

public enum SandboxLoadoutSourceKind
{
    CurrentLocalProfile = 0,
    AuthoredSyntheticTeam = 1,
    SavedSnapshotAsset = 2,
    SnapshotJson = 3,
    RemoteDeckRef = 4,
}

public enum SandboxUnitSourceKind
{
    LocalProfileHero = 0,
    Archetype = 1,
    Character = 2,
}

public enum SandboxSeedMode
{
    Fixed = 0,
    Batch = 1,
    Sweep = 2,
}

[Serializable]
public sealed class CombatSandboxItemOverrideData
{
    public string ItemId = string.Empty;
    public List<string> AffixIds = new();
}

[Serializable]
public sealed class CombatSandboxBuildOverrideData
{
    public string OverrideId = string.Empty;
    public string DisplayName = string.Empty;
    public List<string> Tags = new();
    public List<CombatSandboxItemOverrideData> EquippedItems = new();
    public string PassiveBoardId = string.Empty;
    public List<string> PassiveNodeIds = new();
    public List<string> TemporaryAugmentIds = new();
    public List<string> PermanentAugmentIds = new();
    public string FlexActiveSkillId = string.Empty;
    public string FlexPassiveSkillId = string.Empty;
    public string PositiveTraitId = string.Empty;
    public string NegativeTraitId = string.Empty;
    public string RoleInstructionIdOverride = string.Empty;
    public int RetrainCount = 0;
    [TextArea] public string Notes = string.Empty;
}

[Serializable]
public sealed class CombatSandboxTeamMemberDefinition
{
    public string MemberId = string.Empty;
    public SandboxUnitSourceKind SourceKind = SandboxUnitSourceKind.Archetype;
    public string HeroId = string.Empty;
    public string DisplayName = string.Empty;
    public string ArchetypeId = string.Empty;
    public string CharacterId = string.Empty;
    public DeploymentAnchorId Anchor = DeploymentAnchorId.FrontCenter;
    public string RoleInstructionId = string.Empty;
    public CombatSandboxBuildOverrideData BuildOverride = new();
    [TextArea] public string Notes = string.Empty;
}

[Serializable]
public sealed class CombatSandboxRemoteDeckReference
{
    public string UserId = string.Empty;
    public string DeckId = string.Empty;
    public string Revision = string.Empty;
}

[Serializable]
public sealed class CombatSandboxExecutionSettings
{
    public string PresetId = string.Empty;
    public string DisplayName = string.Empty;
    public SandboxSeedMode SeedMode = SandboxSeedMode.Fixed;
    public int Seed = 17;
    public int BatchCount = 1;
    public bool RunSideSwap = false;
    public bool RecordReplay = true;
    public bool StopOnMismatch = false;
    public bool StopOnReadabilityViolation = false;
    [TextArea] public string Notes = string.Empty;
}

[Serializable]
public sealed class CombatSandboxTeamDefinition
{
    public string TeamId = string.Empty;
    public string DisplayName = string.Empty;
    public SandboxLoadoutSourceKind SourceMode = SandboxLoadoutSourceKind.AuthoredSyntheticTeam;
    public TeamPostureType TeamPosture = TeamPostureType.StandardAdvance;
    public string TeamTacticId = string.Empty;
    public List<string> Tags = new();
    public List<CombatSandboxTeamMemberDefinition> Members = new();
    public List<string> TeamTemporaryAugmentIds = new();
    public List<string> TeamPermanentAugmentIds = new();
    public CombatSandboxRemoteDeckReference RemoteDeck = new();
    public string ProvenanceLabel = string.Empty;
    [TextArea] public string Notes = string.Empty;
}

[Serializable]
public sealed class CombatSandboxScenarioMetadata
{
    public string ScenarioId = string.Empty;
    public string DisplayName = string.Empty;
    public List<string> Tags = new();
    [TextArea] public string Notes = string.Empty;
    [TextArea] public string ExpectedOutcome = string.Empty;
}

