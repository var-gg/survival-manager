using System;
using UnityEngine;
using UnityEngine.UI;

namespace SM.Unity;

public sealed class BattleSettingsPanelController : MonoBehaviour
{
    [SerializeField] private RectTransform panelRoot = null!;
    [SerializeField] private Text worldHpButtonLabel = null!;
    [SerializeField] private Text overlayHpButtonLabel = null!;
    [SerializeField] private Text teamSummaryButtonLabel = null!;
    [SerializeField] private Text statusText = null!;

    private BattlePresentationOptions _options = null!;
    private Action<BattlePresentationOptions>? _applyCallback;
    private bool _initialized;

    public void Initialize(BattlePresentationOptions options, Action<BattlePresentationOptions> applyCallback)
    {
        _options = options;
        _applyCallback = applyCallback;

        if (!_initialized && panelRoot != null)
        {
            panelRoot.gameObject.SetActive(false);
            _initialized = true;
        }

        RefreshLabels();
        _applyCallback?.Invoke(_options);
    }

    public void TogglePanel()
    {
        if (!EnsureReady()) return;
        panelRoot.gameObject.SetActive(!panelRoot.gameObject.activeSelf);
        statusText.text = panelRoot.gameObject.activeSelf
            ? "전투 표시 옵션"
            : "설정 패널 닫힘";
        RefreshLabels();
    }

    public void ToggleWorldActorHp()
    {
        if (!EnsureReady()) return;
        _options.ToggleWorldActorHp();
        Apply("캐릭터 기반 HP 표시");
    }

    public void ToggleOverlayActorHp()
    {
        if (!EnsureReady()) return;
        _options.ToggleOverlayActorHp();
        Apply("오버레이 HP 카드");
    }

    public void ToggleTeamSummary()
    {
        if (!EnsureReady()) return;
        _options.ToggleTeamHpSummary();
        Apply("좌/우 팀 요약 패널");
    }

    private void Apply(string label)
    {
        _applyCallback?.Invoke(_options);
        statusText.text = $"{label}: {BuildStateLabel(label)}";
        RefreshLabels();
    }

    private string BuildStateLabel(string label)
    {
        return label switch
        {
            "캐릭터 기반 HP 표시" => _options.ShowWorldActorHp ? "ON" : "OFF",
            "오버레이 HP 카드" => _options.ShowOverlayActorHp ? "ON" : "OFF",
            _ => _options.ShowTeamHpSummary ? "ON" : "OFF"
        };
    }

    private void RefreshLabels()
    {
        if (worldHpButtonLabel != null)
        {
            worldHpButtonLabel.text = $"Actor HP {( _options?.ShowWorldActorHp == true ? "ON" : "OFF")}";
        }

        if (overlayHpButtonLabel != null)
        {
            overlayHpButtonLabel.text = $"Overlay HP {( _options?.ShowOverlayActorHp == true ? "ON" : "OFF")}";
        }

        if (teamSummaryButtonLabel != null)
        {
            teamSummaryButtonLabel.text = $"Team Summary {( _options?.ShowTeamHpSummary == true ? "ON" : "OFF")}";
        }

        if (statusText != null && string.IsNullOrWhiteSpace(statusText.text))
        {
            statusText.text = "전투 표시 옵션";
        }
    }

    private bool EnsureReady()
    {
        if (panelRoot == null || worldHpButtonLabel == null || overlayHpButtonLabel == null || teamSummaryButtonLabel == null || statusText == null)
        {
            Debug.LogError("[BattleSettingsPanelController] Missing settings panel references.");
            return false;
        }

        if (_options == null)
        {
            Debug.LogError("[BattleSettingsPanelController] Presentation options are not initialized.");
            return false;
        }

        return true;
    }
}
