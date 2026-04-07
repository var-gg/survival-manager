using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using SM.Combat.Model;
using SM.Content.Definitions;
using SM.Core.Stats;
using SM.Meta.Model;
using SM.Tests.EditMode.Fakes;
using SM.Unity;
using UnityEngine;

namespace SM.Tests.EditMode;

[Category("FastUnit")]
public sealed class RunLoopContractFastTests
{
    [Test]
    public void CampaignSelection_IsLocked_While_RunActive()
    {
        var fixtures = CreateAuthoredFixtures();
        try
        {
            var session = CreateBoundSession(fixtures.Lookup);

            Assert.That(session.CanChangeCampaignSelection, Is.True);
            Assert.That(session.CanStartQuickBattleSmoke, Is.True);
            Assert.That(session.SelectedCampaignChapterId, Is.EqualTo("chapter_alpha"));
            Assert.That(session.SelectedCampaignSiteId, Is.EqualTo("site_alpha_gate"));

            Assert.That(session.TryCycleCampaignSite(1), Is.True);
            Assert.That(session.SelectedCampaignSiteId, Is.EqualTo("site_alpha_depths"));
            Assert.That(session.TryCycleCampaignChapter(1), Is.True);
            Assert.That(session.SelectedCampaignChapterId, Is.EqualTo("chapter_beta"));
            Assert.That(session.SelectedCampaignSiteId, Is.EqualTo("site_beta_watch"));

            session.BeginNewExpedition();

            Assert.That(session.CanChangeCampaignSelection, Is.False);
            Assert.That(session.CanStartQuickBattleSmoke, Is.False);
            Assert.That(session.TryCycleCampaignChapter(1), Is.False);
            Assert.That(session.TryCycleCampaignSite(1), Is.False);
            Assert.That(session.SelectedCampaignChapterId, Is.EqualTo("chapter_beta"));
            Assert.That(session.SelectedCampaignSiteId, Is.EqualTo("site_beta_watch"));
        }
        finally
        {
            fixtures.Dispose();
        }
    }

    [Test]
    public void QuickBattle_DoesNotMutate_CampaignProgress()
    {
        var fixtures = CreateAuthoredFixtures();
        try
        {
            var session = CreateBoundSession(fixtures.Lookup);
            Assert.That(session.TryCycleCampaignSite(1), Is.True);

            var selectedChapterId = session.SelectedCampaignChapterId;
            var selectedSiteId = session.SelectedCampaignSiteId;
            var clearedChapterIds = session.Profile.CampaignProgress.ClearedChapterIds.ToArray();
            var clearedSiteIds = session.Profile.CampaignProgress.ClearedSiteIds.ToArray();

            session.PrepareQuickBattleSmoke();
            session.SetLastBattleResult(true, "quick smoke");

            Assert.That(session.IsQuickBattleSmokeActive, Is.True);
            Assert.That(session.HasPendingRewardSettlement, Is.True);
            Assert.That(session.PendingRewardChoices, Has.Count.EqualTo(3));
            Assert.That(session.ApplyRewardChoice(0), Is.True, "Quick Battle smoke should still settle one reward card.");
            session.ReturnToTownAfterReward();

            Assert.That(session.IsQuickBattleSmokeActive, Is.False);
            Assert.That(session.CanResumeExpedition, Is.False);
            Assert.That(session.SelectedCampaignChapterId, Is.EqualTo(selectedChapterId));
            Assert.That(session.SelectedCampaignSiteId, Is.EqualTo(selectedSiteId));
            Assert.That(session.Profile.CampaignProgress.ClearedChapterIds, Is.EqualTo(clearedChapterIds));
            Assert.That(session.Profile.CampaignProgress.ClearedSiteIds, Is.EqualTo(clearedSiteIds));
            Assert.That(session.Profile.CampaignProgress.StoryCleared, Is.False);
            Assert.That(session.Profile.CampaignProgress.EndlessUnlocked, Is.False);
        }
        finally
        {
            fixtures.Dispose();
        }
    }

    [Test]
    public void ExtractSettlement_Creates_RewardHandoff_And_Closes_Run()
    {
        var fixtures = CreateAuthoredFixtures();
        try
        {
            var session = CreateBoundSession(fixtures.Lookup);
            var siteId = session.SelectedCampaignSiteId;

            session.BeginNewExpedition();
            while (session.GetSelectedExpeditionNode()?.RequiresBattle == true)
            {
                Assert.That(session.ResolveSelectedExpeditionNode(), Is.True);
            }

            var extractNode = session.GetSelectedExpeditionNode();
            Assert.That(extractNode, Is.Not.Null);
            Assert.That(extractNode!.RequiresBattle, Is.False);
            Assert.That(extractNode.Id, Is.EqualTo($"{siteId}:extract"));

            Assert.That(session.ResolveSelectedNodeToRewardSettlement(), Is.True);
            Assert.That(session.HasPendingRewardSettlement, Is.True);
            Assert.That(session.PendingRewardChoices, Has.Count.EqualTo(3));
            Assert.That(session.CanResumeExpedition, Is.False);

            Assert.That(session.ApplyRewardChoice(0), Is.True);
            session.ReturnToTownAfterReward();

            Assert.That(session.HasPendingRewardSettlement, Is.False);
            Assert.That(session.HasActiveExpeditionRun, Is.False);
            Assert.That(session.CanResumeExpedition, Is.False);
            Assert.That(session.CanChangeCampaignSelection, Is.True);
            Assert.That(session.Profile.CampaignProgress.ClearedSiteIds, Does.Contain(siteId));
            Assert.That(session.Profile.ActiveRun.RunId, Is.Empty);
        }
        finally
        {
            fixtures.Dispose();
        }
    }

    private static GameSessionState CreateBoundSession(ICombatContentLookup lookup)
    {
        var session = new GameSessionState(lookup);
        session.BindProfile(new SaveProfile
        {
            Heroes = new List<HeroInstanceRecord>
            {
                CreateHero("hero-1", "Hero One", "vanguard", DeploymentAnchorId.FrontCenter),
                CreateHero("hero-2", "Hero Two", "ranger", DeploymentAnchorId.BackCenter),
                CreateHero("hero-3", "Hero Three", "duelist", DeploymentAnchorId.FrontTop),
                CreateHero("hero-4", "Hero Four", "mystic", DeploymentAnchorId.BackTop),
            },
        });
        session.SetCurrentScene(SceneNames.Town);
        return session;
    }

    private static HeroInstanceRecord CreateHero(string heroId, string name, string classId, DeploymentAnchorId _)
    {
        return new HeroInstanceRecord
        {
            HeroId = heroId,
            Name = name,
            ArchetypeId = $"{classId}_archetype",
            RaceId = "human",
            ClassId = classId,
            PositiveTraitId = "trait_positive",
            NegativeTraitId = "trait_negative",
            EquippedItemIds = new List<string>(),
        };
    }

    private static AuthoredFixtures CreateAuthoredFixtures()
    {
        var chapterAlpha = ScriptableObject.CreateInstance<CampaignChapterDefinition>();
        chapterAlpha.Id = "chapter_alpha";
        chapterAlpha.NameKey = "chapter.alpha";
        chapterAlpha.DescriptionKey = "chapter.alpha.desc";
        chapterAlpha.StoryOrder = 0;
        chapterAlpha.SiteIds = new List<string> { "site_alpha_gate", "site_alpha_depths" };

        var chapterBeta = ScriptableObject.CreateInstance<CampaignChapterDefinition>();
        chapterBeta.Id = "chapter_beta";
        chapterBeta.NameKey = "chapter.beta";
        chapterBeta.DescriptionKey = "chapter.beta.desc";
        chapterBeta.StoryOrder = 1;
        chapterBeta.SiteIds = new List<string> { "site_beta_watch" };
        chapterBeta.UnlocksEndlessOnClear = true;

        var siteAlphaGate = CreateSite("site_alpha_gate", "chapter_alpha", 0);
        var siteAlphaDepths = CreateSite("site_alpha_depths", "chapter_alpha", 1);
        var siteBetaWatch = CreateSite("site_beta_watch", "chapter_beta", 0);

        var snapshot = new CombatContentSnapshot(
            Archetypes: new Dictionary<string, CombatArchetypeTemplate>(),
            TraitPackages: new Dictionary<string, CombatModifierPackage>(),
            ItemPackages: new Dictionary<string, CombatModifierPackage>(),
            AffixPackages: new Dictionary<string, CombatModifierPackage>(),
            AugmentPackages: new Dictionary<string, CombatModifierPackage>(),
            SkillCatalog: new Dictionary<string, BattleSkillSpec>(),
            TeamTactics: new Dictionary<string, TeamTacticTemplate>(),
            RoleInstructions: new Dictionary<string, RoleInstructionTemplate>(),
            PassiveNodes: new Dictionary<string, PassiveNodeTemplate>(),
            AugmentCatalog: new Dictionary<string, AugmentCatalogEntry>(),
            SynergyCatalog: new Dictionary<string, SynergyTierTemplate>(),
            CampaignChapters: new Dictionary<string, CampaignChapterTemplate>(StringComparer.Ordinal)
            {
                [chapterAlpha.Id] = new CampaignChapterTemplate(chapterAlpha.Id, chapterAlpha.NameKey, chapterAlpha.StoryOrder, chapterAlpha.SiteIds, chapterAlpha.UnlocksEndlessOnClear),
                [chapterBeta.Id] = new CampaignChapterTemplate(chapterBeta.Id, chapterBeta.NameKey, chapterBeta.StoryOrder, chapterBeta.SiteIds, chapterBeta.UnlocksEndlessOnClear),
            },
            ExpeditionSites: new Dictionary<string, ExpeditionSiteTemplate>(StringComparer.Ordinal)
            {
                [siteAlphaGate.Id] = CreateSiteTemplate(siteAlphaGate),
                [siteAlphaDepths.Id] = CreateSiteTemplate(siteAlphaDepths),
                [siteBetaWatch.Id] = CreateSiteTemplate(siteBetaWatch),
            },
            Encounters: BuildEncounterTemplates(siteAlphaGate, siteAlphaDepths, siteBetaWatch),
            EnemySquads: new Dictionary<string, EnemySquadTemplate>(StringComparer.Ordinal)
            {
                ["enemy_squad_debug"] = new EnemySquadTemplate(
                    "enemy_squad_debug",
                    "Debug Squad",
                    "faction_debug",
                    TeamPostureType.StandardAdvance,
                    1,
                    1,
                    Array.Empty<string>(),
                    Array.Empty<EnemySquadMemberTemplate>()),
            },
            RewardSources: new Dictionary<string, RewardSourceTemplate>(StringComparer.Ordinal)
            {
                ["reward_source_skirmish"] = new RewardSourceTemplate("reward_source_skirmish", "Skirmish", RewardSourceKindValue.Skirmish, "drop.skirmish", true, new[] { RarityBracketValue.Common }),
                ["reward_source_elite"] = new RewardSourceTemplate("reward_source_elite", "Elite", RewardSourceKindValue.Elite, "drop.elite", true, new[] { RarityBracketValue.Advanced }),
                ["reward_source_boss"] = new RewardSourceTemplate("reward_source_boss", "Boss", RewardSourceKindValue.Boss, "drop.boss", true, new[] { RarityBracketValue.Boss }),
                ["reward_source_extract"] = new RewardSourceTemplate("reward_source_extract", "Extract", RewardSourceKindValue.ExtractEndRun, "drop.extract", true, new[] { RarityBracketValue.Advanced }),
            });

        var lookup = new FakeCombatContentLookup(
            snapshot: snapshot,
            campaignChapters: new Dictionary<string, CampaignChapterDefinition>(StringComparer.Ordinal)
            {
                [chapterAlpha.Id] = chapterAlpha,
                [chapterBeta.Id] = chapterBeta,
            },
            expeditionSites: new Dictionary<string, ExpeditionSiteDefinition>(StringComparer.Ordinal)
            {
                [siteAlphaGate.Id] = siteAlphaGate,
                [siteAlphaDepths.Id] = siteAlphaDepths,
                [siteBetaWatch.Id] = siteBetaWatch,
            },
            orderedCampaignChapters: new[] { chapterAlpha, chapterBeta });

        return new AuthoredFixtures(
            lookup,
            new UnityEngine.Object[] { chapterAlpha, chapterBeta, siteAlphaGate, siteAlphaDepths, siteBetaWatch });
    }

    private static ExpeditionSiteDefinition CreateSite(string siteId, string chapterId, int siteOrder)
    {
        var site = ScriptableObject.CreateInstance<ExpeditionSiteDefinition>();
        site.Id = siteId;
        site.ChapterId = chapterId;
        site.NameKey = $"{siteId}.name";
        site.DescriptionKey = $"{siteId}.desc";
        site.SiteOrder = siteOrder;
        site.FactionId = "faction_debug";
        site.EncounterIds = new List<string>
        {
            $"{siteId}:skirmish_a",
            $"{siteId}:skirmish_b",
            $"{siteId}:elite",
            $"{siteId}:boss",
        };
        site.ExtractRewardSourceId = "reward_source_extract";
        site.ThreatTier = ThreatTierValue.Tier1;
        return site;
    }

    private static ExpeditionSiteTemplate CreateSiteTemplate(ExpeditionSiteDefinition site)
    {
        return new ExpeditionSiteTemplate(
            site.Id,
            site.ChapterId,
            site.NameKey,
            site.SiteOrder,
            site.FactionId,
            site.EncounterIds,
            site.ExtractRewardSourceId,
            (int)site.ThreatTier);
    }

    private static IReadOnlyDictionary<string, EncounterTemplate> BuildEncounterTemplates(params ExpeditionSiteDefinition[] sites)
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
                    (int)site.ThreatTier,
                    1,
                    Math.Max(1, index + 1),
                    kind.ToString(),
                    kind,
                    Array.Empty<string>());
            }
        }

        return templates;
    }

    private sealed class AuthoredFixtures : IDisposable
    {
        private readonly IReadOnlyList<UnityEngine.Object> _objects;

        public AuthoredFixtures(FakeCombatContentLookup lookup, IReadOnlyList<UnityEngine.Object> objects)
        {
            Lookup = lookup;
            _objects = objects;
        }

        public FakeCombatContentLookup Lookup { get; }

        public void Dispose()
        {
            foreach (var obj in _objects)
            {
                if (obj != null)
                {
                    UnityEngine.Object.DestroyImmediate(obj);
                }
            }
        }
    }
}
