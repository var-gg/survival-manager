using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using SM.Combat.Model;
using SM.Core.Content;
using SM.Meta.Model;
using SM.Tests.EditMode.Fakes;
using SM.Unity;
using SM.Unity.UI.Expedition;

namespace SM.Tests.EditMode;

[Category("FastUnit")]
public sealed class ExpeditionEncounterPreviewBuilderFastTests
{
    [Test]
    public void TryBuild_SurfacesBossOverlayAndExtraActorNames()
    {
        var node = new ExpeditionNodeViewModel(
            3,
            "site_worldscar_depths_boss_1",
            "label",
            "reward",
            "description",
            true,
            ExpeditionNodeEffectKind.None,
            0,
            "reward_source_boss",
            Array.Empty<int>());
        var snapshot = CreateSnapshot();

        Assert.That(
            ExpeditionEncounterPreviewBuilder.TryBuild(
                snapshot,
                node,
                ResolveCharacterName,
                ResolveArchetypeName,
                out var preview),
            Is.True);

        Assert.That(preview.Kind, Is.EqualTo(EncounterKindValue.Boss));
        Assert.That(preview.BossOverlayName, Is.EqualTo("Worldscar Law"));
        Assert.That(preview.EnemyNames, Does.Contain("Captain: Baekgyu (Hexer)"));
        Assert.That(preview.EnemyNames, Does.Contain("Escort: Worldscar Archive Cell (Mirror Cantor)"));
        Assert.That(preview.RewardDropTags, Is.SupersetOf(new[] { "reward_source_boss", "answer_lane_adaptive_mastery", "boss_ask_final_record" }));
    }

    [Test]
    public void TryBuild_SkipsSettlementNode()
    {
        var node = new ExpeditionNodeViewModel(
            4,
            "site_worldscar_depths:extract",
            "label",
            "reward",
            "description",
            false,
            ExpeditionNodeEffectKind.None,
            0,
            string.Empty,
            Array.Empty<int>());

        Assert.That(
            ExpeditionEncounterPreviewBuilder.TryBuild(
                CreateSnapshot(),
                node,
                ResolveCharacterName,
                ResolveArchetypeName,
                out _),
            Is.False);
    }

    private static CombatContentSnapshot CreateSnapshot()
    {
        return EditorFreeCombatContentFixture.CreateSnapshot(
            encounters: new Dictionary<string, EncounterTemplate>(StringComparer.Ordinal)
            {
                ["site_worldscar_depths_boss_1"] = new(
                    "site_worldscar_depths_boss_1",
                    "Worldscar Depths Boss",
                    "site_worldscar_depths",
                    "site_worldscar_depths_boss_1_squad",
                    "boss_overlay_worldscar_depths",
                    "reward_source_boss",
                    "faction_worldscar_depths",
                    3,
                    3,
                    3,
                    "site_boss",
                    EncounterKindValue.Boss,
                    new[] { "reward_source_boss", "answer_lane_adaptive_mastery" }),
            },
            enemySquads: new Dictionary<string, EnemySquadTemplate>(StringComparer.Ordinal)
            {
                ["site_worldscar_depths_boss_1_squad"] = new(
                    "site_worldscar_depths_boss_1_squad",
                    "Worldscar Boss Squad",
                    "faction_worldscar_depths",
                    TeamPostureType.ProtectCarry,
                    3,
                    3,
                    new[] { "faction_worldscar_depths", "boss" },
                    new[]
                    {
                        Member("captain", "hexer", "npc_baekgyu_sternheim", DeploymentAnchorId.FrontCenter, EnemySquadMemberRoleValue.Captain),
                        Member("escort_1", "mirror_cantor", "extra_worldscar_archive_cell", DeploymentAnchorId.BackTop, EnemySquadMemberRoleValue.Escort),
                        Member("escort_2", "guardian", "extra_worldscar_record_bailiff", DeploymentAnchorId.BackBottom, EnemySquadMemberRoleValue.Escort),
                    }),
            },
            bossOverlays: new Dictionary<string, BossOverlayTemplate>(StringComparer.Ordinal)
            {
                ["boss_overlay_worldscar_depths"] = new(
                    "boss_overlay_worldscar_depths",
                    "Worldscar Law",
                    BossPhaseTriggerValue.HealthBelowHalf,
                    3,
                    "boss_aura_worldscar_law",
                    "boss_utility_final_record",
                    new[] { "boss_ask_final_record" },
                    Array.Empty<StatusApplicationSpec>()),
            });
    }

    private static EnemySquadMemberTemplate Member(
        string id,
        string archetypeId,
        string characterId,
        DeploymentAnchorId anchor,
        EnemySquadMemberRoleValue role)
    {
        return new EnemySquadMemberTemplate(
            id,
            characterId,
            archetypeId,
            characterId,
            anchor,
            string.Empty,
            string.Empty,
            role,
            Array.Empty<string>());
    }

    private static string ResolveCharacterName(string characterId, string fallbackArchetypeId)
    {
        return characterId switch
        {
            "npc_baekgyu_sternheim" => "Baekgyu",
            "extra_worldscar_archive_cell" => "Worldscar Archive Cell",
            "extra_worldscar_record_bailiff" => "Worldscar Record Bailiff",
            _ => fallbackArchetypeId,
        };
    }

    private static string ResolveArchetypeName(string archetypeId)
    {
        return string.Join(
            " ",
            archetypeId
                .Split('_', StringSplitOptions.RemoveEmptyEntries)
                .Select(word => char.ToUpperInvariant(word[0]) + word[1..]));
    }
}
