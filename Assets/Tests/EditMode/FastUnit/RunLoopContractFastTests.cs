using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using SM.Combat.Model;
using SM.Meta;
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
    public void ApplyRewardChoice_FinalizesPendingSettlement_SoTownReentryReadyForResume()
    {
        // task-reward-settlement-commit-v1 acceptance #7 EditMode evidence:
        // ApplyRewardChoice가 pending → settled로 transition시키고 Town 복귀 시 expedition resume이
        // 가능한 상태로 만든다. reward ledger entry가 추가되어 inventory/equipment refresh의
        // source-of-truth가 박힌다.
        var lookup = EditorFreeCombatContentFixture.CreateRunLoopLookup();
        var session = CreateBoundSession(lookup);
        session.BeginNewExpedition();

        Assert.That(session.PrepareSelectedBattleNodeHandoff(), Is.True);
        session.BuildBattleLoadoutSnapshot();
        session.MarkBattleResolved(true, 8, 4);

        Assert.That(session.HasPendingRewardSettlement, Is.True,
            "Battle resolve 후 pending state는 commit 전.");

        var ledgerCountBefore = session.Profile.RewardLedger.Count(entry =>
            entry.SourceKind.EndsWith(":reward_choice", StringComparison.Ordinal));

        Assert.That(session.ApplyRewardChoice(0), Is.True);
        session.ReturnToTownAfterReward();

        Assert.That(session.HasPendingRewardSettlement, Is.False,
            "ReturnToTownAfterReward(Town 복귀 path)가 FinalizeRewardSettlement 트리거.");
        Assert.That(session.CanResumeExpedition, Is.True,
            "Town 복귀 후 expedition resume 가능한 상태.");

        var ledgerCountAfter = session.Profile.RewardLedger.Count(entry =>
            entry.SourceKind.EndsWith(":reward_choice", StringComparison.Ordinal));
        Assert.That(ledgerCountAfter, Is.GreaterThan(ledgerCountBefore),
            "reward_choice ledger에 새 entry 추가 — Town inventory/equipment refresh source.");
    }

    [Test]
    public void ApplyRewardChoice_PopulatesLastCommittedRewardSummary()
    {
        // p1 RewardCommitted wiring evidence: ApplyRewardChoice가 commit 시점에
        // LastCommittedRewardSummary(RewardSummaryRecord)를 채워, RewardScreenController가
        // RewardCommitted moment context에 실어 보낼 수 있게 한다.
        var lookup = EditorFreeCombatContentFixture.CreateRunLoopLookup();
        var session = CreateBoundSession(lookup);
        session.BeginNewExpedition();

        Assert.That(session.PrepareSelectedBattleNodeHandoff(), Is.True);
        session.BuildBattleLoadoutSnapshot();
        session.MarkBattleResolved(true, 8, 4);

        Assert.That(session.LastCommittedRewardSummary, Is.Null,
            "commit 전에는 마지막 보상 요약이 없다.");

        var rewardSourceId = session.ActiveRun?.Overlay.RewardSourceId ?? string.Empty;
        Assert.That(session.ApplyRewardChoice(0), Is.True);

        var summary = session.LastCommittedRewardSummary;
        Assert.That(summary, Is.Not.Null,
            "ApplyRewardChoice가 RewardSummary를 채운다.");
        Assert.That(summary!.ChoiceIndex, Is.EqualTo(0));
        Assert.That(summary.RewardSourceId, Is.EqualTo(rewardSourceId));
        Assert.That(summary.WasRecoveredSettlement, Is.False,
            "정상 commit 경로는 recovered settlement가 아니다.");
        Assert.That(summary.ChoiceKind, Is.Not.Empty);
        Assert.That(summary.ChapterId, Is.Not.Empty, "ChapterId stamping.");
        Assert.That(summary.SiteId, Is.Not.Empty, "SiteId stamping.");
    }

    [Test]
    public void RunOverlay_TraceFields_AreFullyPopulatedAfterBattleResolve()
    {
        // task-vertical-slice-smoke-evidence-v1 acceptance #2 EditMode 통합 evidence:
        // Town → Atlas (implicit BeginNew) → Battle → MarkBattleResolved 한 phase가 끝났을 때
        // RunId + ChapterId + SiteId + SiteNodeIndex + EncounterId + BattleContextHash +
        // RewardSourceId + RewardCommitId 7 trace field가 모두 stamping돼 있어야 한다.
        // PlayMode smoke가 동등한 traversal을 돌릴 때 이 fields를 직접 report로 dump한다.
        var lookup = EditorFreeCombatContentFixture.CreateRunLoopLookup();
        var session = CreateBoundSession(lookup);
        session.BeginNewExpedition();

        Assert.That(session.PrepareSelectedBattleNodeHandoff(), Is.True);
        session.BuildBattleLoadoutSnapshot();
        session.MarkBattleResolved(true, 10, 5);

        var run = session.ActiveRun;
        Assert.That(run, Is.Not.Null);
        Assert.That(run!.RunId, Is.Not.Empty, "RunId stamping.");

        var overlay = run.Overlay;
        Assert.That(overlay.ChapterId, Is.Not.Empty, "ChapterId stamping.");
        Assert.That(overlay.SiteId, Is.Not.Empty, "SiteId stamping.");
        Assert.That(overlay.SiteNodeIndex, Is.GreaterThanOrEqualTo(0), "SiteNodeIndex stamping (0-based).");
        Assert.That(overlay.EncounterId, Is.Not.Empty, "EncounterId stamping (authored or canonical).");
        Assert.That(overlay.BattleContextHash, Is.Not.Empty, "BattleContextHash stamping (deterministic hash).");
        Assert.That(overlay.RewardSourceId, Is.Not.Empty, "RewardSourceId stamping (settlement payload).");
        Assert.That(overlay.RewardCommitId, Is.Not.Empty, "RewardCommitId stamping (commit-once dedup key).");
    }

    [Test]
    public void BindProfile_RestoresExpeditionSelectionAndOverlayTraceAfterReload()
    {
        // task-run-save-resume-idempotence-v1 acceptance #1: Atlas selection 결과로 advance한
        // expedition state(CurrentNodeIndex, Overlay trace)가 reload 후 같은 위치로 복원.
        var lookup = EditorFreeCombatContentFixture.CreateRunLoopLookup();
        var session = CreateBoundSession(lookup);
        session.BeginNewExpedition();

        Assert.That(session.PrepareSelectedBattleNodeHandoff(), Is.True);
        session.BuildBattleLoadoutSnapshot();
        session.MarkBattleResolved(true, 8, 4);
        Assert.That(session.ApplyRewardChoice(0), Is.True);
        session.ReturnToTownAfterReward();

        var originalCurrentNodeIndex = session.CurrentExpeditionNodeIndex;
        var originalSiteId = session.SelectedCampaignSiteId;
        var originalChapterId = session.SelectedCampaignChapterId;

        var reloaded = GameSessionTestFactory.Create(lookup);
        reloaded.BindProfile(CloneProfile(session.Profile));
        reloaded.SetCurrentScene(SceneNames.Town);

        Assert.That(reloaded.CurrentExpeditionNodeIndex, Is.EqualTo(originalCurrentNodeIndex),
            "advance한 CurrentExpeditionNodeIndex가 reload 후 복원.");
        Assert.That(reloaded.SelectedCampaignSiteId, Is.EqualTo(originalSiteId),
            "campaign site selection 보존.");
        Assert.That(reloaded.SelectedCampaignChapterId, Is.EqualTo(originalChapterId),
            "campaign chapter selection 보존.");
        Assert.That(reloaded.CanResumeExpedition, Is.True,
            "Atlas re-entry가 가능한 resume state로 복원.");
    }

    [Test]
    public void BindProfile_WithNullActiveRun_LoadsCleanStateWithoutFailure()
    {
        // task-run-save-resume-idempotence-v1 acceptance #6: load failure 또는 fresh save에서
        // Profile.ActiveRun이 null인 경우에도 safe fallback (NRE/corrupt state 없음).
        var lookup = EditorFreeCombatContentFixture.CreateRunLoopLookup();
        var donor = CreateBoundSession(lookup);
        var profile = CloneProfile(donor.Profile);
        profile.ActiveRun = null;

        var reloaded = GameSessionTestFactory.Create(lookup);
        reloaded.BindProfile(profile);
        reloaded.SetCurrentScene(SceneNames.Town);

        Assert.That(reloaded.ActiveRun, Is.Null,
            "null ActiveRun이 그대로 null로 복원 (NRE 없이).");
        Assert.That(reloaded.HasActiveExpeditionRun, Is.False);
        Assert.That(reloaded.HasPendingRewardSettlement, Is.False);
        Assert.That(reloaded.CanResumeExpedition, Is.False);
        Assert.That(reloaded.CanChangeCampaignSelection, Is.True,
            "fresh state는 새 expedition 시작 가능.");
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

    [Test]
    public void PledgedWarrant_Intact_Kept_RecordsDossierAndStampsFlag()
    {
        // ludonarrative 루프 P2a 통합 evidence: 출격 전 서약(PledgeWarrant) → 전투 사실로 판정
        // (WarrantJudge) → DossierEntryRecord에 outcome 기록 + chapter-scoped story flag stamp.
        // overlay(write) → settlement → judge → record/flag(read) 전 rail을 한 번에 통과시킨다.
        var lookup = EditorFreeCombatContentFixture.CreateRunLoopLookup();
        var session = CreateBoundSession(lookup);
        session.BeginNewExpedition();
        Assert.That(session.PrepareSelectedBattleNodeHandoff(), Is.True);
        session.BuildBattleLoadoutSnapshot();

        session.PledgeWarrant(WarrantCatalog.IntactId);

        // 전원 생존(손실 0) + 승리 → Intact Kept.
        var finalUnits = new List<BattleUnitReadModel>
        {
            CreateAllyUnit("hero-1", alive: true),
            CreateAllyUnit("hero-2", alive: true),
            CreateAllyUnit("hero-3", alive: true),
            CreateAllyUnit("hero-4", alive: true),
        };
        session.MarkBattleResolved(victory: true, stepCount: 8, eventCount: 4, finalUnits);

        var dossier = session.Profile.Dossier.LastOrDefault();
        Assert.That(dossier, Is.Not.Null, "MarkBattleResolved가 Dossier entry를 기록해야 한다.");
        Assert.That(dossier!.WarrantId, Is.EqualTo(WarrantCatalog.IntactId));
        Assert.That(dossier.WarrantOutcome, Is.EqualTo("kept"));
        Assert.That(dossier.ChapterId, Is.Not.Empty);

        Assert.That(session.NarrativeProgress.StoryFlags,
            Does.Contain($"story_flag_{dossier.ChapterId}_warrant_kept"),
            "서약 이행이 chapter-scoped story flag로 stamping돼야 한다(P2b 분기 입력).");
    }

    [Test]
    public void PledgedWarrant_Intact_Broken_WhenAllyFalls_StampsBrokenFlag()
    {
        var lookup = EditorFreeCombatContentFixture.CreateRunLoopLookup();
        var session = CreateBoundSession(lookup);
        session.BeginNewExpedition();
        Assert.That(session.PrepareSelectedBattleNodeHandoff(), Is.True);
        session.BuildBattleLoadoutSnapshot();

        session.PledgeWarrant(WarrantCatalog.IntactId);

        // 승리했지만 1명 전사(손실 1) → Intact Broken.
        var finalUnits = new List<BattleUnitReadModel>
        {
            CreateAllyUnit("hero-1", alive: true),
            CreateAllyUnit("hero-2", alive: true),
            CreateAllyUnit("hero-3", alive: true),
            CreateAllyUnit("hero-4", alive: false),
        };
        session.MarkBattleResolved(victory: true, stepCount: 8, eventCount: 4, finalUnits);

        var dossier = session.Profile.Dossier.LastOrDefault();
        Assert.That(dossier, Is.Not.Null);
        Assert.That(dossier!.WarrantId, Is.EqualTo(WarrantCatalog.IntactId));
        Assert.That(dossier.WarrantOutcome, Is.EqualTo("broken"));
        // GPT Pro §5.2/§5.3: "왜 깼나"가 Dossier에 남는다 — 온전 위반은 ally_killed/major.
        Assert.That(dossier.WarrantFailureReason, Is.EqualTo("ally_killed"));
        Assert.That(dossier.WarrantSeverity, Is.EqualTo("major"));

        Assert.That(session.NarrativeProgress.StoryFlags,
            Does.Contain($"story_flag_{dossier.ChapterId}_warrant_broken"));
    }

    [Test]
    public void PledgedWarrant_NotPledged_RecordsNotApplicable_AndStampsNoWarrantFlag()
    {
        // 미서약(default) sortie는 NotApplicable로 기록되고 warrant flag를 stamp하지 않는다 — ships dark 안전성.
        var lookup = EditorFreeCombatContentFixture.CreateRunLoopLookup();
        var session = CreateBoundSession(lookup);
        session.BeginNewExpedition();
        Assert.That(session.PrepareSelectedBattleNodeHandoff(), Is.True);
        session.BuildBattleLoadoutSnapshot();

        var finalUnits = new List<BattleUnitReadModel>
        {
            CreateAllyUnit("hero-1", alive: true),
            CreateAllyUnit("hero-2", alive: true),
            CreateAllyUnit("hero-3", alive: true),
            CreateAllyUnit("hero-4", alive: true),
        };
        session.MarkBattleResolved(victory: true, stepCount: 8, eventCount: 4, finalUnits);

        var dossier = session.Profile.Dossier.LastOrDefault();
        Assert.That(dossier, Is.Not.Null);
        Assert.That(dossier!.WarrantId, Is.Empty);
        Assert.That(dossier.WarrantOutcome, Is.EqualTo("not_applicable"));
        Assert.That(session.NarrativeProgress.StoryFlags,
            Has.None.Contains("_warrant_"),
            "미서약은 어떤 warrant flag도 stamp하지 않는다.");
    }

    [Test]
    public void PledgedWarrant_SolarumOrder_Satisfied_ShiftsFactionTrust()
    {
        // ADR-0028 정치 warrant 통합: issuer/opposed faction이 붙은 warrant를 이행하면
        // SaveProfile.FactionStanding(profile truth) trust가 바뀐다 — 전투→정치 절반의 rail.
        var lookup = EditorFreeCombatContentFixture.CreateRunLoopLookup();
        var session = CreateBoundSession(lookup);
        session.BeginNewExpedition();
        Assert.That(session.PrepareSelectedBattleNodeHandoff(), Is.True);
        session.BuildBattleLoadoutSnapshot();

        // 솔라룸 질서 위임 — issuer=faction_solarum, opposed=faction_pale_conclave, 조건=Intact(손실 0).
        session.PledgeWarrant(WarrantCatalog.SolarumOrderId);

        var finalUnits = new List<BattleUnitReadModel>
        {
            CreateAllyUnit("hero-1", alive: true),
            CreateAllyUnit("hero-2", alive: true),
            CreateAllyUnit("hero-3", alive: true),
            CreateAllyUnit("hero-4", alive: true),
        };
        session.MarkBattleResolved(victory: true, stepCount: 8, eventCount: 4, finalUnits);

        var solarum = session.Profile.FactionStanding.FirstOrDefault(f => f.FactionId == WarrantCatalog.SolarumId);
        var paleConclave = session.Profile.FactionStanding.FirstOrDefault(f => f.FactionId == WarrantCatalog.PaleConclaveId);
        Assert.That(solarum, Is.Not.Null, "satisfied 시 issuer 세력 standing이 생긴다.");
        Assert.That(solarum!.Trust, Is.EqualTo(FactionTrustService.SatisfiedIssuerGain));
        Assert.That(paleConclave, Is.Not.Null, "satisfied 시 opposed 세력도 영향받는다.");
        Assert.That(paleConclave!.Trust, Is.EqualTo(-FactionTrustService.SatisfiedOpposedLoss));

        // WarrantResult(issuer/opposed)가 Dossier에 영속.
        var dossier = session.Profile.Dossier.LastOrDefault();
        Assert.That(dossier!.IssuerFactionId, Is.EqualTo(WarrantCatalog.SolarumId));
        Assert.That(dossier.OpposedFactionId, Is.EqualTo(WarrantCatalog.PaleConclaveId));

        // #5 OfferSet: solarum 사이드는 wolfpine/lattice 제안을 거절 → Dossier 기록 + 그 세력 신뢰 하락(대립 pale은 제외).
        Assert.That(dossier.RejectedFactionIds, Does.Contain(WarrantCatalog.WolfpineId));
        Assert.That(dossier.RejectedFactionIds, Does.Contain(WarrantCatalog.LatticeId));
        Assert.That(dossier.RejectedFactionIds, Does.Not.Contain(WarrantCatalog.PaleConclaveId));
        var wolfpine = session.Profile.FactionStanding.FirstOrDefault(f => f.FactionId == WarrantCatalog.WolfpineId);
        Assert.That(wolfpine!.Trust, Is.EqualTo(-WarrantOfferService.RejectedOfferLoss));

        // incident-centric Dossier: 구조적 세력 효과(세력·delta·사유)가 incident에 박힌다 — id projection의 source.
        var solarumEffect = dossier.PoliticalEffects.Single(effect => effect.FactionId == WarrantCatalog.SolarumId);
        Assert.That(solarumEffect.Delta, Is.EqualTo(FactionTrustService.SatisfiedIssuerGain));
        Assert.That(solarumEffect.Reason, Is.EqualTo("kept_issuer"));
        Assert.That(dossier.PoliticalEffects.Any(effect => effect.Reason == "rejected_offer"), Is.True, "거절 효과도 구조적으로 기록된다.");
    }

    private static BattleUnitReadModel CreateAllyUnit(string id, bool alive)
    {
        return new BattleUnitReadModel(
            Id: id,
            Name: id,
            Side: TeamSide.Ally,
            Anchor: DeploymentAnchorId.FrontCenter,
            RaceId: "human",
            ClassId: "vanguard",
            Position: new CombatVector2(0f, 0f),
            CurrentHealth: alive ? 20f : 0f,
            MaxHealth: 20f,
            IsAlive: alive,
            ActionState: CombatActionState.AcquireTarget,
            PendingActionType: BattleActionType.BasicAttack,
            TargetId: "enemy",
            TargetName: "Enemy",
            WindupProgress: 0f,
            CooldownRemaining: 0f,
            CurrentEnergy: 0f,
            MaxEnergy: 100f,
            IsDefending: false,
            HeadAnchorHeight: 1.5f);
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
