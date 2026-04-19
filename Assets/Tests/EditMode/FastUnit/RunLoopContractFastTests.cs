using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using SM.Combat.Model;
using SM.Persistence.Abstractions.Models;
using SM.Persistence.Json;
using SM.Tests.EditMode.Fakes;
using SM.Unity;

namespace SM.Tests.EditMode;

[Category("FastUnit")]
public sealed class RunLoopContractFastTests
{
    [Test]
    public void CampaignSelection_IsLocked_While_RunActive()
    {
        var lookup = EditorFreeCombatContentFixture.CreateRunLoopLookup();
        var session = CreateBoundSession(lookup);

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

    [Test]
    public void QuickBattle_DoesNotMutate_CampaignProgress()
    {
        var lookup = EditorFreeCombatContentFixture.CreateRunLoopLookup();
        var session = CreateBoundSession(lookup);
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

    [Test]
    public void ExtractSettlement_Creates_RewardHandoff_And_Closes_Run()
    {
        var lookup = EditorFreeCombatContentFixture.CreateRunLoopLookup();
        var session = CreateBoundSession(lookup);
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

    [Test]
    public void ManualProfileReload_IsBlocked_WhileRunRewardOrSmokeIsActive()
    {
        var lookup = EditorFreeCombatContentFixture.CreateRunLoopLookup();
        var session = CreateBoundSession(lookup);
        Assert.That(session.CanManualProfileReload(out var idleReason), Is.True, idleReason);

        session.BeginNewExpedition();
        Assert.That(session.CanManualProfileReload(out var activeRunReason), Is.False);
        Assert.That(activeRunReason, Does.Contain("expedition"));

        session = CreateBoundSession(lookup);
        Assert.That(session.ResolveSelectedNodeToRewardSettlement(), Is.False, "첫 노드는 battle route이므로 바로 settlement로 가지 않아야 한다.");
        session.PrepareSelectedBattleNodeHandoff();
        session.MarkBattleResolved(true, 8, 4);
        Assert.That(session.CanManualProfileReload(out var rewardReason), Is.False);
        Assert.That(rewardReason, Does.Contain("settlement"));

        session = CreateBoundSession(lookup);
        session.PrepareQuickBattleSmoke();
        Assert.That(session.CanManualProfileReload(out var smokeReason), Is.False);
        Assert.That(smokeReason, Does.Contain("Quick Battle smoke"));
    }

    [Test]
    public void BindProfile_ResumesRewardSettlementWithoutDuplicatingRewardLedger()
    {
        var lookup = EditorFreeCombatContentFixture.CreateRunLoopLookup();
        var session = CreateBoundSession(lookup);
        session.BeginNewExpedition();

        Assert.That(session.PrepareSelectedBattleNodeHandoff(), Is.True);
        session.MarkBattleResolved(true, 12, 6);

        var rewardSourceId = session.ActiveRun?.Overlay.RewardSourceId ?? string.Empty;
        Assert.That(rewardSourceId, Is.Not.Empty);
        Assert.That(session.PendingRewardChoices, Has.Count.EqualTo(3));
        Assert.That(session.ApplyRewardChoice(0), Is.True);

        var rewardChoiceLedgerCount = session.Profile.RewardLedger.Count(entry =>
            string.Equals(entry.SourceId, rewardSourceId, StringComparison.Ordinal)
            && entry.SourceKind.EndsWith(":reward_choice", StringComparison.Ordinal));
        Assert.That(rewardChoiceLedgerCount, Is.EqualTo(1));

        var reloaded = GameSessionTestFactory.Create(lookup);
        reloaded.BindProfile(CloneProfile(session.Profile));
        reloaded.SetCurrentScene(SceneNames.Town);

        Assert.That(reloaded.HasPendingRewardSettlement, Is.False);
        Assert.That(reloaded.CanResumeExpedition, Is.True);
        Assert.That(reloaded.CurrentExpeditionNodeIndex, Is.EqualTo(1));
        Assert.That(reloaded.Profile.RewardLedger.Count(entry =>
            string.Equals(entry.SourceId, rewardSourceId, StringComparison.Ordinal)
            && entry.SourceKind.EndsWith(":reward_choice", StringComparison.Ordinal)), Is.EqualTo(1));
    }

    private static GameSessionState CreateBoundSession(ICombatContentLookup lookup)
    {
        var session = GameSessionTestFactory.Create(lookup);
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

    private static SaveProfile CloneProfile(SaveProfile profile)
    {
        var root = Path.Combine(Path.GetTempPath(), "SM_RunLoopContractFastTests", Guid.NewGuid().ToString("N"));
        try
        {
            var repository = new JsonSaveRepository(root);
            repository.Save(profile);
            return repository.LoadOrCreate(profile.ProfileId);
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
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
}
