using System;
using System.Collections.Generic;
using SM.Content.Definitions;
using SM.Unity;
using UnityEngine;

namespace SM.Editor.Validation;

internal sealed record ContentDefinitionFallbackLoadResult(
    IReadOnlyList<ScriptableObject> Assets,
    IReadOnlyDictionary<int, string> AssetPaths);

internal static class ContentDefinitionFileFallbackLoader
{
    internal static ContentDefinitionFallbackLoadResult Load()
    {
        if (!RuntimeCombatContentFileParser.TryLoad(out var parsed, out var error))
        {
            throw new InvalidOperationException($"Fallback content parser failed. {error}");
        }

        return new ContentDefinitionFallbackLoadResult(parsed.AllAssets, parsed.AssetPaths);
    }
}
