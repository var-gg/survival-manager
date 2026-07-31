using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UIElements;

namespace SM.Tests.PlayMode;

public sealed partial class UxBiblePlayModeWitnessTests
{
    private const float GeometryTolerance = 1.5f;

    private static void AssertCharacterSheetContainment(VisualElement root)
    {
        var frame = Require<VisualElement>(root, "TownCharacterSheetFrame");
        var header = RequireClass(root, "tcs-header");
        var role = Require<Label>(root, "TcsRoleLabel");
        AssertContained(frame, header, "Character Sheet header");
        AssertContained(header, role, "Character Sheet role chip");
        AssertSingleLineHeight(role, "Character Sheet role chip");

        var statGrid = Require<VisualElement>(root, "TcsStatGrid");
        var overviewBody = Require<VisualElement>(root, "TcsOverviewBody");
        AssertNoOverlap(statGrid, overviewBody, "Character Sheet stat grid and overview rows");
        var statTiles = statGrid.Query<VisualElement>(className: "tcs-stat").ToList();
        Assert.That(statTiles, Has.Count.EqualTo(8), "Character Sheet should render all eight stat tiles.");
        foreach (var tile in statTiles)
        {
            AssertVisibleTextContained(tile, "Character Sheet stat tile");
        }

        var overviewRows = overviewBody.Query<VisualElement>(className: "tcs-row").ToList();
        Assert.That(overviewRows, Has.Count.EqualTo(6), "Character Sheet overview should render all six metadata rows.");
        foreach (var row in overviewRows)
        {
            AssertContained(overviewBody, row, "Character Sheet overview row");
            AssertVisibleTextContained(row, "Character Sheet overview row");
        }

        AssertVerticalSequence(overviewRows, "Character Sheet overview rows");

        var rail = Require<VisualElement>(root, "TcsHeroRail");
        foreach (var entry in rail.Query<VisualElement>(className: "tcs-hero-rail__entry").ToList())
        {
            var portrait = RequireClass(entry, "tcs-hero-rail__portrait");
            var copy = RequireClass(entry, "tcs-hero-rail__copy");
            AssertContained(entry, portrait, "Character Sheet thumbnail portrait");
            AssertContained(entry, copy, "Character Sheet thumbnail copy");
            AssertNoOverlap(portrait, copy, "Character Sheet thumbnail portrait and copy");
            AssertVisibleTextContained(copy, "Character Sheet thumbnail copy");
        }
    }

    private static void AssertBattleHudContainment(VisualElement root)
    {
        var battleRoot = Require<VisualElement>(root, "BattleScreenRoot");
        var panels = new[]
        {
            Require<VisualElement>(root, "TopChrome"),
            Require<VisualElement>(root, "CompactLog"),
            Require<VisualElement>(root, "AllySummaryPanel"),
            Require<VisualElement>(root, "EnemySummaryPanel"),
            Require<VisualElement>(root, "ControlBar"),
        };

        foreach (var panel in panels.Where(IsEffectivelyVisible))
        {
            AssertContained(battleRoot, panel, $"Battle HUD panel '{panel.name}'");
            AssertVisibleTextContained(panel, $"Battle HUD panel '{panel.name}'");
        }

        AssertNoPairwiseOverlap(panels, "Battle HUD top-level panels");

        var compactLog = Require<VisualElement>(root, "CompactLog");
        var controlBar = Require<VisualElement>(root, "ControlBar");
        var logToControlGap = controlBar.worldBound.yMin - compactLog.worldBound.yMax;
        Assert.That(logToControlGap, Is.InRange(8f, 24f),
            "Battle HUD log must be anchored immediately above the Control Bar instead of floating in the battlefield.");

        foreach (var roster in new[]
                 {
                     Require<VisualElement>(root, "AllyRosterList"),
                     Require<VisualElement>(root, "EnemyRosterList"),
                 })
        {
            foreach (var unit in roster.Query<VisualElement>(className: "sm-bs-roster-unit").ToList())
            {
                AssertContained(roster, unit, "Battle HUD roster unit");
                AssertVisibleTextContained(unit, "Battle HUD roster unit");
            }
        }

        // StatusLabel / BattleIntelTitleLabel / TacticalReadoutTitleLabel 검사는 2026-07-31 에 제거됐다.
        // 셋 다 그 요소들과 함께 사라졌다 — 상단 raw 스텝 로그와, 전장 배치를 다시 그리던
        // 전력 분포·전황 판단 패널이다. 새 계약은 BattleHudShellVisualContractFastTests 가 든다.
        //
        // 같은 날 `EnemySummaryPanel` / `EnemyRosterList` 는 <b>되살아났다</b> — 시안
        // (ui_ux_bible_battle_hud_v1)의 골격이 좌우 팀 기둥이기 때문이다.
        //
        // 교훈: 이 파일이 요구하던 `TacticalReadoutPanel` 은 UXML 에서 사라진 뒤에도 하루 가까이
        // 남아 있었다. `test-batch-fast` 는 FastUnit 만 돌리므로 <b>이 PlayMode 위트니스는
        // 그 게이트에 보이지 않는다.</b> HUD 골격을 건드리면 여기도 같은 작업 단위에서 손봐야 한다.
    }

    private static void AssertRewardContainment(VisualElement root)
    {
        var rewardRoot = Require<VisualElement>(root, "RewardScreenRoot");
        var board = RequireClass(root, "reward-bible__board");
        var leftRail = Require<ScrollView>(root, "RewardLeftRail");
        var settlement = Require<VisualElement>(root, "SettlementSummaryPanel");
        var survivors = Require<VisualElement>(root, "RewardSurvivorPanel");
        var progression = Require<VisualElement>(root, "RewardProgressionPanel");
        var timeline = Require<VisualElement>(root, "RewardTimelinePanel");
        var statusBand = Require<VisualElement>(root, "RewardStatusBand");
        var choicePanel = Require<VisualElement>(root, "RewardChoicePanel");
        var choiceTitle = Require<Label>(root, "ChoicesHeaderLabel");
        var victoryBanner = Require<VisualElement>(root, "RewardVictoryBanner");

        AssertContained(board, leftRail, "Reward left-rail viewport");
        var leftPanels = new[] { settlement, survivors, progression };
        foreach (var panel in leftPanels)
        {
            AssertContained(leftRail.contentContainer, panel, $"Reward left-rail panel '{panel.name}'");
            AssertVisibleTextContained(panel, $"Reward left-rail panel '{panel.name}'");
        }

        AssertVerticalSequence(leftPanels, "Reward left-rail panels");
        AssertContained(rewardRoot, timeline, "Reward timeline");
        AssertContained(rewardRoot, statusBand, "Reward status band");
        AssertNoOverlap(board, timeline, "Reward board and timeline");
        AssertNoOverlap(timeline, statusBand, "Reward timeline and status band");
        AssertContained(choicePanel, victoryBanner, "Reward result banner");
        AssertNoOverlap(choiceTitle, victoryBanner, "Reward choice title and RESULT banner");
        AssertVisibleTextContained(victoryBanner, "Reward result banner");
    }

    private static void AssertEquipmentRefitContainment(VisualElement root)
    {
        var affixList = Require<VisualElement>(root, "AffixList");
        var rows = affixList.Query<VisualElement>(className: "erp-affix-row").ToList();
        Assert.That(rows, Is.Not.Empty, "Equipment Refit should render affix rows.");

        foreach (var row in rows)
        {
            var header = RequireClass(row, "erp-affix-row__header");
            var name = RequireClass(row, "erp-affix-row__name");
            var rollContext = RequireClass(row, "erp-affix-row__roll-context");
            var effects = row.Query<VisualElement>(className: "erp-affix-row__effect").ToList();
            Assert.That(effects, Is.Not.Empty, "Equipment Refit affix rows should render at least one effect line.");

            AssertContainedWithinPadding(row, header, "Equipment Refit affix header");
            AssertContainedWithinPadding(row, rollContext, "Equipment Refit affix roll context");
            foreach (var effect in effects)
            {
                AssertContainedWithinPadding(row, effect, "Equipment Refit affix effect");
            }

            AssertVisibleTextContained(row, "Equipment Refit affix row");
            AssertNoOverlap(name, rollContext, "Equipment Refit affix name and roll context");
        }

        AssertVerticalSequence(rows, "Equipment Refit affix rows");
    }

    private static void AssertInventoryContainment(VisualElement root)
    {
        var cells = root.Query<VisualElement>(className: "inv-grid__cell").ToList();
        Assert.That(cells, Is.Not.Empty, "Inventory should render grid cells.");

        foreach (var cell in cells)
        {
            var name = RequireClass(cell, "inv-grid__cell-name");
            var meta = RequireClass(cell, "inv-grid__cell-meta");
            AssertContained(cell, name, "Inventory grid cell name");
            AssertContained(cell, meta, "Inventory grid cell meta");
            AssertVisibleTextContained(cell, "Inventory grid cell");
        }
    }

    private static VisualElement RequireClass(VisualElement root, string className)
    {
        var element = root.Query<VisualElement>(className: className).First();
        Assert.That(element, Is.Not.Null, $"Missing UITK element with class '{className}'.");
        return element!;
    }

    private static void AssertContained(
        VisualElement owner,
        VisualElement child,
        string context,
        float tolerance = GeometryTolerance)
    {
        Assert.That(IsEffectivelyVisible(owner), Is.True, $"{context}: owner should be visible.");
        Assert.That(IsEffectivelyVisible(child), Is.True, $"{context}: child should be visible.");
        var ownerBounds = owner.worldBound;
        var childBounds = child.worldBound;
        Assert.That(childBounds.width, Is.GreaterThan(0f), $"{context}: child must have rendered width.");
        Assert.That(childBounds.height, Is.GreaterThan(0f), $"{context}: child must have rendered height.");
        Assert.That(childBounds.xMin, Is.GreaterThanOrEqualTo(ownerBounds.xMin - tolerance),
            $"{context}: child escaped the owner's left edge.");
        Assert.That(childBounds.xMax, Is.LessThanOrEqualTo(ownerBounds.xMax + tolerance),
            $"{context}: child escaped the owner's right edge.");
        Assert.That(childBounds.yMin, Is.GreaterThanOrEqualTo(ownerBounds.yMin - tolerance),
            $"{context}: child escaped the owner's top edge.");
        Assert.That(childBounds.yMax, Is.LessThanOrEqualTo(ownerBounds.yMax + tolerance),
            $"{context}: child escaped the owner's bottom edge.");
    }

    private static void AssertContainedWithinPadding(
        VisualElement owner,
        VisualElement child,
        string context,
        float tolerance = GeometryTolerance)
    {
        AssertContained(owner, child, context, tolerance);

        var ownerBounds = owner.worldBound;
        var style = owner.resolvedStyle;
        var contentBounds = Rect.MinMaxRect(
            ownerBounds.xMin + style.borderLeftWidth + style.paddingLeft,
            ownerBounds.yMin + style.borderTopWidth + style.paddingTop,
            ownerBounds.xMax - style.borderRightWidth - style.paddingRight,
            ownerBounds.yMax - style.borderBottomWidth - style.paddingBottom);
        var childBounds = child.worldBound;
        Assert.That(childBounds.xMin, Is.GreaterThanOrEqualTo(contentBounds.xMin - tolerance),
            $"{context}: child intruded into the owner's left padding.");
        Assert.That(childBounds.xMax, Is.LessThanOrEqualTo(contentBounds.xMax + tolerance),
            $"{context}: child intruded into the owner's right padding.");
        Assert.That(childBounds.yMin, Is.GreaterThanOrEqualTo(contentBounds.yMin - tolerance),
            $"{context}: child intruded into the owner's top padding.");
        Assert.That(childBounds.yMax, Is.LessThanOrEqualTo(contentBounds.yMax + tolerance),
            $"{context}: child intruded into the owner's bottom padding.");
    }

    private static void AssertVisibleTextContained(VisualElement owner, string context)
    {
        foreach (var text in owner.Query<TextElement>().ToList()
                     .Where(element => IsEffectivelyVisible(element) && !string.IsNullOrWhiteSpace(element.text)))
        {
            AssertContained(owner, text, $"{context} text '{ShortText(text.text)}'");
            AssertSingleLineHeight(text, $"{context} text '{ShortText(text.text)}'");
        }
    }

    private static void AssertSingleLineHeight(TextElement text, string context)
    {
        var fontSize = text.resolvedStyle.fontSize;
        Assert.That(text.worldBound.height + GeometryTolerance, Is.GreaterThanOrEqualTo(fontSize),
            $"{context}: the laid-out label is shorter than its rendered font size.");
    }

    private static void AssertNoPairwiseOverlap(IEnumerable<VisualElement> elements, string context)
    {
        var visible = elements.Where(IsEffectivelyVisible).ToArray();
        for (var left = 0; left < visible.Length; left++)
        {
            for (var right = left + 1; right < visible.Length; right++)
            {
                AssertNoOverlap(
                    visible[left],
                    visible[right],
                    $"{context}: '{visible[left].name}' and '{visible[right].name}'");
            }
        }
    }

    private static void AssertVerticalSequence(IReadOnlyList<VisualElement> elements, string context)
    {
        for (var index = 1; index < elements.Count; index++)
        {
            var previous = elements[index - 1];
            var current = elements[index];
            Assert.That(current.worldBound.yMin, Is.GreaterThanOrEqualTo(previous.worldBound.yMax - GeometryTolerance),
                $"{context}: '{current.name}' overlaps preceding sibling '{previous.name}'.");
        }
    }

    private static void AssertNoOverlap(
        VisualElement first,
        VisualElement second,
        string context,
        float tolerance = GeometryTolerance)
    {
        var firstBounds = first.worldBound;
        var secondBounds = second.worldBound;
        var overlaps = firstBounds.xMin < secondBounds.xMax - tolerance
                       && firstBounds.xMax > secondBounds.xMin + tolerance
                       && firstBounds.yMin < secondBounds.yMax - tolerance
                       && firstBounds.yMax > secondBounds.yMin + tolerance;
        Assert.That(overlaps, Is.False,
            $"{context} overlap: first={Format(firstBounds)}, second={Format(secondBounds)}.");
    }

    private static string ShortText(string text)
    {
        var singleLine = text.Replace('\n', ' ').Trim();
        return singleLine.Length <= 28 ? singleLine : $"{singleLine[..28]}...";
    }

    private static string Format(Rect rect)
    {
        return $"({rect.xMin:0.0},{rect.yMin:0.0})-({rect.xMax:0.0},{rect.yMax:0.0})";
    }
}
