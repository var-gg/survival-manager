using SM.Meta.Model;
using SM.Unity.UI;
using UnityEngine;
using UnityEngine.UIElements;

namespace SM.Unity
{

/// <summary>
/// 시작 화면.
///
/// 2026-07-31 에 레거시 uGUI(<c>UnityEngine.UI.Text</c> / <c>Button</c>)에서 UITK 로 옮겼다.
/// 이 화면만 다른 UI 시스템에 있어서, 게임의 <b>첫 화면</b>에 프로젝트의 금테·딥네이비
/// 언어가 하나도 붙지 않았다 — 기본 스카이박스 위에 민무늬 파란 버튼 하나였다.
///
/// 문구 주의 — <b>폴백이 곧 이 화면의 문구다.</b> 시작 화면은 로컬라이제이션이 초기화되기
/// 전에 그려지므로 테이블 값이 아니라 아래 폴백이 그대로 화면에 나온다(실제로 시드를
/// 한국어로 바꾼 뒤에도 "Start" 가 떴다). 그래서 폴백을 제품 문구로 취급하고,
/// 시드와 일치하는지를 <c>BootScreenCopyFastTests</c> 가 계약으로 든다.
/// </summary>
public sealed class BootScreenController : MonoBehaviour
{
    [SerializeField] private RuntimePanelHost panelHost = null!;

    private GameSessionRoot _root = null!;
    private Label _titleLabel = null!;
    private Label _statusLabel = null!;
    private Label _hintLabel = null!;
    private Button _startButton = null!;
    private bool _bound;

    private void Start()
    {
        _root = GameSessionRoot.EnsureInstance();
        EnsureBound();
        Refresh();
    }

    public void Refresh()
    {
        if (_root == null || !EnsureBound())
        {
            return;
        }

        _titleLabel.text = Localize(
            GameLocalizationTables.UICommon,
            "ui.common.start_screen.title",
            "잿골 연대기");

        _statusLabel.text = BuildStatusText();

        var hint = BuildHintText();
        _hintLabel.text = hint;
        _hintLabel.style.display = string.IsNullOrWhiteSpace(hint) ? DisplayStyle.None : DisplayStyle.Flex;

        _startButton.text = Localize(
            GameLocalizationTables.UICommon,
            "ui.common.start_local_run",
            "이어서 시작");
        _startButton.SetEnabled(!_root.HasBlockingError);
    }

    private bool EnsureBound()
    {
        if (_bound)
        {
            return true;
        }

        if (panelHost == null)
        {
            return false;
        }

        var root = panelHost.Root;
        _titleLabel = root.Q<Label>("BootTitleLabel");
        _statusLabel = root.Q<Label>("BootStatusLabel");
        _hintLabel = root.Q<Label>("BootHintLabel");
        _startButton = root.Q<Button>("BootStartButton");
        if (_titleLabel == null || _statusLabel == null || _hintLabel == null || _startButton == null)
        {
            return false;
        }

        _startButton.clicked += HandleOfflineSelected;
        _bound = true;
        return true;
    }

    private void HandleOfflineSelected()
    {
        if (!_root.StartRealm(SessionRealm.OfflineLocal, out var error))
        {
            _statusLabel.text = error;
            return;
        }

        _root.ClearBlockingError();
        _root.SceneFlow.GoToTown();
    }

    private string BuildStatusText()
    {
        if (_root.HasBlockingError)
        {
            return _root.LastBlockingError ?? string.Empty;
        }

        return Localize(
            GameLocalizationTables.UICommon,
            "ui.common.start_screen.status",
            "잿문이 닫힌 뒤로, 변방에 남은 것은 잿골 하나뿐이다.");
    }

    private string BuildHintText()
    {
        return Localize(
            GameLocalizationTables.UICommon,
            "ui.common.start_screen.hint",
            "마을을 꾸리고, 분대를 짜고, 원정에 나선다.");
    }

    private string Localize(string table, string key, string fallback, params object[] args)
    {
        return _root.Localization.LocalizeOrFallback(table, key, fallback, args);
    }
}
}
