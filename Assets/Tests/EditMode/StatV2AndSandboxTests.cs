using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using SM.Combat.Model;
using SM.Combat.Services;
using SM.Core.Stats;
using SM.Editor.Authoring.CombatSandbox;
using SM.Editor.SeedData;
using SM.Editor.Validation;
using SM.Unity.Sandbox;
using UnityEditor;
using UnityEngine;

namespace SM.Tests.EditMode;

public sealed class StatV2AndSandboxTests
{
    [Test]
    public void StatKey_ResolvesCanonicalAndLegacyAliases()
    {
        Assert.That(StatKey.TryResolve("phys_power", out var canonicalKey, out var canonicalIsLegacy), Is.True);
        Assert.That(canonicalKey, Is.EqualTo(StatKey.PhysPower));
        Assert.That(canonicalIsLegacy, Is.False);

        Assert.That(StatKey.TryResolve("attack", out var legacyKey, out var legacyIsLegacy), Is.True);
        Assert.That(legacyKey, Is.EqualTo(StatKey.PhysPower));
        Assert.That(legacyIsLegacy, Is.True);
        Assert.That(StatKey.TryResolve("lifesteal", out var lifestealKey, out var lifestealIsLegacy), Is.True);
        Assert.That(lifestealKey, Is.EqualTo(StatKey.Lifesteal));
        Assert.That(lifestealIsLegacy, Is.False);
        Assert.That(StatKey.TryResolve("omnivamp", out var omnivampKey, out var omnivampIsLegacy), Is.True);
        Assert.That(omnivampKey, Is.EqualTo(StatKey.Omnivamp));
        Assert.That(omnivampIsLegacy, Is.False);
        Assert.That(ContentDefinitionValidator.GetStatIdStatus("attack"), Is.EqualTo(StatIdValidationStatus.LegacyAlias));
        Assert.That(ContentDefinitionValidator.GetStatIdStatus("phys_power"), Is.EqualTo(StatIdValidationStatus.Canonical));
        Assert.That(ContentDefinitionValidator.GetStatIdStatus("lifesteal"), Is.EqualTo(StatIdValidationStatus.Canonical));
        Assert.That(ContentDefinitionValidator.GetStatIdStatus("omnivamp"), Is.EqualTo(StatIdValidationStatus.Canonical));
        Assert.That(ContentDefinitionValidator.GetStatIdStatus("unsupported_stat"), Is.EqualTo(StatIdValidationStatus.Unsupported));
    }

    [Test]
    public void ContentDefinitionValidator_ExposesLaunchScopeBuckets()
    {
        Assert.That(ContentDefinitionValidator.CurrentMvpMinimum.ArchetypeCount, Is.EqualTo(8));
        Assert.That(ContentDefinitionValidator.CurrentMvpMinimum.TemporaryAugmentCount, Is.EqualTo(9));
        Assert.That(ContentDefinitionValidator.PaidLaunchFloor.ArchetypeCount, Is.EqualTo(12));
        Assert.That(ContentDefinitionValidator.PaidLaunchFloor.SkillCount, Is.EqualTo(40));
        Assert.That(ContentDefinitionValidator.PaidLaunchSafeTarget.ArchetypeCount, Is.EqualTo(16));
        Assert.That(ContentDefinitionValidator.PaidLaunchSafeTarget.PassiveNodeCount, Is.EqualTo(96));
        Assert.That(CountAssets("Archetypes"), Is.EqualTo(12));
        Assert.That(CountAssets("Skills"), Is.EqualTo(40));
        Assert.That(CountAssets("Items"), Is.EqualTo(36));
        Assert.That(CountAssets("PassiveBoards"), Is.EqualTo(4));
        Assert.That(CountAssets("PassiveNodes"), Is.EqualTo(72));
        Assert.That(CountAugments(isPermanent: false), Is.EqualTo(18));
        Assert.That(CountAugments(isPermanent: true), Is.EqualTo(9));
        Assert.That(CountSynergyFamilies(), Is.EqualTo(7));
    }

    [Test]
    public void UnitSnapshot_CompatibilityAccessors_ReadCanonicalStats()
    {
        var loadout = new BattleUnitLoadout(
            "unit.v2",
            "unit.v2",
            "human",
            "vanguard",
            DeploymentAnchorId.FrontCenter,
            new Dictionary<StatKey, float>
            {
                [StatKey.MaxHealth] = 20f,
                [StatKey.PhysPower] = 8f,
                [StatKey.Armor] = 3f,
                [StatKey.AttackSpeed] = 2.5f,
                [StatKey.MoveSpeed] = 1.8f,
                [StatKey.AttackRange] = 1.2f,
                [StatKey.AttackWindup] = 0.1f,
                [StatKey.AttackCooldown] = 0.6f,
                [StatKey.LeashDistance] = 6f,
                [StatKey.TargetSwitchDelay] = 0.3f,
            },
            new[]
            {
                new UnitRuleChain("rules:v2", new[]
                {
                    new TacticRule(0, TacticConditionType.LowestHpEnemy, 0f, BattleActionType.BasicAttack, TargetSelectorType.LowestHpEnemy)
                })
            },
            new BattleSkillSpec[0]);

        var state = BattleFactory.Create(new[] { loadout }, new[] { CombatTestFactory.CreateUnit("enemy.v2", race: "undead") });
        var actor = state.Allies[0];

        Assert.That(actor.Attack, Is.EqualTo(8f));
        Assert.That(actor.Defense, Is.EqualTo(3f));
        Assert.That(actor.Speed, Is.EqualTo(2.5f));
    }

    [Test]
    public void CombatSandboxWindow_BindsAndBuildsRunRequest_WithoutPlayMode()
    {
        SampleSeedGenerator.EnsureCanonicalSampleContent();
        var config = ScriptableObject.CreateInstance<CombatSandboxConfig>();
        config.Seed = 29;
        config.BatchCount = 2;

        var window = EditorWindow.GetWindow<CombatSandboxWindow>();
        try
        {
            window.State.Config = config;
            var request = CombatSandboxExecutionService.BuildRequest(window.State);
            var result = CombatSandboxSceneController.Execute(request);

            Assert.That(window.rootVisualElement.childCount, Is.GreaterThan(0));
            Assert.That(request.PlayerSnapshot.CompileHash, Is.Not.Empty);
            Assert.That(result.LastReplay.Header.FinalStateHash, Is.Not.Empty);
            Assert.That(result.Metrics.BatchCount, Is.EqualTo(2));
        }
        finally
        {
            window.Close();
            Object.DestroyImmediate(config);
        }
    }

    private static int CountAssets(string folderName)
    {
        var folder = Path.Combine("Assets", "Resources", "_Game", "Content", "Definitions", folderName);
        return Directory.Exists(folder)
            ? Directory.EnumerateFiles(folder, "*.asset", SearchOption.TopDirectoryOnly).Count()
            : 0;
    }

    private static int CountAugments(bool isPermanent)
    {
        var folder = Path.Combine("Assets", "Resources", "_Game", "Content", "Definitions", "Augments");
        if (!Directory.Exists(folder))
        {
            return 0;
        }

        var marker = isPermanent ? "augment_perm_" : "augment_";
        return Directory.EnumerateFiles(folder, "*.asset", SearchOption.TopDirectoryOnly)
            .Select(Path.GetFileNameWithoutExtension)
            .Count(name => name != null
                && (isPermanent
                    ? name.StartsWith(marker, System.StringComparison.Ordinal)
                    : name.StartsWith(marker, System.StringComparison.Ordinal) && !name.StartsWith("augment_perm_", System.StringComparison.Ordinal)));
    }

    private static int CountSynergyFamilies()
    {
        var folder = Path.Combine("Assets", "Resources", "_Game", "Content", "Definitions", "Synergies");
        if (!Directory.Exists(folder))
        {
            return 0;
        }

        return Directory.EnumerateFiles(folder, "synergy_*.asset", SearchOption.TopDirectoryOnly)
            .Select(Path.GetFileNameWithoutExtension)
            .Count(name => name != null && !name.StartsWith("synergytier_", System.StringComparison.Ordinal));
    }
}
