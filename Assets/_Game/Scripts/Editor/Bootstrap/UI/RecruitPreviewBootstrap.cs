using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace SM.Editor.Bootstrap.UI;

/// <summary>
/// SM/Town/Recruit 미리보기 — 시안 갤러리 V1 #3.
/// navy frame + 4 candidate card (lead hero portrait + class glyph + tier stars) +
/// scout / refresh CTA / recruit confirm action bar.
/// </summary>
public sealed class RecruitPreviewBootstrap : EditorWindow
{
    private const string VisualTreePath = "Assets/_Game/UI/Screens/Town/Preview/RecruitPreview.uxml";
    private const string ThemeTokensPath = "Assets/_Game/UI/Foundation/Styles/ThemeTokens.uss";
    private const string RuntimePanelThemePath = "Assets/_Game/UI/Foundation/Styles/RuntimePanelTheme.uss";

    private const string ClassSpriteFmt = "Assets/_Game/UI/Foundation/Sprites/Class/class_{0}.png";
    private const string PortraitPathFmt = "Assets/Resources/_Game/Art/Characters/hero_{0}/portrait_full.png";

    private readonly struct Candidate
    {
        public Candidate(string heroId, string classKey, int starTier, int synergyCount, bool protectedLock)
        {
            HeroId = heroId; ClassKey = classKey; StarTier = starTier; SynergyCount = synergyCount; ProtectedLock = protectedLock;
        }
        public string HeroId { get; }
        public string ClassKey { get; }
        public int StarTier { get; }
        public int SynergyCount { get; }
        public bool ProtectedLock { get; }
    }

    private static readonly Candidate[] Candidates =
    {
        new("dawn_priest",   "vanguard", 4, 4, false),
        new("pack_raider",   "duelist",  5, 5, true),   // protected (gold lock glow)
        new("echo_savant",   "ranger",   3, 3, false),
        new("grave_hexer",   "mystic",   4, 4, false),
    };

    [MenuItem("SM/Town/Recruit 미리보기", false, 10)]
    public static void Open()
    {
        var window = GetWindow<RecruitPreviewBootstrap>("Recruit 미리보기");
        window.minSize = new Vector2(1280f, 760f);
    }

    private void CreateGUI()
    {
        var visualTree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(VisualTreePath);
        if (visualTree == null) { rootVisualElement.Add(new Label($"UXML 못 찾음: {VisualTreePath}")); return; }

        var tokens = AssetDatabase.LoadAssetAtPath<StyleSheet>(ThemeTokensPath);
        var theme = AssetDatabase.LoadAssetAtPath<StyleSheet>(RuntimePanelThemePath);
        if (tokens != null) rootVisualElement.styleSheets.Add(tokens);
        if (theme != null) rootVisualElement.styleSheets.Add(theme);

        visualTree.CloneTree(rootVisualElement);
        InjectCandidates();
    }

    private void InjectCandidates()
    {
        var row = rootVisualElement.Q<VisualElement>("CardRow");
        if (row == null) return;
        row.Clear();

        foreach (var c in Candidates)
        {
            var card = new VisualElement();
            card.AddToClassList("rcp-card");
            if (c.ProtectedLock) card.AddToClassList("rcp-card--protected");

            if (c.ProtectedLock)
            {
                var lockIcon = new VisualElement();
                lockIcon.AddToClassList("rcp-card__lock");
                card.Add(lockIcon);
            }

            var portrait = new VisualElement();
            portrait.AddToClassList("rcp-card__portrait");
            var portraitPath = string.Format(PortraitPathFmt, c.HeroId);
            var portraitTex = AssetDatabase.LoadAssetAtPath<Texture2D>(portraitPath);
            if (portraitTex != null) portrait.style.backgroundImage = new StyleBackground(portraitTex);
            card.Add(portrait);

            var classGlyph = new VisualElement();
            classGlyph.AddToClassList("rcp-card__class-glyph");
            var classPath = string.Format(ClassSpriteFmt, c.ClassKey);
            var classTex = AssetDatabase.LoadAssetAtPath<Texture2D>(classPath);
            if (classTex != null) classGlyph.style.backgroundImage = new StyleBackground(classTex);
            card.Add(classGlyph);

            var synergyRow = new VisualElement();
            synergyRow.AddToClassList("rcp-card__synergy-row");
            for (var i = 0; i < c.SynergyCount; i++)
            {
                var pip = new VisualElement();
                pip.AddToClassList("rcp-card__synergy-pip");
                if (i == 0) pip.AddToClassList("rcp-card__synergy-pip--accent");
                synergyRow.Add(pip);
            }
            card.Add(synergyRow);

            var starRow = new VisualElement();
            starRow.AddToClassList("rcp-card__star-row");
            for (var i = 0; i < c.StarTier; i++)
            {
                var s = new VisualElement();
                s.AddToClassList("rcp-card__star");
                starRow.Add(s);
            }
            card.Add(starRow);

            row.Add(card);
        }
    }
}
