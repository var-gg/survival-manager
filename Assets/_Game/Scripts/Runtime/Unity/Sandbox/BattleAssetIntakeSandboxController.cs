using System.Collections.Generic;
using SM.Combat.Model;
using CoreEntityId = SM.Core.Ids.EntityId;
using UnityEngine;

namespace SM.Unity.Sandbox;

public sealed class BattleAssetIntakeSandboxController : MonoBehaviour
{
    [SerializeField] private BattlePresentationController presentationController = null!;
    [SerializeField] private BattleActorWrapper primitiveWrapperPrefab = null!;
    [SerializeField] private BattleActorWrapper vendorTemplateWrapperPrefab = null!;
    [SerializeField] private BattleActorWrapper vendorCandidateWrapperPrefab = null!;

    private BattleActorPresentationCatalog _runtimeCatalog = null!;
    private BattleSimulationStep _initialStep = null!;
    private BattleSimulationStep _currentStep = null!;
    private string _selectedActorId = "sandbox_ally";

    private void Start()
    {
        if (presentationController == null)
        {
            return;
        }

        _runtimeCatalog = ScriptableObject.CreateInstance<BattleActorPresentationCatalog>();
        var defaultWrapper = primitiveWrapperPrefab != null
            ? primitiveWrapperPrefab
            : BattleActorPresentationCatalog.ResolveRuntimeCatalog(null).ResolveWrapperPrefab(CreateUnit("fallback", TeamSide.Ally));
        var enemyWrapper = vendorCandidateWrapperPrefab != null
            ? vendorCandidateWrapperPrefab
            : vendorTemplateWrapperPrefab != null
                ? vendorTemplateWrapperPrefab
                : defaultWrapper;

        _runtimeCatalog.SetDefaultWrapper(defaultWrapper);
        _runtimeCatalog.SetTeamDefaultWrapper(TeamSide.Ally, defaultWrapper);
        _runtimeCatalog.SetTeamDefaultWrapper(TeamSide.Enemy, enemyWrapper);
        _runtimeCatalog.SetCharacterOverride("sandbox_ally", defaultWrapper);
        _runtimeCatalog.SetCharacterOverride("sandbox_enemy", enemyWrapper);

        presentationController.ConfigurePresentationCatalog(_runtimeCatalog);
        _initialStep = CreateIdleStep(0);
        _currentStep = _initialStep;
        presentationController.Initialize(_initialStep);
        presentationController.SetFocus(_currentStep, _selectedActorId);
    }

    private void Update()
    {
        if (presentationController == null)
        {
            return;
        }

        presentationController.TickTransients(Time.deltaTime, 1f, false);

        if (Input.GetMouseButtonDown(0) && presentationController.TryPickActor(Input.mousePosition, out var actorId))
        {
            _selectedActorId = actorId;
            presentationController.SetFocus(_currentStep, _selectedActorId);
        }

        if (Input.GetKeyDown(KeyCode.Alpha1))
        {
            ApplyStep(CreateWindupStep(1));
        }

        if (Input.GetKeyDown(KeyCode.Alpha2))
        {
            ApplyStep(CreateCommitStep(2));
        }

        if (Input.GetKeyDown(KeyCode.Alpha3))
        {
            ApplyStep(CreateGuardStep(3));
        }

        if (Input.GetKeyDown(KeyCode.Alpha4))
        {
            ApplyStep(CreateRepositionStep(4));
        }

        if (Input.GetKeyDown(KeyCode.Alpha5))
        {
            ApplyStep(CreateDeathStep(5));
        }

        if (Input.GetKeyDown(KeyCode.R))
        {
            ResetPlayback(BattlePresentationCueType.PlaybackReset);
        }

        if (Input.GetKeyDown(KeyCode.S))
        {
            ResetPlayback(BattlePresentationCueType.SeekSnapshotApplied);
        }
    }

    private void OnGUI()
    {
        const float width = 300f;
        GUILayout.BeginArea(new Rect(16f, 16f, width, 420f), GUI.skin.box);
        GUILayout.Label("Battle Asset Intake Sandbox");
        GUILayout.Label("1 Windup | 2 Commit | 3 Guard | 4 Reposition | 5 Death | R Reset | S Seek");
        GUILayout.Label($"Selected: {_selectedActorId}");

        if (GUILayout.Button("Windup"))
        {
            ApplyStep(CreateWindupStep(1));
        }

        if (GUILayout.Button("Commit / Impact"))
        {
            ApplyStep(CreateCommitStep(2));
        }

        if (GUILayout.Button("Guard"))
        {
            ApplyStep(CreateGuardStep(3));
        }

        if (GUILayout.Button("Reposition"))
        {
            ApplyStep(CreateRepositionStep(4));
        }

        if (GUILayout.Button("Death"))
        {
            ApplyStep(CreateDeathStep(5));
        }

        if (GUILayout.Button("Reset"))
        {
            ResetPlayback(BattlePresentationCueType.PlaybackReset);
        }

        if (GUILayout.Button("Seek Snapshot"))
        {
            ResetPlayback(BattlePresentationCueType.SeekSnapshotApplied);
        }

        foreach (var wrapper in presentationController.GetComponentsInChildren<BattleActorWrapper>())
        {
            GUILayout.Space(8f);
            GUILayout.Label(wrapper.name);
            foreach (var socket in wrapper.CaptureSocketStatus())
            {
                GUILayout.Label($"{socket.SocketId}: {(socket.UsesFallback ? "fallback" : "authored")}");
            }
        }

        GUILayout.EndArea();
    }

    private void OnDrawGizmos()
    {
        if (presentationController == null)
        {
            return;
        }

        foreach (var wrapper in presentationController.GetComponentsInChildren<BattleActorWrapper>())
        {
            foreach (var socket in wrapper.CaptureSocketStatus())
            {
                Gizmos.color = socket.UsesFallback
                    ? new Color(1f, 0.64f, 0.24f, 1f)
                    : new Color(0.24f, 0.92f, 0.84f, 1f);
                Gizmos.DrawSphere(socket.WorldPosition, 0.06f);
            }
        }
    }

    private void ApplyStep(BattleSimulationStep nextStep)
    {
        if (presentationController == null)
        {
            return;
        }

        presentationController.AdvanceStep(_currentStep, nextStep);
        _currentStep = nextStep;
        presentationController.SetFocus(_currentStep, _selectedActorId);
    }

    private void ResetPlayback(BattlePresentationCueType reason)
    {
        _currentStep = _initialStep;
        presentationController.ClearTransients(reason);
        presentationController.RenderSnapshot(_initialStep);
        presentationController.SetFocus(_currentStep, _selectedActorId);
    }

    private static BattleSimulationStep CreateIdleStep(int stepIndex)
    {
        return new BattleSimulationStep(
            stepIndex,
            stepIndex * 0.1f,
            new[]
            {
                CreateUnit("sandbox_ally", TeamSide.Ally, CombatActionState.AcquireTarget, new CombatVector2(-1.8f, -0.4f), "sandbox_enemy", "sandbox_ally", "warden"),
                CreateUnit("sandbox_enemy", TeamSide.Enemy, CombatActionState.AcquireTarget, new CombatVector2(1.8f, 0.4f), "sandbox_ally", "sandbox_enemy", "guardian"),
            },
            System.Array.Empty<BattleEvent>(),
            false,
            null);
    }

    private static BattleSimulationStep CreateWindupStep(int stepIndex)
    {
        return new BattleSimulationStep(
            stepIndex,
            stepIndex * 0.1f,
            new[]
            {
                CreateUnit("sandbox_ally", TeamSide.Ally, CombatActionState.ExecuteAction, new CombatVector2(-1.8f, -0.4f), "sandbox_enemy", "sandbox_ally", "warden", windup: 0.48f),
                CreateUnit("sandbox_enemy", TeamSide.Enemy, CombatActionState.AcquireTarget, new CombatVector2(1.8f, 0.4f), "sandbox_ally", "sandbox_enemy", "guardian"),
            },
            System.Array.Empty<BattleEvent>(),
            false,
            null);
    }

    private static BattleSimulationStep CreateCommitStep(int stepIndex)
    {
        return new BattleSimulationStep(
            stepIndex,
            stepIndex * 0.1f,
            new[]
            {
                CreateUnit("sandbox_ally", TeamSide.Ally, CombatActionState.Recover, new CombatVector2(-1.7f, -0.4f), "sandbox_enemy", "sandbox_ally", "warden"),
                CreateUnit("sandbox_enemy", TeamSide.Enemy, CombatActionState.Recover, new CombatVector2(1.8f, 0.4f), "sandbox_ally", "sandbox_enemy", "guardian", currentHealth: 14f),
            },
            new[]
            {
                new BattleEvent(
                    stepIndex,
                    stepIndex * 0.1f,
                    new CoreEntityId("sandbox_ally"),
                    "Sandbox Ally",
                    BattleActionType.BasicAttack,
                    BattleLogCode.BasicAttackDamage,
                    new CoreEntityId("sandbox_enemy"),
                    "Sandbox Enemy",
                    6f),
            },
            false,
            null);
    }

    private static BattleSimulationStep CreateGuardStep(int stepIndex)
    {
        return new BattleSimulationStep(
            stepIndex,
            stepIndex * 0.1f,
            new[]
            {
                CreateUnit("sandbox_ally", TeamSide.Ally, CombatActionState.Recover, new CombatVector2(-1.7f, -0.4f), "sandbox_enemy", "sandbox_ally", "warden", isDefending: true),
                CreateUnit("sandbox_enemy", TeamSide.Enemy, CombatActionState.AcquireTarget, new CombatVector2(1.8f, 0.4f), "sandbox_ally", "sandbox_enemy", "guardian"),
            },
            System.Array.Empty<BattleEvent>(),
            false,
            null);
    }

    private static BattleSimulationStep CreateRepositionStep(int stepIndex)
    {
        return new BattleSimulationStep(
            stepIndex,
            stepIndex * 0.1f,
            new[]
            {
                CreateUnit("sandbox_ally", TeamSide.Ally, CombatActionState.Reposition, new CombatVector2(-0.8f, -0.1f), "sandbox_enemy", "sandbox_ally", "warden"),
                CreateUnit("sandbox_enemy", TeamSide.Enemy, CombatActionState.AcquireTarget, new CombatVector2(1.8f, 0.4f), "sandbox_ally", "sandbox_enemy", "guardian"),
            },
            System.Array.Empty<BattleEvent>(),
            false,
            null);
    }

    private static BattleSimulationStep CreateDeathStep(int stepIndex)
    {
        return new BattleSimulationStep(
            stepIndex,
            stepIndex * 0.1f,
            new[]
            {
                CreateUnit("sandbox_ally", TeamSide.Ally, CombatActionState.Recover, new CombatVector2(-1.7f, -0.4f), "sandbox_enemy", "sandbox_ally", "warden"),
                CreateUnit("sandbox_enemy", TeamSide.Enemy, CombatActionState.Dead, new CombatVector2(1.8f, 0.4f), "sandbox_ally", "sandbox_enemy", "guardian", currentHealth: 0f, isAlive: false),
            },
            new[]
            {
                new BattleEvent(
                    stepIndex,
                    stepIndex * 0.1f,
                    new CoreEntityId("sandbox_ally"),
                    "Sandbox Ally",
                    BattleActionType.BasicAttack,
                    BattleLogCode.BasicAttackDamage,
                    new CoreEntityId("sandbox_enemy"),
                    "Sandbox Enemy",
                    14f,
                    BattleEventKind.Kill),
            },
            false,
            null);
    }

    private static BattleUnitReadModel CreateUnit(
        string id,
        TeamSide side,
        CombatActionState actionState = CombatActionState.AcquireTarget,
        CombatVector2 position = default,
        string? targetId = null,
        string characterId = "",
        string archetypeId = "",
        float currentHealth = 20f,
        bool isAlive = true,
        float windup = 0f,
        bool isDefending = false)
    {
        var fallbackPosition = side == TeamSide.Ally ? new CombatVector2(-1f, 0f) : new CombatVector2(1f, 0f);
        var resolvedPosition = position == default ? fallbackPosition : position;
        return new BattleUnitReadModel(
            Id: id,
            Name: side == TeamSide.Ally ? "Sandbox Ally" : "Sandbox Enemy",
            Side: side,
            Anchor: side == TeamSide.Ally ? DeploymentAnchorId.FrontCenter : DeploymentAnchorId.BackCenter,
            RaceId: "human",
            ClassId: "vanguard",
            Position: resolvedPosition,
            CurrentHealth: currentHealth,
            MaxHealth: 20f,
            IsAlive: isAlive,
            ActionState: actionState,
            PendingActionType: BattleActionType.BasicAttack,
            TargetId: targetId,
            TargetName: targetId,
            WindupProgress: windup,
            CooldownRemaining: 0f,
            CurrentEnergy: 0f,
            MaxEnergy: 100f,
            IsDefending: isDefending,
            ArchetypeId: archetypeId,
            CharacterId: characterId);
    }
}
