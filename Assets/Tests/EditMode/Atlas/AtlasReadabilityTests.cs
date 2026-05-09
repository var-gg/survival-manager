using System.Linq;
using NUnit.Framework;
using SM.Atlas.Model;
using SM.Atlas.Services;
using SM.Unity.UI.Atlas;

namespace SM.Tests.EditMode.Atlas;

[Category("BatchOnly")]
public sealed class AtlasReadabilityTests
{
    [Test]
    public void Formatter_MapsAtlasJargonToKoreanLabels()
    {
        Assert.That(AtlasReadabilityFormatter.FormatAnswerLane("peel_anti_dive"), Is.EqualTo("측면 차단·다이브 방어"));
        Assert.That(AtlasReadabilityFormatter.FormatAnswerLane("anti_mark_cleanse"), Is.EqualTo("표식 해제·정화"));
        Assert.That(AtlasReadabilityFormatter.FormatEnemyPreview("beast skirmish / weakside dive"), Does.Contain("약측 다이브"));
        Assert.That(AtlasReadabilityFormatter.FormatModifierCategory(AtlasModifierCategory.RewardBias), Is.EqualTo("보상 가중"));
        Assert.That(AtlasReadabilityFormatter.FormatModifierCategory(AtlasModifierCategory.ThreatPressure), Is.EqualTo("위협 압력"));
        Assert.That(AtlasReadabilityFormatter.FormatModifierCategory(AtlasModifierCategory.AffinityBoost), Is.EqualTo("인연 보정"));
    }

    [Test]
    public void Formatter_UsesRegisteredKoreanCharacterNames()
    {
        Assert.That(AtlasReadabilityFormatter.FormatCharacterName("hero_pack_raider", "legacy"), Is.EqualTo("이빨바람 / Pack Raider"));
        Assert.That(AtlasReadabilityFormatter.FormatCharacterName("hero_dawn_priest", "legacy"), Is.EqualTo("단린 / Dawn Priest"));
        Assert.That(AtlasReadabilityFormatter.FormatCharacterName("hero_echo_savant", "legacy"), Is.EqualTo("공한 / Echo Savant"));
        Assert.That(AtlasReadabilityFormatter.FormatCharacterName("hero_grave_hexer", "legacy"), Is.EqualTo("묵향 / Grave Hexer"));
        Assert.That(AtlasReadabilityFormatter.FormatCharacterName("hero_trail_scout", "legacy"), Is.EqualTo("숲살이 / Trail Scout"));
    }

    [Test]
    public void Presenter_BuildsRouteLabelsAndFourChipTypesWithoutChangingHashes()
    {
        var presenter = new AtlasScreenPresenter(AtlasGrayboxDataFactory.CreateRegion());
        var state = presenter.Build();

        Assert.That(state.Routes.Select(route => route.Label), Has.Some.Contains("서쪽 보상 길"));
        Assert.That(state.Routes.Select(route => route.Label), Has.Some.Contains("중앙 욕망 길"));
        Assert.That(state.Routes.Select(route => route.Label), Has.None.Contains("Reward +"));
        Assert.That(state.Tiles, Is.All.Matches<AtlasHexTileViewState>(tile =>
            !string.IsNullOrWhiteSpace(tile.TypeChip.Label)
            && !string.IsNullOrWhiteSpace(tile.RewardFamilyChip.Label)
            && !string.IsNullOrWhiteSpace(tile.DifficultyChip.Label)
            && tile.ModifierChips.Count is >= 1 and <= 2));
        Assert.That(state.Tiles.SelectMany(tile => tile.ModifierChips).Select(chip => chip.Label), Has.None.Contains("RewardBias"));
        Assert.That(state.Tiles.SelectMany(tile => tile.ModifierChips).Select(chip => chip.Label), Has.None.Contains("ThreatPressure"));
        Assert.That(state.Tiles.Count(tile => tile.AuraCategories.Count > 1), Is.GreaterThan(0));
        Assert.That(state.Preview.DebugHashLine, Does.Contain("NodeOverlayHash="));
        Assert.That(state.Preview.DebugHashLine, Does.Contain("BattleContextHash="));
    }

    [Test]
    public void PreviewPanel_UsesKoreanProseExceptDebugHashLine()
    {
        var presenter = new AtlasScreenPresenter(AtlasGrayboxDataFactory.CreateRegion());
        var state = presenter.Build();
        var prose = string.Join(
            "\n",
            state.RegionTitle,
            state.PlacementSummary,
            state.Preview.Title,
            state.Preview.JudgementLine,
            state.Preview.EnemyPreview,
            state.Preview.ModifierStack,
            state.Preview.RewardPreview,
            state.Preview.RecommendedCharacters,
            state.Preview.BoundaryNote);

        Assert.That(prose, Does.Not.Contain("RewardBias"));
        Assert.That(prose, Does.Not.Contain("ThreatPressure"));
        Assert.That(prose, Does.Not.Contain("AffinityBoost"));
        Assert.That(prose, Does.Not.Contain("weakside dive"));
        Assert.That(prose, Does.Not.Contain("Stable"));
        Assert.That(state.Preview.RecommendedCharacters, Does.Contain("이빨바람 / Pack Raider"));
    }
}
