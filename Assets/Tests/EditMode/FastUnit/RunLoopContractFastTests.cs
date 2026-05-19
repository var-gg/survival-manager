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
    public void MarkBattleResolved_StampsDeterministicRewardCommitIdOnOverlay()
    {
        // task-reward-settlement-commit-v1 acceptance #1 통합 evidence:
        // MarkBattleResolved 시점에 RewardCommitIdService.Compute로 deterministic id를 만들고
        // ActiveRun.Overlay.RewardCommitId에 stamp한다. battleContextHash + outcome 조합으로
        // dedup key가 결정되므로 같은 input에서 같은 id가 재현된다.
        var lookup = EditorFreeCombatContentFixture.CreateRunLoopLookup();
        var session = CreateBoundSession(lookup);
        session.BeginNewExpedition();

        Assert.That(session.PrepareSelectedBattleNodeHandoff(), Is.True);
        // BattleContextHash는 BuildBattleLoadoutSnapshot 시점(TryBuildBattleContext + SetBattleContext)에 stamping된다.
        var snapshot = session.BuildBattleLoadoutSnapshot();
        Assert.That(snapshot, Is.Not.Null);
        var battleContextHash = session.ActiveRun?.Overlay.BattleContextHash;
        Assert.That(battleContextHash, Is.Not.Null);
        Assert.That(battleContextHash, Is.Not.Empty,
            "Battle loadout compile 후 BattleContextHash가 overlay에 stamping돼야 한다.");

        session.MarkBattleResolved(true, 8, 4);
        var commitId = session.ActiveRun?.Overlay.RewardCommitId;
        Assert.That(commitId, Is.Not.Empty,
            "MarkBattleResolved가 RewardCommitId를 overlay에 stamping해야 한다 (acceptance #1).");

        var expected = SM.Meta.Services.RewardCommitIdService.Compute(battleContextHash!, "victory");
        Assert.That(commitId, Is.EqualTo(expected),
            "stamping된 RewardCommitId는 RewardCommitIdService.Compute 결과와 일치한다 (deterministic dedup key).");
    }

    [Test]
    public void RunEnd_InvalidatesAtlasSessionAndRunBattlePayloadAndPendingReward()
    {
        // task-run-save-resume-idempotence-v1 acceptance #4: run end는 Atlas session, RunBattlePayload,
        // pending reward를 invalidate해야 한다. 이후 reload나 새 run에서 stale state로 corrupt 안 됨.
        var lookup = EditorFreeCombatContentFixture.CreateRunLoopLookup();
        var session = CreateBoundSession(lookup);
        session.BeginNewExpedition();

        Assert.That(session.PrepareSelectedBattleNodeHandoff(), Is.True);
        session.BuildBattleLoadoutSnapshot();
        session.MarkBattleResolved(true, 5, 2);
        Assert.That(session.HasPendingRewardSettlement, Is.True);

        // ExitCombatSandbox는 run end의 한 형태로 모든 transient state를 invalidate한다.
        session.ExitCombatSandbox();

        Assert.That(session.ActiveRun, Is.Null,
            "run end → ActiveRun null.");
        Assert.That(session.AtlasSession, Is.Null,
            "run end → AtlasSession null.");
        Assert.That(session.RunBattlePayload, Is.Null,
            "run end → RunBattlePayload null (acceptance #4).");
        Assert.That(session.HasPendingRewardSettlement, Is.False,
            "run end → pending reward cleared.");
    }

    [Test]
    public void Migration_LegacyActiveRunRecordWithoutRewardCommitId_LoadsAsEmptyString()
    {
        // task-run-save-resume-idempotence-v1 acceptance #5: 신규 필드 RewardCommitId 없는
        // legacy save profile이 default ""로 safe 복원되어 normal flow에 NRE 없이 진입.
        var lookup = EditorFreeCombatContentFixture.CreateRunLoopLookup();
        var session = CreateBoundSession(lookup);
        var legacyProfile = CloneProfile(session.Profile);
        legacyProfile.ActiveRun = new ActiveRunRecord
        {
            RunId = "legacy_run_001",
            ExpeditionId = "exp_legacy",
            BlueprintId = "blueprint_legacy",
            ChapterId = "chapter_alpha",
            SiteId = "site_alpha_gate",
            BattleContextHash = "legacy_context_hash",
            RewardSourceId = "legacy_source",
            // RewardCommitId 미설정 — legacy save profile에 없는 신규 field.
        };

        var reloaded = GameSessionTestFactory.Create(lookup);
        reloaded.BindProfile(legacyProfile);

        Assert.That(reloaded.ActiveRun, Is.Not.Null);
        Assert.That(reloaded.ActiveRun!.Overlay.RewardCommitId, Is.EqualTo(string.Empty),
            "legacy ActiveRunRecord의 missing RewardCommitId가 default empty로 복원 (NRE 없이 safe).");
        Assert.That(reloaded.ActiveRun.Overlay.BattleContextHash, Is.EqualTo("legacy_context_hash"),
            "기존 fields는 그대로 복원.");
    }

    [Test]
    public void ReloadBeforeCommit_RestoresPendingChoicesAndRewardCommitId()
    {
        // task-reward-settlement-commit-v1 acceptance #4: commit 전 reload 시 같은 pending state
        // (commitId + reward choices)가 복원된다.
        var lookup = EditorFreeCombatContentFixture.CreateRunLoopLookup();
        var session = CreateBoundSession(lookup);
        session.BeginNewExpedition();

        Assert.That(session.PrepareSelectedBattleNodeHandoff(), Is.True);
        session.BuildBattleLoadoutSnapshot();
        session.MarkBattleResolved(true, 9, 5);

        var originalCommitId = session.ActiveRun?.Overlay.RewardCommitId;
        var originalRewardSourceId = session.ActiveRun?.Overlay.RewardSourceId ?? string.Empty;
        var originalPendingCount = session.PendingRewardChoices.Count;
        Assert.That(originalCommitId, Is.Not.Empty);
        Assert.That(originalRewardSourceId, Is.Not.Empty);
        Assert.That(originalPendingCount, Is.GreaterThan(0));

        // commit 전(ApplyRewardChoice 호출 X) reload.
        var reloaded = GameSessionTestFactory.Create(lookup);
        reloaded.BindProfile(CloneProfile(session.Profile));
        reloaded.SetCurrentScene(SceneNames.Town);

        Assert.That(reloaded.HasPendingRewardSettlement, Is.True,
            "commit 전 reload는 pending state를 유지해야 한다.");
        Assert.That(reloaded.ActiveRun?.Overlay.RewardCommitId, Is.EqualTo(originalCommitId),
            "RewardCommitId는 reload 후 같은 값으로 복원된다 (deterministic dedup baseline).");
        Assert.That(reloaded.ActiveRun?.Overlay.RewardSourceId, Is.EqualTo(originalRewardSourceId));
        Assert.That(reloaded.PendingRewardChoices.Count, Is.EqualTo(originalPendingCount),
            "pending reward choice 수가 reload 후 같다.");
    }

    [Test]
    public void Defeat_StampsRewardCommitId_AndDoesNotEmitRewardChoiceLedgerEntry()
    {
        // task-reward-settlement-commit-v1 acceptance #5/#6 evidence:
        // defeat path는 RewardCommitId를 stamp하지만 reward_choice ledger entry를 만들지 않는다
        // (commit 미발생). ActiveRun도 corrupt되지 않는다.
        var lookup = EditorFreeCombatContentFixture.CreateRunLoopLookup();
        var session = CreateBoundSession(lookup);
        session.BeginNewExpedition();

        Assert.That(session.PrepareSelectedBattleNodeHandoff(), Is.True);
        session.BuildBattleLoadoutSnapshot();
        session.MarkBattleResolved(false, 5, 2);

        Assert.That(session.ActiveRun, Is.Not.Null);
        Assert.That(session.ActiveRun!.LastSettlementWasVictory, Is.False,
            "defeat 결과가 ActiveRun에 반영된다.");

        var defeatCommitId = session.ActiveRun.Overlay.RewardCommitId;
        Assert.That(defeatCommitId, Is.Not.Empty,
            "defeat도 deterministic CommitId를 stamp한다 (outcome=defeat).");

        var rewardChoiceEntries = session.Profile.RewardLedger.Count(entry =>
            entry.SourceKind.EndsWith(":reward_choice", StringComparison.Ordinal));
        Assert.That(rewardChoiceEntries, Is.EqualTo(0),
            "defeat 시 ApplyRewardChoice 미호출 → reward_choice ledger entry는 0.");
    }

    [Test]
    public void BindProfile_RestoresRewardCommitId_AndLedgerEntriesAreStampedWithIt()
    {
        // task-reward-settlement-commit-v1 acceptance #3 / #4:
        // - #3: ApplyRewardChoice 후 reward_choice ledger entry가 RewardCommitId를 stamp한다.
        // - #4: reload(BindProfile) 후 그 entry들이 그대로 복원되고 pending state는 finalize된다.
        var lookup = EditorFreeCombatContentFixture.CreateRunLoopLookup();
        var session = CreateBoundSession(lookup);
        session.BeginNewExpedition();

        Assert.That(session.PrepareSelectedBattleNodeHandoff(), Is.True);
        session.BuildBattleLoadoutSnapshot();
        session.MarkBattleResolved(true, 12, 6);

        var originalCommitId = session.ActiveRun?.Overlay.RewardCommitId;
        Assert.That(originalCommitId, Is.Not.Empty,
            "stamping이 완료된 상태로 ApplyRewardChoice 진입.");

        Assert.That(session.ApplyRewardChoice(0), Is.True);

        var stampedEntries = session.Profile.RewardLedger.Count(entry =>
            string.Equals(entry.CommitId, originalCommitId, StringComparison.Ordinal)
            && entry.SourceKind.EndsWith(":reward_choice", StringComparison.Ordinal));
        Assert.That(stampedEntries, Is.GreaterThanOrEqualTo(1),
            "acceptance #3: reward_choice ledger entry가 RewardCommitId를 stamp한다.");

        var reloaded = GameSessionTestFactory.Create(lookup);
        reloaded.BindProfile(CloneProfile(session.Profile));
        reloaded.SetCurrentScene(SceneNames.Town);

        Assert.That(reloaded.HasPendingRewardSettlement, Is.False,
            "acceptance #4: commit이 끝난 settlement은 reload 후 pending이 아니다.");

        var reloadedStampedEntries = reloaded.Profile.RewardLedger.Count(entry =>
            string.Equals(entry.CommitId, originalCommitId, StringComparison.Ordinal)
            && entry.SourceKind.EndsWith(":reward_choice", StringComparison.Ordinal));
        Assert.That(reloadedStampedEntries, Is.EqualTo(stampedEntries),
            "acceptance #4: CommitId-stamped ledger entry가 reload로 그대로 복원된다.");
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
