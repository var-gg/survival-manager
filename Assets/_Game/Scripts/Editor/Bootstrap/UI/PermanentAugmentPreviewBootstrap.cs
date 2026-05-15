using System;
using System.Collections.Generic;
using System.Linq;
using SM.Unity.UI.Town.Preview;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace SM.Editor.Bootstrap.UI;

/// <summary>
/// SM/Town/Permanent Augment 미리보기 — Sprint 2 real-wire dev tool.
/// 모델: "장착한 영구 augment 1개 + 해금 후보 풀" (audit §4.1 P0-4 다운스코프 — posture 그리드 폐기).
///
/// 진입: real GameSessionRoot 우선 (UnlockedPermanentAugmentIds + EquippedAugmentIds 기반).
/// 실패 시 mock fallback.
/// </summary>
public sealed class PermanentAugmentPreviewBootstrap : EditorWindow
{
    private const string VisualTreePath = "Assets/_Game/UI/Screens/Town/Preview/PermanentAugmentPreview.uxml";
    private const string ThemeTokensPath = "Assets/_Game/UI/Foundation/Styles/ThemeTokens.uss";
    private const string RuntimePanelThemePath = "Assets/_Game/UI/Foundation/Styles/RuntimePanelTheme.uss";

    private const string AugmentSpriteFmt = "Assets/_Game/UI/Foundation/Sprites/Augment/augment_{0}.png";

    private PermanentAugmentView? _view;
    private PermanentAugmentPresenter? _presenter;

    [MenuItem("SM/Town/Permanent Augment 미리보기", false, 12)]
    public static void Open()
    {
        var window = GetWindow<PermanentAugmentPreviewBootstrap>("Permanent Augment 미리보기");
        window.minSize = new Vector2(1320f, 780f);
    }

    private void CreateGUI() => BuildInto(rootVisualElement);

    /// <summary>EditorWindow + TownPreviewCaptureUtility 공용 — 지정 root에 surface preview 빌드.</summary>
    public void BuildInto(VisualElement root)
    {
        var visualTree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(VisualTreePath);
        if (visualTree == null) { root.Add(new Label($"UXML 못 찾음: {VisualTreePath}")); return; }

        var tokens = AssetDatabase.LoadAssetAtPath<StyleSheet>(ThemeTokensPath);
        var theme = AssetDatabase.LoadAssetAtPath<StyleSheet>(RuntimePanelThemePath);
        if (tokens != null) root.styleSheets.Add(tokens);
        if (theme != null) root.styleSheets.Add(theme);

        visualTree.CloneTree(root);

        _view = new PermanentAugmentView(root);

        if (TryWireRealSession(_view))
        {
            return;
        }

        _view.Render(BuildMockViewState());
    }

    private bool TryWireRealSession(PermanentAugmentView view)
    {
        try
        {
            var sessionRoot = PreviewSessionContext.EnsureSession();
            _presenter = new PermanentAugmentPresenter(
                sessionRoot,
                view,
                PreviewSessionContext.LoadAugmentSprite);
            _presenter.Initialize();
            return true;
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[PermanentAugmentPreview] real-session wire 실패, mock fallback: {e.Message}");
            _presenter = null;
            return false;
        }
    }

    private PermanentAugmentViewState BuildMockViewState()
    {
        // Presenter catalog 재사용 + mock unlock/equip 상태. P0-4 다운스코프 — progress/posture 폐기.
        const string equippedId = "augment_perm_guardian_detail";   // 단일 슬롯 장착
        const string selectedId = "augment_perm_guardian_detail";   // detail panel 선택
        var mockUnlocked = new HashSet<string>
        {
            "augment_perm_citadel_doctrine",
            "augment_perm_guardian_detail",
            "augment_perm_breakthrough_orders",
            // "augment_perm_night_hunt_mandate" — 미해금
        };

        var cells = PermanentAugmentPresenter.Catalog
            .Select(p => new PermanentAugmentCellViewState(
                AugmentId: p.AugmentId,
                KoLabel: p.KoLabel,
                FamilyBucket: p.FamilyBucket,
                Unlocked: mockUnlocked.Contains(p.AugmentId),
                IsEquipped: p.AugmentId == equippedId,
                IsSelected: p.AugmentId == selectedId,
                IconSprite: LoadAugmentSprite(p.Motif)))
            .ToList();

        var selectedSpec = PermanentAugmentPresenter.Catalog.First(p => p.AugmentId == selectedId);
        var detail = new PermanentAugmentDetailViewState(
            SelectedAugmentId: selectedSpec.AugmentId,
            KoLabel: selectedSpec.KoLabel,
            EnLabel: selectedSpec.EnLabel,
            FamilyBucket: selectedSpec.FamilyBucket,
            SignatureEffect: selectedSpec.SignatureEffect,
            FlavorText: selectedSpec.Flavor,
            IsUnlocked: mockUnlocked.Contains(selectedSpec.AugmentId),
            IsEquipped: selectedSpec.AugmentId == equippedId,
            IconSprite: LoadAugmentSprite(selectedSpec.Motif),
            MetaRows: new[]
            {
                new PermanentAugmentMetaRowViewState("FAMILY", selectedSpec.FamilyBucket),
                new PermanentAugmentMetaRowViewState("강화 효과", selectedSpec.SignatureEffect),
            });

        return new PermanentAugmentViewState(cells, detail);
    }

    private static Texture2D? LoadAugmentSprite(string key) =>
        AssetDatabase.LoadAssetAtPath<Texture2D>(string.Format(AugmentSpriteFmt, key));
}
