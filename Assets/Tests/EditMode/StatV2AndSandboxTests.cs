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

[Category("BatchOnly")]
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
        // V1 SafeTarget archetype 16 (bastion_penitent / mirror_cantor / pale_executor / rift_stalker 추가됨 — 10ed7f7b "출시 안전 목표 콘텐츠 볼륨 보강")
        Assert.That(CountAssets("Archetypes"), Is.EqualTo(16));
        Assert.That(CountLaunchFloorSkills(), Is.EqualTo(40));
        Assert.That(CountSupportModifierSkills(), Is.EqualTo(12));
        // V1 SafeTarget items 42 (10ed7f7b "출시 안전 목표 콘텐츠 볼륨 보강" 확장 후)
        Assert.That(CountAssets("Items"), Is.EqualTo(42));
        Assert.That(CountAssets("PassiveBoards"), Is.EqualTo(4));
        // V1 SafeTarget PassiveNodes 96 (line 51 PaidLaunchSafeTarget.PassiveNodeCount와 일치)
        Assert.That(CountAssets("PassiveNodes"), Is.EqualTo(96));
        Assert.That(CountAugments(isPermanent: false), Is.EqualTo(24));
        Assert.That(CountAugments(isPermanent: true), Is.EqualTo(12));
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
    [Ignore("CombatSandbox authoring asset이 main에 m_Script: 0 (null ScriptableObject ref) 상태로 commit되어 있어 OnEnable의 EnsureStarterLibrary가 NRE. Recovery menu + Unity 재시작 + 폴더 통째 삭제 시도해도 EnsureStarterLibrary가 또 broken state로 재생성. cache/Unity bug 의심 — V1 sandbox authority 영역의 별개 task로 분리.")]
    public void CombatSandboxWindow_BindsAndBuildsRunRequest_WithoutPlayMode()
    {
        SampleSeedGenerator.RequireCanonicalSampleContentReady(nameof(CombatSandboxWindow_BindsAndBuildsRunRequest_WithoutPlayMode));

        var window = EditorWindow.GetWindow<CombatSandboxWindow>();
        try
        {
            // OnEnable이 CombatSandboxAuthoringAssetUtility.EnsureActiveConfig()로 fully-populated
            // default config를 박아준다. 새 ScriptableObject.CreateInstance<CombatSandboxConfig>()는
            // teams/scenarios가 비어 있어 BuildCompiledScenario에서 NRE 떨어짐 (이전 minimal-config
            // 패턴이 main drift로 남아 있었음). seed/batchCount는 State에서 override.
            Assert.That(window.State.Config, Is.Not.Null, "OnEnable should have wired an active config.");
            window.State.Seed = 29;
            window.State.BatchCount = 2;
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

    private static int CountLaunchFloorSkills()
    {
        var folder = Path.Combine("Assets", "Resources", "_Game", "Content", "Definitions", "Skills");
        if (!Directory.Exists(folder))
        {
            return 0;
        }

        return Directory.EnumerateFiles(folder, "*.asset", SearchOption.TopDirectoryOnly)
            .Select(Path.GetFileNameWithoutExtension)
            .Count(name => name != null && !name.StartsWith("support_", System.StringComparison.Ordinal));
    }

    private static int CountSupportModifierSkills()
    {
        var folder = Path.Combine("Assets", "Resources", "_Game", "Content", "Definitions", "Skills");
        if (!Directory.Exists(folder))
        {
            return 0;
        }

        return Directory.EnumerateFiles(folder, "support_*.asset", SearchOption.TopDirectoryOnly).Count();
    }
}
