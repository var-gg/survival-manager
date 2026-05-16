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
    private TownScreenPresenter? _presenter;
    private SquadBuilderPresenter? _squadBuilderPresenter;
    private RecruitPresenter? _recruitPresenter;
    private EquipmentRefitPresenter? _equipmentRefitPresenter;
    private PassiveBoardPresenter? _passiveBoardPresenter;
    private InventoryPresenter? _inventoryPresenter;
    private PermanentAugmentPresenter? _permanentAugmentPresenter;
    private RosterGridView? _rosterModalView;
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

        if (_presenter != null)
        {
            panelHost.EnsureReady();
            return true;
        }

        panelHost.EnsureReady();
        var view = new TownScreenView(panelHost.Root);
        _presenter = new TownScreenPresenter(_root, _localization, _contentText, view);

        // Modal Presenter 인스턴스화 — 각 modal 별도 try/catch로 격리.
        // 한 modal의 element 누락이 hub 전체를 깨지 않게. sprite loader는 null fallback (production runtime).
        TryWireRecruit(panelHost.Root, view);
        TryWireEquipmentRefit(panelHost.Root, view);
        TryWirePassiveBoard(panelHost.Root, view);
        TryWireInventory(panelHost.Root, view);
        TryWirePermanentAugment(panelHost.Root, view);
        TryWireSquadBuilder(panelHost.Root, view);
        TryWireRoster(panelHost.Root, view);

        view.BindTheaterOpen(() => _presenter?.Refresh("극장 (Theater) — story replay surface 후속 wire."));
        return true;
    }

    private void TryWireRecruit(UnityEngine.UIElements.VisualElement root, TownScreenView view)
    {
        try
        {
            var recruitView = new RecruitView(root);
            _recruitPresenter = new RecruitPresenter(_root, recruitView, _contentText);
            _recruitPresenter.Initialize();
            _recruitPresenter.Close();
            _presenter?.SetNpcOpener("dalmok", _recruitPresenter.Open);
        }
        catch (System.Exception e) { Debug.LogWarning($"[TownScreenController] Recruit wire 실패: {e.Message}"); }
    }

    private void TryWireEquipmentRefit(UnityEngine.UIElements.VisualElement root, TownScreenView view)
    {
        try
        {
            var equipmentRefitView = new EquipmentRefitView(root);
            _equipmentRefitPresenter = new EquipmentRefitPresenter(_root, equipmentRefitView, _contentText);
            _equipmentRefitPresenter.Initialize();
            _equipmentRefitPresenter.Close();
            _presenter?.SetNpcOpener("soemae", _equipmentRefitPresenter.Open);
        }
        catch (System.Exception e) { Debug.LogWarning($"[TownScreenController] EquipmentRefit wire 실패: {e.Message}"); }
    }

    private void TryWirePassiveBoard(UnityEngine.UIElements.VisualElement root, TownScreenView view)
    {
        try
        {
            var passiveBoardView = new PassiveBoardView(root);
            _passiveBoardPresenter = new PassiveBoardPresenter(_root, passiveBoardView, _contentText);
            _passiveBoardPresenter.Initialize();
            _passiveBoardPresenter.Close();
            _presenter?.SetNpcOpener("galma", _passiveBoardPresenter.Open);
        }
        catch (System.Exception e) { Debug.LogWarning($"[TownScreenController] PassiveBoard wire 실패: {e.Message}"); }
    }

    private void TryWireInventory(UnityEngine.UIElements.VisualElement root, TownScreenView view)
    {
        try
        {
            var inventoryView = new InventoryView(root);
            _inventoryPresenter = new InventoryPresenter(_root, inventoryView);
            _inventoryPresenter.Initialize();
            _inventoryPresenter.Close();
            _presenter?.SetNpcOpener("solgil", _inventoryPresenter.Open);
        }
        catch (System.Exception e) { Debug.LogWarning($"[TownScreenController] Inventory wire 실패: {e.Message}"); }
    }

    private void TryWirePermanentAugment(UnityEngine.UIElements.VisualElement root, TownScreenView view)
    {
        try
        {
            var permanentAugmentView = new PermanentAugmentView(root);
            _permanentAugmentPresenter = new PermanentAugmentPresenter(_root, permanentAugmentView);
            _permanentAugmentPresenter.Initialize();
            _permanentAugmentPresenter.Close();
            view.BindPermanentAugmentOpen(_permanentAugmentPresenter.Open);
        }
        catch (System.Exception e) { Debug.LogWarning($"[TownScreenController] PermanentAugment wire 실패: {e.Message}"); }
    }

    private void TryWireSquadBuilder(UnityEngine.UIElements.VisualElement root, TownScreenView view)
    {
        try
        {
            _squadBuilderPresenter = new SquadBuilderPresenter(root, _root, _contentText);
            view.BindSquadBuilderOpen(_squadBuilderPresenter.Open);
        }
        catch (System.Exception e) { Debug.LogWarning($"[TownScreenController] SquadBuilder wire 실패: {e.Message}"); }
    }

    private void TryWireRoster(UnityEngine.UIElements.VisualElement root, TownScreenView view)
    {
        try
        {
            _rosterModalView = new RosterGridView(root, heroCardTemplate: null);
            _rosterModalView.Close();
            view.BindRosterOpen(() => _presenter?.Refresh("동료 명부 — heroCardTemplate Resources copy 후속 wire."));
        }
        catch (System.Exception e) { Debug.LogWarning($"[TownScreenController] Roster wire 실패: {e.Message}"); }
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
    }
}
}
