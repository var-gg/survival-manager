using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using SM.Combat.Model;
using SM.Meta;
using SM.Persistence.Abstractions.Models;
using SM.Tests.EditMode.Fakes;
using SM.Unity;
using SM.Unity.UI.Atlas;

namespace SM.Tests.EditMode;

/// <summary>
/// Headless UX-sim 골든 (Phase 2 Stage 0) — "Unity는 껍데기, 게임플레이 truth는 순수 C#" 비전의 첫 실증.
/// 씬·VisualElement 없이 한 판을 끝까지 플레이한다: Town 편성 → 출격 확인(SortieConfirm) → 서약(WarrantSelection)
/// → 전투 판정 → 보상 정산 → Town 복귀. 각 단계에서 "플레이어가 볼 화면 데이터"를 conformant presenter의
/// BuildState()로 렌더 없이 읽어 세션 진실과 일치하는지 검증한다 — 즉 AI가 화면을 보며 게임을 진행하는 것과
/// 동등한 시뮬레이션이 C# 선에서 성립함을 골든으로 고정한다.
///
/// 이 골든이 구동하는 화면 = headless conformance 감사에서 'presenter ctor가 VisualElement 비의존'으로 판정된
/// 부류(SortieConfirm/WarrantSelection). Town hub / Battle HUD / Reward presenter는 아직 view-bound라
/// 여기선 세션 레벨로만 검증하고, presenter 직접 구동은 Phase 2 후속 stage(presenter decoupling)에서 합류한다.
/// 토대: 세션 전 루프는 RunLoopContractFastTests가 이미 씬 없이 구동(BeginNewExpedition→…→ReturnToTown).
/// </summary>
[Category("FastUnit")]
public sealed class HeadlessPlaythroughGoldenFastTests
{
    [Test]
    public void AiPlaythrough_DrivesFullLoopAndReadsConformantScreens_WithoutScene()
    {
        // --- Boot/Town: 세션을 띄우고 4인 로스터 바인딩 (씬 0) ---
        var lookup = EditorFreeCombatContentFixture.CreateRunLoopLookup();
        var session = GameSessionTestFactory.Create(lookup);
        session.BindProfile(new SaveProfile
        {
            ProfileId = "headless_playthrough_golden",
            Heroes = new List<HeroInstanceRecord>
            {
                CreateHero("hero-1", "선봉 하나", "vanguard"),
                CreateHero("hero-2", "사수 하나", "ranger"),
                CreateHero("hero-3", "결투 하나", "duelist"),
                CreateHero("hero-4", "신비 하나", "mystic"),
            },
        });
        session.SetCurrentScene(SceneNames.Town);

        // --- 편성: 플레이어가 4인을 anchor에 배치 (진형 셋업) ---
        DeployExactly(session,
            ("hero-1", DeploymentAnchorId.FrontTop),
            ("hero-2", DeploymentAnchorId.BackTop),
            ("hero-3", DeploymentAnchorId.FrontCenter),
            ("hero-4", DeploymentAnchorId.BackCenter));

        // --- 출격 확인 화면(SortieConfirm presenter)을 렌더 없이 읽는다 — AI가 "보낼 진형"을 본다 ---
        var catalog = new LaunchCoreRosterBaselineCatalog(lookup);
        var sortie = new SortieConfirmPresenter(session, catalog, id => id, id => id, anchor => anchor.ToString())
            .BuildState();
        Assert.That(sortie.DeploySlots.Count, Is.EqualTo(6), "6 anchor 전부 화면에 노출.");
        Assert.That(sortie.DeployCount, Is.EqualTo(4), "배치한 4인이 화면 슬롯에 보인다.");
        Assert.That(sortie.CanLaunch, Is.True, "1인 이상 배치 → 출격 가능.");
        Assert.That(sortie.SynergyChips.Any(chip => chip.IsActive), Is.True, "활성 시너지가 화면에 노출된다.");

        // --- 출발: expedition 시작 + 첫 전투 노드 핸드오프 ---
        session.BeginNewExpedition();
        Assert.That(session.PrepareSelectedBattleNodeHandoff(), Is.True, "첫 노드는 전투 — 핸드오프 준비.");
        session.BuildBattleLoadoutSnapshot();

        // --- 서약 화면(WarrantSelection presenter)을 렌더 없이 읽고 선택한다 (presenter command 경로) ---
        var warrant = new WarrantSelectionPresenter(standingLookup: _ => 0, factionName: id => id, warrantName: id => id);
        var warrantState = warrant.BuildState();
        Assert.That(warrantState.Cards.Count, Is.GreaterThan(0), "서약 카드가 화면에 노출된다.");
        warrant.Select(warrantState.Cards[0].WarrantId);
        Assert.That(warrant.SelectedWarrantId, Is.EqualTo(warrantState.Cards[0].WarrantId),
            "서약 선택이 presenter 상태에 반영(command headless).");
        session.PledgeWarrant(WarrantCatalog.IntactId);

        // --- 전투 판정 (sim은 이미 headless): 전원 생존 승리 ---
        session.MarkBattleResolved(victory: true, stepCount: 8, eventCount: 4, new List<BattleUnitReadModel>
        {
            CreateAllyUnit("hero-1", alive: true),
            CreateAllyUnit("hero-2", alive: true),
            CreateAllyUnit("hero-3", alive: true),
            CreateAllyUnit("hero-4", alive: true),
        });
        var dossier = session.Profile.Dossier.LastOrDefault();
        Assert.That(dossier, Is.Not.Null, "전투 판정이 Dossier에 기록된다.");
        Assert.That(dossier!.WarrantOutcome, Is.EqualTo("kept"), "전원 생존 → Intact 서약 이행(kept).");

        // --- 보상 정산: 카드가 제시되고 하나를 고른다 ---
        Assert.That(session.HasPendingRewardSettlement, Is.True, "전투 후 보상 정산 대기.");
        Assert.That(session.PendingRewardChoices, Has.Count.EqualTo(3), "보상 3안 제시.");
        Assert.That(session.ApplyRewardChoice(0), Is.True, "보상 1안 확정.");

        // --- Town 복귀: 루프가 닫히고 보상이 영구 ledger에 커밋된다 ---
        session.ReturnToTownAfterReward();
        Assert.That(session.HasPendingRewardSettlement, Is.False, "복귀 후 정산 종료(루프 닫힘).");
        Assert.That(
            session.Profile.RewardLedger.Any(entry => entry.SourceKind.EndsWith(":reward_choice", System.StringComparison.Ordinal)),
            Is.True,
            "확정한 보상이 영구 ledger에 커밋됐다 — Town inventory/equipment refresh의 source.");
    }

    private static void DeployExactly(GameSessionState session, params (string HeroId, DeploymentAnchorId Anchor)[] placements)
    {
        // 자동배치 잔여를 먼저 비우고 명시 배치만 남긴다 → 배치가 placements와 정확히 일치.
        foreach (var anchor in session.DeploymentAnchors)
        {
            session.AssignHeroToAnchor(anchor, null);
        }

        foreach (var (heroId, anchor) in placements)
        {
            Assert.That(session.AssignHeroToAnchor(anchor, heroId), Is.True, $"{heroId} → {anchor} 배치 성공.");
        }
    }

    private static HeroInstanceRecord CreateHero(string heroId, string name, string classId)
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
}
