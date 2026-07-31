using System.Collections;
using NUnit.Framework;
using SM.Core;
using SM.Meta;
using SM.Unity;
using SM.Unity.UI.Atlas;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace SM.Tests.PlayMode;

/// <summary>
/// <b>조건부로만 뜨는 화면</b>의 도달 경로.
///
/// 2026-07-31 에 인트로 컷씬이 "한 번도 실화면을 못 본 채로" 살아 있던 것이 발견됐다
/// (<c>66a8532f</c>). 원인은 결함이 아니라 <b>도달 경로의 부재</b>였다 — 그 화면은 신규 프로필
/// 첫 실행에서만 뜨는데, 그 실행은 도달 전에 다른 이유로 죽었다.
///
/// 같은 이유로 아직 못 본 화면이 셋 더 있었다. 여기서 각각에 전용 경로를 둔다.
///
/// <list type="bullet">
/// <item>패배 정산 — 정산 화면의 절반은 <c>LastBattleVictory</c> 분기다. 승리만 봐 왔다.</item>
/// <item>차단 에러 시작 화면 — 세이브를 못 읽으면 뜬다. 정상 실행에서는 영원히 안 뜬다.</item>
/// <item>무한 순환 진입 — 엔딩 이후에만 원정 CTA 가 바뀐다.</item>
/// </list>
///
/// 공통 원칙은 컷씬 위트니스와 같다. <b>표면 하나만 겨냥</b>하고(스모크 레인에 얹으면 그 레인이
/// 다른 이유로 죽을 때 같이 못 본다), <b>조건만 세우고 화면 규칙은 우회하지 않는다.</b>
/// </summary>
public sealed partial class UxBiblePlayModeWitnessTests
{
    /// <summary>
    /// 패배 정산 — 정산 모달이 패배 분기에서도 성립하는가.
    ///
    /// 정산 화면은 승리/패배로 <b>머리줄·상태 힌트·복귀 CTA·생존자 HP 문구</b>가 모두 갈린다.
    /// 게다가 패배는 전리품 번들을 비우므로(<c>MarkBattleResolved</c> 가 <c>_lastAutomaticLootBundle</c>
    /// 를 null 로 되돌린다) <b>화폐 칩 줄이 통째로 빈다.</b> 4구역 모달이 그 상태에서 무너지지
    /// 않는지는 실제로 띄워 보지 않으면 알 수 없다.
    ///
    /// 승리 경로 위트니스와 같은 API(<c>MarkBattleResolved</c>)로 결과만 뒤집는다 — sim 이 실제로
    /// 졌다고 주장하지 않는다. 여기서 보는 것은 <b>정산 표면</b>이다.
    /// </summary>
    [UnityTest]
    public IEnumerator Reward_DefeatSettlement_IsReachableAndKeepsTheModalIntact()
    {
        yield return EnterOfflineTownFromBoot();

        var root = GameSessionRoot.Instance!;
        yield return WalkAuthoredRouteToBattle(root);

        var battle = RequireAny<BattleScreenController>("Battle controller should exist for the defeat settlement route.");
        FinishBattleForWitness(battle);
        yield return WaitFrames(2);

        root.SessionState.MarkBattleResolved(false, battle.LatestStep?.StepIndex ?? 0, 0);
        var checkpoint = root.SaveProfile(SessionCheckpointKind.BattleResolved);
        Assert.That(checkpoint.IsSuccessful, Is.True, checkpoint.Message);
        Assert.That(root.SessionState.LastBattleVictory, Is.False, "패배 정산 상태여야 이 위트니스가 의미를 가진다.");

        battle.ContinueToReward();
        yield return WaitForScene(SceneNames.Reward);
        yield return WaitForComponent<RewardScreenController>();
        var rewardHost = RequirePanelHost("RewardRuntimePanelHost");
        yield return WaitFrames(2);

        var headline = Require<Label>(rewardHost.Root, "ResultHeadlineLabel");
        Assert.That(headline.text, Is.Not.Empty, "패배도 결과 머리줄을 쓴다 — 빈 줄은 화면이 무너졌다는 뜻이다.");
        Assert.That(
            headline.text,
            Does.Not.Contain("승리"),
            $"패배 정산에 승리 머리줄이 나왔다: '{headline.text}'");

        // 복귀 CTA 는 패배 전용 라벨을 쓴다. 라벨이 비면 플레이어가 나갈 문을 못 찾는다.
        AssertNonEmptyText<Button>(rewardHost.Root, "ReturnTownButton");
        AssertNonEmptyText<Label>(rewardHost.Root, "StatusLabel");

        AssertRewardContainment(rewardHost.Root);
        AssertNoRedText(rewardHost.Root, "Reward defeat settlement");
        yield return Capture("reward_defeat");
        _packet?.RecordPass($"Reward defeat settlement rendered: {headline.text}");
    }

    /// <summary>
    /// 차단 에러 상태의 시작 화면.
    ///
    /// 세이브를 못 읽으면 <see cref="GameSessionRoot.LastBlockingError"/> 가 채워지고 시작 CTA 가
    /// 잠긴다. 정상 실행에서는 절대 안 뜨므로 <b>이 화면은 한 번도 검토된 적이 없다.</b>
    ///
    /// 여기 넣는 문자열은 지어낸 것이 아니라 <c>JsonSaveRepository.BuildCorruptionMessage</c> 가
    /// 실제로 만드는 문자열이다. 즉 <b>플레이어가 실제로 보게 될 문장</b>이다 — 그리고 처음
    /// 띄워 봤을 때 이 문자열이 화면 본문 자리에 그대로 앉아 있었다.
    /// </summary>
    [UnityTest]
    public IEnumerator Boot_BlockingError_IsReachableAndLocksTheStartCta()
    {
        SceneManager.LoadScene(SceneNames.Boot);
        yield return WaitForScene(SceneNames.Boot);
        yield return WaitForCondition(() => GameSessionRoot.Instance != null, 8f);
        yield return WaitForComponent<BootScreenController>();

        var root = GameSessionRoot.Instance!;
        var boot = RequireAny<BootScreenController>("Boot controller should exist for the blocking-error witness.");
        var bootHost = RequirePanelHost("BootPanelHost");
        yield return WaitForCondition(() => bootHost.Root.Q<Button>("BootStartButton") != null, 4f);

        // GameBootstrap 의 부트스트랩 코루틴은 로컬라이제이션 초기화를 기다린 뒤
        // ClearBlockingError() + 시작 화면 재렌더를 한다. 그 전에 에러를 세우면 지워진다 —
        // 실제로 이 위트니스가 그렇게 한 번 조용히 통과하고 한 번 실패했다(초기화 시간 차이).
        // 부트스트랩이 끝난 뒤에 세운다.
        yield return WaitForCondition(() => root.Localization.IsInitialized, 15f);
        yield return WaitFrames(3);

        // JsonSaveRepository.BuildCorruptionMessage 가 만드는 형태 그대로.
        const string RepositoryFailure = "Save recovery failed. primary=invalid; backup=missing";
        LogAssert.Expect(LogType.Error, RepositoryFailure);
        root.SetBlockingError(RepositoryFailure);
        boot.Refresh();
        yield return WaitFrames(2);

        Assert.That(
            root.HasBlockingError,
            Is.True,
            "차단 에러가 위트니스 도중 지워졌다 — 부트스트랩 코루틴이 아직 돌고 있었다는 뜻이다.");

        var startButton = Require<Button>(bootHost.Root, "BootStartButton");
        var status = Require<Label>(bootHost.Root, "BootStatusLabel");
        var diagnostic = $"HasBlockingError={root.HasBlockingError}, "
                         + $"LastBlockingError='{root.LastBlockingError}', "
                         + $"status='{status.text}', cta='{startButton.text}'";

        Assert.That(startButton.enabledSelf, Is.False, $"차단 에러 상태에서는 시작 CTA 가 잠겨야 한다. {diagnostic}");
        Assert.That(startButton.text, Is.Not.Empty, "잠긴 버튼도 라벨은 있어야 한다.");

        // 사유는 서비스가 정하고 문구는 UI 가 정한다 — 본문 자리에 진단 문자열이 오면 안 된다.
        Assert.That(status.text, Is.Not.Empty, "무엇이 막혔는지 화면이 말해야 한다.");
        Assert.That(
            status.text,
            Is.Not.EqualTo(RepositoryFailure),
            "저장소 진단 문자열이 첫 화면 본문에 그대로 나왔다 — 여기는 플레이어의 문장 자리다.");
        Assert.That(status.text, Does.Not.Contain("primary="), "진단 키가 플레이어 문장에 섞이면 안 된다.");

        // 그렇다고 사유를 숨기지도 않는다 — 아래 줄이 받는다.
        var reason = Require<Label>(bootHost.Root, "BootHintLabel");
        Assert.That(reason.text, Is.EqualTo(RepositoryFailure), "차단 사유 자체는 화면에서 사라지면 안 된다.");
        Assert.That(
            reason.resolvedStyle.fontSize,
            Is.LessThan(status.resolvedStyle.fontSize),
            "사유 줄은 본문보다 작아야 한다 — 무엇이 읽을 문장인지 눈이 먼저 알아야 한다.");

        AssertVisibleTextContained(Require<VisualElement>(bootHost.Root, "BootCard"), "Boot blocking error card");

        yield return Capture("boot_blocking_error");
        _packet?.RecordPass($"Boot blocking-error state rendered: {status.text}");

        root.ClearBlockingError();
        boot.Refresh();
    }

    /// <summary>
    /// 무한 순환 진입 — 엔딩 이후 원정 CTA.
    ///
    /// 버튼은 하나고 라벨/라우팅만 바뀐다(죽은 affordance 방지). 그래서 <b>엔딩을 보기 전에는
    /// 이 라벨이 존재하는지조차 확인할 길이 없었다.</b> 영속 진행도의 <c>EndlessUnlocked</c> 만
    /// 세우고 마을을 다시 들어간다 — 판정(<see cref="EndlessEntryResolver"/>)이 읽는 값이 그것뿐이고,
    /// 실제 플레이어 경로도 "엔딩 → 마을 복귀" 다.
    /// </summary>
    [UnityTest]
    public IEnumerator Town_EndlessCycleEntry_IsReachableAndRoutesIntoAnEndlessRun()
    {
        yield return EnterOfflineTownFromBoot();

        var root = GameSessionRoot.Instance!;

        // 진행도는 디스크에 남는다 — 스모크 프로필도 예외가 아니다. 이 위트니스가 세우는
        // EndlessUnlocked 를 그대로 두면 <b>다음 실행의 기준선이 오염된다</b>(실제로 2회차에서
        // "엔딩 전" 라벨이 이미 무한 순환이라 비교가 무너졌다). 두 상태를 모두 명시로 세운다.
        // 회차 카운터도 같이 되감는다 — 안 그러면 실행할수록 5회차, 9회차 … 로 올라가
        // 캡쳐가 "처음 무한 순환에 들어간 화면"을 더는 보여주지 않는다.
        root.SessionState.StoryDirector.SetEndlessCycle(EndlessCycleStateRecord.Empty);
        yield return SetEndlessUnlocked(root, false);
        var townHost = RequirePanelHost("TownRuntimePanelHost");
        var storyCtaLabel = Require<Button>(townHost.Root, "ExpeditionButton").text;
        Assert.That(storyCtaLabel, Is.Not.Empty, "스토리 원정 CTA 라벨이 있어야 비교가 성립한다.");

        yield return SetEndlessUnlocked(root, true);

        var endlessHost = RequirePanelHost("TownRuntimePanelHost");
        var endlessCta = Require<Button>(endlessHost.Root, "ExpeditionButton");
        Assert.That(endlessCta.text, Is.Not.Empty);
        Assert.That(
            endlessCta.text,
            Is.Not.EqualTo(storyCtaLabel),
            $"엔딩 이후 원정 CTA 는 무한 순환 진입으로 바뀌어야 한다 — 라벨이 '{storyCtaLabel}' 그대로다.");
        AssertNoRedText(endlessHost.Root, "Town endless entry");
        yield return Capture("town_endless_entry");
        _packet?.RecordPass($"Town endless entry CTA: {endlessCta.text}");

        // 라벨만 바뀌고 라우팅이 안 바뀌면 그것이야말로 죽은 affordance다 — 실제로 눌러 확인한다.
        RequireAny<TownScreenController>("Town controller should exist to open the endless cycle.").OpenExpedition();
        yield return WaitForScene(SceneNames.Atlas);
        yield return WaitForComponent<AtlasScreenController>();
        var atlasHost = RequirePanelHost("AtlasRuntimePanelHost");
        yield return WaitFrames(2);

        Assert.That(
            root.SessionState.ActiveRun?.EndlessCycleIndex ?? 0,
            Is.GreaterThan(0),
            "무한 순환 CTA 는 무한 run 으로 들어가야 한다(스토리 원정으로 안전 강하하면 라벨이 거짓말이다).");

        // 모드가 실재하는데 화면에 안 보이면 플레이어는 자기가 어디 있는지 모른다.
        // 2026-07-31 이전 각인도는 스토리 원정과 글자 하나 다르지 않았다.
        var regionTitle = Require<Label>(atlasHost.Root, "atlas-region-title");
        Assert.That(
            regionTitle.text,
            Does.Contain("순환"),
            $"각인도가 무한 순환 run 임을 말해야 한다 — 제목이 '{regionTitle.text}' 다.");

        AssertNoRedText(atlasHost.Root, "Atlas endless region");
        yield return Capture("atlas_endless_region");
        _packet?.RecordPass(
            $"Endless cycle run started: cycle={root.SessionState.ActiveRun!.EndlessCycleIndex}, "
            + $"heat={root.SessionState.StoryDirector.Progress.EndlessCycle.Heat}");

        // 진행도를 원래대로 돌려놓는다 — 안 그러면 다음 실행이 이미 엔딩 후 상태로 시작한다.
        root.SessionState.AbandonExpeditionRun();
        root.SessionState.Profile.CampaignProgress.EndlessUnlocked = false;
        var restored = root.SaveProfile(SessionCheckpointKind.ManualSave);
        Assert.That(restored.IsSuccessful, Is.True, restored.Message);
    }

    /// <summary>
    /// 엔딩 도달 여부만 세우고 마을을 다시 그린다. 판정(<see cref="EndlessEntryResolver"/>)이 읽는
    /// 값이 <c>EndlessUnlocked</c> 하나뿐이므로 여기만 건드리면 실제 경로와 같은 화면이 나온다.
    /// </summary>
    private static IEnumerator SetEndlessUnlocked(GameSessionRoot root, bool unlocked)
    {
        root.SessionState.Profile.CampaignProgress.EndlessUnlocked = unlocked;
        var checkpoint = root.SaveProfile(SessionCheckpointKind.ManualSave);
        Assert.That(checkpoint.IsSuccessful, Is.True, checkpoint.Message);
        yield return ReEnterTown();
        RequireAny<TownScreenController>("Town controller should exist after re-entry.").EnsureRuntimeControls();
        yield return WaitFrames(2);
    }

    /// <summary>
    /// Town → Atlas → 출격 확인 → 서약 스킵 → Battle. <c>RunNormalRouteWitness</c> 와 같은 문들을
    /// 지나되 검증은 하지 않는다 — 이 파일의 테스트들은 <b>도착한 화면</b>을 보는 것이 목적이다.
    /// </summary>
    private IEnumerator WalkAuthoredRouteToBattle(GameSessionRoot root)
    {
        PrepareAuthoredRouteWitnessFormation(root);
        RequireAny<TownScreenController>("Town controller should exist before the authored route walk.")
            .OpenExpedition();

        yield return WaitForScene(SceneNames.Atlas);
        yield return WaitForComponent<AtlasScreenController>();
        var atlas = RequireAny<AtlasScreenController>("Atlas controller should exist during the authored route walk.");
        var atlasHost = RequirePanelHost("AtlasRuntimePanelHost");
        atlas.ContinueToExpedition();

        yield return WaitForCondition(() => atlasHost.Root.Q<Button>("SortieLaunchButton") != null, 5f);
        ClickButton(atlasHost.Root, "SortieLaunchButton");

        yield return WaitForCondition(() =>
        {
            var row = atlasHost.Root.Q<VisualElement>("WarrantCardRow");
            return row != null && row.childCount > 0;
        }, 5f);
        ClickButton(atlasHost.Root, "ProceedButton");

        yield return WaitForScene(SceneNames.Battle);
        yield return WaitForComponent<BattleScreenController>();
        var battle = RequireAny<BattleScreenController>("Battle controller should exist after the authored route walk.");
        yield return WaitForCondition(() => battle.LatestStep != null, 5f);
    }

    /// <summary>
    /// 마을 재진입. 영속 진행도를 바꾼 뒤 화면을 다시 그리게 하는 유일하게 정직한 방법이다 —
    /// presenter 를 밖에서 흔들지 않고 실제 씬 전이를 탄다. 같은 씬을 다시 로드하므로
    /// <b>새 controller 인스턴스가 생길 때까지</b> 기다린다(같은 프레임에 옛 것을 잡지 않도록).
    /// </summary>
    private static IEnumerator ReEnterTown()
    {
        var previous = FindAny<TownScreenController>();
        GameSessionRoot.Instance!.SceneFlow.GoToTown();
        yield return WaitForCondition(() =>
        {
            var current = FindAny<TownScreenController>();
            return current != null && current != previous;
        }, 8f);
        yield return WaitFrames(2);
    }
}
