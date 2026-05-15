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

    private static CombatContentSnapshot CreateSnapshot()
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
                            Array.Empty<string>()),
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
