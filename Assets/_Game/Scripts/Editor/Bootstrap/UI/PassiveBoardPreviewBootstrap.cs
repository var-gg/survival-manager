using System.Collections.Generic;
using System.Linq;
using SM.Unity.UI.Town.Preview;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace SM.Editor.Bootstrap.UI;

/// <summary>
/// SM/Town/Passive Board 미리보기 — Sprint 1 presenter 패턴 dev tool.
/// pindoc V1 SoT (passive-board-node-catalog.md): 4 board × 18 node (12 small + 5 notable + 1 keystone).
/// </summary>
public sealed class PassiveBoardPreviewBootstrap : EditorWindow
{
    private const string VisualTreePath = "Assets/_Game/UI/Screens/Town/Preview/PassiveBoardPreview.uxml";
    private const string ThemeTokensPath = "Assets/_Game/UI/Foundation/Styles/ThemeTokens.uss";
    private const string RuntimePanelThemePath = "Assets/_Game/UI/Foundation/Styles/RuntimePanelTheme.uss";
    private const string ClassSpriteFmt = "Assets/_Game/UI/Foundation/Sprites/Class/class_{0}.png";
    private const string AffixSpriteFmt = "Assets/_Game/UI/Foundation/Sprites/Affix/affix_{0}.png";
    private const string PortraitPathFmt = "Assets/Resources/_Game/Art/Characters/hero_{0}/portrait_full.png";

    private PassiveBoardView? _view;

    [MenuItem("SM/Town/Passive Board 미리보기", false, 13)]
    public static void Open()
    {
        var window = GetWindow<PassiveBoardPreviewBootstrap>("Passive Board 미리보기");
        window.minSize = new Vector2(1280f, 760f);
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

        _view = new PassiveBoardView(root);
        _view.Render(BuildMockViewState());
    }

    private PassiveBoardViewState BuildMockViewState()
    {
        // per-hero 컨텍스트 mock — duelist hero (portrait 보유한 pack_raider).
        var header = new PassiveBoardHeaderViewState(
            HeroId: "pack_raider",
            HeroDisplayName: "팩 레이더 / Pack Raider",
            ClassKey: "duelist",
            ClassLabel: "결투가 보드",
            BoardId: "board_duelist",
            HeroPortrait: LoadHeroPortrait("pack_raider"),
            ClassIconSprite: LoadClassSprite("duelist"));

        var nodes = BuildMockNodes();
        var (activeSmall, totalSmall, activeNotable, totalNotable, activeKeystone, totalKeystone) = CountActive(nodes);

        var keystoneNode = nodes.First(n => n.NodeId == "passive_duelist_keystone_01");
        var detail = new PassiveBoardDetailViewState(
            SelectedNodeId: keystoneNode.NodeId,
            KindLabel: "KEYSTONE",
            TitleText: "KILLING INTENT",
            RuleSummary: keystoneNode.RuleSummary,
            Tags: keystoneNode.Tags,
            AvailableLabel: "ACTIVE",
            ButtonLabel: "DEACTIVATE",
            IconSprite: LoadAffixSprite("crit"));

        var footer = new PassiveBoardFooterViewState(
            BreakdownText: $"DUELIST · SMALL {activeSmall}/{totalSmall} · NOTABLE {activeNotable}/{totalNotable} · KEYSTONE {activeKeystone}/{totalKeystone}");

        return new PassiveBoardViewState(header, nodes, detail, footer);
    }

    private IReadOnlyList<PassiveBoardNodeViewState> BuildMockNodes()
    {
        // duelist board 18 node — hex tree 배치 (1 keystone + 5 notable + 12 small)
        var raw = new (float Left, float Top, string Kind, bool Active, string Icon, string NodeId, string Rule, string Tags)[]
        {
            (0.46f, 0.40f, "keystone", true,  "crit",      "passive_duelist_keystone_01",  "phys_power +1.5, attack_speed +0.12, crit_multiplier +0.15", "frontline · burst"),
            (0.46f, 0.22f, "notable",  true,  "atk",       "passive_duelist_notable_01",   "phys_power +1, attack_speed +0.08",   "frontline · burst"),
            (0.63f, 0.34f, "notable",  true,  "crit",      "passive_duelist_notable_02",   "crit_chance +0.025, lifesteal +0.02", "frontline · burst"),
            (0.57f, 0.55f, "notable",  false, "pierce",    "passive_duelist_notable_03",   "phys_power +0.9, phys_pen +0.8",      "frontline · burst"),
            (0.35f, 0.55f, "notable",  false, "speed",     "passive_duelist_notable_04",   "move_speed +0.06, target_switch_delay -0.05", "frontline · burst"),
            (0.29f, 0.34f, "notable",  false, "lifesteal", "passive_duelist_notable_05",   "phys_power +1, crit_chance +0.02",    "frontline · burst"),
            (0.46f, 0.04f, "small",    true,  "atk",       "passive_duelist_small_01",     "phys_power +0.8",      "frontline · burst"),
            (0.64f, 0.09f, "small",    true,  "speed",     "passive_duelist_small_02",     "attack_speed +0.1",    "frontline · burst"),
            (0.77f, 0.22f, "small",    true,  "crit",      "passive_duelist_small_03",     "crit_chance +0.02",    "frontline · burst"),
            (0.82f, 0.40f, "small",    false, "lifesteal", "passive_duelist_small_04",     "lifesteal +0.02",      "frontline · burst"),
            (0.77f, 0.58f, "small",    false, "pierce",    "passive_duelist_small_05",     "phys_pen +0.7",        "frontline · burst"),
            (0.64f, 0.71f, "small",    false, "speed",     "passive_duelist_small_06",     "move_speed +0.05",     "frontline · burst"),
            (0.46f, 0.76f, "small",    false, "atk",       "passive_duelist_small_07",     "phys_power +0.8",      "frontline · burst"),
            (0.28f, 0.71f, "small",    true,  "speed",     "passive_duelist_small_08",     "attack_speed +0.1",    "frontline · burst"),
            (0.15f, 0.58f, "small",    false, "crit",      "passive_duelist_small_09",     "crit_chance +0.02",    "frontline · burst"),
            (0.10f, 0.40f, "small",    true,  "lifesteal", "passive_duelist_small_10",     "lifesteal +0.02",      "frontline · burst"),
            (0.15f, 0.22f, "small",    false, "pierce",    "passive_duelist_small_11",     "phys_pen +0.7",        "frontline · burst"),
            (0.28f, 0.09f, "small",    false, "speed",     "passive_duelist_small_12",     "move_speed +0.05",     "frontline · burst"),
        };

        return raw.Select(n => new PassiveBoardNodeViewState(
            NodeId: n.NodeId,
            KindKey: n.Kind,
            Left: n.Left,
            Top: n.Top,
            IconKey: n.Icon,
            IconSprite: LoadAffixSprite(n.Icon),
            RuleSummary: n.Rule,
            Tags: n.Tags,
            IsActive: n.Active)).ToList();
    }

    private static (int activeSmall, int totalSmall, int activeNotable, int totalNotable, int activeKeystone, int totalKeystone)
        CountActive(IReadOnlyList<PassiveBoardNodeViewState> nodes)
    {
        int activeSmall = 0, totalSmall = 0, activeNotable = 0, totalNotable = 0, activeKeystone = 0, totalKeystone = 0;
        foreach (var n in nodes)
        {
            switch (n.KindKey)
            {
                case "small":    totalSmall++;    if (n.IsActive) activeSmall++;    break;
                case "notable":  totalNotable++;  if (n.IsActive) activeNotable++;  break;
                case "keystone": totalKeystone++; if (n.IsActive) activeKeystone++; break;
            }
        }
        return (activeSmall, totalSmall, activeNotable, totalNotable, activeKeystone, totalKeystone);
    }

    private static Texture2D? LoadClassSprite(string key) =>
        AssetDatabase.LoadAssetAtPath<Texture2D>(string.Format(ClassSpriteFmt, key));
    private static Texture2D? LoadAffixSprite(string key) =>
        AssetDatabase.LoadAssetAtPath<Texture2D>(string.Format(AffixSpriteFmt, key));
    private static Texture2D? LoadHeroPortrait(string heroId) =>
        AssetDatabase.LoadAssetAtPath<Texture2D>(string.Format(PortraitPathFmt, heroId));
}
