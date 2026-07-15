using System;
using System.Linq;
using NUnit.Framework;
using SM.Combat.Model;
using SM.Combat.Services;
using SM.Core.Stats;
using SM.Meta.Model;
using SM.Meta.Services;
using SM.Persistence.Abstractions.Models;
using SM.Unity;

namespace SM.Tests.EditMode;

[Category("BatchOnly")]
public sealed class BattleSetupBuilderTests
{
    [Test]
    public void BuildBattleSetup_UsesDeploymentAnchors_FromSessionWithoutSceneTables()
    {
        var lookup = new RuntimeCombatContentLookup();
        var session = new GameSessionState(lookup);
        session.BindProfile(new SaveProfile());

        var heroA = session.ExpeditionSquadHeroIds[0];
        var heroB = session.ExpeditionSquadHeroIds[1];
        Assert.That(session.AssignHeroToAnchor(DeploymentAnchorId.BackBottom, heroA), Is.True);
        Assert.That(session.AssignHeroToAnchor(DeploymentAnchorId.FrontCenter, heroB), Is.True);

        var allies = session.BuildBattleParticipants();
        Assert.That(allies.First(spec => spec.ParticipantId == heroA).Anchor, Is.EqualTo(DeploymentAnchorId.BackBottom));
        Assert.That(allies.First(spec => spec.ParticipantId == heroB).Anchor, Is.EqualTo(DeploymentAnchorId.FrontCenter));

        Assert.That(lookup.TryGetCombatSnapshot(out var snapshot, out var error), Is.True, error);
        var result = BattleSetupBuilder.Build(allies, new BattleEncounterPlan(Array.Empty<BattleParticipantSpec>(), TeamPostureType.StandardAdvance), snapshot);

        Assert.That(result.IsSuccess, Is.True, result.Error);
        Assert.That(result.StatusRules, Is.Not.Null);
        Assert.That(result.StatusRules!.AppliesPeriodicDamage("burn"), Is.True);
        Assert.That(result.Allies.First(definition => definition.Id == heroA).PreferredAnchor, Is.EqualTo(DeploymentAnchorId.BackBottom));
        Assert.That(result.Allies.First(definition => definition.Id == heroB).PreferredAnchor, Is.EqualTo(DeploymentAnchorId.FrontCenter));
    }

    [Test]
    public void EnemyLane_FiresAuthoredSynergyTiers_LikeAllyCompileLane()
    {
        // 적군 시너지 대칭 배선 witness(오너 게이트② 채택, 2026-07-12) — 과거 이 레인은 CompileTags/
        // TeamPackages를 null로 구워 authored 시너지 tier가 적군에 영원히 미적용(V1 하드코딩 폴백만)이었다.
        // 특정 id 하드코딩 없이 실 카탈로그에서 "아키타입 race/class와 일치하는 CountedTagId tier"를
        // 동적으로 찾아, threshold 수만큼 같은 아키타입 comp을 굽고 tier 패키지 발화를 단언한다.
        var lookup = new RuntimeCombatContentLookup();
        Assert.That(lookup.TryGetCombatSnapshot(out var snapshot, out var error), Is.True, error);

        var archetypes = snapshot.Archetypes.Values.ToList();
        var match = snapshot.SynergyCatalog.Values
            .Select(template => template.Rule)
            .Select(rule => (Rule: rule, Archetype: archetypes.FirstOrDefault(archetype =>
                archetype.RaceId == rule.CountedTagId || archetype.ClassId == rule.CountedTagId)))
            .FirstOrDefault(pair => pair.Archetype != null);
        Assert.That(match.Archetype, Is.Not.Null,
            "실 콘텐츠에 아키타입 race/class와 일치하는 CountedTagId tier가 최소 하나 존재해야 한다(시너지 카탈로그 계약)");

        var enemies = Enumerable.Range(0, match.Rule.Threshold)
            .Select(index => new BattleParticipantSpec(
                $"enemy_synergy_{index}",
                $"Synergy Probe {index}",
                match.Archetype!.Id,
                DeploymentAnchorId.FrontCenter,
                string.Empty,
                string.Empty,
                Array.Empty<BattleEquippedItemSpec>(),
                Array.Empty<string>()))
            .ToList();
        var result = BattleSetupBuilder.Build(
            Array.Empty<BattleParticipantSpec>(),
            new BattleEncounterPlan(enemies, TeamPostureType.StandardAdvance),
            snapshot);
        Assert.That(result.IsSuccess, Is.True, result.Error);

        var enemy = result.Enemies.First();
        Assert.That(enemy.CompileTags, Is.Not.Null.And.Not.Empty,
            "적군 유닛 CompileTags가 저작돼야 한다(null parity 해소) — 시너지 카운트의 소재");
        Assert.That(enemy.CompileTags, Does.Contain(match.Rule.CountedTagId),
            "적군 CompileTags에 tier가 세는 race/class 태그가 실려야 한다");
        Assert.That(enemy.TeamPackages, Is.Not.Null.And.Not.Empty,
            "적군 TeamPackages에 팀 평가 결과가 실려야 한다");
        Assert.That(
            enemy.TeamPackages!.Any(package => package.SourceId.StartsWith($"synergy:{match.Rule.SynergyId}", StringComparison.Ordinal)),
            Is.True,
            "적군 팀 패키지에 authored tier(synergy:*)가 발화해야 한다 — race:/class:* 폴백만 있으면 대칭 배선 회귀");
    }

    [Test]
    public void RealRaceFourComps_CarryUpperTierRules_ThroughCompiledPath()
    {
        // Move 4 make-or-break — 실 authored tier의 GrantedTeamRuleId는 아직 빈 값이다. SynergyService의
        // (CountedTagId, Threshold) 코드-SoT overlay와 CombatModifierPackage 전달 필드가 빠지면,
        // Default 폴백 테스트는 통과해도 실게임 BattleSetupBuilder 경로의 TeamRuleSet은 비게 된다.
        var lookup = new RuntimeCombatContentLookup();
        Assert.That(lookup.TryGetCombatSnapshot(out var snapshot, out var error), Is.True, error);

        foreach (var (raceId, expectedRuleId) in new[]
                 {
                     ("human", TeamRuleSet.PhalanxRuleId),
                     ("beastkin", TeamRuleSet.BloodrushRuleId),
                     ("undead", TeamRuleSet.DeathTollRuleId),
                 })
        {
            var tier = snapshot.SynergyCatalog.Values
                .Select(template => template.Rule)
                .Single(rule => rule.CountedTagId == raceId && rule.Threshold == 4);
            var archetypes = snapshot.Archetypes.Values
                .Where(candidate => candidate.RaceId == raceId)
                .OrderBy(candidate => candidate.Id, StringComparer.Ordinal)
                .Take(tier.Threshold)
                .ToList();
            Assert.That(archetypes, Has.Count.EqualTo(tier.Threshold),
                $"실 {raceId} roster가 race@4 witness에 필요한 {tier.Threshold}개 아키타입을 제공해야 한다");
            var allies = Enumerable.Range(0, tier.Threshold)
                .Select(index => new BattleParticipantSpec(
                    $"{raceId}_rule_witness_{index}",
                    $"{raceId} Rule Witness {index}",
                    archetypes[index].Id,
                    DeploymentAnchorId.FrontCenter,
                    string.Empty,
                    string.Empty,
                    Array.Empty<BattleEquippedItemSpec>(),
                    Array.Empty<string>()))
                .ToList();
            var build = BattleSetupBuilder.Build(
                allies,
                new BattleEncounterPlan(Array.Empty<BattleParticipantSpec>(), TeamPostureType.StandardAdvance),
                snapshot);
            Assert.That(build.IsSuccess, Is.True, build.Error);

            var state = BattleFactory.Create(build.Allies, build.Enemies, statusRules: build.StatusRules);

            Assert.That(build.Allies[0].TeamPackages!.Any(package => package.GrantedTeamRuleId == expectedRuleId), Is.True,
                $"{raceId}@4 authored tier가 compiled package에 {expectedRuleId}를 실어야 한다");
            Assert.That(state.TeamRuleSet.Has(TeamSide.Ally, expectedRuleId), Is.True,
                $"{raceId}@4 실 콘텐츠 comp이 BattleState.TeamRuleSet에 {expectedRuleId}를 활성화해야 한다");
        }
    }

    [Test]
    public void RealClassThreeComps_CarryUpperTierRules_ThroughCompiledPath()
    {
        // class@3 make-or-break — 실 asset의 GrantedTeamRuleId는 비어 있으므로 (CountedTagId, Threshold)
        // overlay가 SynergyLoadoutService authored lane을 통과하지 못하면 이 witness에서 바로 0으로 추락한다.
        var lookup = new RuntimeCombatContentLookup();
        Assert.That(lookup.TryGetCombatSnapshot(out var snapshot, out var error), Is.True, error);

        foreach (var (classId, expectedRuleId) in new[]
                 {
                     ("vanguard", TeamRuleSet.BulwarkRuleId),
                     ("duelist", TeamRuleSet.ExecuteRuleId),
                     ("ranger", TeamRuleSet.KillzoneRuleId),
                     ("mystic", TeamRuleSet.ResonanceRuleId),
                 })
        {
            var tier = snapshot.SynergyCatalog.Values
                .Select(template => template.Rule)
                .Single(rule => rule.CountedTagId == classId && rule.Threshold == 3);
            var archetypes = snapshot.Archetypes.Values
                .Where(candidate => candidate.ClassId == classId)
                .OrderBy(candidate => candidate.Id, StringComparer.Ordinal)
                .Take(tier.Threshold)
                .ToList();
            Assert.That(archetypes, Has.Count.EqualTo(tier.Threshold),
                $"실 {classId} roster가 class@3 witness에 필요한 {tier.Threshold}개 아키타입을 제공해야 한다");
            var allies = Enumerable.Range(0, tier.Threshold)
                .Select(index => new BattleParticipantSpec(
                    $"{classId}_rule_witness_{index}",
                    $"{classId} Rule Witness {index}",
                    archetypes[index].Id,
                    DeploymentAnchorId.FrontCenter,
                    string.Empty,
                    string.Empty,
                    Array.Empty<BattleEquippedItemSpec>(),
                    Array.Empty<string>()))
                .ToList();
            var build = BattleSetupBuilder.Build(
                allies,
                new BattleEncounterPlan(Array.Empty<BattleParticipantSpec>(), TeamPostureType.StandardAdvance),
                snapshot);
            Assert.That(build.IsSuccess, Is.True, build.Error);

            var state = BattleFactory.Create(build.Allies, build.Enemies, statusRules: build.StatusRules);

            Assert.That(build.Allies[0].TeamPackages!.Any(package => package.GrantedTeamRuleId == expectedRuleId), Is.True,
                $"{classId}@3 authored tier가 compiled package에 {expectedRuleId}를 실어야 한다");
            Assert.That(state.TeamRuleSet.Has(TeamSide.Ally, expectedRuleId), Is.True,
                $"{classId}@3 실 콘텐츠 comp이 BattleState.TeamRuleSet에 {expectedRuleId}를 활성화해야 한다");
        }
    }

    [Test]
    public void SameArchetypeHeroes_WithDifferentTraitItemAndAugmentInputs_EndWithDifferentStats()
    {
        var lookup = new RuntimeCombatContentLookup();
        Assert.That(lookup.TryGetCombatSnapshot(out var snapshot, out var error), Is.True, error);

        var archetypeId = lookup.GetCanonicalArchetypeIds().First();
        Assert.That(lookup.TryGetTraitIds(archetypeId, out var positiveTraits, out var negativeTraits), Is.True);
        var itemId = lookup.GetCanonicalItemIds().First();
        var affixId = lookup.GetCanonicalAffixIds().First();
        var augmentId = lookup.GetCanonicalTemporaryAugmentIds().First();

        var allies = new[]
        {
            new BattleParticipantSpec(
                "ally_base",
                "Base",
                archetypeId,
                DeploymentAnchorId.FrontCenter,
                positiveTraits[0],
                negativeTraits[0],
                Array.Empty<BattleEquippedItemSpec>(),
                Array.Empty<string>()),
            new BattleParticipantSpec(
                "ally_variant",
                "Variant",
                archetypeId,
                DeploymentAnchorId.BackCenter,
                positiveTraits[Math.Min(1, positiveTraits.Count - 1)],
                negativeTraits[Math.Min(1, negativeTraits.Count - 1)],
                new[]
                {
                    new BattleEquippedItemSpec(itemId, new[] { affixId })
                },
                new[] { augmentId })
        };

        var result = BattleSetupBuilder.Build(allies, new BattleEncounterPlan(Array.Empty<BattleParticipantSpec>(), TeamPostureType.StandardAdvance), snapshot);
        Assert.That(result.IsSuccess, Is.True, result.Error);

        var state = BattleFactory.Create(result.Allies, result.Enemies, statusRules: result.StatusRules);
        var baseUnit = state.Allies.First(unit => unit.Definition.Id == "ally_base");
        var variantUnit = state.Allies.First(unit => unit.Definition.Id == "ally_variant");

        Assert.That(result.Allies[1].Packages!.Any(package => package.Source == ModifierSource.Item), Is.True);
        Assert.That(result.Allies[1].Packages!.Any(package => package.Source == ModifierSource.Augment), Is.True);
        Assert.That(result.Allies[1].Packages!.Any(package => package.Source == ModifierSource.Trait), Is.True);
        Assert.That(variantUnit.Attack + variantUnit.Defense + variantUnit.Speed + variantUnit.MaxHealth,
            Is.Not.EqualTo(baseUnit.Attack + baseUnit.Defense + baseUnit.Speed + baseUnit.MaxHealth).Within(0.01f));
    }
}
