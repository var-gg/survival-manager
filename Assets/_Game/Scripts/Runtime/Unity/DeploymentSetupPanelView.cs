using System;
using System.Collections.Generic;
using System.Linq;
using SM.Combat.Model;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace SM.Unity;

public sealed class DeploymentSetupPanelView
{
    private readonly Dictionary<DeploymentAnchorId, Text> _anchorLabels = new();
    private readonly Text _postureLabel;
    private readonly Text _summaryLabel;

    private DeploymentSetupPanelView(Text postureLabel, Text summaryLabel)
    {
        _postureLabel = postureLabel;
        _summaryLabel = summaryLabel;
    }

    public static DeploymentSetupPanelView Create(
        string panelName,
        RectTransform parent,
        Action<DeploymentAnchorId> onCycleAnchor,
        Action onCyclePosture)
    {
        var font = GameFontCatalog.LoadSharedUiFont();
        var root = CreatePanelRoot(panelName, parent);
        CreateHeader(root, font);

        var postureButton = CreateButton(root, "TeamPostureButton", new Vector2(0f, -34f), new Vector2(210f, 42f), font, onCyclePosture.Invoke);
        var summary = CreateLabel(root, "DeploymentSummaryText", new Vector2(0f, -76f), new Vector2(210f, 44f), font, 13, TextAnchor.MiddleCenter);

        var view = new DeploymentSetupPanelView(postureButton, summary);
        var buttons = new[]
        {
            (DeploymentAnchorId.FrontTop, new Vector2(-55f, -132f)),
            (DeploymentAnchorId.FrontCenter, new Vector2(55f, -132f)),
            (DeploymentAnchorId.FrontBottom, new Vector2(-55f, -184f)),
            (DeploymentAnchorId.BackTop, new Vector2(55f, -184f)),
            (DeploymentAnchorId.BackCenter, new Vector2(-55f, -236f)),
            (DeploymentAnchorId.BackBottom, new Vector2(55f, -236f)),
        };

        foreach (var (anchor, position) in buttons)
        {
            var label = CreateButton(
                root,
                $"DeployButton_{anchor}",
                position,
                new Vector2(100f, 46f),
                font,
                () => onCycleAnchor(anchor));
            view._anchorLabels[anchor] = label;
        }

        return view;
    }

    public void Refresh(GameSessionState session)
    {
        var localization = GameSessionRoot.Instance?.Localization;
        foreach (var anchor in session.DeploymentAnchors)
        {
            if (!_anchorLabels.TryGetValue(anchor, out var label))
            {
                continue;
            }

            var heroId = session.GetAssignedHeroId(anchor);
            var heroName = session.Profile.Heroes.FirstOrDefault(hero => hero.HeroId == heroId)?.Name
                ?? Localize(localization, GameLocalizationTables.UICommon, "ui.common.empty", "Empty");
            label.text = $"{LocalizeAnchor(localization, anchor)}\n{heroName}";
        }

        _postureLabel.text = $"{Localize(localization, GameLocalizationTables.UICommon, "ui.common.posture", "Posture")}\n{session.SelectedTeamPosture}";
        _summaryLabel.text = Localize(localization, GameLocalizationTables.UICommon, "ui.common.deploy_summary", "Deploy {0}/4", session.BattleDeployHeroIds.Count);
    }

    private static RectTransform CreatePanelRoot(string panelName, RectTransform parent)
    {
        var existing = parent.Find(panelName) as RectTransform;
        if (existing != null)
        {
            return existing;
        }

        var go = new GameObject(panelName, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        var rect = go.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(1f, 1f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(1f, 1f);
        rect.anchoredPosition = new Vector2(-24f, -24f);
        rect.sizeDelta = new Vector2(230f, 300f);

        var image = go.GetComponent<Image>();
        image.color = new Color(0.06f, 0.08f, 0.12f, 0.92f);
        return rect;
    }

    private static void CreateHeader(RectTransform root, Font font)
    {
        var header = CreateLabel(root, "DeploymentHeaderText", new Vector2(0f, -10f), new Vector2(210f, 24f), font, 16, TextAnchor.MiddleCenter);
        header.text = Localize(GameSessionRoot.Instance?.Localization, GameLocalizationTables.UICommon, "ui.common.deployment_setup", "Deployment Setup");
        header.color = Color.white;
    }

    private static Text CreateButton(
        RectTransform parent,
        string name,
        Vector2 anchoredPosition,
        Vector2 size,
        Font font,
        UnityAction onClick)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);

        var rect = go.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 1f);
        rect.anchorMax = new Vector2(0.5f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = size;

        var image = go.GetComponent<Image>();
        image.color = new Color(0.18f, 0.26f, 0.38f, 0.98f);

        var button = go.GetComponent<Button>();
        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(onClick);

        var label = CreateLabel(rect, "Label", Vector2.zero, size - new Vector2(10f, 8f), font, 12, TextAnchor.MiddleCenter);
        label.text = name;
        return label;
    }

    private static Text CreateLabel(
        RectTransform parent,
        string name,
        Vector2 anchoredPosition,
        Vector2 size,
        Font font,
        int fontSize,
        TextAnchor alignment)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);

        var rect = go.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 1f);
        rect.anchorMax = new Vector2(0.5f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = size;

        var text = go.AddComponent<Text>();
        text.font = font;
        text.fontSize = fontSize;
        text.alignment = alignment;
        text.color = new Color(0.94f, 0.96f, 1f, 1f);
        text.raycastTarget = false;

        var outline = go.AddComponent<Outline>();
        outline.effectColor = new Color(0f, 0f, 0f, 0.85f);
        outline.effectDistance = new Vector2(1f, -1f);
        return text;
    }

    private static string LocalizeAnchor(GameLocalizationController? localization, DeploymentAnchorId anchor)
    {
        return Localize(localization, GameLocalizationTables.UICommon, anchor.ToLocalizationKey(), anchor.ToDisplayName());
    }

    private static string Localize(GameLocalizationController? localization, string table, string key, string fallback, params object[] args)
    {
        return localization != null
            ? localization.LocalizeOrFallback(table, key, fallback, args)
            : args.Length == 0
                ? fallback
                : string.Format(fallback, args);
    }
}
