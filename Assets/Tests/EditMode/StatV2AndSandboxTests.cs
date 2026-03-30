using System.Collections.Generic;
using NUnit.Framework;
using SM.Combat.Model;
using SM.Combat.Services;
using SM.Core.Stats;
using SM.Editor.Authoring.CombatSandbox;
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
        Assert.That(ContentDefinitionValidator.GetStatIdStatus("attack"), Is.EqualTo(StatIdValidationStatus.LegacyAlias));
        Assert.That(ContentDefinitionValidator.GetStatIdStatus("phys_power"), Is.EqualTo(StatIdValidationStatus.Canonical));
        Assert.That(ContentDefinitionValidator.GetStatIdStatus("unsupported_stat"), Is.EqualTo(StatIdValidationStatus.Unsupported));
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
}
