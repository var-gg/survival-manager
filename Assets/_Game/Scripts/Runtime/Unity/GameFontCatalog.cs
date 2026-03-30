using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace SM.Unity;

[CreateAssetMenu(menuName = "SM/UI/Game Font Catalog", fileName = "GameFontCatalog")]
public sealed class GameFontCatalog : ScriptableObject
{
    public const string ResourcePath = "_Game/Fonts/GameFontCatalog";

    [SerializeField] private Font sharedUiFont = null!;

    public Font SharedUiFont => sharedUiFont != null ? sharedUiFont : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

    public static GameFontCatalog? Load()
    {
        return Resources.Load<GameFontCatalog>(ResourcePath);
    }

    public static Font LoadSharedUiFont()
    {
        return Load()?.SharedUiFont ?? Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
    }

    public static void ApplyFont(Text? text)
    {
        if (text == null)
        {
            return;
        }

        text.font = LoadSharedUiFont();
    }

    public static void ApplyFont(TextMesh? textMesh)
    {
        if (textMesh == null)
        {
            return;
        }

        var font = LoadSharedUiFont();
        textMesh.font = font;
        var renderer = textMesh.GetComponent<MeshRenderer>();
        if (renderer != null)
        {
            renderer.sharedMaterial = font.material;
        }
    }

    public static void ApplyToHierarchy(Transform? root)
    {
        if (root == null)
        {
            return;
        }

        foreach (var text in root.GetComponentsInChildren<Text>(true))
        {
            ApplyFont(text);
        }

        foreach (var textMesh in root.GetComponentsInChildren<TextMesh>(true))
        {
            ApplyFont(textMesh);
        }
    }
}
