using System.Text;
using SM.Combat.Model;
using SM.Content.Definitions;
using SM.Core.Content;
using UnityEditor;
using UnityEngine;

namespace SM.Editor.Authoring.Inspectors;

[CustomEditor(typeof(UnitArchetypeDefinition))]
public sealed class UnitArchetypeDefinitionEditor : UnityEditor.Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        var archetype = (UnitArchetypeDefinition)target;
        EditorGUILayout.Space(8);
        EditorGUILayout.LabelField("Derived Preview", EditorStyles.boldLabel);

        using (new EditorGUI.DisabledScope(true))
        {
            DrawIdentitySummary(archetype);
            DrawStatSummary(archetype);
            DrawSkillSlotSummary(archetype);
            DrawProfileSummary(archetype);
            DrawCompiledEffectiveValues(archetype);
        }
    }

    private static void DrawIdentitySummary(UnitArchetypeDefinition archetype)
    {
        EditorGUILayout.LabelField("Localized Taxonomy", EditorStyles.miniBoldLabel);

        var characterName = EditorLocalizedTextResolver.GetCharacterName(null, archetype.Id);
        var archetypeName = EditorLocalizedTextResolver.GetArchetypeName(archetype, archetype.Id);
        var raceName = EditorLocalizedTextResolver.GetRaceName(archetype.Race, archetype.Race != null ? archetype.Race.Id : string.Empty);
        var className = EditorLocalizedTextResolver.GetClassName(archetype.Class, archetype.Class != null ? archetype.Class.Id : string.Empty);
        var roleFamily = EditorLocalizedTextResolver.GetRoleFamilyName(archetype.Class);

        var builder = new StringBuilder();
        builder.AppendLine($"{EditorLocalizedTextResolver.LocalizeUi("ui.battle.axis.character", "캐릭터", "Character")}: {characterName} [{archetype.Id}]");
        builder.AppendLine($"{EditorLocalizedTextResolver.LocalizeUi("ui.battle.axis.archetype", "전투 원형", "Archetype")}: {archetypeName} [{archetype.Id}]");
        builder.AppendLine($"{EditorLocalizedTextResolver.LocalizeUi("ui.battle.axis.race", "종족", "Race")}: {raceName}");
        builder.AppendLine($"{EditorLocalizedTextResolver.LocalizeUi("ui.battle.axis.class", "직업", "Class")}: {className}");
        builder.AppendLine($"{EditorLocalizedTextResolver.LocalizeUi("ui.battle.axis.role", "역할", "Role")}: {EditorLocalizedTextResolver.GetRoleName(null, archetype.RoleTag, archetype.RoleTag)}");
        builder.Append($"{EditorLocalizedTextResolver.LocalizeUi("ui.battle.axis.role_family", "역할군", "Role Family")}: {roleFamily}");
        EditorGUILayout.TextArea(builder.ToString(), EditorStyles.helpBox);
    }

    private static void DrawStatSummary(UnitArchetypeDefinition archetype)
    {
        EditorGUILayout.LabelField("Base Stats (non-default)", EditorStyles.miniBoldLabel);

        var builder = new StringBuilder();
        AppendIfNonDefault(builder, "MaxHealth", archetype.BaseMaxHealth, 20f);
        AppendIfNonDefault(builder, "Armor", archetype.BaseArmor, 2f);
        AppendIfNonDefault(builder, "Resist", archetype.BaseResist, 0f);
        AppendIfNonDefault(builder, "BarrierPower", archetype.BaseBarrierPower, 0f);
        AppendIfNonDefault(builder, "Tenacity", archetype.BaseTenacity, 0f);
        AppendIfNonDefault(builder, "PhysPower", archetype.BasePhysPower, 5f);
        AppendIfNonDefault(builder, "MagPower", archetype.BaseMagPower, 0f);
        AppendIfNonDefault(builder, "AttackSpeed", archetype.BaseAttackSpeed, 3f);
        AppendIfNonDefault(builder, "Attack", archetype.BaseAttack, 5f);
        AppendIfNonDefault(builder, "Defense", archetype.BaseDefense, 2f);
        AppendIfNonDefault(builder, "Speed", archetype.BaseSpeed, 3f);
        AppendIfNonDefault(builder, "HealPower", archetype.BaseHealPower, 0f);
        AppendIfNonDefault(builder, "MoveSpeed", archetype.BaseMoveSpeed, 1.7f);
        AppendIfNonDefault(builder, "AttackRange", archetype.BaseAttackRange, 1.5f);
        AppendIfNonDefault(builder, "MaxEnergy", archetype.BaseMaxEnergy, 100f);
        AppendIfNonDefault(builder, "StartingEnergy", archetype.BaseStartingEnergy, 10f);
        AppendIfNonDefault(builder, "SkillHaste", archetype.BaseSkillHaste, 0f);
        AppendIfNonDefault(builder, "CritChance", archetype.BaseCritChance, 0f);
        AppendIfNonDefault(builder, "CritMultiplier", archetype.BaseCritMultiplier, 0f);
        AppendIfNonDefault(builder, "PhysPen", archetype.BasePhysPen, 0f);
        AppendIfNonDefault(builder, "MagPen", archetype.BaseMagPen, 0f);
        AppendIfNonDefault(builder, "AggroRadius", archetype.BaseAggroRadius, 7f);
        AppendIfNonDefault(builder, "AttackWindup", archetype.BaseAttackWindup, 0.22f);
        AppendIfNonDefault(builder, "CastWindup", archetype.BaseCastWindup, 0.22f);
        AppendIfNonDefault(builder, "ProjectileSpeed", archetype.BaseProjectileSpeed, 0f);
        AppendIfNonDefault(builder, "AttackCooldown", archetype.BaseAttackCooldown, 0.95f);
        AppendIfNonDefault(builder, "TargetSwitchDelay", archetype.BaseTargetSwitchDelay, 0.35f);

        var text = builder.Length > 0 ? builder.ToString().TrimEnd() : "(all defaults)";
        EditorGUILayout.TextArea(text, EditorStyles.helpBox);
    }

    private static void DrawSkillSlotSummary(UnitArchetypeDefinition archetype)
    {
        EditorGUILayout.LabelField("Skill Slots", EditorStyles.miniBoldLabel);
        var builder = new StringBuilder();

        if (archetype.LockedSignatureActiveSkill != null)
            builder.AppendLine($"SignatureActive: {archetype.LockedSignatureActiveSkill.Id}");
        if (archetype.LockedSignaturePassiveSkill != null)
            builder.AppendLine($"SignaturePassive: {archetype.LockedSignaturePassiveSkill.Id}");

        builder.AppendLine($"Skills: {archetype.Skills.Count}");
        builder.AppendLine($"FlexUtility: {archetype.FlexUtilitySkillPool.Count}  FlexSupport: {archetype.FlexSupportSkillPool.Count}");
        builder.AppendLine($"RecruitActive: {archetype.RecruitFlexActivePool.Count}  RecruitPassive: {archetype.RecruitFlexPassivePool.Count}");
        builder.Append($"Tactics: {archetype.TacticPreset.Count}  BannedPairings: {archetype.RecruitBannedPairings.Count}");

        EditorGUILayout.TextArea(builder.ToString(), EditorStyles.helpBox);
    }

    private static void DrawProfileSummary(UnitArchetypeDefinition archetype)
    {
        EditorGUILayout.LabelField("Profiles", EditorStyles.miniBoldLabel);
        var builder = new StringBuilder();

        builder.Append($"Anchor: {archetype.DefaultAnchor}  Posture: {archetype.PreferredTeamPosture}");
        builder.AppendLine($"  Role: {archetype.RoleTag}");

        if (archetype.FootprintProfile != null)
        {
            var fp = archetype.FootprintProfile;
            builder.AppendLine($"Footprint: nav={fp.NavigationRadius} sep={fp.SeparationRadius} reach={fp.CombatReach} body={fp.BodySizeCategory}");
        }

        if (archetype.BehaviorProfile != null)
        {
            var bp = archetype.BehaviorProfile;
            builder.AppendLine($"Behavior: {bp.FormationLine} {bp.RangeDiscipline} opp={bp.Opportunism} disc={bp.Discipline}");
        }

        if (archetype.MobilityProfile != null)
        {
            var mp = archetype.MobilityProfile;
            builder.Append($"Mobility: {mp.Style} {mp.Purpose} dist={mp.Distance} cd={mp.Cooldown}");
        }

        EditorGUILayout.TextArea(builder.ToString().TrimEnd(), EditorStyles.helpBox);
    }

    private static void DrawCompiledEffectiveValues(UnitArchetypeDefinition archetype)
    {
        EditorGUILayout.LabelField("Compiled Effective Values", EditorStyles.miniBoldLabel);
        var builder = new StringBuilder();

        var classId = archetype.Class != null ? archetype.Class.Id : "";
        var behavior = archetype.BehaviorProfile != null
            ? BuildBehaviorFromDefinition(archetype.BehaviorProfile)
            : null;
        var resolved = CombatProfileDefaults.ResolveBehavior(behavior, classId);

        builder.AppendLine($"Formation: {resolved.FormationLine}  Discipline: {resolved.RangeDiscipline}");
        builder.AppendLine($"Range: {resolved.PreferredRangeMin:0.##}–{resolved.PreferredRangeMax:0.##}  Approach: {resolved.ApproachBuffer:0.##}  Retreat: {resolved.RetreatBuffer:0.##}");
        builder.AppendLine($"Guard Radius: {resolved.FrontlineGuardRadius:0.#}  Leash: {resolved.ChaseLeashMeters:0.#}");
        builder.AppendLine($"Opportunism: {resolved.Opportunism:0.##}  Discipline: {resolved.Discipline:0.##}");
        builder.AppendLine($"Dodge: {resolved.DodgeChance:P0}  Block: {resolved.BlockChance:P0} ({resolved.BlockMitigation:P0} mit)  Stability: {resolved.Stability:0.##}");

        if (behavior != null)
            builder.Append("Source: authored profile");
        else
            builder.Append($"Source: class default ({(string.IsNullOrEmpty(classId) ? "generic" : classId)})");

        EditorGUILayout.TextArea(builder.ToString(), EditorStyles.helpBox);
    }

    private static BehaviorProfile BuildBehaviorFromDefinition(BehaviorProfileDefinition def)
    {
        return new BehaviorProfile(
            def.ReevaluationInterval, def.RangeHysteresis, def.RetreatBias,
            def.MaintainRangeBias, def.Opportunism, def.Discipline,
            def.DodgeChance, def.BlockChance, def.BlockMitigation, def.Stability,
            def.BlockCooldownSeconds, def.FormationLine, def.RangeDiscipline,
            def.PreferredRangeMin, def.PreferredRangeMax,
            def.ApproachBuffer, def.RetreatBuffer, def.ChaseLeashMeters,
            def.RetreatAtHpPercent, def.FrontlineGuardRadius);
    }

    private static void AppendIfNonDefault(StringBuilder builder, string label, float value, float defaultValue)
    {
        if (Mathf.Abs(value - defaultValue) > 0.001f)
        {
            builder.AppendLine($"{label}: {value}");
        }
    }
}
