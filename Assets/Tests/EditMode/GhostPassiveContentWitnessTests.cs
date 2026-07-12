using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using SM.Combat.Model;
using SM.Combat.Services;
using SM.Core.Contracts;
using SM.Editor.SeedData;
using SM.Meta;
using SM.Meta.Model;
using SM.Meta.Services;
using SM.Persistence.Abstractions.Models;
using SM.Unity;

namespace SM.Tests.EditMode;

/// <summary>
/// 유령 패시브 12종의 획득 경로(PoE식 패시브 보드 도달 보상, passive-granted-skill.v1) 실 콘텐츠 witness.
/// 과거 이 12종은 payload 전무 + 획득 경로 0의 이중 유령이었다 — 여기서는 실 committed asset 쌍으로
/// (a) 호스트 노드 grant 지정, (b) 부여 스킬의 sim-effective payload, (c) 5노드 예산 내 합법 도달,
/// (d) 컴파일 채널 도달, (e) 실 BattleSimulator 랜딩까지 전 사슬을 단언한다.
/// 설계 SoT: pindoc analysis-ghost-passive-board-placement-v1.
/// </summary>
[Category("BatchOnly")]
public sealed class GhostPassiveContentWitnessTests
{
    private static readonly (string NodeId, string SkillId)[] HostGrants =
    {
        ("passive_vanguard_notable_01", "skill_iron_hide_memory"),
        ("passive_vanguard_notable_02", "skill_sentinel_oath"),
        ("passive_vanguard_notable_05", "skill_lattice_bastion"),
        ("passive_duelist_notable_01", "skill_shard_memory"),
        ("passive_duelist_notable_02", "skill_edge_of_sentence"),
        ("passive_duelist_notable_05", "skill_bloodless_form"),
        ("passive_ranger_notable_01", "skill_prism_sight"),
        ("passive_ranger_notable_02", "skill_quick_kindling"),
        ("passive_ranger_notable_04", "skill_heat_haze"),
        ("passive_ranger_notable_05", "skill_glass_pathfinder"),
        ("passive_mystic_notable_01", "skill_echo_archive"),
        ("passive_mystic_notable_04", "skill_lattice_listener"),
    };

    [SetUp]
    public void SetUp()
    {
        SampleSeedGenerator.RequireCanonicalSampleContentReady(nameof(GhostPassiveContentWitnessTests));
    }

    [Test]
    public void RealHostNodes_CarryGrants_AndGrantedSkillsAreSimEffective()
    {
        var snapshot = new RuntimeCombatContentLookup().Snapshot;
        foreach (var (nodeId, skillId) in HostGrants)
        {
            Assert.That(snapshot.PassiveNodes.ContainsKey(nodeId), Is.True, $"{nodeId} 노드 존재");
            Assert.That(snapshot.PassiveNodes[nodeId].GrantedSkillId, Is.EqualTo(skillId),
                $"{nodeId}는 {skillId}를 부여해야 한다 — 유령 패시브 획득 경로의 실 asset 계약");
            Assert.That(snapshot.SkillCatalog.ContainsKey(skillId), Is.True, $"{skillId} 카탈로그 존재");

            var skill = snapshot.SkillCatalog[skillId];
            var effective = (skill.TriggeredEffects?.Count ?? 0) > 0 || skill.SupportModifier != null;
            Assert.That(effective, Is.True,
                $"{skillId}는 sim-effective payload(TriggeredEffects 또는 SupportModifier)를 가져야 한다 — " +
                "빈 껍데기 유령 회귀 방지 계약");
        }
    }

    [Test]
    public void RealHostNodes_AreReachable_WithinFiveNodeBudget()
    {
        // 도달 비용(선행 closure 크기)이 선택 예산(5)을 넘으면 grant는 죽은 콘텐츠다 —
        // notable 06~08/keystone처럼 수학적으로 도달 불능인 자리에 배치되는 회귀를 차단한다.
        var snapshot = new RuntimeCombatContentLookup().Snapshot;
        foreach (var (nodeId, skillId) in HostGrants)
        {
            var closure = BuildPrerequisiteClosure(snapshot, nodeId);
            Assert.That(closure.Count, Is.LessThanOrEqualTo(PassiveBoardSelectionValidator.MaxActiveNodeCount),
                $"{nodeId}({skillId})의 도달 비용({closure.Count})이 선택 예산({PassiveBoardSelectionValidator.MaxActiveNodeCount})을 넘으면 안 된다");

            var boardId = snapshot.PassiveNodes[nodeId].BoardId;
            var boardNodes = snapshot.PassiveNodes.Values
                .Where(node => node.BoardId == boardId)
                .ToDictionary(node => node.Id, StringComparer.Ordinal);
            var normalized = PassiveBoardSelectionValidator.Normalize(boardId, closure, boardNodes);
            Assert.That(normalized.NormalizedNodeIds, Does.Contain(nodeId),
                $"{nodeId}의 선행 closure 전체 선택은 합법이어야 한다(선행/예산/키스톤 규칙 통과)");
        }
    }

    [Test]
    public void RealCompile_SentinelOathChain_DeliversGuardedTrigger_AndLandsInRealSim()
    {
        var snapshot = new RuntimeCombatContentLookup().Snapshot;
        var chain = BuildPrerequisiteClosure(snapshot, "passive_vanguard_notable_02");
        var compiled = CompileHero(snapshot, "warden", chain);
        var unit = compiled.Allies.Single();

        Assert.That(
            unit.EffectiveTriggeredEffects.Any(effect =>
                effect.SourceId == "skill_sentinel_oath"
                && effect.Trigger == CombatTriggerKind.BattleStart
                && effect.Op == TriggeredEffectOp.ApplyStatus
                && effect.StatusId == "guarded"),
            Is.True,
            "노드 도달로 부여된 맹세의 무게(개전 guarded 8s)가 유닛 트리거 채널에 도달해야 한다");

        var enemy = CombatTestFactory.CreateUnit(
            "enemy.dummy",
            race: "undead",
            classId: "vanguard",
            anchor: DeploymentAnchorId.FrontBottom,
            hp: 400f,
            attack: 2f);
        var state = CombatTestFactory.CreateBattleState(
            compiled.Allies,
            new[] { enemy },
            seed: 11,
            statusRules: CombatStatusRuleCompiler.Compile(snapshot));
        var simulator = new BattleSimulator(state, 900);

        var guardedLanded = false;
        var guard = 0;
        while (!simulator.IsFinished && guard++ < 60)
        {
            simulator.Step();
            if (state.Allies.Any(ally => ally.HasStatus("guarded")))
            {
                guardedLanded = true;
                break;
            }
        }

        Assert.That(guardedLanded, Is.True,
            "실 전투에서 개전 직후 guarded가 자신에게 랜딩해야 한다 — 노드 도달→부여→발화→상태적용 전 사슬의 실 콘텐츠 witness");
    }

    [Test]
    public void RealCompile_PrismSightGrant_TransformsHunterCore()
    {
        var snapshot = new RuntimeCombatContentLookup().Snapshot;
        var baseline = CompileHero(snapshot, "hunter", Array.Empty<string>());
        var baselineCore = baseline.Allies.Single().Skills.Single(skill => skill.Id == "skill_precision_shot");

        var chain = BuildPrerequisiteClosure(snapshot, "passive_ranger_notable_01");
        var granted = CompileHero(snapshot, "hunter", chain);
        var grantedCore = granted.Allies.Single().Skills.Single(skill => skill.Id == "skill_precision_shot");

        Assert.That(grantedCore.Range, Is.EqualTo(baselineCore.Range + 0.5f).Within(0.001f),
            "프리즘 시야(노드 부여 젬)가 hunter 코어(projectile)의 사거리를 +0.5 연장해야 한다");
        Assert.That(grantedCore.CanCrit, Is.True,
            "프리즘 시야는 매칭 액티브에 치명타 허용을 강제해야 한다");
        Assert.That(granted.Allies.Single().Skills.Any(skill => skill.Id == "skill_prism_sight"), Is.False,
            "부여 젬은 스킬 슬롯을 차지하지 않아야 한다(4슬롯 계약 무접촉)");
    }

    [Test]
    public void RealCompile_EchoArchiveGrant_ExtendsHexerStatusDuration()
    {
        var snapshot = new RuntimeCombatContentLookup().Snapshot;
        var baseline = CompileHero(snapshot, "hexer", Array.Empty<string>());
        var baselineBurn = FindStatus(baseline, "skill_hexer_core", "burn");

        var chain = BuildPrerequisiteClosure(snapshot, "passive_mystic_notable_01");
        var granted = CompileHero(snapshot, "hexer", chain);
        var grantedBurn = FindStatus(granted, "skill_hexer_core", "burn");

        Assert.That(grantedBurn.DurationSeconds, Is.EqualTo(baselineBurn.DurationSeconds * 1.3f).Within(0.001f),
            "반향 기록고(노드 부여 젬)가 hexer 코어의 상태이상(burn) 지속을 ×1.3 연장해야 한다");
    }

    private static StatusApplicationSpec FindStatus(BattleLoadoutSnapshot compiled, string skillId, string statusId)
    {
        var skill = compiled.Allies.Single().Skills.Single(entry => entry.Id == skillId);
        return (skill.AppliedStatuses ?? Array.Empty<StatusApplicationSpec>()).Single(status => status.StatusId == statusId);
    }

    /// <summary>호스트 노드 + 선행 전체(transitive) — 도달 비용 계산과 합법 선택 구성의 공용 closure.</summary>
    private static IReadOnlyList<string> BuildPrerequisiteClosure(CombatContentSnapshot snapshot, string nodeId)
    {
        var closure = new List<string>();
        var visited = new HashSet<string>(StringComparer.Ordinal);
        void Visit(string id)
        {
            if (!visited.Add(id) || !snapshot.PassiveNodes.TryGetValue(id, out var node))
            {
                return;
            }

            foreach (var prerequisite in node.PrerequisiteNodeIds ?? Array.Empty<string>())
            {
                Visit(prerequisite);
            }

            closure.Add(id);
        }

        Visit(nodeId);
        return closure;
    }

    private static BattleLoadoutSnapshot CompileHero(
        CombatContentSnapshot content,
        string archetypeId,
        IReadOnlyList<string> selectedNodeIds)
    {
        var archetype = content.Archetypes[archetypeId];
        var boardId = $"board_{archetype.ClassId}";
        var heroes = new List<HeroRecord>
        {
            new("hero.witness", "hero.witness", archetype.Id, archetype.RaceId, archetype.ClassId, string.Empty, string.Empty),
        };
        var heroLoadouts = new Dictionary<string, HeroLoadoutState>(StringComparer.Ordinal)
        {
            ["hero.witness"] = new(
                "hero.witness",
                Array.Empty<string>(),
                Array.Empty<string>(),
                boardId,
                Array.Empty<string>(),
                Array.Empty<string>()),
        };
        var heroProgressions = new Dictionary<string, HeroProgressionState>(StringComparer.Ordinal)
        {
            ["hero.witness"] = new("hero.witness", 1, 0, Array.Empty<string>(), archetype.Skills.Select(skill => skill.Id).ToList()),
        };
        var passiveSelections = new Dictionary<string, PassiveBoardSelectionState>(StringComparer.Ordinal);
        if (selectedNodeIds.Count > 0)
        {
            passiveSelections["hero.witness"] = new PassiveBoardSelectionState("hero.witness", boardId, selectedNodeIds);
        }

        return new LoadoutCompiler().Compile(
            heroes,
            heroLoadouts,
            heroProgressions,
            new Dictionary<string, ItemInstanceState>(StringComparer.Ordinal),
            new Dictionary<string, SkillInstanceState>(StringComparer.Ordinal),
            passiveSelections,
            new PermanentAugmentLoadoutState("bp.ghost_witness", Array.Empty<string>()),
            new SquadBlueprintState(
                "bp.ghost_witness",
                "bp.ghost_witness",
                TeamPostureType.StandardAdvance,
                "team_tactic_standard_advance",
                new Dictionary<DeploymentAnchorId, string> { [DeploymentAnchorId.FrontCenter] = "hero.witness" },
                new[] { "hero.witness" },
                new Dictionary<string, string>(StringComparer.Ordinal),
                null),
            new RunOverlayState(0, Array.Empty<string>(), Array.Empty<string>(), LoadoutCompiler.CurrentCompileVersion, string.Empty),
            content);
    }
}
