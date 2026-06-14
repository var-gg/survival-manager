using System.Collections.Generic;
using SM.Unity.UI.HeroDetail;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace SM.Editor.Bootstrap.UI;

/// <summary>
/// HeroDetail v0.5 미리보기 — 대표 HeroDetailViewState로 3-rail surface를 렌더한다.
/// TownPreviewCaptureUtility의 offscreen RT 캡처 surface로도 쓰인다(비파괴 시각 QA).
/// 실세션 미의존(대표 데이터) — working Town 표면(TownCharacterSheet)을 건드리지 않는다.
/// </summary>
public sealed class HeroDetailPreviewBootstrap : EditorWindow
{
    private const string VisualTreePath = "Assets/_Game/UI/Panels/HeroDetail/HeroDetailPanel.uxml";
    private const string ThemeTokensPath = "Assets/_Game/UI/Foundation/Styles/ThemeTokens.uss";
    private const string RuntimePanelThemePath = "Assets/_Game/UI/Foundation/Styles/RuntimePanelTheme.uss";

    [MenuItem("SM/Town/HeroDetail 미리보기", false, 19)]
    public static void Open()
    {
        var window = GetWindow<HeroDetailPreviewBootstrap>("HeroDetail 미리보기");
        window.minSize = new Vector2(1100f, 760f);
    }

    private void CreateGUI() => BuildInto(rootVisualElement);

    /// <summary>EditorWindow + TownPreviewCaptureUtility 공용 — 지정 root에 surface preview 빌드.</summary>
    public void BuildInto(VisualElement root)
    {
        var visualTree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(VisualTreePath);
        if (visualTree == null)
        {
            root.Add(new Label($"UXML 못 찾음: {VisualTreePath}"));
            return;
        }

        var tokens = AssetDatabase.LoadAssetAtPath<StyleSheet>(ThemeTokensPath);
        var theme = AssetDatabase.LoadAssetAtPath<StyleSheet>(RuntimePanelThemePath);
        var commonDetail = AssetDatabase.LoadAssetAtPath<StyleSheet>("Assets/_Game/UI/Foundation/USS/common_detail.uss");
        var heroDetailUss = AssetDatabase.LoadAssetAtPath<StyleSheet>("Assets/_Game/UI/Panels/HeroDetail/HeroDetailPanel.uss");
        if (tokens != null) root.styleSheets.Add(tokens);
        if (theme != null) root.styleSheets.Add(theme);
        if (commonDetail != null) root.styleSheets.Add(commonDetail);
        if (heroDetailUss != null) root.styleSheets.Add(heroDetailUss);

        var container = visualTree.Instantiate();
        container.style.flexGrow = 1f;
        container.style.width = Length.Percent(100f);
        container.style.height = Length.Percent(100f);
        root.Add(container);

        var view = new HeroDetailView(root);
        view.Render(BuildPreviewState());
        view.Open();
    }

    private static HeroDetailViewState BuildPreviewState()
    {
        return new HeroDetailViewState(
            HeroId: "hero_preview",
            DisplayName: "단린",
            ArchetypeLabel: "여명의 사제",
            RoleLabel: "전열",
            FamilyKey: "vanguard",
            TierLabel: "Rare",
            LevelLabel: "Lv. 7",
            PortraitSprite: null,
            Stats: new List<HeroDetailStatViewState>
            {
                new("hp", "HP", "1200", "vital"),
                new("armor", "ARM", "40", "guard"),
                new("resist", "RES", "30", "guard"),
                new("phys", "PWR", "85", "attack"),
                new("magic", "MAG", "20", "magic"),
                new("speed", "SPD", "5.5", "tempo"),
                new("range", "RNG", "2", "tempo"),
                new("haste", "HST", "10", "magic"),
            },
            SkillSlots: new List<HeroDetailSkillSlotViewState>
            {
                new(HeroDetailSlotKind.SignatureLock, "sig_a", "수호 강타", "SIG · ACTIVE", "전열 보호 일격.", "icon_sig", HeroDetailIconState.Present, "8s", false),
                new(HeroDetailSlotKind.FlexActive, "flex_a", "신성 정화", "FLEX · ACTIVE", "상태이상 정화.", "icon_flex", HeroDetailIconState.Fallback, "12s", false),
                new(HeroDetailSlotKind.FlexRetrain, "sig_p", "불굴", "SIG · PASSIVE", "받는 피해 감소.", "", HeroDetailIconState.Missing, "Passive", true),
                new(HeroDetailSlotKind.LateUnlock, "late", "여명의 가호", "FLEX · PASSIVE", "후반 해금 패시브.", "icon_late", HeroDetailIconState.Present, "Passive", true),
            },
            Equipment: new List<HeroDetailEquipSlotViewState>
            {
                new("weapon", "Weapon", "여명의 창", "Rare / 2 affixes", "icon_weapon", true),
                new("armor", "Armor", "수호 갑주", "Common / 1 affixes", "icon_armor", true),
                new("accessory", "Accessory", "—", "Empty", "accessory", false),
            },
            Traits: new List<HeroDetailTraitViewState>
            {
                new("boon", "굳건함", string.Empty),
                new("bane", "느린 손", string.Empty),
            });
    }
}
