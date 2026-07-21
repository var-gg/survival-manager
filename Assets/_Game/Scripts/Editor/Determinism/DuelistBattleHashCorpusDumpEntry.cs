using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using SM.Combat.Model;
using SM.Combat.Services;
using SM.Core.Contracts;
using SM.Meta;
using SM.Meta.Model;
using SM.Meta.Services;
using SM.Persistence.Abstractions.Models;
using SM.Unity;
using UnityEditor;
using UnityEngine;

namespace SM.Editor.Determinism;

public static class DuelistBattleHashCorpusDumpEntry
{
    private const string GoldenRelativePath
        = "Tests/EditMode/FastUnit/Golden/duelist-battle-hash-corpus.golden.txt";

    public static void WriteGolden()
    {
        try
        {
            var content = new RuntimeCombatContentLookup().Snapshot;
            var corpus = DuelistBattleHashCorpus.Generate(content);
            var path = Path.Combine(Application.dataPath, GoldenRelativePath);
            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllText(path, corpus);
            AssetDatabase.Refresh();
            Debug.Log($"[DuelistBattleHashCorpus] golden written: {path} ({corpus.Length} chars)");
            Exit(0);
        }
        catch (Exception exception)
        {
            Debug.LogError($"[DuelistBattleHashCorpus] WriteGolden failed: {exception}");
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

internal static class DuelistBattleHashCorpus
{
    internal static readonly IReadOnlyList<int> Seeds = new[] { 7, 23, 42, 101, 2024 };

    private const int KeyframeInterval = 10;
    private const int MaxSteps = 400;

    internal static string Generate(CombatContentSnapshot content)
    {
        var guardian = CompileHero(content, "duelist-corpus-guardian", "guardian", Array.Empty<string>())
            .Allies.Single();
        var classOnly = CompileHero(content, "duelist-corpus-class", "slayer", Array.Empty<string>())
            .Allies.Single();
        var gateNodes = BuildPrerequisiteClosure(content, "passive_duelist_notable_01");
        var gate = CompileHero(content, "duelist-corpus-gate", "slayer", gateNodes)
            .Allies.Single();
        var statusRules = CombatStatusRuleCompiler.Compile(content);

        var builder = new StringBuilder();
        builder.Append("# CanonicalStateHashV1 duelist-content-corpus schema=")
            .Append(BattleStateCanonicalHash.SchemaVersion)
            .Append(" interval=")
            .Append(KeyframeInterval)
            .Append('\n');
        AppendFixture(builder, "duelist-class-1v1", classOnly, guardian, statusRules);
        AppendFixture(builder, "duelist-build-gate-1v1", gate, guardian, statusRules);
        return builder.ToString();
    }

    private static void AppendFixture(
        StringBuilder builder,
        string fixtureId,
        BattleUnitLoadout duelist,
        BattleUnitLoadout guardian,
        CombatStatusRules statusRules)
    {
        builder.Append("fixture=").Append(fixtureId).Append('\n');
        foreach (var seed in Seeds)
        {
            var state = BattleFactory.Create(
                new[] { duelist },
                new[] { guardian },
                TeamPostureType.StandardAdvance,
                TeamPostureType.StandardAdvance,
                BattleSimulator.DefaultFixedStepSeconds,
                seed,
                statusRules: statusRules);
            AppendRun(builder, state, seed);
        }
    }

    private static void AppendRun(StringBuilder builder, BattleState state, int seed)
    {
        var simulator = new BattleSimulator(state, MaxSteps);
        builder.Append("seed=").Append(seed).Append('\n');
        builder.Append(simulator.State.StepIndex)
            .Append(':')
            .Append(BattleStateCanonicalHash.Compute(simulator.State))
            .Append('\n');

        var guard = 0;
        while (!simulator.IsFinished && guard++ < 10000)
        {
            simulator.Step();
            var step = simulator.State.StepIndex;
            if (simulator.IsFinished || step % KeyframeInterval == 0)
            {
                builder.Append(step)
                    .Append(':')
                    .Append(BattleStateCanonicalHash.Compute(simulator.State))
                    .Append('\n');
            }
        }

        builder.Append("final:")
            .Append(BattleStateCanonicalHash.Compute(simulator.State))
            .Append(" steps=")
            .Append(simulator.State.StepIndex)
            .Append(" winner=")
            .Append(simulator.Winner?.ToString() ?? "none")
            .Append('\n');
    }

    private static IReadOnlyList<string> BuildPrerequisiteClosure(CombatContentSnapshot content, string nodeId)
    {
        var result = new List<string>();
        var visited = new HashSet<string>(StringComparer.Ordinal);

        void Visit(string id)
        {
            if (!visited.Add(id) || !content.PassiveNodes.TryGetValue(id, out var node))
            {
                return;
            }

            foreach (var prerequisite in node.PrerequisiteNodeIds ?? Array.Empty<string>())
            {
                Visit(prerequisite);
            }

            result.Add(id);
        }

        Visit(nodeId);
        return result;
    }

    private static BattleLoadoutSnapshot CompileHero(
        CombatContentSnapshot content,
        string identity,
        string archetypeId,
        IReadOnlyList<string> selectedNodeIds)
    {
        var archetype = content.Archetypes[archetypeId];
        var heroId = $"hero.{identity}";
        var boardId = $"board_{archetype.ClassId}";
        var heroes = new[]
        {
            new HeroRecord(
                heroId,
                heroId,
                archetype.Id,
                archetype.RaceId,
                archetype.ClassId,
                string.Empty,
                string.Empty),
        };
        var loadouts = new Dictionary<string, HeroLoadoutState>(StringComparer.Ordinal)
        {
            [heroId] = new(
                heroId,
                Array.Empty<string>(),
                Array.Empty<string>(),
                boardId,
                Array.Empty<string>(),
                Array.Empty<string>()),
        };
        var progressions = new Dictionary<string, HeroProgressionState>(StringComparer.Ordinal)
        {
            [heroId] = new(
                heroId,
                1,
                0,
                Array.Empty<string>(),
                archetype.Skills.Select(skill => skill.Id).ToArray()),
        };
        var passives = selectedNodeIds.Count == 0
            ? new Dictionary<string, PassiveBoardSelectionState>(StringComparer.Ordinal)
            : new Dictionary<string, PassiveBoardSelectionState>(StringComparer.Ordinal)
            {
                [heroId] = new(heroId, boardId, selectedNodeIds),
            };

        return new LoadoutCompiler().Compile(
            heroes,
            loadouts,
            progressions,
            new Dictionary<string, ItemInstanceState>(StringComparer.Ordinal),
            new Dictionary<string, SkillInstanceState>(StringComparer.Ordinal),
            passives,
            new PermanentAugmentLoadoutState($"bp.{identity}", Array.Empty<string>()),
            new SquadBlueprintState(
                $"bp.{identity}",
                $"bp.{identity}",
                TeamPostureType.StandardAdvance,
                "team_tactic_standard_advance",
                new Dictionary<DeploymentAnchorId, string>
                {
                    [DeploymentAnchorId.FrontCenter] = heroId,
                },
                new[] { heroId },
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
