using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using NUnit.Framework;
using SM.Content.Definitions;
using SM.Tests.EditMode.Fakes;
using SM.Unity.UI;
using UnityEditor;
using UnityEngine;

namespace SM.Tests.EditMode;

[Category("BatchOnly")]
public sealed class ContentIconRoutingTests
{
    private static readonly IReadOnlyDictionary<string, (string IconId, string Sha256)> LegacyBaseline =
        new Dictionary<string, (string IconId, string Sha256)>(StringComparer.Ordinal)
        {
            ["affix_sharp"] = new("affix_atk", "26e422a126205995d61a8a85a986c209f6cb7958d94f7e913a391f5811e72e2d"),
            ["affix_focusing"] = new("affix_magic_atk", "26032d0c4138e640085e3ba33dbf092f5031ffeb35b4a476fe3acd7c7a879152"),
            ["affix_sturdy"] = new("affix_armor", "d3cf5f164347dbdb22d4b9b80c7847839747af83ebfa8c02017ca2fcdeb787bc"),
            ["affix_warded"] = new("affix_resist_magic", "cfd275d7fb3cf162624449c12f782c708b8e108217604c9bbbbfeeb4fa87083e"),
            ["affix_blessed"] = new("affix_heal", "41ca8a5429e401af9d6b705fd38e499384564bd916d3facfbfeca0b908562934"),
            ["affix_hasty"] = new("affix_speed", "c648590c0472577d7c0a12f43527cb7e961ed9a846dad66f40095bb499be6326"),
            ["affix_fierce"] = new("affix_atk", "26e422a126205995d61a8a85a986c209f6cb7958d94f7e913a391f5811e72e2d"),
            ["affix_precise"] = new("affix_crit", "51d45f246df746a9e19bc53731804e0cddf235f8e5a7d089ce488a811e4587cc"),
            ["affix_piercing"] = new("affix_pierce", "a427b6c416341ed22e1796f64e57335ce9b4886d4a8e8b3de4dd98defa936aa0"),
            ["affix_vital"] = new("affix_hp", "a0d6c24c5f1287973ebbedf0619a5335b0e54b820719be79ecd51a97174f8f44"),
            ["affix_ironclad"] = new("affix_armor", "d3cf5f164347dbdb22d4b9b80c7847839747af83ebfa8c02017ca2fcdeb787bc"),
            ["affix_mender"] = new("affix_heal", "41ca8a5429e401af9d6b705fd38e499384564bd916d3facfbfeca0b908562934"),
            ["affix_lithe"] = new("affix_speed", "c648590c0472577d7c0a12f43527cb7e961ed9a846dad66f40095bb499be6326"),
            ["affix_lucid"] = new("affix_cooldown", "ea71eaadd3f581f97b442e0e7dad2c25fa31aa988659167ab8209f5baa12724f"),
            ["affix_farshot"] = new("affix_pierce", "a427b6c416341ed22e1796f64e57335ce9b4886d4a8e8b3de4dd98defa936aa0"),
            ["affix_guarded"] = new("affix_block", "5383fd04f4b3945172d31aa1b3ca04f41f4b87c2bfe3747de0538c66b9c412fe"),
            ["affix_channeling"] = new("affix_cast_speed", "7dc6cad7db8739caf577eacdd1985343c60d6e32a7b4c3f46f850cc5660e2c5e"),
            ["affix_cleansing"] = new("affix_cleanse", "785e77301553d7251df7e1ab238dce61c3304182ca28b6c9a725862d37a990d2"),
            ["affix_bracing"] = new("affix_hp", "a0d6c24c5f1287973ebbedf0619a5335b0e54b820719be79ecd51a97174f8f44"),
            ["affix_resolute"] = new("affix_resist_phys", "afb8df75a10943eaaceff395f11d297d3162d509628d5188a16efe75fd68a9f7"),
            ["affix_relentless"] = new("affix_charge", "7703d8292add7a0d957db00c628c4e96f2e4132f69d31fa06107b6a3661214f0"),
            ["affix_watchful"] = new("affix_crit", "51d45f246df746a9e19bc53731804e0cddf235f8e5a7d089ce488a811e4587cc"),
            ["affix_packborn"] = new("affix_link", "9e233cb0af4aee6e7498ba36072b1555a639acd12686f8604c586839f99a829f"),
            ["affix_wraithbound"] = new("affix_amplify", "fae64062f1e84fea4185a8f839eab1112a62ea6eece0102973e5839867a12caa"),
        };

    [Test]
    public void ResolveAffix_MigratedAuthoredMapping_PreservesPinnedPngBytes()
    {
        var affixes = new Dictionary<string, AffixDefinition>(StringComparer.Ordinal);
        foreach (var affixId in LegacyBaseline.Keys)
        {
            var path = $"Assets/Resources/_Game/Content/Definitions/Affixes/{affixId}.asset";
            var definition = AssetDatabase.LoadAssetAtPath<AffixDefinition>(path);
            Assert.That(definition, Is.Not.Null, $"Missing authored affix asset: {path}");
            affixes[affixId] = definition;
        }

        var resolver = new ContentIconResolver(new FakeCombatContentLookup(affixes: affixes));
        foreach (var (affixId, baseline) in LegacyBaseline)
        {
            var expectedPath = $"Assets/Resources/_Game/Art/Icons/Affix/{baseline.IconId}.png";
            var texture = resolver.ResolveAffix(affixId);

            Assert.That(affixes[affixId].IconId, Is.EqualTo(baseline.IconId), $"{affixId} authored IconId drifted.");
            Assert.That(texture, Is.Not.Null, $"{affixId} did not resolve.");
            Assert.That(AssetDatabase.GetAssetPath(texture), Is.EqualTo(expectedPath), $"{affixId} resolved a different texture.");
            Assert.That(ComputeSha256(expectedPath), Is.EqualTo(baseline.Sha256), $"{affixId} texture bytes drifted.");
        }
    }

    [Test]
    public void ResolveAffix_AuthoredIconId_WinsOverLegacyFallback()
    {
        var definition = ScriptableObject.CreateInstance<AffixDefinition>();
        try
        {
            definition.Id = "affix_sharp";
            definition.IconId = "affix_hp";
            var affixes = new Dictionary<string, AffixDefinition>(StringComparer.Ordinal)
            {
                [definition.Id] = definition,
            };
            var resolver = new ContentIconResolver(new FakeCombatContentLookup(affixes: affixes));

            var texture = resolver.ResolveAffix(definition.Id);

            Assert.That(texture, Is.Not.Null);
            Assert.That(
                AssetDatabase.GetAssetPath(texture),
                Is.EqualTo("Assets/Resources/_Game/Art/Icons/Affix/affix_hp.png"));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(definition);
        }
    }

    private static string ComputeSha256(string path)
    {
        using var sha256 = SHA256.Create();
        var hash = sha256.ComputeHash(File.ReadAllBytes(path));
        return BitConverter.ToString(hash).Replace("-", string.Empty).ToLowerInvariant();
    }
}
