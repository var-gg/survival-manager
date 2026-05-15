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
    private RosterGridView? _rosterModalView;   // heroCardTemplate 미주입 — close-only placeholder.

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

        // 잿골 hub V2 NPC 매핑 (pindoc://v1-scene-screen-routing-ashglen-hub-analysis 정합):
        //   달목 (여관) → Recruit / 쇠매 (공방) → EquipmentRefit / 갈마 (진료소) → PassiveBoard / 솔길 (기록) → Inventory.
        // sprite loader는 null fallback — production runtime은 Resources/Addressables 미설치, 후속 task에서 wire.
        var recruitView = new RecruitView(panelHost.Root);
        _recruitPresenter = new RecruitPresenter(_root, recruitView, _contentText);
        _recruitPresenter.Initialize();
        _recruitPresenter.Close();
        view.BindNpcDalmokOpen(_recruitPresenter.Open);

        var equipmentRefitView = new EquipmentRefitView(panelHost.Root);
        _equipmentRefitPresenter = new EquipmentRefitPresenter(_root, equipmentRefitView, _contentText);
        _equipmentRefitPresenter.Initialize();
        _equipmentRefitPresenter.Close();
        view.BindNpcSoemaeOpen(_equipmentRefitPresenter.Open);

        var passiveBoardView = new PassiveBoardView(panelHost.Root);
        _passiveBoardPresenter = new PassiveBoardPresenter(_root, passiveBoardView, _contentText);
        _passiveBoardPresenter.Initialize();
        _passiveBoardPresenter.Close();
        view.BindNpcGalmaOpen(_passiveBoardPresenter.Open);

        var inventoryView = new InventoryView(panelHost.Root);
        _inventoryPresenter = new InventoryPresenter(_root, inventoryView);
        _inventoryPresenter.Initialize();
        _inventoryPresenter.Close();
        view.BindNpcSolgilOpen(_inventoryPresenter.Open);

        // Utility entries (우 column): PermanentAugment / SquadBuilder / Roster placeholder.
        var permanentAugmentView = new PermanentAugmentView(panelHost.Root);
        _permanentAugmentPresenter = new PermanentAugmentPresenter(_root, permanentAugmentView);
        _permanentAugmentPresenter.Initialize();
        _permanentAugmentPresenter.Close();
        view.BindPermanentAugmentOpen(_permanentAugmentPresenter.Open);

        _squadBuilderPresenter = new SquadBuilderPresenter(panelHost.Root, _root, _contentText);
        view.BindSquadBuilderOpen(_squadBuilderPresenter.Open);

        // Roster modal — heroCardTemplate 미주입 (Resources/Addressables 후속). default closed +
        // RosterButton click은 placeholder status로 wire 안내. heroCardTemplate 마련 후 Render wire.
        _rosterModalView = new RosterGridView(panelHost.Root, heroCardTemplate: null);
        _rosterModalView.Close();
        view.BindRosterOpen(() => _presenter?.Refresh("동료 명부 — heroCardTemplate Resources copy 후속 wire."));
        return true;
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
