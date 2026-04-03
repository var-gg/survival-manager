using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using SM.Content.Definitions;
using UnityEditor;
using UnityEngine;

namespace SM.Editor.Validation;

public static class BaselineDocGenerator
{
    private const string OutputDirectory = "Logs/baseline-docs";

    [MenuItem("SM/Generate/Write Baseline Asset Docs")]
    public static void Generate()
    {
        Directory.CreateDirectory(OutputDirectory);

        var archetypes = FindAssets<UnitArchetypeDefinition>();
        var skills = FindAssets<SkillDefinitionAsset>();
        var footprints = FindAssets<FootprintProfileDefinition>();
        var behaviors = FindAssets<BehaviorProfileDefinition>();
        var mobility = FindAssets<MobilityProfileDefinition>();

        var builder = new StringBuilder();
        AppendHeader(builder);
        AppendArchetypeSection(builder, archetypes);
        AppendSkillSection(builder, skills);
        AppendProfileSections(builder, footprints, behaviors, mobility);
        AppendFooter(builder, archetypes.Count, skills.Count, footprints.Count, behaviors.Count, mobility.Count);

        var outputPath = Path.Combine(OutputDirectory, "baseline-asset-summary.md");
        File.WriteAllText(outputPath, builder.ToString());
        Debug.Log($"[BaselineDocGenerator] Wrote {outputPath}");

        AssetDatabase.Refresh();
        EditorUtility.RevealInFinder(outputPath);
    }

    [MenuItem("SM/Generate/Write Baseline Asset Docs", true)]
    private static bool CanGenerate() => !EditorApplication.isCompiling;

    private static List<T> FindAssets<T>() where T : ScriptableObject
    {
        return AssetDatabase.FindAssets($"t:{typeof(T).Name}")
            .Select(AssetDatabase.GUIDToAssetPath)
            .Where(path => path.EndsWith(".asset", StringComparison.OrdinalIgnoreCase))
            .Select(AssetDatabase.LoadAssetAtPath<T>)
            .Where(asset => asset != null)
            .OrderBy(asset => asset.name)
            .ToList();
    }

    private static void AppendHeader(StringBuilder builder)
    {
        builder.AppendLine("---");
        builder.AppendLine("status: generated");
        builder.AppendLine($"generated: {DateTime.Now:yyyy-MM-dd HH:mm}");
        builder.AppendLine("source-of-truth: committed assets");
        builder.AppendLine("---");
        builder.AppendLine();
        builder.AppendLine("# Baseline Asset Summary");
        builder.AppendLine();
    }

    private static void AppendArchetypeSection(StringBuilder builder, List<UnitArchetypeDefinition> archetypes)
    {
        builder.AppendLine("## Archetypes");
        builder.AppendLine();

        if (archetypes.Count == 0)
        {
            builder.AppendLine("(none found)");
            builder.AppendLine();
            return;
        }

        builder.AppendLine($"| Id | Scope | Race | Class | Role | Anchor | HP | Atk | AtkSpd | MoveSpd | Range |");
        builder.AppendLine($"|---|---|---|---|---|---|---|---|---|---|---|");

        foreach (var a in archetypes)
        {
            var raceId = a.Race != null ? a.Race.name : "-";
            var classId = a.Class != null ? a.Class.name : "-";
            builder.AppendLine($"| {a.Id} | {a.ScopeKind} | {raceId} | {classId} | {a.RoleTag} | {a.DefaultAnchor} | {a.BaseMaxHealth} | {a.BaseAttack} | {a.BaseAttackSpeed} | {a.BaseMoveSpeed} | {a.BaseAttackRange} |");
        }

        builder.AppendLine();

        foreach (var a in archetypes)
        {
            builder.AppendLine($"### {a.Id}");
            builder.AppendLine();

            var skillIds = a.Skills.Where(s => s != null).Select(s => s.Id);
            builder.AppendLine($"- Skills: [{string.Join(", ", skillIds)}]");

            if (a.LockedSignatureActiveSkill != null)
                builder.AppendLine($"- SignatureActive: {a.LockedSignatureActiveSkill.Id}");
            if (a.LockedSignaturePassiveSkill != null)
                builder.AppendLine($"- SignaturePassive: {a.LockedSignaturePassiveSkill.Id}");

            builder.AppendLine($"- Tactics: {a.TacticPreset.Count} rules");
            builder.AppendLine($"- Posture: {a.PreferredTeamPosture}");

            if (a.FootprintProfile != null)
                builder.AppendLine($"- Footprint: nav={a.FootprintProfile.NavigationRadius} sep={a.FootprintProfile.SeparationRadius} reach={a.FootprintProfile.CombatReach} body={a.FootprintProfile.BodySizeCategory}");
            if (a.BehaviorProfile != null)
                builder.AppendLine($"- Behavior: {a.BehaviorProfile.FormationLine} {a.BehaviorProfile.RangeDiscipline} opp={a.BehaviorProfile.Opportunism} disc={a.BehaviorProfile.Discipline}");
            if (a.MobilityProfile != null)
                builder.AppendLine($"- Mobility: {a.MobilityProfile.Style} {a.MobilityProfile.Purpose} dist={a.MobilityProfile.Distance}");

            builder.AppendLine();
        }
    }

    private static void AppendSkillSection(StringBuilder builder, List<SkillDefinitionAsset> skills)
    {
        builder.AppendLine("## Skills");
        builder.AppendLine();

        if (skills.Count == 0)
        {
            builder.AppendLine("(none found)");
            builder.AppendLine();
            return;
        }

        builder.AppendLine($"| Id | Kind | Slot | Damage | Delivery | Range | Cooldown | Windup | Power |");
        builder.AppendLine($"|---|---|---|---|---|---|---|---|---|");

        foreach (var s in skills)
        {
            var cd = s.CooldownSeconds >= 0f ? s.CooldownSeconds : s.BaseCooldownSeconds;
            builder.AppendLine($"| {s.Id} | {s.Kind} | {s.SlotKind} | {s.DamageType} | {s.Delivery} | {s.Range} | {cd:0.##} | {s.CastWindupSeconds:0.##} | {s.Power} |");
        }

        builder.AppendLine();
    }

    private static void AppendProfileSections(
        StringBuilder builder,
        List<FootprintProfileDefinition> footprints,
        List<BehaviorProfileDefinition> behaviors,
        List<MobilityProfileDefinition> mobility)
    {
        builder.AppendLine("## Footprint Profiles");
        builder.AppendLine();
        if (footprints.Count > 0)
        {
            builder.AppendLine($"| Name | Body | Nav | Sep | Reach | Range | Slots |");
            builder.AppendLine($"|---|---|---|---|---|---|---|");
            foreach (var fp in footprints)
            {
                builder.AppendLine($"| {fp.name} | {fp.BodySizeCategory} | {fp.NavigationRadius} | {fp.SeparationRadius} | {fp.CombatReach} | {fp.PreferredRangeMin}–{fp.PreferredRangeMax} | {fp.EngagementSlotCount} |");
            }
        }
        else
        {
            builder.AppendLine("(none found)");
        }

        builder.AppendLine();
        builder.AppendLine("## Behavior Profiles");
        builder.AppendLine();
        if (behaviors.Count > 0)
        {
            builder.AppendLine($"| Name | Formation | Discipline | Range | Retreat% | Opportunism | Stability |");
            builder.AppendLine($"|---|---|---|---|---|---|---|");
            foreach (var bp in behaviors)
            {
                builder.AppendLine($"| {bp.name} | {bp.FormationLine} | {bp.RangeDiscipline} | {bp.PreferredRangeMin}–{bp.PreferredRangeMax} | {bp.RetreatAtHpPercent} | {bp.Opportunism} | {bp.Stability} |");
            }
        }
        else
        {
            builder.AppendLine("(none found)");
        }

        builder.AppendLine();
        builder.AppendLine("## Mobility Profiles");
        builder.AppendLine();
        if (mobility.Count > 0)
        {
            builder.AppendLine($"| Name | Style | Purpose | Distance | Cooldown | Trigger |");
            builder.AppendLine($"|---|---|---|---|---|---|");
            foreach (var mp in mobility)
            {
                builder.AppendLine($"| {mp.name} | {mp.Style} | {mp.Purpose} | {mp.Distance} | {mp.Cooldown} | {mp.TriggerMinDistance}–{mp.TriggerMaxDistance} |");
            }
        }
        else
        {
            builder.AppendLine("(none found)");
        }

        builder.AppendLine();
    }

    private static void AppendFooter(StringBuilder builder, int archetypes, int skills, int footprints, int behaviors, int mobility)
    {
        builder.AppendLine("---");
        builder.AppendLine();
        builder.AppendLine($"Total: {archetypes} archetypes, {skills} skills, {footprints} footprints, {behaviors} behaviors, {mobility} mobility profiles");
    }
}
