using System.Collections.Generic;
using System.Linq;
using SM.Unity.UI.Town.Preview;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace SM.Editor.Bootstrap.UI;

/// <summary>
/// SM/Town/Inventory 미리보기 — Sprint 1 presenter 패턴 dev tool.
/// Bootstrap = mock ViewState injection. 실 GameSessionRoot 없이 View 직접 render.
/// 시안 SoT: pindoc://town-ui-ux-시안-갤러리-v1 (6. Inventory tab)
/// </summary>
public sealed class InventoryPreviewBootstrap : EditorWindow
{
    private const string VisualTreePath = "Assets/_Game/UI/Screens/Town/Preview/InventoryPreview.uxml";
    private const string ThemeTokensPath = "Assets/_Game/UI/Foundation/Styles/ThemeTokens.uss";
    private const string RuntimePanelThemePath = "Assets/_Game/UI/Foundation/Styles/RuntimePanelTheme.uss";

    private const string AffixSpriteFmt = "Assets/_Game/UI/Foundation/Sprites/Affix/affix_{0}.png";
    private const string CurrencySpriteFmt = "Assets/_Game/UI/Foundation/Sprites/Currency/currency_{0}.png";

    private InventoryView? _view;

    [MenuItem("SM/Town/Inventory 미리보기", false, 14)]
    public static void Open()
    {
        var window = GetWindow<InventoryPreviewBootstrap>("Inventory 미리보기");
        window.minSize = new Vector2(1400f, 820f);
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

        _view = new InventoryView(root);
        _view.Render(BuildMockViewState());
    }

    private InventoryViewState BuildMockViewState()
    {
        return new InventoryViewState(
            Gold: 9_876_543,
            Echo: 543_210,
            GoldSprite: LoadCurrencySprite("gold"),
            EchoSprite: LoadCurrencySprite("echo"),
            Categories: BuildMockCategories(),
            Items: BuildMockItems(),
            Detail: BuildMockDetail());
    }

    private IReadOnlyList<InventoryCategoryViewState> BuildMockCategories()
    {
        // Presenter static catalog 재사용 + mock count.
        var counts = new[] { "109/300", "48/100", "36/100", "25/100" };
        var i = 0;
        return InventoryPresenter.Categories
            .Select(c => new InventoryCategoryViewState(
                Key: c.Key,
                Label: c.Label,
                Count: counts[i++],
                IconSprite: LoadAffixSprite(c.IconKey),
                IsSelected: c.Key == "weapon"))
            .ToList();
    }

    private IReadOnlyList<InventoryItemViewState> BuildMockItems()
    {
        // 5×4 = 20 cell mock. rarity / weapon family / equipped 다양 demo.
        var rows = new (string Icon, string Rarity, string WeaponFamily, bool Equipped)[]
        {
            ("atk",       "epic",   "blade",  true ),
            ("crit",      "epic",   "blade",  false),
            ("pierce",    "rare",   "bow",    false),
            ("speed",     "epic",   "bow",    true ),
            ("magic_atk", "epic",   "focus",  false),
            ("armor",     "rare",   "shield", true ),
            ("crit",      "rare",   "blade",  false),
            ("pierce",    "epic",   "bow",    false),
            ("speed",     "rare",   "bow",    false),
            ("magic_atk", "rare",   "focus",  true ),
            ("atk",       "common", "blade",  false),
            ("armor",     "common", "shield", false),
            ("pierce",    "common", "bow",    false),
            ("speed",     "common", "bow",    false),
            ("magic_atk", "common", "focus",  false),
            ("crit",      "rare",   "blade",  false),
            ("armor",     "epic",   "shield", false),
            ("pierce",    "rare",   "bow",    false),
            ("speed",     "epic",   "bow",    false),
            ("magic_atk", "common", "focus",  false),
        };

        var items = new List<InventoryItemViewState>(rows.Length);
        for (var i = 0; i < rows.Length; i++)
        {
            var r = rows[i];
            var familyLabel = InventoryPresenter.WeaponFamilyLabels.TryGetValue(r.WeaponFamily, out var lbl)
                ? lbl : r.WeaponFamily;
            items.Add(new InventoryItemViewState(
                ItemInstanceId: $"mock_item_{i:D2}",
                IconKey: r.Icon,
                RarityKey: r.Rarity,
                WeaponFamilyKey: r.WeaponFamily,
                WeaponFamilyLabel: familyLabel,
                IsEquipped: r.Equipped,
                IconSprite: LoadAffixSprite(r.Icon)));
        }
        return items;
    }

    private InventoryDetailViewState BuildMockDetail()
    {
        // affix detail — 이름 + 값 범위 (AffixDefinition.NameKey / ValueMin~ValueMax).
        // instance 확정 roll 미저장이라 범위 표기 (가짜 rolled value 아님 — audit §4.1 P1-3).
        // implicit 1 + prefix 2 + suffix 2 = 5 line (item-and-affix-system.md V1 floor).
        var affixes = new[]
        {
            new InventoryAffixRowViewState("implicit", "기본 공격력", "180 ~ 256"),
            new InventoryAffixRowViewState("prefix",   "치명타 확률", "8 ~ 19"),
            new InventoryAffixRowViewState("prefix",   "관통",        "0.6 ~ 1.4"),
            new InventoryAffixRowViewState("suffix",   "공격 속도",   "60 ~ 140"),
            new InventoryAffixRowViewState("suffix",   "흡혈",        "4 ~ 10"),
        };
        return new InventoryDetailViewState(
            ItemInstanceId: "mock_item_00",
            IconSprite: LoadAffixSprite("atk"),
            Affixes: affixes);
    }

    private static Texture2D? LoadAffixSprite(string key) =>
        AssetDatabase.LoadAssetAtPath<Texture2D>(string.Format(AffixSpriteFmt, key));
    private static Texture2D? LoadCurrencySprite(string key) =>
        AssetDatabase.LoadAssetAtPath<Texture2D>(string.Format(CurrencySpriteFmt, key));
}
