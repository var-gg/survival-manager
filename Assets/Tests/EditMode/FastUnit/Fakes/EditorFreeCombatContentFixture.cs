using System;
using System.Collections.Generic;
using System.Linq;
using SM.Combat.Model;
using SM.Core.Content;
using SM.Core.Stats;
using SM.Meta.Model;

namespace SM.Tests.EditMode.Fakes;

public static class EditorFreeCombatContentFixture
{
    public static FakeCombatContentLookup CreateRunLoopLookup(
        SiteGraphTemplate? siteAlphaGateGraph = null,
        IReadOnlyDictionary<string, SiteEventTemplate>? siteEvents = null,
        IReadOnlyDictionary<string, string>? siteEventChoiceIconIds = null)
    {
        var augmentCatalog = CreateRunLoopAugmentCatalog();
        var firstPlayableSlice = new FirstPlayableSliceDefinition
        {
            TemporaryAugmentCap = augmentCatalog.Count,
            TemporaryAugmentIds = augmentCatalog.Keys.OrderBy(id => id, StringComparer.Ordinal).ToArray(),
        };
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
        var siteAlphaGate = CreateSite("site_alpha_gate", "chapter_alpha", 0, siteAlphaGateGraph);
        var siteAlphaDepths = CreateSite("site_alpha_depths", "chapter_alpha", 1);
        var siteBetaWatch = CreateSite("site_beta_watch", "chapter_beta", 0);

        var snapshot = CreateSnapshot(
            firstPlayableSlice: firstPlayableSlice,
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
            augmentCatalog: augmentCatalog,
            rewardSources: CreateRewardSources(),
            siteEvents: siteEvents,
            warWound: siteEvents == null ? null : new WarWoundSpec(0.25f, 0.9f, 1, 3, 1, true));

        return new FakeCombatContentLookup(
            snapshot: snapshot,
            firstPlayableSlice: firstPlayableSlice,
            siteEventChoiceIconIds: siteEventChoiceIconIds);
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

    /// <summary>
    /// authored 카탈로그(chapters/sites/encounters/enemySquads 모두 non-empty → HasAuthoredCatalog=true)이되
    /// encounter가 가리키는 squad("enemy_squad_missing")가 enemySquads에 없는 의도적 broken fixture.
    /// 런타임 TryResolveEncounter가 실패하는 상황을 만들어 "무음 디버그 스모크 강등 거부"(fail-closed) 가드를 검증한다.
    /// (validator는 이 dangling ref를 빌드타임에 막으므로, 이 fixture는 validation이 스킵된 환경의 런타임 방어선만 노린다.)
    /// </summary>
    public static FakeCombatContentLookup CreateAuthoredLookupWithDanglingEncounterSquad()
    {
        var firstPlayableSlice = new FirstPlayableSliceDefinition();
        var chapter = new CampaignChapterTemplate(
            "chapter_alpha",
            "chapter.alpha",
            0,
            new[] { "site_alpha_gate" },
            true);
        var site = CreateSite("site_alpha_gate", "chapter_alpha", 0);
        var snapshot = CreateSnapshot(
            firstPlayableSlice: firstPlayableSlice,
            campaignChapters: new Dictionary<string, CampaignChapterTemplate>(StringComparer.Ordinal)
            {
                [chapter.Id] = chapter,
            },
            expeditionSites: new Dictionary<string, ExpeditionSiteTemplate>(StringComparer.Ordinal)
            {
                [site.Id] = site,
            },
            encounters: BuildEncounterTemplates("enemy_squad_missing", site),
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
        return new FakeCombatContentLookup(snapshot: snapshot, firstPlayableSlice: firstPlayableSlice);
    }

    public static CombatContentSnapshot CreateSnapshot(
        FirstPlayableSliceDefinition? firstPlayableSlice = null,
        IReadOnlyDictionary<string, PassiveNodeTemplate>? passiveNodes = null,
        IReadOnlyDictionary<string, CampaignChapterTemplate>? campaignChapters = null,
        IReadOnlyDictionary<string, ExpeditionSiteTemplate>? expeditionSites = null,
        IReadOnlyDictionary<string, EncounterTemplate>? encounters = null,
        IReadOnlyDictionary<string, EnemySquadTemplate>? enemySquads = null,
        IReadOnlyDictionary<string, BossOverlayTemplate>? bossOverlays = null,
        IReadOnlyDictionary<string, AugmentCatalogEntry>? augmentCatalog = null,
        IReadOnlyDictionary<string, RewardSourceTemplate>? rewardSources = null,
        IReadOnlyDictionary<string, DropTableTemplate>? dropTables = null,
        IReadOnlyDictionary<string, LootBundleTemplate>? lootBundles = null,
        IReadOnlyDictionary<string, SiteEventTemplate>? siteEvents = null,
        WarWoundSpec? warWound = null)
    {
        var emptyPackages = new Dictionary<string, CombatModifierPackage>(StringComparer.Ordinal);
        var augmentPackages = augmentCatalog?
            .ToDictionary(pair => pair.Key, pair => pair.Value.Package, StringComparer.Ordinal)
            ?? new Dictionary<string, CombatModifierPackage>(StringComparer.Ordinal);
        return new CombatContentSnapshot(
            Archetypes: new Dictionary<string, CombatArchetypeTemplate>(StringComparer.Ordinal),
            TraitPackages: emptyPackages,
            ItemPackages: new Dictionary<string, CombatModifierPackage>(StringComparer.Ordinal),
            AffixPackages: new Dictionary<string, CombatModifierPackage>(StringComparer.Ordinal),
            AugmentPackages: augmentPackages,
            SkillCatalog: new Dictionary<string, BattleSkillSpec>(StringComparer.Ordinal),
            TeamTactics: new Dictionary<string, TeamTacticTemplate>(StringComparer.Ordinal),
            RoleInstructions: new Dictionary<string, RoleInstructionTemplate>(StringComparer.Ordinal),
            PassiveNodes: passiveNodes ?? new Dictionary<string, PassiveNodeTemplate>(StringComparer.Ordinal),
            AugmentCatalog: augmentCatalog ?? new Dictionary<string, AugmentCatalogEntry>(StringComparer.Ordinal),
            SynergyCatalog: new Dictionary<string, SynergyTierTemplate>(StringComparer.Ordinal),
            CampaignChapters: campaignChapters,
            ExpeditionSites: expeditionSites,
            Encounters: encounters,
            EnemySquads: enemySquads,
            BossOverlays: bossOverlays,
            RewardSources: rewardSources,
            DropTables: dropTables,
            LootBundles: lootBundles,
            FirstPlayableSlice: firstPlayableSlice,
            WarWound: warWound,
            SiteEvents: siteEvents);
    }

    private static IReadOnlyDictionary<string, AugmentCatalogEntry> CreateRunLoopAugmentCatalog()
    {
        return new Dictionary<string, AugmentCatalogEntry>(StringComparer.Ordinal)
        {
            ["augment_test_tactical_guard"] = CreateAugment(
                "augment_test_tactical_guard",
                "family_guard",
                "TacticalRewrite",
                2,
                new[] { "role_bound", "frontline" },
                new[] { "frontline" }),
            ["augment_test_tactical_reach"] = CreateAugment(
                "augment_test_tactical_reach",
                "family_reach",
                "TacticalRewrite",
                2,
                new[] { "role_bound", "backline" },
                new[] { "backline" }),
            ["augment_test_tactical_focus"] = CreateAugment(
                "augment_test_tactical_focus",
                "family_focus",
                "TacticalRewrite",
                1,
                new[] { "stat_light" },
                Array.Empty<string>()),
            ["augment_test_hero_vanguard"] = CreateAugment(
                "augment_test_hero_vanguard",
                "family_vanguard",
                "HeroRewrite",
                2,
                new[] { "hero_bound", "vanguard" },
                new[] { "vanguard" }),
            ["augment_test_scaling_engine"] = CreateAugment(
                "augment_test_scaling_engine",
                "family_scaling",
                "ScalingEngine",
                3,
                new[] { "volatile_run" },
                Array.Empty<string>()),
            ["augment_test_economy_pack"] = CreateAugment(
                "augment_test_economy_pack",
                "family_economy",
                "EconomyAndLoot",
                1,
                new[] { "economy_loot" },
                Array.Empty<string>(),
                category: "economy_loot"),
        };
    }

    private static AugmentCatalogEntry CreateAugment(
        string id,
        string familyId,
        string offerBucket,
        int tier,
        IReadOnlyList<string> tags,
        IReadOnlyList<string> buildBiasTags,
        string category = "combat")
    {
        return new AugmentCatalogEntry(
            id,
            category,
            familyId,
            tier,
            IsPermanent: false,
            SuppressIfPermanentEquipped: false,
            tags,
            Array.Empty<string>(),
            new CombatModifierPackage(id, ModifierSource.Augment, Array.Empty<StatModifier>()),
            OfferBucket: offerBucket,
            BuildBiasTags: buildBiasTags);
    }

    private static ExpeditionSiteTemplate CreateSite(
        string siteId,
        string chapterId,
        int siteOrder,
        SiteGraphTemplate? graph = null)
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
            (int)ThreatTierValue.Tier1,
            graph);
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
        => BuildEncounterTemplates("enemy_squad_debug", sites);

    private static IReadOnlyDictionary<string, EncounterTemplate> BuildEncounterTemplates(string enemySquadId, params ExpeditionSiteTemplate[] sites)
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
                    enemySquadId,
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
            ["reward_source_shrine_event"] = new("reward_source_shrine_event", "Shrine Event", RewardSourceKindValue.ShrineEvent, "drop_table_shrine_event", true, new[] { RarityBracketValue.Common }),
        };
    }
}
