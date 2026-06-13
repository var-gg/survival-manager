using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using SM.Combat.Model;
using SM.Core.Content;
using SM.Core.Stats;
using SM.Meta.Model;
using SM.Meta.Services;

namespace SM.Tests.EditMode;

[Category("FastUnit")]
public sealed class EncounterResolutionServiceFastTests
{
    [Test]
    public void TryResolveEncounter_PreservesEnemySquadMemberCharacterId()
    {
        var snapshot = CreateSnapshot();
        var resolver = new EncounterResolutionService(snapshot);
        var context = new BattleContextState(
            "chapter_test",
            "site_test",
            0,
            "encounter_test",
            123,
            "hash:test",
            "reward_source_test",
            1,
            false,
            "faction_wolfpine",
            string.Empty);

        Assert.That(resolver.TryResolveEncounter(context, out var resolved, out var error), Is.True, error);

        var enemy = resolved.Enemies.Single();
        Assert.That(enemy.ArchetypeId, Is.EqualTo("reaver"));
        Assert.That(enemy.CharacterId, Is.EqualTo("npc_grey_fang"));
    }

    [Test]
    public void TryResolveEncounter_CompilesEnemySquadMemberRuleModifierTags()
    {
        var snapshot = CreateSnapshot(new[] { "threat:flanker", "threat:ambusher" });
        var resolver = new EncounterResolutionService(snapshot);
        var context = new BattleContextState(
            "chapter_test",
            "site_test",
            0,
            "encounter_test",
            123,
            "hash:test",
            "reward_source_test",
            1,
            false,
            "faction_wolfpine",
            string.Empty);

        Assert.That(resolver.TryResolveEncounter(context, out var resolved, out var error), Is.True, error);

        var enemy = resolved.Enemies.Single();
        var participantPackage = enemy.RulePackages?.Single(package => package.SourceId == "participant:enemy_grey_fang");
        Assert.That(participantPackage, Is.Not.Null);
        Assert.That(participantPackage!.Source, Is.EqualTo(ModifierSource.Other));
        Assert.That(
            participantPackage.Modifiers.Select(modifier => $"{modifier.Kind}:{modifier.Value}"),
            Is.EquivalentTo(new[] { "BehaviorTag:threat:flanker", "BehaviorTag:threat:ambusher" }));
    }

    [Test]
    public void BuildBattleContextFromPayload_AuthoredEncounter_PreservesIdentityAndHash()
    {
        var snapshot = CreateSnapshot();
        var resolver = new EncounterResolutionService(snapshot);
        var run = CreateRun();
        var payload = new RunBattlePayload(
            RunId: run.RunId,
            ChapterId: "chapter_test",
            SiteId: "site_test",
            SiteNodeIndex: 0,
            EncounterId: "encounter_test",
            ExpeditionNodeId: "expedition_node_0",
            SquadSnapshotId: "squad_snapshot_1",
            StageCandidatePathHash: "spch_a",
            NodeOverlayHash: "noh_b",
            BattleContextHash: "deadbeef1234567890abcdef1234567890abcdef1234567890abcdef12345678",
            RewardBiasPercent: 15,
            ThreatPressurePercent: 5,
            AffinityBoostPercent: 10,
            ResolvedModifierIds: new[] { "RewardBias:reward" });

        var context = resolver.BuildBattleContextFromPayload(run, payload);

        // payload.EncounterId가 catalog에 있으므로 직접 lookup; debug_smoke_observer로 fallback 금지.
        Assert.That(context.EncounterId, Is.EqualTo("encounter_test"));
        Assert.That(context.ChapterId, Is.EqualTo("chapter_test"));
        Assert.That(context.SiteId, Is.EqualTo("site_test"));
        Assert.That(context.SiteNodeIndex, Is.EqualTo(0));
        // payload.BattleContextHash가 그대로 보존된다 (Atlas hash → Battle context 운반).
        Assert.That(context.BattleContextHash, Is.EqualTo("deadbeef1234567890abcdef1234567890abcdef1234567890abcdef12345678"));
        // Encounter content가 reward/faction/kind를 채운다.
        Assert.That(context.RewardSourceId, Is.EqualTo("reward_source_test"));
        Assert.That(context.FactionId, Is.EqualTo("faction_wolfpine"));
        Assert.That(context.IsBoss, Is.False);
    }

    [Test]
    public void BuildBattleContextFromPayload_TryResolveEncounter_PreservesAuthoredCharacterId()
    {
        // task-battle-entry-authored-node-v1 acceptance #2: payload entry path가 authored V2
        // CharacterId (예: npc_*, extra_*)를 보존한 채 BattleParticipantSpec까지 전달해야 한다.
        var snapshot = CreateSnapshot();
        var resolver = new EncounterResolutionService(snapshot);
        var run = CreateRun();
        var payload = new RunBattlePayload(
            RunId: run.RunId,
            ChapterId: "chapter_test",
            SiteId: "site_test",
            SiteNodeIndex: 0,
            EncounterId: "encounter_test",
            ExpeditionNodeId: "node_0",
            SquadSnapshotId: "snap_1",
            StageCandidatePathHash: "spch",
            NodeOverlayHash: "noh",
            BattleContextHash: "deadbeef1234567890abcdef1234567890abcdef1234567890abcdef12345678",
            RewardBiasPercent: 0,
            ThreatPressurePercent: 0,
            AffinityBoostPercent: 0,
            ResolvedModifierIds: Array.Empty<string>());

        var context = resolver.BuildBattleContextFromPayload(run, payload);
        Assert.That(resolver.TryResolveEncounter(context, out var resolved, out var error), Is.True, error);

        var enemy = resolved.Enemies.Single();
        Assert.That(enemy.ArchetypeId, Is.EqualTo("reaver"));
        Assert.That(enemy.CharacterId, Is.EqualTo("npc_grey_fang"),
            "RunBattlePayload entry path가 authored CharacterId를 BattleParticipantSpec까지 보존해야.");
    }

    [Test]
    public void BuildSiteTrack_ExtractNode_HasRequiresBattleFalse_RoutesCallerToReward()
    {
        // task-battle-entry-authored-node-v1 acceptance #5 caller 측 evidence.
        // BuildSiteTrack 결과의 extract node가 RequiresBattle=false라
        // AtlasScreenController.ContinueToExpedition이 GoToReward로 분기한다.
        var snapshot = CreateSnapshot();
        var resolver = new EncounterResolutionService(snapshot);

        var siteTrack = resolver.BuildSiteTrack("chapter_test", "site_test");
        Assert.That(siteTrack.Count, Is.GreaterThan(0));

        var extract = siteTrack[siteTrack.Count - 1];
        Assert.That(extract.EncounterId, Is.EqualTo("site_test:extract"),
            "마지막 node는 extract id pattern을 가진다.");
        Assert.That(extract.RequiresBattle, Is.False,
            "Extract node는 Battle scene 진입을 요구하지 않는다 (RequiresBattle=false).");
        Assert.That(extract.RewardSourceId, Is.EqualTo("reward_source_extract"),
            "Extract node는 site.ExtractRewardSourceId를 운반.");
    }

    [Test]
    public void BuildBattleContextFromPayload_ExtractNode_RoutesToSettlementSignal()
    {
        // task-battle-entry-authored-node-v1 acceptance #5: extract node payload는 Battle scene을
        // 열지 않고 settlement signal을 운반해야 한다. BuildBattleContextFromPayload가 IsBoss=false,
        // RewardSourceId=site.ExtractRewardSourceId, EncounterId=":extract" suffix 유지. caller는
        // EncounterId 또는 RewardSourceId로 GoToReward 분기.
        var snapshot = CreateSnapshot();
        var resolver = new EncounterResolutionService(snapshot);
        var run = CreateRun();
        var payload = new RunBattlePayload(
            RunId: run.RunId,
            ChapterId: "chapter_test",
            SiteId: "site_test",
            SiteNodeIndex: 4,
            EncounterId: "site_test:extract",
            ExpeditionNodeId: "extract_node",
            SquadSnapshotId: "snap_1",
            StageCandidatePathHash: "spch",
            NodeOverlayHash: "noh",
            BattleContextHash: "abcdef0123456789abcdef0123456789abcdef0123456789abcdef0123456789",
            RewardBiasPercent: 0,
            ThreatPressurePercent: 0,
            AffinityBoostPercent: 0,
            ResolvedModifierIds: Array.Empty<string>());

        Assert.That(payload.IsExtract, Is.True, "RunBattlePayload.IsExtract가 `:extract` suffix를 감지해야.");

        var context = resolver.BuildBattleContextFromPayload(run, payload);

        Assert.That(context.EncounterId, Is.EqualTo("site_test:extract"));
        Assert.That(context.RewardSourceId, Is.EqualTo("reward_source_extract"),
            "Extract context는 site.ExtractRewardSourceId를 운반해야.");
        Assert.That(context.IsBoss, Is.False);
        Assert.That(context.BattleContextHash, Is.EqualTo(payload.BattleContextHash),
            "payload hash는 그대로 보존.");
    }

    [Test]
    public void BuildBattleContextFromPayload_InvalidPayload_FallsBackToDebugSmoke()
    {
        var snapshot = CreateSnapshot();
        var resolver = new EncounterResolutionService(snapshot);
        var run = CreateRun();

        var context = resolver.BuildBattleContextFromPayload(run, RunBattlePayload.Empty);

        // 무효 payload는 normal authored path를 타지 않고 debug smoke로 fallback (safety).
        Assert.That(context.EncounterId, Is.EqualTo("debug_smoke_observer"));
        Assert.That(context.SiteId, Is.EqualTo("quick_smoke"));
    }

    private static ActiveRunState CreateRun()
    {
        return new ActiveRunState(
            RunId: "run_test_001",
            ExpeditionId: "expedition_test_001",
            Blueprint: new SquadBlueprintState(
                BlueprintId: "blueprint_test",
                DisplayName: "Blueprint Test",
                TeamPosture: TeamPostureType.StandardAdvance,
                TeamTacticId: "tactic_default",
                DeploymentAssignments: new Dictionary<DeploymentAnchorId, string>(),
                ExpeditionSquadHeroIds: Array.Empty<string>(),
                HeroRoleIds: new Dictionary<string, string>(StringComparer.Ordinal)),
            Overlay: new RunOverlayState(
                CurrentNodeIndex: 0,
                TemporaryAugmentIds: Array.Empty<string>(),
                PendingRewardIds: Array.Empty<string>(),
                CompileVersion: "v1",
                LastCompileHash: "hash_v1"),
            BattleDeployHeroIds: Array.Empty<string>(),
            IsQuickBattle: false);
    }

    private static CombatContentSnapshot CreateSnapshot(IReadOnlyList<string>? memberRuleModifierTags = null)
    {
        return new CombatContentSnapshot(
            Archetypes: new Dictionary<string, CombatArchetypeTemplate>(StringComparer.Ordinal)
            {
                ["reaver"] = new(
                    "reaver",
                    "Reaver",
                    "race_wolfpine",
                    "class_duelist",
                    DeploymentAnchorId.FrontTop,
                    new Dictionary<StatKey, float>(),
                    Array.Empty<TacticRule>(),
                    Array.Empty<BattleSkillSpec>()),
            },
            TraitPackages: EmptyPackages(),
            ItemPackages: EmptyPackages(),
            AffixPackages: EmptyPackages(),
            AugmentPackages: EmptyPackages(),
            SkillCatalog: new Dictionary<string, BattleSkillSpec>(StringComparer.Ordinal),
            TeamTactics: new Dictionary<string, TeamTacticTemplate>(StringComparer.Ordinal),
            RoleInstructions: new Dictionary<string, RoleInstructionTemplate>(StringComparer.Ordinal),
            PassiveNodes: new Dictionary<string, PassiveNodeTemplate>(StringComparer.Ordinal),
            AugmentCatalog: new Dictionary<string, AugmentCatalogEntry>(StringComparer.Ordinal),
            SynergyCatalog: new Dictionary<string, SynergyTierTemplate>(StringComparer.Ordinal),
            CampaignChapters: new Dictionary<string, CampaignChapterTemplate>(StringComparer.Ordinal)
            {
                ["chapter_test"] = new("chapter_test", "Chapter Test", 0, new[] { "site_test" }, false),
            },
            ExpeditionSites: new Dictionary<string, ExpeditionSiteTemplate>(StringComparer.Ordinal)
            {
                ["site_test"] = new(
                    "site_test",
                    "chapter_test",
                    "Site Test",
                    0,
                    "faction_wolfpine",
                    new[] { "encounter_test" },
                    "reward_source_extract",
                    1),
            },
            Encounters: new Dictionary<string, EncounterTemplate>(StringComparer.Ordinal)
            {
                ["encounter_test"] = new(
                    "encounter_test",
                    "Encounter Test",
                    "site_test",
                    "squad_test",
                    string.Empty,
                    "reward_source_test",
                    "faction_wolfpine",
                    1,
                    1,
                    1,
                    "test",
                    EncounterKindValue.Skirmish,
                    Array.Empty<string>()),
            },
            EnemySquads: new Dictionary<string, EnemySquadTemplate>(StringComparer.Ordinal)
            {
                ["squad_test"] = new(
                    "squad_test",
                    "Squad Test",
                    "faction_wolfpine",
                    TeamPostureType.StandardAdvance,
                    1,
                    1,
                    Array.Empty<string>(),
                    new[]
                    {
                        new EnemySquadMemberTemplate(
                            "enemy_grey_fang",
                            "Grey Fang",
                            "reaver",
                            "npc_grey_fang",
                            DeploymentAnchorId.FrontTop,
                            string.Empty,
                            string.Empty,
                            EnemySquadMemberRoleValue.Captain,
                            memberRuleModifierTags ?? Array.Empty<string>()),
                    }),
            },
            RewardSources: new Dictionary<string, RewardSourceTemplate>(StringComparer.Ordinal)
            {
                ["reward_source_test"] = new(
                    "reward_source_test",
                    "Reward Test",
                    RewardSourceKindValue.Skirmish,
                    "drop.test",
                    true,
                    new[] { RarityBracketValue.Common }),
                ["reward_source_extract"] = new(
                    "reward_source_extract",
                    "Extract Test",
                    RewardSourceKindValue.ExtractEndRun,
                    "drop.extract",
                    true,
                    new[] { RarityBracketValue.Common }),
            },
            Characters: new Dictionary<string, CharacterTemplate>(StringComparer.Ordinal)
            {
                ["npc_grey_fang"] = new(
                    "npc_grey_fang",
                    "race_wolfpine",
                    "class_duelist",
                    "reaver",
                    string.Empty),
            });
    }

    private static IReadOnlyDictionary<string, CombatModifierPackage> EmptyPackages() =>
        new Dictionary<string, CombatModifierPackage>(StringComparer.Ordinal);
}
