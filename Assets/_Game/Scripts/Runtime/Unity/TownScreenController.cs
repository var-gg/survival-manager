using SM.Core;
using SM.Meta;
using SM.Unity.Narrative;
using SM.Unity.UI;
using SM.Unity.UI.Town;
using SM.Unity.UI.Town.Preview;
using UnityEngine;

namespace SM.Unity
{

public sealed class TownScreenController : MonoBehaviour
{
    [SerializeField] private RuntimePanelHost panelHost = null!;
    [SerializeField] private StorySceneFlowBridge _storyBridge = null!;

    private GameSessionRoot _root = null!;
    private GameLocalizationController _localization = null!;
    private ContentTextResolver _contentText = null!;
    private ContentIconResolver _contentIconResolver = null!;
    private TownScreenPresenter? _presenter;
    private SquadBuilderPresenter? _squadBuilderPresenter;
    private TacticalWorkshopPresenter? _tacticalWorkshopPresenter;
    private RecruitPresenter? _recruitPresenter;
    private EquipmentRefitPresenter? _equipmentRefitPresenter;
    private PassiveBoardPresenter? _passiveBoardPresenter;
    private InventoryPresenter? _inventoryPresenter;
    private PermanentAugmentPresenter? _permanentAugmentPresenter;
    private CompendiumPresenter? _compendiumPresenter;
    private TownCharacterSheetPresenter? _characterSheetPresenter;
    private RosterGridView? _rosterModalView;
    private RosterGridPresenter? _rosterGridPresenter;
    // jjjj hub V3 NPC mapping (pindoc://decision-town-hub-v3-ashglen-face-cluster):
    //   달목 → Recruit / 쇠매 → EquipmentRefit / 갈마 → PassiveBoard / 솔길 → Inventory.

    private void Start()
    {
        if (!EnsureViewReady()) return;

        _localization.LocaleChanged += HandleLocaleChanged;
        _root.SessionState.SetCurrentScene(SceneNames.Town);
        _presenter!.Initialize();
        if (EnsureStoryBridgeReady())
        {
            _storyBridge.Advance(NarrativeMoment.TownEntered, BuildStoryMomentContext());
        }
    }

    private void OnDestroy()
    {
        _storyBridge?.ClearPending();
        if (_localization != null)
        {
            _localization.LocaleChanged -= HandleLocaleChanged;
        }
    }

    public void SaveProfile() => _presenter?.SaveProfile();
    public void LoadProfile() => _presenter?.LoadProfile();
    public void ReturnToStart() => _presenter?.ReturnToStart();
    public void OpenExpedition() => _presenter?.OpenExpedition();
    public void QuickBattle() => _presenter?.QuickBattle();

    public void EnsureRuntimeControls()
    {
        if (panelHost != null)
        {
            panelHost.EnsureReady();
        }

        if (!Application.isPlaying)
        {
            return;
        }

        EnsureViewReady();
    }

    private bool EnsureViewReady()
    {
        if (!EnsureSessionReady())
        {
            return false;
        }

        if (panelHost == null)
        {
            Debug.LogError("[TownScreenController] Missing RuntimePanelHost reference.");
            return false;
        }

        // wave-visual-qa: Town hub flow는 EquipmentRefitController(SerializeField scene 의존)를 거치지 않고
        // panel modal을 직접 instantiate한다. 그래서 EquipmentRefitController.EnsureReady의 dev seed
        // 호출이 안 일어나 Equipment Refit / Inventory panel이 빈약하게 보인다. Town 진입 시 한 번
        // 호출해 inventory + affix 패딩 보장. production gameplay에서는 ifEmpty 가드로 no-op.
        _root.SessionState.SeedDevDemoInventoryIfEmpty();

        if (_presenter != null)
        {
            panelHost.EnsureReady();
            return true;
        }

        panelHost.EnsureReady();
        var view = new TownScreenView(panelHost.Root, _contentIconResolver.ResolveCharacterPortrait);
        _presenter = new TownScreenPresenter(_root, _localization, _contentText, view);

        // Modal Presenter 인스턴스화 — 각 modal 별도 try/catch로 격리.
        // 한 modal의 element 누락이 hub 전체를 깨지 않게. sprite loader는 null fallback (production runtime).
        var corePanelReadyCount = 0;
        corePanelReadyCount += TryWireRecruit(panelHost.Root, view) ? 1 : 0;
        corePanelReadyCount += TryWireEquipmentRefit(panelHost.Root, view) ? 1 : 0;
        corePanelReadyCount += TryWirePassiveBoard(panelHost.Root, view) ? 1 : 0;
        corePanelReadyCount += TryWireInventory(panelHost.Root, view) ? 1 : 0;
        corePanelReadyCount += TryWirePermanentAugment(panelHost.Root, view) ? 1 : 0;
        corePanelReadyCount += TryWireCompendium(panelHost.Root, view) ? 1 : 0;
        corePanelReadyCount += TryWireCharacterSheet(panelHost.Root, view) ? 1 : 0;
        corePanelReadyCount += TryWireTacticalSetup(panelHost.Root, view) ? 1 : 0;
        corePanelReadyCount += TryWireTacticalWorkshop(panelHost.Root, view) ? 1 : 0;
        corePanelReadyCount += TryWireRoster(panelHost.Root, view) ? 1 : 0;
        _presenter.SetCorePanelReadiness(corePanelReadyCount, 10);
        return true;
    }

    private bool TryWireRecruit(UnityEngine.UIElements.VisualElement root, TownScreenView view)
    {
        try
        {
            var recruitView = new RecruitView(root);
            _recruitPresenter = new RecruitPresenter(
                _root,
                recruitView,
                _contentText,
                classSprite: null,
                portraitLoader: _contentIconResolver.ResolveCharacterPortrait);
            _recruitPresenter.Initialize();
            _recruitPresenter.Close();
            _presenter?.SetNpcOpener("dalmok", _recruitPresenter.Open);
            return true;
        }
        catch (System.Exception e) { Debug.LogWarning($"[TownScreenController] Recruit wire 실패: {e.Message}"); return false; }
    }

    private bool TryWireEquipmentRefit(UnityEngine.UIElements.VisualElement root, TownScreenView view)
    {
        try
        {
            var equipmentRefitView = new EquipmentRefitView(root);
            _equipmentRefitPresenter = new EquipmentRefitPresenter(
                _root.SessionState,
                _root.CombatContentLookup,
                equipmentRefitView,
                _contentText.GetItemName,
                _contentText.GetAffixName,
                _contentText.GetCharacterName,
                itemIconSprite: _contentIconResolver.ResolveItem,
                currencySprite: null,
                portraitLoader: _contentIconResolver.ResolveCharacterPortrait,
                affixIconSprite: _contentIconResolver.ResolveAffix);
            _equipmentRefitPresenter.Initialize();
            _equipmentRefitPresenter.Close();
            _presenter?.SetNpcOpener("soemae", _equipmentRefitPresenter.Open);
            return true;
        }
        catch (System.Exception e) { Debug.LogWarning($"[TownScreenController] EquipmentRefit wire 실패: {e.Message}"); return false; }
    }

    private bool TryWirePassiveBoard(UnityEngine.UIElements.VisualElement root, TownScreenView view)
    {
        try
        {
            var passiveBoardView = new PassiveBoardView(root);
            _passiveBoardPresenter = new PassiveBoardPresenter(
                _root,
                passiveBoardView,
                _contentText,
                classSprite: null,
                affixSprite: _contentIconResolver.ResolveAffix);
            _passiveBoardPresenter.Initialize();
            _passiveBoardPresenter.Close();
            _presenter?.SetNpcOpener("galma", _passiveBoardPresenter.Open);
            return true;
        }
        catch (System.Exception e) { Debug.LogWarning($"[TownScreenController] PassiveBoard wire 실패: {e.Message}"); return false; }
    }

    private bool TryWireInventory(UnityEngine.UIElements.VisualElement root, TownScreenView view)
    {
        try
        {
            var inventoryView = new InventoryView(root);
            _inventoryPresenter = new InventoryPresenter(
                _root.SessionState,
                _root.CombatContentLookup,
                inventoryView,
                currencySprite: null,
                itemIconSprite: _contentIconResolver.ResolveItem,
                contentText: _contentText,
                affixIconSprite: _contentIconResolver.ResolveAffix);
            _inventoryPresenter.Initialize();
            _inventoryPresenter.Close();
            _presenter?.SetNpcOpener("solgil", _inventoryPresenter.Open);
            return true;
        }
        catch (System.Exception e) { Debug.LogWarning($"[TownScreenController] Inventory wire 실패: {e.Message}"); return false; }
    }

    private bool TryWirePermanentAugment(UnityEngine.UIElements.VisualElement root, TownScreenView view)
    {
        try
        {
            var permanentAugmentView = new PermanentAugmentView(root);
            _permanentAugmentPresenter = new PermanentAugmentPresenter(
                _root,
                permanentAugmentView,
                _contentIconResolver.ResolveAny);
            _permanentAugmentPresenter.Initialize();
            _permanentAugmentPresenter.Close();
            view.BindPermanentAugmentOpen(_permanentAugmentPresenter.Open);
            return true;
        }
        catch (System.Exception e) { Debug.LogWarning($"[TownScreenController] PermanentAugment wire 실패: {e.Message}"); return false; }
    }

    private bool TryWireCompendium(UnityEngine.UIElements.VisualElement root, TownScreenView view)
    {
        try
        {
            var compendiumView = new CompendiumView(root);
            _compendiumPresenter = new CompendiumPresenter(
                _root,
                _localization,
                _contentText,
                _contentIconResolver,
                compendiumView);
            _compendiumPresenter.Initialize();
            _compendiumPresenter.Close();
            view.BindCompendiumOpen(_compendiumPresenter.Open);
            return true;
        }
        catch (System.Exception e) { Debug.LogWarning($"[TownScreenController] Compendium wire 실패: {e.Message}"); return false; }
    }

    private bool TryWireTacticalSetup(UnityEngine.UIElements.VisualElement root, TownScreenView view)
    {
        try
        {
            // 헤드리스-순수화: presenter는 GameSessionState + ICombatContentLookup + delegate seam만 알고,
            // UITK DOM/포트레잇 resolve는 SquadBuilderView가 소유 (EquipmentRefit 패턴).
            var squadBuilderView = new SquadBuilderView(root, _contentIconResolver.ResolveCharacterPortrait);
            _squadBuilderPresenter = new SquadBuilderPresenter(
                _root.SessionState,
                _root.CombatContentLookup,
                squadBuilderView,
                () => _root.ProfileQueries.GetLoadoutView(_root.ActiveProfileId),
                () => _root.SaveProfile(),
                _contentText.GetClassName,
                _contentText.GetRaceName,
                _contentText.GetSynergyName,
                _contentText.GetRoleName,
                _contentText.GetArchetypeName);
            _squadBuilderPresenter.Initialize();
            view.BindTacticalSetupOpen(_squadBuilderPresenter.Open);
            return true;
        }
        catch (System.Exception e) { Debug.LogWarning($"[TownScreenController] TacticalSetup wire 실패: {e.Message}"); return false; }
    }

    private bool TryWireTacticalWorkshop(UnityEngine.UIElements.VisualElement root, TownScreenView view)
    {
        try
        {
            var workshopView = new TacticalWorkshopView(root);
            _tacticalWorkshopPresenter = new TacticalWorkshopPresenter(
                _root.SessionState,
                _root.CombatContentLookup,
                workshopView,
                _contentText.GetCharacterName,
                _contentText.GetRoleName,
                _contentText.GetSynergyName);
            _tacticalWorkshopPresenter.Initialize();
            _tacticalWorkshopPresenter.Close();
            view.BindTacticalWorkshopOpen(_tacticalWorkshopPresenter.Open);
            return true;
        }
        catch (System.Exception e) { Debug.LogWarning($"[TownScreenController] TacticalWorkshop wire 실패: {e.Message}"); return false; }
    }

    private bool TryWireCharacterSheet(UnityEngine.UIElements.VisualElement root, TownScreenView view)
    {
        try
        {
            var sheetView = new TownCharacterSheetView(root);
            _characterSheetPresenter = new TownCharacterSheetPresenter(
                _root,
                _localization,
                _contentText,
                sheetView,
                portraitSprite: key => _contentIconResolver.ResolveCharacterStandee(key),
                railPortraitSprite: key => _contentIconResolver.ResolveCharacterPortrait(key),
                skillSprite: key => _contentIconResolver.ResolveSkill(key),
                itemSprite: key => _contentIconResolver.ResolveItem(key));
            _characterSheetPresenter.Initialize();
            _characterSheetPresenter.Close();
            // wave-50 P2 — character sheet 4 CTA action bar cross-presenter wire.
            // dismiss/retrain은 후속 wave에서 service wire (DismissService.Commit, RetrainOperationKind 적용).
            _characterSheetPresenter.BindActionBar(
                passiveBoardOpener: heroId =>
                {
                    _passiveBoardPresenter?.SetSelectedHero(heroId);
                    _passiveBoardPresenter?.Open();
                },
                refitOpener: heroId =>
                {
                    // EquipmentRefit은 item 중심 — hero forward는 후속 wave 과제
                    // (hero가 장착한 item 자동 선택). 본 wave는 panel open만.
                    _equipmentRefitPresenter?.Open();
                });
            _presenter?.SetHeroOpener(heroId =>
            {
                _inventoryPresenter?.SetTargetHero(heroId);
                _characterSheetPresenter.Open(heroId);
            });
            return true;
        }
        catch (System.Exception e) { Debug.LogWarning($"[TownScreenController] CharacterSheet wire 실패: {e.Message}"); return false; }
    }

    private bool TryWireRoster(UnityEngine.UIElements.VisualElement root, TownScreenView view)
    {
        try
        {
            _rosterModalView = new RosterGridView(root, heroCardTemplate: null, portraitLoader: _contentIconResolver.ResolveCharacterPortrait);
            _rosterGridPresenter = new RosterGridPresenter(
                _root.SessionState,
                _root.CombatContentLookup,
                _rosterModalView,
                _contentText.GetClassName,
                _contentText.GetRaceName,
                _contentText.GetCharacterName,
                quickBattle: () =>
                {
                    _root.BeginTransientTownSmoke();
                    _root.SessionState.PrepareTownQuickBattleSmoke();
                    _root.SceneFlow.GoToBattle();
                },
                heroSelected: heroId =>
                {
                    _inventoryPresenter?.SetTargetHero(heroId);
                    _characterSheetPresenter?.Open(heroId);
                });
            _rosterGridPresenter.Initialize();
            _rosterModalView.BindClose(_rosterModalView.Close);
            _rosterModalView.Close();
            view.BindRosterOpen(() =>
            {
                _rosterModalView.Open();
                _rosterGridPresenter.Refresh();
            });
            return true;
        }
        catch (System.Exception e) { Debug.LogWarning($"[TownScreenController] Roster wire 실패: {e.Message}"); return false; }
    }

    private bool EnsureSessionReady()
    {
        if (_root != null)
        {
            return true;
        }

        _root = GameSessionRoot.EnsureInstance();
        if (_root == null)
        {
            Debug.LogError("[TownScreenController] GameSessionRoot가 없습니다.");
            return false;
        }

        _localization = _root.Localization;
        _contentText = new ContentTextResolver(_localization, _root.CombatContentLookup);
        _contentIconResolver = new ContentIconResolver(_root.CombatContentLookup);
        return true;
    }

    private bool EnsureStoryBridgeReady()
    {
        if (_storyBridge != null)
        {
            return true;
        }

        _storyBridge = GetComponent<StorySceneFlowBridge>();
        if (_storyBridge == null)
        {
            _storyBridge = gameObject.AddComponent<StorySceneFlowBridge>();
        }

        return _storyBridge != null;
    }

    private StoryMomentContext BuildStoryMomentContext()
    {
        var session = _root.SessionState;
        return new StoryMomentContext
        {
            ChapterId = session.SelectedCampaignChapterId,
            SiteId = session.SelectedCampaignSiteId,
            NodeIndex = session.CurrentExpeditionNodeIndex,
        };
    }

    private void HandleLocaleChanged(UnityEngine.Localization.Locale _)
    {
        _presenter?.Refresh();
        _compendiumPresenter?.Refresh();
        _characterSheetPresenter?.Refresh();
    }
}
}
