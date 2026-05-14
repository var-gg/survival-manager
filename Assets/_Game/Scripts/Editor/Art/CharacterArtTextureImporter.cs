using System;
using UnityEditor;
using UnityEngine;

namespace SM.Editor.Art;

/// <summary>
/// Assets/Resources/_Game/Art/Characters/ 아래 모든 텍스처를 Sprite로 자동 import한다.
/// 포트레잇·스킬 아이콘은 UITK Image.sprite + Resources.Load&lt;Sprite&gt;로 소비되므로
/// textureType이 Sprite가 아니면 런타임에서 null이 된다.
/// 자산 구조 SoT: pindoc://analysis-character-asset-matrix-dawn-priest
/// </summary>
public sealed class CharacterArtTextureImporter : AssetPostprocessor
{
    private const string CharacterArtRoot = "Assets/Resources/_Game/Art/Characters/";

    private void OnPreprocessTexture()
    {
        if (!assetPath.StartsWith(CharacterArtRoot, StringComparison.Ordinal))
        {
            return;
        }

        var importer = (TextureImporter)assetImporter;
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.spritePixelsPerUnit = 100f;
        importer.mipmapEnabled = false;
        importer.alphaIsTransparency = true;
        importer.wrapMode = TextureWrapMode.Clamp;
    }

    /// <summary>
    /// AssetPostprocessor 도입 전에 이미 import된 캐릭터 아트(textureType=Default)를
    /// Sprite로 일괄 재임포트한다. 1회성 — 이후 신규 자산은 OnPreprocessTexture가 처리한다.
    /// </summary>
    [MenuItem("SM/내러티브/캐릭터 아트 Sprite 재임포트")]
    public static void ReimportCharacterArt()
    {
        AssetDatabase.ImportAsset(
            CharacterArtRoot.TrimEnd('/'),
            ImportAssetOptions.ForceUpdate | ImportAssetOptions.ImportRecursive);
        AssetDatabase.Refresh();
        // console이 Debug.Log를 캡처하지 않는 unity-cli 환경이라 LogError로 완료를 알린다.
        Debug.LogError("[CharacterArtTextureImporter] 캐릭터 아트 Sprite 재임포트 완료");
    }
}
