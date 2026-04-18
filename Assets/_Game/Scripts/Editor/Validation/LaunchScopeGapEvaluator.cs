using System;
using System.Collections.Generic;
using System.Linq;
using SM.Content.Definitions;
using SM.Core.Content;
using UnityEngine;

namespace SM.Editor.Validation;

internal static class LaunchScopeGapEvaluator
{
    internal static LaunchScopeCountReport BuildCountReport(IEnumerable<ScriptableObject> assets)
    {
        var assetList = assets.ToList();
        var archetypes = assetList.OfType<UnitArchetypeDefinition>().ToList();
        var augments = assetList.OfType<AugmentDefinition>().ToList();
        var authoredSkills = assetList.OfType<SkillDefinitionAsset>()
            .Where(skill => !skill.Id.StartsWith("support_", StringComparison.Ordinal))
            .ToList();

        return new LaunchScopeCountReport
        {
            ArchetypeCount = archetypes.Count,
            CoreArchetypeCount = archetypes.Count(archetype => archetype.ScopeKind == ArchetypeScopeValue.Core),
            SpecialistArchetypeCount = archetypes.Count(archetype => archetype.ScopeKind == ArchetypeScopeValue.Specialist),
            SkillCount = authoredSkills.Count,
            EquippableCount = assetList.OfType<ItemBaseDefinition>().Count(),
            AffixCount = assetList.OfType<AffixDefinition>().Count(),
            PassiveBoardCount = assetList.OfType<PassiveBoardDefinition>().Count(),
            PassiveNodeCount = assetList.OfType<PassiveNodeDefinition>().Count(),
            TemporaryAugmentCount = augments.Count(augment => !augment.IsPermanent),
            PermanentAugmentCount = augments.Count(augment => augment.IsPermanent),
            SynergyFamilyCount = assetList.OfType<SynergyDefinition>().Count(),
            TeamTacticCount = assetList.OfType<TeamTacticDefinition>().Count(),
            RoleInstructionCount = assetList.OfType<RoleInstructionDefinition>().Count(),
        };
    }

    internal static List<string> BuildGapList(LaunchScopeCountReport report, LaunchScopeThreshold threshold)
    {
        var gaps = new List<string>();
        AddThresholdGap(gaps, "archetypes", report.ArchetypeCount, threshold.ArchetypeCount);
        AddThresholdGap(gaps, "coreArchetypes", report.CoreArchetypeCount, threshold.CoreArchetypeCount);
        AddThresholdGap(gaps, "specialists", report.SpecialistArchetypeCount, threshold.SpecialistArchetypeCount);
        AddThresholdGap(gaps, "skills", report.SkillCount, threshold.SkillCount);
        AddThresholdGap(gaps, "equippables", report.EquippableCount, threshold.EquippableCount);
        AddThresholdGap(gaps, "affixes", report.AffixCount, threshold.AffixCount);
        AddThresholdGap(gaps, "passiveBoards", report.PassiveBoardCount, threshold.PassiveBoardCount);
        AddThresholdGap(gaps, "passiveNodes", report.PassiveNodeCount, threshold.PassiveNodeCount);
        AddThresholdGap(gaps, "tempAugments", report.TemporaryAugmentCount, threshold.TemporaryAugmentCount);
        AddThresholdGap(gaps, "permAugments", report.PermanentAugmentCount, threshold.PermanentAugmentCount);
        AddThresholdGap(gaps, "synergyFamilies", report.SynergyFamilyCount, threshold.SynergyFamilyCount);
        return gaps;
    }

    internal static IReadOnlyList<PassiveBoardShapeReport> BuildPassiveBoardReports(IEnumerable<ScriptableObject> assets)
    {
        return assets
            .OfType<PassiveBoardDefinition>()
            .OrderBy(board => board.ClassId, StringComparer.Ordinal)
            .Select(board => new PassiveBoardShapeReport
            {
                BoardId = board.Id,
                ClassId = board.ClassId,
                SmallCount = board.Nodes.Count(node => node != null && node.NodeKind == PassiveNodeKindValue.Small),
                NotableCount = board.Nodes.Count(node => node != null && node.NodeKind == PassiveNodeKindValue.Notable),
                KeystoneCount = board.Nodes.Count(node => node != null && node.NodeKind == PassiveNodeKindValue.Keystone),
            })
            .ToList();
    }

    internal static void ValidatePassiveBoardShape(PassiveBoardShapeReport board, ICollection<ContentValidationIssue> issues)
    {
        var matchesFloor = board.SmallCount == 12 && board.NotableCount == 5 && board.KeystoneCount == 1;
        var matchesSafeTarget = board.SmallCount == 14 && board.NotableCount == 8 && board.KeystoneCount == 2;
        if (!matchesFloor && !matchesSafeTarget)
        {
            ContentValidationIssueFactory.AddError(
                issues,
                "passive_board.shape",
                $"Passive board '{board.BoardId}' must match the paid launch floor shape 12/5/1 or safe target shape 14/8/2. Found {board.SmallCount}/{board.NotableCount}/{board.KeystoneCount}.",
                board.BoardId,
                board.ClassId);
        }
    }

    private static void AddThresholdGap(ICollection<string> gaps, string label, int current, int? target)
    {
        if (!target.HasValue || current >= target.Value)
        {
            return;
        }

        gaps.Add($"{label} {current}/{target.Value}");
    }
}
