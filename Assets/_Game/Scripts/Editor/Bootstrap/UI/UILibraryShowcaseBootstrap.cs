using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SM.Editor.Bootstrap.UI;

public static class UILibraryShowcaseBootstrap
{
    private const string ScenePath = "Assets/_Game/UI/Foundation/Scenes/UILibraryShowcase.unity";
    private const string MenuPath = "SM/Internal/UI/Rebuild Foundation Library Showcase";
    private const float PixelsPerUnit = 100f;

    private static readonly ShowcaseAsset[] Assets =
    {
        new("panel_frame_outer", "Assets/_Game/UI/Foundation/Sprites/ArtBible/Frame/ui_panel_frame_outer.png"),
        new("panel_frame_inner", "Assets/_Game/UI/Foundation/Sprites/ArtBible/Frame/ui_panel_frame_inner.png"),
        new("card_frame_normal", "Assets/_Game/UI/Foundation/Sprites/ArtBible/Frame/ui_card_frame_normal.png"),
        new("card_frame_selected_base", "Assets/_Game/UI/Foundation/Sprites/ArtBible/Frame/ui_card_frame_selected_base.png"),
        new("card_frame_locked", "Assets/_Game/UI/Foundation/Sprites/ArtBible/Frame/ui_card_frame_locked.png"),
        new("icon_slot_frame", "Assets/_Game/UI/Foundation/Sprites/ArtBible/IconSlot/ui_icon_slot_frame.png"),
        new("button_gold", "Assets/_Game/UI/Foundation/Sprites/ArtBible/Button/ui_button_gold.png"),
        new("button_dark", "Assets/_Game/UI/Foundation/Sprites/ArtBible/Button/ui_button_dark.png"),
        new("button_disabled", "Assets/_Game/UI/Foundation/Sprites/ArtBible/Button/ui_button_disabled.png"),
        new("input_bg", "Assets/_Game/UI/Foundation/Sprites/ArtBible/Input/ui_input_bg.png"),
        new("dropdown_bg", "Assets/_Game/UI/Foundation/Sprites/ArtBible/Input/ui_dropdown_bg.png"),
        new("tab_active", "Assets/_Game/UI/Foundation/Sprites/ArtBible/Tab/ui_tab_active.png"),
        new("tab_inactive", "Assets/_Game/UI/Foundation/Sprites/ArtBible/Tab/ui_tab_inactive.png"),
        new("corner_ornament_tl", "Assets/_Game/UI/Foundation/Sprites/ArtBible/Ornament/ui_corner_ornament_tl.png"),
        new("corner_ornament_tr", "Assets/_Game/UI/Foundation/Sprites/ArtBible/Ornament/ui_corner_ornament_tr.png"),
        new("corner_ornament_bl", "Assets/_Game/UI/Foundation/Sprites/ArtBible/Ornament/ui_corner_ornament_bl.png"),
        new("corner_ornament_br", "Assets/_Game/UI/Foundation/Sprites/ArtBible/Ornament/ui_corner_ornament_br.png"),
        new("header_decoration_center", "Assets/_Game/UI/Foundation/Sprites/ArtBible/Ornament/ui_header_decoration_center.png"),
        new("divider_horizontal_diamond", "Assets/_Game/UI/Foundation/Sprites/ArtBible/Divider/ui_divider_horizontal_diamond.png"),
        new("divider_vertical_diamond", "Assets/_Game/UI/Foundation/Sprites/ArtBible/Divider/ui_divider_vertical_diamond.png"),
        new("scroll_thumb", "Assets/_Game/UI/Foundation/Sprites/ArtBible/Scroll/ui_scroll_thumb.png"),
        new("scroll_track", "Assets/_Game/UI/Foundation/Sprites/ArtBible/Scroll/ui_scroll_track.png"),
        new("close_button", "Assets/_Game/UI/Foundation/Sprites/ArtBible/Button/ui_close_button.png"),
        new("back_button", "Assets/_Game/UI/Foundation/Sprites/ArtBible/Button/ui_back_button.png"),
        new("lock_icon", "Assets/_Game/UI/Foundation/Sprites/ArtBible/Icon/ui_lock_icon.png"),
        new("arrow_marker", "Assets/_Game/UI/Foundation/Sprites/ArtBible/Icon/ui_arrow_marker.png"),
    };

    [MenuItem(MenuPath)]
    public static void RebuildShowcaseSceneMenu()
    {
        RebuildShowcaseScene();
    }

    public static void RebuildShowcaseScene()
    {
        EnsureFolderPath(Path.GetDirectoryName(ScenePath)!.Replace('\\', '/'));

        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        EnsureCamera();
        CreateHeader();

        const float sectionWidth = 15.8f;
        const float rowHeight = 1.42f;
        const int rowsPerSection = 13;

        for (var i = 0; i < Assets.Length; i++)
        {
            var section = i / rowsPerSection;
            var row = i % rowsPerSection;
            var x = -12.5f + section * sectionWidth;
            var y = 7.2f - row * rowHeight;
            CreateAssetRow(Assets[i], x, y);
        }

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene, ScenePath);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"[UILibraryShowcase] Scene rebuilt with {Assets.Length} accepted assets at 200/100/50 scale.");
    }

    private static void EnsureCamera()
    {
        var cameraGo = new GameObject("Main Camera");
        cameraGo.tag = "MainCamera";
        cameraGo.transform.position = new Vector3(0f, 0f, -10f);

        var camera = cameraGo.AddComponent<Camera>();
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(0.047f, 0.066f, 0.149f, 1f);
        camera.orthographic = true;
        camera.orthographicSize = 10f;
        camera.nearClipPlane = 0.1f;
        camera.farClipPlane = 50f;
    }

    private static void CreateHeader()
    {
        CreateText("Title", "Foundation UI Component Library - 200% / 100% / 50%", new Vector3(-12.6f, 9.1f, 0f), 0.16f, TextAnchor.MiddleLeft, new Color(0.96f, 0.84f, 0.54f));
        CreateText("Column200", "200%", new Vector3(-5.4f, 8.45f, 0f), 0.11f, TextAnchor.MiddleCenter, new Color(0.86f, 0.77f, 0.58f));
        CreateText("Column100", "100%", new Vector3(-2.55f, 8.45f, 0f), 0.11f, TextAnchor.MiddleCenter, new Color(0.86f, 0.77f, 0.58f));
        CreateText("Column50", "50%", new Vector3(-0.2f, 8.45f, 0f), 0.11f, TextAnchor.MiddleCenter, new Color(0.86f, 0.77f, 0.58f));
        CreateText("Column200B", "200%", new Vector3(10.4f, 8.45f, 0f), 0.11f, TextAnchor.MiddleCenter, new Color(0.86f, 0.77f, 0.58f));
        CreateText("Column100B", "100%", new Vector3(13.25f, 8.45f, 0f), 0.11f, TextAnchor.MiddleCenter, new Color(0.86f, 0.77f, 0.58f));
        CreateText("Column50B", "50%", new Vector3(15.6f, 8.45f, 0f), 0.11f, TextAnchor.MiddleCenter, new Color(0.86f, 0.77f, 0.58f));
    }

    private static void CreateAssetRow(ShowcaseAsset asset, float x, float y)
    {
        var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(asset.AssetPath);
        if (sprite == null)
        {
            throw new FileNotFoundException($"Sprite not found or not imported: {asset.AssetPath}");
        }

        CreateText($"Label_{asset.Id}", asset.Id, new Vector3(x, y, 0f), 0.07f, TextAnchor.MiddleRight, new Color(0.88f, 0.84f, 0.74f));

        var baseSize = ComputeBaseSize(sprite);
        CreateSprite(asset, sprite, "200", new Vector3(x + 3.25f, y, 0f), baseSize * 2f);
        CreateSprite(asset, sprite, "100", new Vector3(x + 6.1f, y, 0f), baseSize);
        CreateSprite(asset, sprite, "050", new Vector3(x + 8.45f, y, 0f), baseSize * 0.5f);
    }

    private static Vector2 ComputeBaseSize(Sprite sprite)
    {
        var worldSize = new Vector2(sprite.rect.width / PixelsPerUnit, sprite.rect.height / PixelsPerUnit);
        var maxEdge = Mathf.Max(worldSize.x, worldSize.y);
        var fit = maxEdge > 0f ? Mathf.Min(1.02f / maxEdge, 0.7f) : 1f;
        return new Vector2(Mathf.Max(0.22f, worldSize.x * fit), Mathf.Max(0.22f, worldSize.y * fit));
    }

    private static void CreateSprite(ShowcaseAsset asset, Sprite sprite, string scaleLabel, Vector3 position, Vector2 size)
    {
        var go = new GameObject($"{asset.Id}_{scaleLabel}");
        go.transform.position = position;

        var renderer = go.AddComponent<SpriteRenderer>();
        renderer.sprite = sprite;
        renderer.sortingOrder = 1;

        if (sprite.border.sqrMagnitude > 0.01f)
        {
            renderer.drawMode = SpriteDrawMode.Sliced;
            renderer.size = size;
        }
        else
        {
            var nativeWorldSize = new Vector2(sprite.rect.width / PixelsPerUnit, sprite.rect.height / PixelsPerUnit);
            var scale = nativeWorldSize.x > 0f && nativeWorldSize.y > 0f
                ? Mathf.Min(size.x / nativeWorldSize.x, size.y / nativeWorldSize.y)
                : 1f;
            go.transform.localScale = new Vector3(scale, scale, 1f);
        }
    }

    private static void CreateText(string name, string text, Vector3 position, float characterSize, TextAnchor anchor, Color color)
    {
        var go = new GameObject(name);
        go.transform.position = position;

        var mesh = go.AddComponent<TextMesh>();
        mesh.text = text;
        mesh.characterSize = characterSize;
        mesh.fontSize = 48;
        mesh.anchor = anchor;
        mesh.alignment = TextAlignment.Left;
        mesh.color = color;
    }

    private static void EnsureFolderPath(string folderPath)
    {
        if (string.IsNullOrWhiteSpace(folderPath) || AssetDatabase.IsValidFolder(folderPath))
        {
            return;
        }

        var parent = Path.GetDirectoryName(folderPath)?.Replace('\\', '/');
        if (!string.IsNullOrWhiteSpace(parent))
        {
            EnsureFolderPath(parent);
        }

        var folderName = Path.GetFileName(folderPath);
        if (!string.IsNullOrWhiteSpace(parent) && !string.IsNullOrWhiteSpace(folderName) && !AssetDatabase.IsValidFolder(folderPath))
        {
            AssetDatabase.CreateFolder(parent, folderName);
        }
    }

    private readonly struct ShowcaseAsset
    {
        public ShowcaseAsset(string id, string assetPath)
        {
            Id = id;
            AssetPath = assetPath;
        }

        public string Id { get; }

        public string AssetPath { get; }
    }
}
