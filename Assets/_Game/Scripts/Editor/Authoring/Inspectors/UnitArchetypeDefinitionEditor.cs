using System.Text;
using SM.Content.Definitions;
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
            DrawStatSummary(archetype);
            DrawSkillSlotSummary(archetype);
            DrawProfileSummary(archetype);
        }
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

    private static void AppendIfNonDefault(StringBuilder builder, string label, float value, float defaultValue)
    {
        if (Mathf.Abs(value - defaultValue) > 0.001f)
        {
            builder.AppendLine($"{label}: {value}");
        }
    }
}
