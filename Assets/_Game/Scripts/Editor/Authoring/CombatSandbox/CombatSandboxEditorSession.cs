using System;
using System.Linq;
using System.Runtime.CompilerServices;
using SM.Combat.Model;
using SM.Combat.Services;
using SM.Persistence.Abstractions;
using SM.Persistence.Abstractions.Models;
using SM.Unity;
using SM.Unity.Sandbox;
using UnityEditor;
using UnityEngine;

namespace SM.Editor.Authoring.CombatSandbox;

public sealed record CombatSandboxEditorPreviewResult(
    CombatSandboxCompiledScenario CompiledScenario,
    CombatSandboxLaunchTruthPreview LaunchTruth,
    string ScenarioSummary,
    string LeftTeamPreview,
    string RightTeamPreview,
    string ValidationMessage);

public sealed record CombatSandboxEditorRunSummary(
    CombatSandboxEditorPreviewResult Preview,
    CombatSandboxRunResult PrimaryResult,
    CombatSandboxRunResult? SideSwapResult,
    string LayoutSourceLabel);

public sealed class CombatSandboxEditorSession
{
    private static CombatSandboxEditorSession? s_shared;

    private RuntimeCombatContentLookup? _lookup;
    private PersistenceEntryPoint? _persistence;
    private SaveProfile _profile = new();
    private CombatSandboxCompilationContext _compilationContext = CombatSandboxCompilationContextFactory.CreatePreviewContext(new SaveProfile());
    private string _profileHash = string.Empty;
    private string _cachedCompileKey = string.Empty;
    private CombatSandboxCompiledScenario? _cachedCompiledScenario;

    public static CombatSandboxEditorSession Shared => s_shared ??= new CombatSandboxEditorSession();

    public RuntimeCombatContentLookup Lookup
    {
        get
        {
            EnsureContext();
            return _lookup!;
        }
    }

    public void Invalidate()
    {
        _cachedCompileKey = string.Empty;
        _cachedCompiledScenario = null;
    }

    public void RefreshProfile()
    {
        _persistence ??= new PersistenceEntryPoint();
        _lookup ??= new RuntimeCombatContentLookup();
        _profile = LoadLocalProfile(_persistence);
        _compilationContext = CombatSandboxCompilationContextFactory.CreatePreviewContext(_profile);
        _profileHash = ComputeProfileHash(_profile);
        Invalidate();
    }

    public CombatSandboxCompiledScenario BuildCompiledScenario(CombatSandboxConfig config, int seedOverride = 0)
    {
        EnsureContext();
        var laneKind = config.DefaultLaneKind == CombatSandboxLaneKind.None
            ? CombatSandboxLaneKind.DirectCombatSandbox
            : config.DefaultLaneKind;
        var cacheKey = BuildCompileCacheKey(config, laneKind, seedOverride);
        if (string.Equals(cacheKey, _cachedCompileKey, StringComparison.Ordinal) && _cachedCompiledScenario != null)
        {
            return _cachedCompiledScenario;
        }

        var compiler = new CombatSandboxScenarioCompiler(_lookup!);
        if (!compiler.TryCompileScenario(
                BuildCompilationContext(),
                config,
                laneKind,
                seedOverride == 0 ? null : seedOverride,
                out var compiled,
                out var error))
        {
            throw new InvalidOperationException(error);
        }

        _cachedCompileKey = cacheKey;
        _cachedCompiledScenario = compiled;
        return compiled;
    }

    public CombatSandboxEditorPreviewResult BuildPreview(CombatSandboxConfig config, int seedOverride = 0)
    {
        var compiled = BuildCompiledScenario(config, seedOverride);
        var launchTruth = CombatSandboxLaunchTruthDiffService.BuildPreview(compiled, Lookup);
        var validationMessage = compiled.Warnings.Count == 0
            ? "Compile preview ready."
            : string.Join("\n", compiled.Warnings.Distinct(StringComparer.Ordinal));
        return new CombatSandboxEditorPreviewResult(
            compiled,
            launchTruth,
            CombatSandboxPreviewFormatter.BuildScenarioSummary(compiled),
            CombatSandboxPreviewFormatter.BuildTeamPreview(compiled.LeftTeam),
            CombatSandboxPreviewFormatter.BuildTeamPreview(compiled.RightTeam),
            validationMessage);
    }

    public CombatSandboxEditorRunSummary Run(
        CombatSandboxConfig config,
        int seedOverride,
        int batchCountOverride,
        bool runAsBatch,
        bool runSideSwap,
        BattlefieldLayout? sceneLayout = null)
    {
        var preview = BuildPreview(config, seedOverride);
        var compiled = preview.CompiledScenario;
        var batchCount = runAsBatch
            ? (batchCountOverride > 0 ? batchCountOverride : Math.Max(1, compiled.Execution.BatchCount))
            : 1;
        var effectiveSceneLayout = sceneLayout ?? config.SceneLayout?.BuildBattlefieldLayout();
        var primary = CombatSandboxSceneController.Execute(new CombatSandboxRunRequest(
            compiled.LeftTeam.Snapshot,
            compiled.RightTeam.Snapshot.Allies,
            compiled.Seed,
            batchCount,
            compiled.ScenarioId,
            effectiveSceneLayout));

        CombatSandboxRunResult? sideSwapResult = null;
        if (runSideSwap || (runAsBatch && compiled.Execution.RunSideSwap))
        {
            sideSwapResult = CombatSandboxSceneController.Execute(new CombatSandboxRunRequest(
                compiled.RightTeam.Snapshot,
                compiled.LeftTeam.Snapshot.Allies,
                compiled.Seed,
                batchCount,
                $"{compiled.ScenarioId}.side_swap",
                effectiveSceneLayout));
        }

        return new CombatSandboxEditorRunSummary(
            preview,
            primary,
            sideSwapResult,
            effectiveSceneLayout != null
                ? sceneLayout != null ? "Scene" : "Config"
                : "Default");
    }

    public BattlefieldLayout? ResolveLayout(CombatSandboxConfig config, CombatSandboxSceneController? sceneController)
    {
        if (sceneController != null)
        {
            return sceneController.ExportSceneLayout();
        }

        return config.SceneLayout?.BuildBattlefieldLayout();
    }

    private void EnsureContext()
    {
        if (_lookup != null)
        {
            return;
        }

        RefreshProfile();
    }

    private CombatSandboxCompilationContext BuildCompilationContext()
    {
        return _compilationContext;
    }

    private string BuildCompileCacheKey(CombatSandboxConfig config, CombatSandboxLaneKind laneKind, int seedOverride)
    {
        var configHash = Hash128.Compute(EditorJsonUtility.ToJson(config)).ToString();
        var layoutHash = config.SceneLayout != null
            ? Hash128.Compute(EditorJsonUtility.ToJson(config.SceneLayout)).ToString()
            : "no-layout";
        var contentIdentity = RuntimeHelpers.GetHashCode(_lookup!.Snapshot).ToString();
        return $"{configHash}|{_profileHash}|{layoutHash}|{contentIdentity}|{laneKind}|{seedOverride}";
    }

    private static string ComputeProfileHash(SaveProfile profile)
    {
        return Hash128.Compute(JsonUtility.ToJson(profile)).ToString();
    }

    private static SaveProfile LoadLocalProfile(PersistenceEntryPoint persistence)
    {
        var profileId = string.IsNullOrWhiteSpace(persistence.Config.ProfileId)
            ? "default"
            : persistence.Config.ProfileId;

        try
        {
            if (persistence.Repository is ISaveRepositoryDiagnostics diagnostics)
            {
                var detailed = diagnostics.LoadOrCreateDetailed(
                    profileId,
                    new SaveRepositoryRequest
                    {
                        CheckpointKind = "CombatSandboxEditorSession",
                    });
                if (detailed.Profile != null)
                {
                    return detailed.Profile;
                }
            }

            return persistence.Repository.LoadOrCreate(profileId);
        }
        catch
        {
            return new SaveProfile
            {
                ProfileId = profileId,
            };
        }
    }
}
