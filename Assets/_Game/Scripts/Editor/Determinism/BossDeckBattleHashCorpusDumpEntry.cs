using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using SM.Combat.Model;
using SM.Combat.Services;
using SM.Core.Content;
using SM.Meta;
using SM.Meta.Model;
using SM.Meta.Services;
using SM.Unity;
using UnityEditor;
using UnityEngine;

namespace SM.Editor.Determinism;

public static class BossDeckBattleHashCorpusDumpEntry
{
    private const string GoldenRelativePath
        = "Tests/EditMode/FastUnit/Golden/boss-deck-battle-hash-corpus.golden.txt";

    public static void WriteGolden()
    {
        try
        {
            SM.Editor.SeedData.SampleSeedGenerator.RequireCanonicalSampleContentReady(nameof(BossDeckBattleHashCorpusDumpEntry));
            var lookup = new RuntimeCombatContentLookup(allowEditorRecoveryFallback: true);
            var corpus = BossDeckBattleHashCorpus.Generate(lookup);
            var path = Path.Combine(Application.dataPath, GoldenRelativePath);
            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllText(path, corpus);
            AssetDatabase.Refresh();
            Debug.Log($"[BossDeckBattleHashCorpus] golden written: {path} ({corpus.Length} chars)");
            Exit(0);
        }
        catch (Exception exception)
        {
            Debug.LogError($"[BossDeckBattleHashCorpus] WriteGolden failed: {exception}");
            Exit(1);
        }
    }

    private static void Exit(int code)
    {
        if (Application.isBatchMode)
        {
            EditorApplication.Exit(code);
        }
    }
}

internal static class BossDeckBattleHashCorpus
{
    internal static readonly IReadOnlyList<string> EncounterIds = new[]
    {
        "site_ashen_gate_boss_1",
        "site_wolfpine_trail_boss_1",
        "site_sunken_bastion_boss_1",
        "site_tithe_road_boss_1",
        "site_ruined_crypts_boss_1",
        "site_bone_orchard_boss_1",
    };

    private const int KeyframeInterval = 10;

    internal static string Generate(ISessionContentLookup lookup)
    {
        if (!lookup.TryGetCombatSnapshot(out var content, out var contentError))
        {
            throw new InvalidOperationException($"Boss deck corpus content unavailable: {contentError}");
        }

        var allies = CompileRangedReferenceSquad(content);
        var resolver = new EncounterResolutionService(content);
        var builder = new StringBuilder();
        builder.Append("# CanonicalStateHashV1 boss-deck-content-corpus schema=")
            .Append(BattleStateCanonicalHash.SchemaVersion)
            .Append(" interval=")
            .Append(KeyframeInterval)
            .Append('\n');

        for (var index = 0; index < EncounterIds.Count; index++)
        {
            var encounterId = EncounterIds[index];
            if (content.Encounters == null || !content.Encounters.TryGetValue(encounterId, out var encounter))
            {
                throw new InvalidOperationException($"Boss deck corpus encounter is missing: {encounterId}");
            }

            if (content.ExpeditionSites == null || !content.ExpeditionSites.TryGetValue(encounter.SiteId, out var site))
            {
                throw new InvalidOperationException($"Boss deck corpus site is missing: {encounter.SiteId}");
            }

            var seed = 1701 + (index * 101);
            var context = new BattleContextState(
                site.ChapterId,
                encounter.SiteId,
                3,
                encounter.Id,
                seed,
                $"boss-deck-corpus:{encounter.Id}:{seed}",
                encounter.RewardSourceId,
                Math.Max(1, encounter.ThreatSkulls),
                true,
                encounter.FactionId,
                encounter.BossOverlayId);
            if (!resolver.TryResolveEncounter(context, out var resolved, out var resolveError))
            {
                throw new InvalidOperationException(resolveError);
            }

            if (!SessionBattleStateComposer.TryCompose(lookup, allies, resolved, out var state, out var composeError))
            {
                throw new InvalidOperationException(composeError);
            }

            builder.Append("fixture=").Append(encounterId).Append('\n');
            builder.Append("seed=").Append(seed).Append('\n');
            AppendRun(builder, state);
        }

        return builder.ToString();
    }

    private static void AppendRun(StringBuilder builder, BattleState state)
    {
        var simulator = new BattleSimulator(state, BattleSimulator.DefaultMaxSteps);
        builder.Append(simulator.State.StepIndex)
            .Append(':')
            .Append(BattleStateCanonicalHash.Compute(simulator.State))
            .Append('\n');

        while (!simulator.IsFinished)
        {
            simulator.Step();
            if (simulator.IsFinished || simulator.State.StepIndex % KeyframeInterval == 0)
            {
                builder.Append(simulator.State.StepIndex)
                    .Append(':')
                    .Append(BattleStateCanonicalHash.Compute(simulator.State))
                    .Append('\n');
            }
        }

        builder.Append("final:")
            .Append(BattleStateCanonicalHash.Compute(simulator.State))
            .Append(" steps=").Append(simulator.State.StepIndex)
            .Append(" winner=").Append(simulator.Winner?.ToString() ?? "none")
            .Append('\n');
    }

    private static BattleLoadoutSnapshot CompileRangedReferenceSquad(CombatContentSnapshot content)
    {
        var roster = new[]
        {
            (Id: "boss-corpus-warden", ArchetypeId: "warden", Anchor: DeploymentAnchorId.FrontCenter),
            (Id: "boss-corpus-marksman", ArchetypeId: "marksman", Anchor: DeploymentAnchorId.BackTop),
            (Id: "boss-corpus-hunter", ArchetypeId: "hunter", Anchor: DeploymentAnchorId.BackCenter),
            (Id: "boss-corpus-scout", ArchetypeId: "scout", Anchor: DeploymentAnchorId.BackBottom),
        };
        var heroes = new List<HeroRecord>(roster.Length);
        var loadouts = new Dictionary<string, HeroLoadoutState>(StringComparer.Ordinal);
        var progressions = new Dictionary<string, HeroProgressionState>(StringComparer.Ordinal);
        var deployments = new Dictionary<DeploymentAnchorId, string>();

        foreach (var member in roster)
        {
            var archetype = content.Archetypes[member.ArchetypeId];
            heroes.Add(new HeroRecord(
                member.Id,
                member.Id,
                archetype.Id,
                archetype.RaceId,
                archetype.ClassId,
                string.Empty,
                string.Empty));
            loadouts[member.Id] = new HeroLoadoutState(
                member.Id,
                Array.Empty<string>(),
                Array.Empty<string>(),
                $"board_{archetype.ClassId}",
                Array.Empty<string>(),
                Array.Empty<string>());
            progressions[member.Id] = new HeroProgressionState(
                member.Id,
                1,
                0,
                Array.Empty<string>(),
                archetype.Skills.Select(skill => skill.Id).ToArray());
            deployments[member.Anchor] = member.Id;
        }

        return new LoadoutCompiler().Compile(
            heroes,
            loadouts,
            progressions,
            new Dictionary<string, ItemInstanceState>(StringComparer.Ordinal),
            new Dictionary<string, SkillInstanceState>(StringComparer.Ordinal),
            new Dictionary<string, PassiveBoardSelectionState>(StringComparer.Ordinal),
            new PermanentAugmentLoadoutState("boss-deck-corpus", Array.Empty<string>()),
            new SquadBlueprintState(
                "boss-deck-corpus",
                "boss-deck-corpus",
                TeamPostureType.StandardAdvance,
                "team_tactic_standard_advance",
                deployments,
                heroes.Select(hero => hero.Id).ToArray(),
                new Dictionary<string, string>(StringComparer.Ordinal)),
            new RunOverlayState(
                0,
                Array.Empty<string>(),
                Array.Empty<string>(),
                LoadoutCompiler.CurrentCompileVersion,
                string.Empty),
            content);
    }
}
