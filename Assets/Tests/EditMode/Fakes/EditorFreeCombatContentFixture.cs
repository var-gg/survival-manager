using System;
using System.Collections.Generic;
using SM.Combat.Model;
using SM.Core.Content;
using SM.Core.Stats;
using SM.Meta.Model;

namespace SM.Tests.EditMode.Fakes;

public static class EditorFreeCombatContentFixture
{
    public static FakeCombatContentLookup CreateRunLoopLookup()
    {
        var chapterAlpha = new CampaignChapterTemplate(
            "chapter_alpha",
            "chapter.alpha",
            0,
            new[] { "site_alpha_gate", "site_alpha_depths" },
            false);
        var chapterBeta = new CampaignChapterTemplate(
            "chapter_beta",
            "chapter.beta",
            1,
            new[] { "site_beta_watch" },
            true);
        var siteAlphaGate = CreateSite("site_alpha_gate", "chapter_alpha", 0);
        var siteAlphaDepths = CreateSite("site_alpha_depths", "chapter_alpha", 1);
        var siteBetaWatch = CreateSite("site_beta_watch", "chapter_beta", 0);

        var snapshot = CreateSnapshot(
            campaignChapters: new Dictionary<string, CampaignChapterTemplate>(StringComparer.Ordinal)
            {
                [chapterAlpha.Id] = chapterAlpha,
                [chapterBeta.Id] = chapterBeta,
            },
            expeditionSites: new Dictionary<string, ExpeditionSiteTemplate>(StringComparer.Ordinal)
            {
                [siteAlphaGate.Id] = siteAlphaGate,
                [siteAlphaDepths.Id] = siteAlphaDepths,
                [siteBetaWatch.Id] = siteBetaWatch,
            },
            encounters: BuildEncounterTemplates(siteAlphaGate, siteAlphaDepths, siteBetaWatch),
            enemySquads: new Dictionary<string, EnemySquadTemplate>(StringComparer.Ordinal)
            {
                ["enemy_squad_debug"] = new(
                    "enemy_squad_debug",
                    "Debug Squad",
                    "faction_debug",
                    TeamPostureType.StandardAdvance,
                    1,
                    1,
                    Array.Empty<string>(),
                    Array.Empty<EnemySquadMemberTemplate>()),
            },
            rewardSources: CreateRewardSources());

        return new FakeCombatContentLookup(snapshot: snapshot);
    }

    public static FakeCombatContentLookup CreateTownBuildLookup()
    {
        var firstPlayableSlice = new FirstPlayableSliceDefinition
        {
            AffixIds = new[] { "affix_a", "affix_b", "affix_c", "affix_d" },
            PassiveBoardIds = new[] { "board_vanguard" },
        };
        var passiveNodes = new Dictionary<string, PassiveNodeTemplate>(StringComparer.Ordinal)
        {
            ["node_1"] = CreatePassiveNode("node_1", "board_vanguard", 0),
            ["node_a"] = CreatePassiveNode("node_a", "board_vanguard", 1),
            ["node_b"] = CreatePassiveNode("node_b", "board_vanguard", 2),
        };
        var snapshot = CreateSnapshot(firstPlayableSlice: firstPlayableSlice, passiveNodes: passiveNodes);
        return new FakeCombatContentLookup(snapshot: snapshot, firstPlayableSlice: firstPlayableSlice);
    }

    public static CombatContentSnapshot CreateSnapshot(
        FirstPlayableSliceDefinition? firstPlayableSlice = null,
        IReadOnlyDictionary<string, PassiveNodeTemplate>? passiveNodes = null,
        IReadOnlyDictionary<string, CampaignChapterTemplate>? campaignChapters = null,
        IReadOnlyDictionary<string, ExpeditionSiteTemplate>? expeditionSites = null,
        IReadOnlyDictionary<string, EncounterTemplate>? encounters = null,
        IReadOnlyDictionary<string, EnemySquadTemplate>? enemySquads = null,
        IReadOnlyDictionary<string, RewardSourceTemplate>? rewardSources = null)
    {
        var emptyPackages = new Dictionary<string, CombatModifierPackage>(StringComparer.Ordinal);
        return new CombatContentSnapshot(
            Archetypes: new Dictionary<string, CombatArchetypeTemplate>(StringComparer.Ordinal),
            TraitPackages: emptyPackages,
            ItemPackages: new Dictionary<string, CombatModifierPackage>(StringComparer.Ordinal),
            AffixPackages: new Dictionary<string, CombatModifierPackage>(StringComparer.Ordinal),
            AugmentPackages: new Dictionary<string, CombatModifierPackage>(StringComparer.Ordinal),
            SkillCatalog: new Dictionary<string, BattleSkillSpec>(StringComparer.Ordinal),
            TeamTactics: new Dictionary<string, TeamTacticTemplate>(StringComparer.Ordinal),
            RoleInstructions: new Dictionary<string, RoleInstructionTemplate>(StringComparer.Ordinal),
            PassiveNodes: passiveNodes ?? new Dictionary<string, PassiveNodeTemplate>(StringComparer.Ordinal),
            AugmentCatalog: new Dictionary<string, AugmentCatalogEntry>(StringComparer.Ordinal),
            SynergyCatalog: new Dictionary<string, SynergyTierTemplate>(StringComparer.Ordinal),
            CampaignChapters: campaignChapters,
            ExpeditionSites: expeditionSites,
            Encounters: encounters,
            EnemySquads: enemySquads,
            RewardSources: rewardSources,
            FirstPlayableSlice: firstPlayableSlice);
    }

    private static ExpeditionSiteTemplate CreateSite(string siteId, string chapterId, int siteOrder)
    {
        return new ExpeditionSiteTemplate(
            siteId,
            chapterId,
            $"{siteId}.name",
            siteOrder,
            "faction_debug",
            new[]
            {
                $"{siteId}:skirmish_a",
                $"{siteId}:skirmish_b",
                $"{siteId}:elite",
                $"{siteId}:boss",
            },
            "reward_source_extract",
            (int)ThreatTierValue.Tier1);
    }

    private static PassiveNodeTemplate CreatePassiveNode(string nodeId, string boardId, int boardDepth)
    {
        return new PassiveNodeTemplate(
            nodeId,
            new CombatModifierPackage(nodeId, ModifierSource.Other, Array.Empty<StatModifier>()),
            Array.Empty<string>(),
            BoardId: boardId,
            BoardDepth: boardDepth);
    }

    private static IReadOnlyDictionary<string, EncounterTemplate> BuildEncounterTemplates(params ExpeditionSiteTemplate[] sites)
    {
        var templates = new Dictionary<string, EncounterTemplate>(StringComparer.Ordinal);
        foreach (var site in sites)
        {
            for (var index = 0; index < site.EncounterIds.Count; index++)
            {
                var encounterId = site.EncounterIds[index];
                var kind = index switch
                {
                    2 => EncounterKindValue.Elite,
                    3 => EncounterKindValue.Boss,
                    _ => EncounterKindValue.Skirmish,
                };
                var rewardSourceId = kind switch
                {
                    EncounterKindValue.Elite => "reward_source_elite",
                    EncounterKindValue.Boss => "reward_source_boss",
                    _ => "reward_source_skirmish",
                };

                templates[encounterId] = new EncounterTemplate(
                    encounterId,
                    encounterId,
                    site.Id,
                    "enemy_squad_debug",
                    string.Empty,
                    rewardSourceId,
                    site.FactionId,
                    site.ThreatTier,
                    1,
                    Math.Max(1, index + 1),
                    kind.ToString(),
                    kind,
                    Array.Empty<string>());
            }
        }

        return templates;
    }

    private static IReadOnlyDictionary<string, RewardSourceTemplate> CreateRewardSources()
    {
        return new Dictionary<string, RewardSourceTemplate>(StringComparer.Ordinal)
        {
            ["reward_source_skirmish"] = new("reward_source_skirmish", "Skirmish", RewardSourceKindValue.Skirmish, "drop.skirmish", true, new[] { RarityBracketValue.Common }),
            ["reward_source_elite"] = new("reward_source_elite", "Elite", RewardSourceKindValue.Elite, "drop.elite", true, new[] { RarityBracketValue.Advanced }),
            ["reward_source_boss"] = new("reward_source_boss", "Boss", RewardSourceKindValue.Boss, "drop.boss", true, new[] { RarityBracketValue.Boss }),
            ["reward_source_extract"] = new("reward_source_extract", "Extract", RewardSourceKindValue.ExtractEndRun, "drop.extract", true, new[] { RarityBracketValue.Advanced }),
        };
    }
}
