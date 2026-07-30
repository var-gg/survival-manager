using System.IO;
using NUnit.Framework;

namespace SM.Tests.EditMode.FastUnit.Battle;

[Category("FastUnit")]
public sealed class BattleHudShellVisualContractFastTests
{
    [Test]
    public void BattleHudShell_UsesProductionCopy_AndHidesPrematureContinue()
    {
        var presenter = File.ReadAllText("Assets/_Game/Scripts/Runtime/Unity/UI/Battle/BattleScreenPresenter.cs");
        var localizationBootstrap = File.ReadAllText("Assets/_Game/Scripts/Editor/Bootstrap/LocalizationFoundationBootstrap.cs");
        var koreanBattleTable = File.ReadAllText("Assets/Localization/StringTables/UI_Battle_ko.asset");
        var englishBattleTable = File.ReadAllText("Assets/Localization/StringTables/UI_Battle_en.asset");

        Assert.That(presenter, Does.Contain("ui.battle.title"));
        Assert.That(presenter, Does.Contain("ui.battle.playback.ingame"));
        Assert.That(presenter, Does.Not.Contain(".production"));
        Assert.That(presenter, Does.Contain("\"원정 전투\""));
        Assert.That(presenter, Does.Contain("!isDirect && isBattleFinished"));
        Assert.That(presenter, Does.Not.Contain("Authored Expedition Battle"));
        Assert.That(localizationBootstrap, Does.Not.Contain("Authored Expedition Battle"));
        Assert.That(localizationBootstrap, Does.Not.Contain("Authored 원정 전투"));
        Assert.That(koreanBattleTable, Does.Not.Contain("Authored"));
        Assert.That(englishBattleTable, Does.Not.Contain("Authored"));
    }

    [Test]
    public void BattleHudShell_UsesExistingPortraitAssets_AsFallbackPresentation()
    {
        var resolver = File.ReadAllText("Assets/_Game/Scripts/Runtime/Unity/UI/Battle/BattleUnitPortraitResolver.cs");

        Assert.That(resolver, Does.Contain("EnumerateCharacterIds"));
        Assert.That(resolver, Does.Contain("hero_aegis_sentinel"));
        Assert.That(resolver, Does.Contain("hero_pale_executor"));
        Assert.That(resolver, Does.Contain("boss_gate_warden"));
    }

    [Test]
    public void BattleHudShell_KeepsBattlefieldVisibleUnderBibleChrome()
    {
        var uss = File.ReadAllText("Assets/_Game/UI/Screens/Battle/BattleScreen.uss");
        var uxml = File.ReadAllText("Assets/_Game/UI/Screens/Battle/BattleScreen.uxml");

        Assert.That(uss, Does.Contain("background-color: rgba(0, 0, 0, 0)"));
        Assert.That(uxml, Does.Not.Contain("BattleBibleOverlay"));
        Assert.That(uxml, Does.Not.Contain("BattleMinimapPanel"));
        Assert.That(uxml, Does.Not.Contain("BattleCommandDock"));
        Assert.That(uxml, Does.Not.Contain("BattleResourceStack"));
        Assert.That(uss, Does.Not.Contain("sm-bs-stage-signals"));
        Assert.That(uss, Does.Not.Contain("sm-bs-stage-signal--blue"));
        Assert.That(uss, Does.Not.Contain("sm-bs-minimap-panel"));
        Assert.That(uss, Does.Not.Contain("sm-bs-command-dock"));
        Assert.That(uss, Does.Not.Contain("sm-bs-resource-stack"));
        Assert.That(uxml, Does.Not.Contain("ui_ux_bible"));
        Assert.That(uxml, Does.Not.Contain("Screenshots/mockups"));
        Assert.That(uxml, Does.Contain("ProgressTrack"));
        Assert.That(uxml, Does.Contain("AllyRosterList"));
        Assert.That(uxml, Does.Contain("PlaybackActionsGroup"));
        Assert.That(uxml, Does.Contain("ObserverStatusGroup"));

        // 2026-07-31 — 아래 패널들은 <b>일부러</b> 없앴다. 다시 들어오면 이 테스트가 잡는다.
        //
        // 이 화면은 같은 8 유닛을 상단 칩 레일 · 적군 로스터 카드 · 좌측 세로 레일 · 전력 분포
        // 산점도 · 전황 판단 텔레메트리로 <b>중복해서</b> 그리고 있었다. 유닛 상태를 네 번,
        // "누가 이기나"를 세 번 반복한 셈이라 전투 화면이 아니라 관측 대시보드로 읽혔다.
        // 적 상태와 유닛 위치는 <b>전장에 이미 그려져 있다</b> — 머리 위 HP 바와 실제 배치가 그것이다.
        // 패널로 다시 그리는 건 정보 추가가 아니라 전장 가림이었다.
        Assert.That(uxml, Does.Not.Contain("TurnOrderStrip"), "상단 칩 레일은 파티 바와 중복이라 제거됨");
        Assert.That(uxml, Does.Not.Contain("EnemyRosterList"), "적 상태는 전장 머리 위 HP 바로 읽는다");
        Assert.That(uxml, Does.Not.Contain("BattleIntelPanel"), "전력 분포 산점도는 전장 배치의 중복 표현");
        Assert.That(uxml, Does.Not.Contain("BattleSideRail"), "좌측 세로 레일은 파티 바와 중복");
        Assert.That(uxml, Does.Not.Contain("TacticalReadoutPanel"), "전황 판단은 텔레메트리 대시보드");
    }

    [Test]
    public void BattleHudShell_UsesRuntimeReadouts_NotStaticMockPanels()
    {
        var viewState = File.ReadAllText("Assets/_Game/Scripts/Runtime/Unity/UI/Battle/BattleScreenViewState.cs");
        var presenter = File.ReadAllText("Assets/_Game/Scripts/Runtime/Unity/UI/Battle/BattleScreenPresenter.cs");
        var view = File.ReadAllText("Assets/_Game/Scripts/Runtime/Unity/UI/Battle/BattleScreenView.cs");
        var uxml = File.ReadAllText("Assets/_Game/UI/Screens/Battle/BattleScreen.uxml");
        var uss = File.ReadAllText("Assets/_Game/UI/Screens/Battle/BattleScreen.uss");

        // 남은 readout 은 여전히 런타임 데이터라야 한다 — 이 테스트의 원래 목적은
        // "정적 목업 패널을 붙이지 마라"였고 그건 유효하다. 대상만 새 구성으로 바뀐 것이다.
        Assert.That(view, Does.Contain("RenderObserverStatusDock"));
        Assert.That(uxml, Does.Contain("ObserverStatusGroup"));
        Assert.That(uss, Does.Contain("sm-bs-observer-group"));
        Assert.That(uxml, Does.Not.Contain("BattleMinimapPanel"));
        Assert.That(uxml, Does.Not.Contain("BattleCommandDock"));

        // 파티 바가 이 화면의 유일한 아군 상태 표면이 됐으므로, HP 가 <b>실제로 칠해지는지</b>를 계약으로 건다.
        //
        // 이건 이론이 아니다. 이 바는 클래스만 걸려 있던 동안 <b>양 팀 모두 화면에 한 번도 안 나왔다</b>.
        // 렌더 코드도 있고 USS 규칙도 하나뿐이었는데 캡쳐를 픽셀로 훑으면 초록 채널 우세 픽셀이 0 이었다.
        // 원인은 트랙이 column 플렉스인데 fill 이 퍼센트 높이를 쓴 것 — 교차축 stretch 와 얽혀 안 칠해졌다.
        // 그래서 트랙을 row 로 눕히고 높이를 픽셀로 박았다. 되돌아가면 HP 바가 조용히 사라진다.
        Assert.That(viewState, Does.Contain("HealthText"), "파티 카드가 HP 수치를 들고 있어야 한다");
        Assert.That(presenter, Does.Contain("BuildRosterHealthText"));
        Assert.That(presenter, Does.Contain("BuildRosterActionLabel"), "카드 문구는 동사만 — 화살표 상태기계 로그 금지");
        Assert.That(view, Does.Contain("ResolveHealthColor"), "체력 구간별 색이 있어야 한 눈에 위험이 읽힌다");
        Assert.That(
            view,
            Does.Contain("track.style.flexDirection = FlexDirection.Row"),
            "HP 트랙이 column 이면 fill 이 칠해지지 않는다 — 실제로 그래서 안 보였다");
    }
}
