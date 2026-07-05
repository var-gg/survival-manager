using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;

namespace SM.Tests.EditMode;

[TestFixture]
[Category("FastUnit")]
public sealed class GamePanelSotRegistryFastTests
{
    private const string RegistryPath = "Assets/_Game/UI/Foundation/ArtBible/game-panel-sot-registry.json";
    private const string ArtBibleRegistryPath = "Assets/_Game/UI/Foundation/ArtBible/artbible-role-registry.json";
    private const string TownUxmlPath = "Assets/_Game/UI/Screens/Town/TownScreen.uxml";
    private const string TownControllerPath = "Assets/_Game/Scripts/Runtime/Unity/TownScreenController.cs";

    [Test]
    public void GamePanelSotRegistry_Declares_Production_Planned_And_Legacy_States()
    {
        Assert.That(File.Exists(RegistryPath), Is.True, RegistryPath);
        var json = File.ReadAllText(RegistryPath);
        var surfaces = ReadSurfaces(json);

        Assert.That(ReadStringArray(json, "states"), Does.Contain("active-production"));
        Assert.That(ReadStringArray(json, "states"), Does.Contain("planned"));
        Assert.That(ReadStringArray(json, "states"), Does.Contain("legacy-preview"));
        Assert.That(surfaces.Values.Select(surface => surface.State), Does.Contain("active-production"));
        Assert.That(surfaces.Values.Select(surface => surface.State), Does.Contain("planned"));
        Assert.That(surfaces.Values.Select(surface => surface.State), Does.Contain("legacy-preview"));
    }

    [Test]
    public void ActiveProductionSurfaces_Have_Existing_Uxml_And_TacticalSetup_Alias()
    {
        var surfaces = ReadSurfaces(File.ReadAllText(RegistryPath));
        Assert.That(surfaces, Does.ContainKey("town.tactical_setup"));

        var tacticalSetup = surfaces["town.tactical_setup"];
        Assert.That(tacticalSetup.DisplayNameKo, Is.EqualTo("전술 설정"));
        Assert.That(tacticalSetup.State, Is.EqualTo("active-production"));
        Assert.That(tacticalSetup.ProductionUxml, Is.EqualTo("Assets/_Game/UI/Panels/TownSquadBuilder/TownSquadBuilder.uxml"));
        Assert.That(tacticalSetup.RouteOwner, Is.EqualTo("TownScreenController.TacticalSetupButton"));
        Assert.That(tacticalSetup.LegacyAliases, Does.Contain("SquadBuilder"));
        Assert.That(tacticalSetup.LegacyAliases, Does.Contain("SquadBuilderPresenter"));

        foreach (var surface in surfaces.Values.Where(surface => surface.State == "active-production"))
        {
            Assert.That(surface.ProductionUxml, Is.Not.Empty, surface.Id);
            Assert.That(File.Exists(surface.ProductionUxml), Is.True, surface.Id + " -> " + surface.ProductionUxml);
        }
    }

    [Test]
    public void TacticalWorkshop_Is_Active_Production_Town_Route()
    {
        // TacticalWorkshop wire cycle (2026-07): 전술 공방 승격. Codex legacy 샌드박스 표면은
        // town.tactical_workshop_sandbox로 legacy-preview에 잔류(씬·에디터 부트스트랩 참조라 삭제 금지).
        var surfaces = ReadSurfaces(File.ReadAllText(RegistryPath));
        Assert.That(surfaces, Does.ContainKey("town.tactical_workshop"));
        Assert.That(surfaces["town.tactical_workshop"].State, Is.EqualTo("active-production"));
        Assert.That(surfaces["town.tactical_workshop"].RouteOwner, Is.EqualTo("TownScreenController.TacticalWorkshopButton"));
        Assert.That(surfaces, Does.ContainKey("town.tactical_workshop_sandbox"));
        Assert.That(surfaces["town.tactical_workshop_sandbox"].State, Is.EqualTo("legacy-preview"));
        Assert.That(surfaces["town.tactical_workshop_sandbox"].RouteOwner, Is.EqualTo("PreviewOnly"));

        var townUxml = File.ReadAllText(TownUxmlPath);
        var townController = File.ReadAllText(TownControllerPath);
        Assert.That(townUxml, Does.Contain("TacticalSetupButton"));
        Assert.That(townUxml, Does.Contain("TacticalSetupTemplate"));
        Assert.That(townUxml, Does.Contain("TacticalWorkshopButton"));
        Assert.That(townUxml, Does.Contain("TacticalWorkshopTemplate"));
        Assert.That(townUxml, Does.Contain("../../Panels/TacticalWorkshop/TacticalWorkshop.uxml"));
        Assert.That(townController, Does.Contain("TryWireTacticalSetup"));
        Assert.That(townController, Does.Contain("TryWireTacticalWorkshop"));
        Assert.That(townController, Does.Contain("BindTacticalWorkshopOpen"));
    }

    [Test]
    public void ArtBibleRegistry_Separates_Active_Production_And_LegacyPreview_Uss()
    {
        var json = File.ReadAllText(ArtBibleRegistryPath);
        var enforced = ReadStringArray(json, "enforcedProductionPanelUss");
        var legacyPreview = ReadStringArray(json, "legacyPreviewPanelUss");

        Assert.That(enforced, Does.Contain("Assets/_Game/UI/Panels/TownSquadBuilder/TownSquadBuilder.uss"));
        Assert.That(enforced, Does.Contain("Assets/_Game/UI/Panels/TacticalWorkshop/TacticalWorkshop.uss"));
        Assert.That(legacyPreview, Does.Not.Contain("Assets/_Game/UI/Panels/TacticalWorkshop/TacticalWorkshop.uss"));
        Assert.That(legacyPreview, Does.Contain("Assets/_Game/UI/TacticalWorkshop/TacticalWorkshop.uss"));
    }

    private static IReadOnlyDictionary<string, PanelSurface> ReadSurfaces(string json)
    {
        var result = new Dictionary<string, PanelSurface>(StringComparer.Ordinal);
        foreach (Match match in Regex.Matches(json, @"\{\s*""id""\s*:\s*""(?<id>[^""]+)""(?<body>.*?)\n    \}", RegexOptions.Singleline))
        {
            var chunk = match.Value;
            var surface = new PanelSurface(
                Id: match.Groups["id"].Value,
                DisplayNameKo: ReadString(chunk, "displayNameKo"),
                State: ReadString(chunk, "state"),
                ProductionUxml: ReadString(chunk, "productionUxml"),
                RouteOwner: ReadString(chunk, "routeOwner"),
                LegacyAliases: ReadStringArray(chunk, "legacyAliases"));
            result[surface.Id] = surface;
        }

        return result;
    }

    private static string ReadString(string json, string propertyName)
    {
        var match = Regex.Match(json, "\"" + Regex.Escape(propertyName) + "\"\\s*:\\s*\"(?<value>(?:\\\\.|[^\"])*)\"");
        return match.Success ? UnescapeJson(match.Groups["value"].Value) : string.Empty;
    }

    private static IReadOnlyList<string> ReadStringArray(string json, string propertyName)
    {
        var pattern = "\"" + Regex.Escape(propertyName) + "\"\\s*:\\s*\\[(?<items>.*?)\\]";
        var match = Regex.Match(json, pattern, RegexOptions.Singleline);
        if (!match.Success)
        {
            return Array.Empty<string>();
        }

        return Regex.Matches(match.Groups["items"].Value, "\"(?<value>(?:\\\\.|[^\"])*)\"")
            .Cast<Match>()
            .Select(matchValue => UnescapeJson(matchValue.Groups["value"].Value))
            .ToArray();
    }

    private static string UnescapeJson(string value)
    {
        return value.Replace("\\\"", "\"", StringComparison.Ordinal)
            .Replace("\\\\", "\\", StringComparison.Ordinal);
    }

    private sealed record PanelSurface(
        string Id,
        string DisplayNameKo,
        string State,
        string ProductionUxml,
        string RouteOwner,
        IReadOnlyList<string> LegacyAliases);
}
