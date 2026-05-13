using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace SM.Editor.Bootstrap.UI;

/// <summary>
/// SM/Town/RosterGrid 미리보기 — Town hub default surface (RosterGrid)를 mock 데이터로
/// EditorWindow에서 렌더링. Scene/Play 없이 디자인 검증.
///
/// 시안 SoT: pindoc://town-ui-ux-시안-갤러리-v1
/// IA SoT: pindoc://ux-surface-catalog-v1-draft (Town.RosterGrid P0 default)
/// </summary>
public sealed class TownRosterGridPreviewBootstrap : EditorWindow
{
    private const string VisualTreePath = "Assets/_Game/UI/Screens/Town/Preview/RosterGridPreview.uxml";
    private const string HeroCardTemplatePath = "Assets/_Game/UI/Foundation/Components/HeroPortraitCard.uxml";
    private const string ThemeTokensPath = "Assets/_Game/UI/Foundation/Styles/ThemeTokens.uss";
    private const string RuntimePanelThemePath = "Assets/_Game/UI/Foundation/Styles/RuntimePanelTheme.uss";

    [MenuItem("SM/Town/RosterGrid 미리보기", false, 8)]
    public static void Open()
    {
        var window = GetWindow<TownRosterGridPreviewBootstrap>("RosterGrid 미리보기");
        window.minSize = new Vector2(1280f, 720f);
    }

    private void CreateGUI()
    {
        var visualTree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(VisualTreePath);
        var heroCardTemplate = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(HeroCardTemplatePath);
        var tokensSheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(ThemeTokensPath);
        var panelThemeSheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(RuntimePanelThemePath);

        if (visualTree == null) { rootVisualElement.Add(new Label($"UXML 못 찾음: {VisualTreePath}")); return; }
        if (heroCardTemplate == null) { rootVisualElement.Add(new Label($"HeroPortraitCard 템플릿 못 찾음: {HeroCardTemplatePath}")); return; }

        // Foundation 토큰 / 테마 시트 — UXML 내부 <Style src> 외에 추가로 로드해 var(--sm-*) 등 cascade 보장
        if (tokensSheet != null) rootVisualElement.styleSheets.Add(tokensSheet);
        if (panelThemeSheet != null) rootVisualElement.styleSheets.Add(panelThemeSheet);

        visualTree.CloneTree(rootVisualElement);
        var grid = rootVisualElement.Q<VisualElement>("GridContainer");
        if (grid == null) { Debug.LogError("[RosterGridPreview] GridContainer 못 찾음"); return; }

        grid.Clear();
        foreach (var entry in MockHeroes)
        {
            var instance = heroCardTemplate.Instantiate();
            var card = instance.Q<VisualElement>("HeroPortraitCard");
            if (card != null)
            {
                ApplyMockEntry(card, entry);
            }
            grid.Add(instance);
        }

        var countLabel = rootVisualElement.Q<Label>("HeroCount");
        if (countLabel != null)
        {
            countLabel.text = $"{MockHeroes.Count} / 12";
        }
    }

    private static void ApplyMockEntry(VisualElement card, MockHero entry)
    {
        RemoveClasses(card, "sm-hpc--default", "sm-hpc--selected", "sm-hpc--ko", "sm-hpc--injured");
        card.AddToClassList($"sm-hpc--{entry.State}");

        RemoveClasses(card,
            "sm-hpc--fam-beastkin", "sm-hpc--fam-vanguard", "sm-hpc--fam-striker",
            "sm-hpc--fam-ranger", "sm-hpc--fam-mystic");
        card.AddToClassList($"sm-hpc--fam-{entry.Family}");

        RemoveClasses(card, "sm-hpc--rare-common", "sm-hpc--rare-rare", "sm-hpc--rare-epic");
        card.AddToClassList($"sm-hpc--rare-{entry.Rarity}");

        var nameKo = card.Q<Label>("name-ko");
        var nameEn = card.Q<Label>("name-en");
        var archetype = card.Q<Label>("archetype-text");
        if (nameKo != null) nameKo.text = entry.NameKo;
        if (nameEn != null) nameEn.text = entry.NameEn;
        if (archetype != null) archetype.text = entry.ArchetypeLabel;
    }

    private static void RemoveClasses(VisualElement card, params string[] classes)
    {
        foreach (var c in classes)
        {
            if (card.ClassListContains(c))
            {
                card.RemoveFromClassList(c);
            }
        }
    }

    private static readonly IReadOnlyList<MockHero> MockHeroes = new[]
    {
        new MockHero("단린",     "DAWN PRIEST",     "선봉 / Vanguard",  "vanguard", "epic",   "default"),
        new MockHero("이빨바람", "PACK RAIDER",     "결투가 / Striker", "striker",  "epic",   "default"),
        new MockHero("묵향",     "GRAVE HEXER",     "비술사 / Mystic",  "mystic",   "epic",   "default"),
        new MockHero("공한",     "ECHO SAVANT",     "사수 / Ranger",    "ranger",   "epic",   "default"),
        new MockHero("선영",     "RIFT STALKER",    "사수 / Ranger",    "ranger",   "rare",   "default"),
        new MockHero("회조",     "GREY FANG",       "결투가 / Striker", "beastkin", "rare",   "ko"),
        new MockHero("침월",     "SILENT MOON",     "비술사 / Mystic",  "mystic",   "rare",   "default"),
        new MockHero("백규",     "BAEKGYU STERN",   "선봉 / Vanguard",  "vanguard", "rare",   "default"),
        new MockHero("철위",     "IRON WARDEN",     "선봉 / Vanguard",  "vanguard", "common", "injured"),
        new MockHero("묘적",     "CRYPT GUARDIAN",  "선봉 / Vanguard",  "vanguard", "common", "default"),
        new MockHero("송곳벽",   "FANG BULWARK",    "결투가 / Striker", "beastkin", "common", "default"),
        new MockHero("서검",     "OATH SLAYER",     "결투가 / Striker", "striker",  "common", "default"),
    };

    private readonly struct MockHero
    {
        public MockHero(string nameKo, string nameEn, string archetypeLabel, string family, string rarity, string state)
        {
            NameKo = nameKo;
            NameEn = nameEn;
            ArchetypeLabel = archetypeLabel;
            Family = family;
            Rarity = rarity;
            State = state;
        }
        public string NameKo { get; }
        public string NameEn { get; }
        public string ArchetypeLabel { get; }
        public string Family { get; }
        public string Rarity { get; }
        public string State { get; }
    }
}
