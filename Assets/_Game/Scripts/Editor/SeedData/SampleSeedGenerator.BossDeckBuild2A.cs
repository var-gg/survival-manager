using System;
using System.Collections.Generic;
using System.Linq;
using SM.Content.Definitions;
using SM.Core.Content;
using SM.Core.Contracts;
using SM.Unity;
using UnityEditor;
using UnityEngine;

namespace SM.Editor.SeedData;

public static partial class SampleSeedGenerator
{
    [MenuItem("SM/Internal/Content/Sync Boss Deck Build 2A (Ch1-Ch3)")]
    public static void SyncBossDeckBuild2A()
    {
        EnsureFolders();
        SyncBossDeckBuild2AAssets();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
        Debug.Log("SM boss deck Build 2A synchronized for ch1-ch3 without a full reseed.");
    }

    private static void SyncBossDeckBuild2AAssets()
    {
        var executionShot = CreateBossSkill(
            "skill_boss_low_hp_execution_shot",
            "skill_precision_shot",
            "빈사 집행 사격",
            "Low-HP Execution Shot",
            "가장 체력이 낮은 적을 원거리에서 겨눠 빈사 처형 태그의 피해 보너스를 환전한다.",
            "Targets the lowest-health enemy at range so the low-HP execute tag can convert an artillery opening.",
            skill =>
            {
                skill.Kind = SkillKindValue.Strike;
                skill.SlotKind = SkillSlotKindValue.UtilityActive;
                skill.DamageType = DamageTypeValue.Physical;
                skill.Delivery = SkillDeliveryValue.Projectile;
                skill.TargetRule = SkillTargetRuleValue.LowestHpEnemy;
                skill.Power = 6f;
                skill.PowerFlat = 6f;
                skill.PhysCoeff = 0.75f;
                skill.MagCoeff = 0f;
                skill.HealCoeff = 0f;
                skill.HealthCoeff = 0f;
                skill.Range = 6.2f;
                skill.RangeMin = 0f;
                skill.RangeMax = 6.2f;
                skill.Radius = 0f;
                skill.AreaEffectFamily = AreaEffectFamilyValue.SingleTarget;
                skill.PunishCluster = false;
                skill.CanCrit = false;
                skill.BaseCooldownSeconds = 2.8f;
                skill.CastWindupSeconds = 0.25f;
                skill.TargetRuleData = EnemyTargetRule(TargetSelector.LowestHpPercentEnemy, 6.2f);
                skill.AppliedStatuses = new List<StatusApplicationRule>();
            });

        var markSentence = CreateBossSkill(
            "skill_boss_tithe_mark_sentence",
            "skill_raider_core",
            "십일조 지목 선고",
            "Tithe Mark Sentence",
            "가장 체력이 낮은 적에게 표식을 내려 집행 화력을 한 점으로 모은다.",
            "Marks the lowest-health enemy so the execution line converges on one deterministic target.",
            skill =>
            {
                skill.Kind = SkillKindValue.Debuff;
                skill.SlotKind = SkillSlotKindValue.UtilityActive;
                skill.DamageType = DamageTypeValue.Magical;
                skill.Delivery = SkillDeliveryValue.Projectile;
                skill.TargetRule = SkillTargetRuleValue.LowestHpEnemy;
                skill.Power = 0f;
                skill.PowerFlat = 0f;
                skill.PhysCoeff = 0f;
                skill.MagCoeff = 0f;
                skill.HealCoeff = 0f;
                skill.HealthCoeff = 0f;
                skill.Range = 6.5f;
                skill.RangeMin = 0f;
                skill.RangeMax = 6.5f;
                skill.Radius = 0f;
                skill.AreaEffectFamily = AreaEffectFamilyValue.SingleTarget;
                skill.PunishCluster = false;
                skill.CanCrit = false;
                skill.BaseCooldownSeconds = 3.5f;
                skill.CastWindupSeconds = 0.2f;
                skill.TargetRuleData = EnemyTargetRule(TargetSelector.LowestHpPercentEnemy, 6.5f);
                skill.AppliedStatuses = new List<StatusApplicationRule>
                {
                    MakeStatus("boss_tithe_marked", "marked", 4f, 0f),
                };
            });

        var sustainMend = CreateBossSkill(
            "skill_boss_sustain_mend",
            "skill_minor_heal",
            "성가대 회복",
            "Choir Mend",
            "가장 크게 다친 아군을 회복해 전열 소모전을 연장한다.",
            "Heals the most injured ally to sustain the authored frontline wall.",
            skill =>
            {
                skill.Kind = SkillKindValue.Heal;
                skill.SlotKind = SkillSlotKindValue.UtilityActive;
                skill.DamageType = DamageTypeValue.Healing;
                skill.Delivery = SkillDeliveryValue.Ranged;
                skill.TargetRule = SkillTargetRuleValue.LowestHpAlly;
                skill.Power = 4f;
                skill.PowerFlat = 4f;
                skill.PhysCoeff = 0f;
                skill.MagCoeff = 0.8f;
                skill.HealCoeff = 1f;
                skill.HealthCoeff = 0f;
                skill.Range = 6.5f;
                skill.RangeMin = 0f;
                skill.RangeMax = 6.5f;
                skill.Radius = 0f;
                skill.AreaEffectFamily = AreaEffectFamilyValue.SingleTarget;
                skill.PunishCluster = false;
                skill.CanCrit = false;
                skill.BaseCooldownSeconds = 3.2f;
                skill.CastWindupSeconds = 0.25f;
                skill.TargetRuleData = AlliedTargetRule(6.5f);
                skill.AppliedStatuses = new List<StatusApplicationRule>();
            });

        var attritionHex = CreateBossSkill(
            "skill_boss_ruined_attrition_hex",
            "skill_hexer_core",
            "기록 소각",
            "Archive Scorch",
            "후열에 짧은 연소를 반복해 기록 성가대 전투가 제한 시간 전에 반드시 결론 나게 한다.",
            "Repeats a short backline burn so the Archive Choir fight reaches a decision before the battle cap.",
            skill => ConfigureBurnClock(skill, power: 4f, range: 12f, radius: 0.85f, cooldown: 5.6f, windup: 3.35f, duration: 0.8f));

        var orchardBurn = CreateBossSkill(
            "skill_boss_orchard_burn_pulse",
            "skill_shaman_core",
            "과수원 연소 맥동",
            "Orchard Burn Pulse",
            "후열에 연소 맥동을 누적해 과수원의 지속 피해 시계를 돌린다.",
            "Stacks short burn pulses on the backline to drive the orchard attrition clock.",
            skill => ConfigureBurnClock(skill, power: 4f, range: 12f, radius: 1f, cooldown: 5.6f, windup: 3.35f, duration: 0.9f));

        CreateNumericTunedBossArchetype(
            "pale_executor_tithe_boss",
            "pale_executor",
            "십일조 집행리",
            "Tithe Executor",
            archetype => archetype.BaseMaxHealth *= 1.10f);

        CreateBossArchetype(
            "pale_executor_nondiving_boss",
            "pale_executor",
            "법정 집행리",
            "Court Executor",
            executionShot,
            DeploymentAnchorValue.FrontBottom,
            TeamPostureTypeValue.HoldLine,
            "bruiser",
            ActiveThenBasic(executionShot, TacticConditionTypeValue.EnemyInRange, TargetSelectorTypeValue.LowestHpEnemy));
        CreateBossArchetype(
            "bastion_penitent_tithe_boss",
            "bastion_penitent",
            "십일조 집전자",
            "Tithe Collector",
            markSentence,
            DeploymentAnchorValue.FrontCenter,
            TeamPostureTypeValue.StandardAdvance,
            "anchor",
            ActiveThenBasic(markSentence, TacticConditionTypeValue.EnemyInRange, TargetSelectorTypeValue.LowestHpEnemy));
        CreateBossArchetype(
            "priest_sustain_boss",
            "priest",
            "성가대 사제",
            "Choir Priest",
            sustainMend,
            DeploymentAnchorValue.BackTop,
            TeamPostureTypeValue.HoldLine,
            "support",
            SustainThenDefend(sustainMend));
        CreateBossArchetype(
            "mirror_cantor_sustain_boss",
            "mirror_cantor",
            "지속 성가대",
            "Sustain Cantor",
            sustainMend,
            DeploymentAnchorValue.BackBottom,
            TeamPostureTypeValue.ProtectCarry,
            "support",
            SustainThenDefend(sustainMend));
        CreateBossArchetype(
            "hexer_attrition_boss",
            "hexer",
            "기록 소각술사",
            "Archive Scorcher",
            attritionHex,
            DeploymentAnchorValue.BackBottom,
            TeamPostureTypeValue.HoldLine,
            "artillery",
            ActiveThenBasic(attritionHex, TacticConditionTypeValue.EnemyInRange, TargetSelectorTypeValue.LowestHpEnemy));
        CreateBossArchetype(
            "shaman_burn_boss",
            "shaman",
            "과수원 연소술사",
            "Orchard Burn Shaman",
            orchardBurn,
            DeploymentAnchorValue.BackTop,
            TeamPostureTypeValue.ProtectCarry,
            "artillery",
            ActiveThenBasic(orchardBurn, TacticConditionTypeValue.EnemyInRange, TargetSelectorTypeValue.LowestHpEnemy));

        ConfigureSunkenBombardment();
        RebuildAuthoredBossDeckAssets();
        PreserveCommittedEncounterFamilyDistribution();
        PatchElitePreviews();
        SyncBossDeckP09AppearancePresets();
        SeedBossDeckLocalization();
    }

    private static SkillDefinitionAsset CreateBossSkill(
        string id,
        string sourceId,
        string koName,
        string enName,
        string koDescription,
        string enDescription,
        Action<SkillDefinitionAsset> configure)
    {
        var source = RequireBossDeckDefinition<SkillDefinitionAsset>($"{ResourcesRoot}/Skills/{sourceId}.asset");
        return CreateAsset<SkillDefinitionAsset>($"{ResourcesRoot}/Skills/{id}.asset", asset =>
        {
            EditorUtility.CopySerialized(source, asset);
            asset.name = id;
            asset.Id = id;
            asset.NameKey = ContentLocalizationTables.BuildSkillNameKey(id);
            asset.DescriptionKey = ContentLocalizationTables.BuildSkillDescriptionKey(id);
            asset.LearnSource = SkillLearnSourceValue.EnemyOnly;
            asset.ActivationModel = ActivationModel.Cooldown;
            asset.Lane = ActionLane.Primary;
            asset.LockRule = ActionLockRule.HardCommit;
            asset.ManaCost = 0f;
            asset.ResourceCost = -1f;
            asset.CooldownSeconds = -1f;
            asset.RecoverySeconds = -1f;
            asset.InterruptRefundScalar = 0.5f;
            asset.EffectFamilyId = id;
            asset.MutuallyExclusiveGroupId = string.Empty;
            asset.RecruitNativeTags = new List<StableTagDefinition>();
            asset.RecruitPlanTags = new List<StableTagDefinition>();
            asset.RecruitScoutTags = new List<StableTagDefinition>();
            asset.CompileTags = new List<StableTagDefinition>();
            asset.RuleModifierTags = new List<StableTagDefinition>();
            asset.SupportAllowedTags = new List<StableTagDefinition>();
            asset.SupportBlockedTags = new List<StableTagDefinition>();
            asset.RequiredWeaponTags = new List<StableTagDefinition>();
            asset.RequiredClassTags = new List<StableTagDefinition>();
            asset.TriggeredEffects = new List<TriggeredEffectSpec>();
            asset.CleanseProfileId = string.Empty;
            configure(asset);
            ApplyLoopCSkillGovernance(asset);
            UpsertStringEntry(ContentLocalizationTables.Skills, asset.NameKey, koName, enName);
            UpsertStringEntry(ContentLocalizationTables.Skills, asset.DescriptionKey, koDescription, enDescription);
        });
    }

    private static UnitArchetypeDefinition CreateBossArchetype(
        string id,
        string sourceId,
        string koName,
        string enName,
        SkillDefinitionAsset skill,
        DeploymentAnchorValue anchor,
        TeamPostureTypeValue posture,
        string roleTag,
        List<TacticPresetEntry> tactics)
    {
        var source = RequireBossDeckDefinition<UnitArchetypeDefinition>($"{ResourcesRoot}/Archetypes/archetype_{sourceId}.asset");
        return CreateAsset<UnitArchetypeDefinition>($"{ResourcesRoot}/Archetypes/archetype_{id}.asset", asset =>
        {
            EditorUtility.CopySerialized(source, asset);
            asset.name = $"archetype_{id}";
            asset.Id = id;
            asset.NameKey = ContentLocalizationTables.BuildArchetypeNameKey(id);
            asset.ScopeKind = ArchetypeScopeValue.Specialist;
            asset.Skills = new List<SkillDefinitionAsset> { skill };
            asset.TacticPreset = tactics;
            asset.DefaultAnchor = anchor;
            asset.PreferredTeamPosture = posture;
            asset.RoleTag = roleTag;
            asset.IsRecruitable = false;
            asset.IsSummonOnly = false;
            asset.IsEventOnly = false;
            asset.IsBossOnly = true;
            asset.IsUnreleased = false;
            asset.IsTestOnly = false;
            asset.LockedSignatureActiveSkill = null;
            asset.LockedSignaturePassiveSkill = null;
            asset.FlexUtilitySkillPool = new List<SkillDefinitionAsset> { skill };
            asset.FlexSupportSkillPool = new List<SkillDefinitionAsset>();
            asset.RecruitFlexActivePool = new List<SkillDefinitionAsset>();
            asset.RecruitFlexPassivePool = new List<SkillDefinitionAsset>();
            asset.Loadout = new UnitLoadoutDefinition { FlexActive = skill };
            ApplyLoopCArchetypeGovernance(asset);
            UpsertStringEntry(ContentLocalizationTables.Archetypes, asset.NameKey, koName, enName);
        });
    }

    private static UnitArchetypeDefinition CreateNumericTunedBossArchetype(
        string id,
        string sourceId,
        string koName,
        string enName,
        Action<UnitArchetypeDefinition> tune)
    {
        var source = RequireBossDeckDefinition<UnitArchetypeDefinition>($"{ResourcesRoot}/Archetypes/archetype_{sourceId}.asset");
        return CreateAsset<UnitArchetypeDefinition>($"{ResourcesRoot}/Archetypes/archetype_{id}.asset", asset =>
        {
            EditorUtility.CopySerialized(source, asset);
            asset.name = $"archetype_{id}";
            asset.Id = id;
            asset.NameKey = ContentLocalizationTables.BuildArchetypeNameKey(id);
            asset.ScopeKind = ArchetypeScopeValue.Specialist;
            asset.IsRecruitable = false;
            asset.IsSummonOnly = false;
            asset.IsEventOnly = false;
            asset.IsBossOnly = true;
            asset.IsUnreleased = false;
            asset.IsTestOnly = false;
            tune(asset);
            ApplyLoopCArchetypeGovernance(asset);
            UpsertStringEntry(ContentLocalizationTables.Archetypes, asset.NameKey, koName, enName);
        });
    }

    private static void ConfigureBurnClock(
        SkillDefinitionAsset skill,
        float power,
        float range,
        float radius,
        float cooldown,
        float windup,
        float duration)
    {
        skill.Kind = SkillKindValue.Strike;
        skill.SlotKind = SkillSlotKindValue.UtilityActive;
        skill.DamageType = DamageTypeValue.Magical;
        skill.Delivery = SkillDeliveryValue.Projectile;
        skill.TargetRule = SkillTargetRuleValue.LowestHpEnemy;
        skill.Power = power;
        skill.PowerFlat = power;
        skill.PhysCoeff = 0f;
        skill.MagCoeff = 0.75f;
        skill.HealCoeff = 0f;
        skill.HealthCoeff = 0f;
        skill.Range = range;
        skill.RangeMin = 0f;
        skill.RangeMax = range;
        skill.Radius = radius;
        skill.AreaEffectFamily = AreaEffectFamilyValue.GroundAoe;
        skill.PunishCluster = false;
        skill.CanCrit = false;
        skill.BaseCooldownSeconds = cooldown;
        skill.CastWindupSeconds = windup;
        skill.TargetRuleData = EnemyTargetRule(TargetSelector.LowestHpPercentEnemy, range);
        skill.TargetRuleData.ClusterRadius = radius;
        skill.AppliedStatuses = new List<StatusApplicationRule>
        {
            MakeStatus($"{skill.Id}_burn", "burn", duration, 1f),
        };
    }

    private static TargetRule EnemyTargetRule(TargetSelector selector, float range)
        => new()
        {
            Domain = TargetDomain.EnemyUnit,
            PrimarySelector = selector,
            FallbackPolicy = TargetFallbackPolicy.LowestCurrentHpEnemy,
            Filters = TargetFilterFlags.InRange | TargetFilterFlags.ExcludeUntargetable,
            ReevaluateIntervalSeconds = 0.25f,
            MinimumCommitSeconds = 0.75f,
            MaxAcquireRange = range,
            PreferredMinTargets = 1,
            ClusterRadius = 1f,
            LockTargetAtCastStart = true,
            RetargetLockMode = RetargetLockMode.UntilCastComplete,
        };

    private static TargetRule AlliedTargetRule(float range)
        => new()
        {
            Domain = TargetDomain.AlliedUnit,
            PrimarySelector = TargetSelector.LowestHpPercentAlly,
            FallbackPolicy = TargetFallbackPolicy.Self,
            Filters = TargetFilterFlags.InRange | TargetFilterFlags.ExcludeUntargetable | TargetFilterFlags.ExcludeFullHealthAllies,
            ReevaluateIntervalSeconds = 0.25f,
            MinimumCommitSeconds = 0.5f,
            MaxAcquireRange = range,
            PreferredMinTargets = 1,
            ClusterRadius = 1f,
            LockTargetAtCastStart = true,
            RetargetLockMode = RetargetLockMode.UntilCastComplete,
        };

    private static List<TacticPresetEntry> ActiveThenBasic(
        SkillDefinitionAsset skill,
        TacticConditionTypeValue condition,
        TargetSelectorTypeValue selector)
        => new()
        {
            new TacticPresetEntry
            {
                Priority = 0,
                ConditionType = condition,
                Threshold = 0f,
                ActionType = BattleActionTypeValue.ActiveSkill,
                TargetSelector = selector,
                Skill = skill,
            },
            new TacticPresetEntry
            {
                Priority = 1,
                ConditionType = TacticConditionTypeValue.LowestHpEnemy,
                Threshold = 0f,
                ActionType = BattleActionTypeValue.BasicAttack,
                TargetSelector = TargetSelectorTypeValue.LowestHpEnemy,
            },
        };

    private static List<TacticPresetEntry> SustainThenDefend(SkillDefinitionAsset skill)
        => new()
        {
            new TacticPresetEntry
            {
                Priority = 0,
                ConditionType = TacticConditionTypeValue.AllyHpBelow,
                Threshold = 0.85f,
                ActionType = BattleActionTypeValue.ActiveSkill,
                TargetSelector = TargetSelectorTypeValue.LowestHpAlly,
                Skill = skill,
            },
            new TacticPresetEntry
            {
                Priority = 1,
                ConditionType = TacticConditionTypeValue.Fallback,
                Threshold = 0f,
                ActionType = BattleActionTypeValue.WaitDefend,
                TargetSelector = TargetSelectorTypeValue.Self,
            },
        };

    private static void ConfigureSunkenBombardment()
    {
        var skill = RequireBossDeckDefinition<SkillDefinitionAsset>($"{ResourcesRoot}/Skills/skill_sunken_anticluster_bombardment.asset");
        skill.Range = 6f;
        skill.RangeMax = 6f;
        skill.TargetRuleData.MaxAcquireRange = 6f;
        skill.TargetRuleData.PreferredMinTargets = 3;
        skill.CastWindupSeconds = 3.75f;
        UpsertStringEntry(ContentLocalizationTables.Skills, skill.NameKey, "밀집 처벌 폭격", "Anti-Cluster Bombardment");
        UpsertStringEntry(
            ContentLocalizationTables.Skills,
            skill.DescriptionKey,
            "밀집한 후열을 겨냥해 같은 피해의 장판 폭격을 가한다. 대열을 벌리면 처형 창을 차단할 수 있다.",
            "Bombards a clustered backline for unchanged raw damage; spreading denies the execution window.");
        EditorUtility.SetDirty(skill);

        var archetype = RequireBossDeckDefinition<UnitArchetypeDefinition>($"{ResourcesRoot}/Archetypes/archetype_sunken_adjudicator_boss.asset");
        archetype.DefaultAnchor = DeploymentAnchorValue.FrontCenter;
        archetype.PreferredTeamPosture = TeamPostureTypeValue.HoldLine;
        archetype.TacticPreset = ActiveThenBasic(
            skill,
            TacticConditionTypeValue.EnemyInRange,
            TargetSelectorTypeValue.LowestHpEnemy);
        archetype.Skills = new List<SkillDefinitionAsset> { skill };
        archetype.FlexUtilitySkillPool = new List<SkillDefinitionAsset> { skill };
        archetype.Loadout ??= new UnitLoadoutDefinition();
        archetype.Loadout.FlexActive = skill;
        UpsertStringEntry(ContentLocalizationTables.Archetypes, archetype.NameKey, "침몰한 재정관", "Sunken Adjudicator");
        EditorUtility.SetDirty(archetype);
    }

    private static void RebuildAuthoredBossDeckAssets()
    {
        var siteIds = new HashSet<string>(StringComparer.Ordinal)
        {
            "site_ashen_gate",
            "site_sunken_bastion",
            "site_tithe_road",
            "site_ruined_crypts",
            "site_bone_orchard",
        };
        foreach (var site in GetCampaignSiteSeeds().Where(site => siteIds.Contains(site.SiteId)))
        {
            CreateBossOverlay(site);
            CreateBossSquad(
                $"{site.SiteId}_boss_1_squad",
                site.FactionId,
                site.BossCaptain,
                site.BossEscorts,
                site.BossPosture,
                site.BossCaptainAnchor,
                site.BossEscortOneAnchor,
                site.BossEscortTwoAnchor,
                site.BossEscortThreeAnchor,
                ThreatTierValue.Tier3,
                3);
        }
    }

    private static void PatchElitePreviews()
    {
        var sunken = RequireBossDeckDefinition<EnemySquadTemplateDefinition>($"{ResourcesRoot}/EnemySquads/site_sunken_bastion_elite_1_squad.asset");
        sunken.EnemyPosture = TeamPostureTypeValue.HoldLine;
        EditorUtility.SetDirty(sunken);

        var tithe = RequireBossDeckDefinition<EnemySquadTemplateDefinition>($"{ResourcesRoot}/EnemySquads/site_tithe_road_elite_1_squad.asset");
        var titheExecutor = tithe.Members.First(member => string.Equals(member.ArchetypeId, "pale_executor", StringComparison.Ordinal));
        titheExecutor.RuleModifierTags = titheExecutor.RuleModifierTags
            .Append("duelist_dive_commit")
            .Distinct(StringComparer.Ordinal)
            .ToList();
        EditorUtility.SetDirty(tithe);

        var ruined = RequireBossDeckDefinition<EnemySquadTemplateDefinition>($"{ResourcesRoot}/EnemySquads/site_ruined_crypts_elite_1_squad.asset");
        var ruinedPriest = ruined.Members.First(member => string.Equals(member.ArchetypeId, "priest", StringComparison.Ordinal));
        ruinedPriest.EquipmentBudget = 2f;
        ruinedPriest.EquipmentItemBaseId = "item_priest_focus";
        ruinedPriest.EquipmentAffixIds = new List<string> { "affix_mender" };
        EditorUtility.SetDirty(ruined);

        var orchard = RequireBossDeckDefinition<EnemySquadTemplateDefinition>($"{ResourcesRoot}/EnemySquads/site_bone_orchard_elite_1_squad.asset");
        var orchardShaman = orchard.Members.First(member =>
            string.Equals(member.ArchetypeId, "shaman", StringComparison.Ordinal)
            || string.Equals(member.ArchetypeId, "shaman_burn_boss", StringComparison.Ordinal));
        orchardShaman.ArchetypeId = "shaman_burn_boss";
        EditorUtility.SetDirty(orchard);

        SeedElitePreviewLocalization("site_sunken_bastion_elite_1", "침몰한 전열이 밀집 대형을 벌하도록 기다린다.", "A held line previews punishment for clustered formations.");
        SeedElitePreviewLocalization("site_tithe_road_elite_1", "집행리가 약한 측면으로 뛰어들어 지목 처벌을 예고한다.", "An executioner dives the marked weak side.");
        SeedElitePreviewLocalization("site_ruined_crypts_elite_1", "회복 접두가 붙은 사제와 수호자가 성가대의 회복벽을 예고한다.", "A mender-backed priest and guardian preview the sustain wall.");
        SeedElitePreviewLocalization("site_bone_orchard_elite_1", "주술승의 연소 맥동이 과수원의 소모전 시계를 예고한다.", "A shaman burn pulse previews the orchard attrition clock.");
    }

    private static void SyncBossDeckP09AppearancePresets()
    {
        var aliases = new[]
        {
            (Target: "extra_sunken_bastion_adjudicator", Source: "bastion_penitent"),
            (Target: "extra_tithe_executioner_proxy", Source: "pale_executor"),
            (Target: "extra_sunken_adjudicator_lieutenant", Source: "priest"),
            (Target: "extra_tithe_chain_cantor", Source: "mirror_cantor"),
            (Target: "extra_pale_memorial_keeper", Source: "priest"),
            (Target: "extra_black_roll_bailiff", Source: "hexer"),
            (Target: "extra_lattice_root_usher", Source: "shaman"),
            (Target: "extra_lattice_echo_caretaker", Source: "mirror_cantor"),
        };

        foreach (var alias in aliases)
        {
            var sourcePath = $"{BattleP09AppearancePreset.AssetFolder}/p09_appearance_{alias.Source}.asset";
            var targetPath = $"{BattleP09AppearancePreset.AssetFolder}/p09_appearance_{alias.Target}.asset";
            var source = RequireBossDeckDefinition<BattleP09AppearancePreset>(sourcePath);
            CreateAsset<BattleP09AppearancePreset>(targetPath, asset =>
            {
                EditorUtility.CopySerialized(source, asset);
                asset.name = $"p09_appearance_{alias.Target}";
                asset.ConfigureIdentity(alias.Target, source.DisplayName, source.Catalog);
            });
        }
    }

    private static void PreserveCommittedEncounterFamilyDistribution()
    {
        var families = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["site_ashen_gate_boss_1"] = "encounter_family_sustain_grind",
            ["site_sunken_bastion_boss_1"] = "encounter_family_protect_carry",
            ["site_tithe_road_boss_1"] = "encounter_family_sustain_grind",
            ["site_ruined_crypts_boss_1"] = "encounter_family_summon_pressure",
            ["site_bone_orchard_boss_1"] = "encounter_family_control_cleanse",
        };
        foreach (var pair in families)
        {
            var encounter = RequireBossDeckDefinition<EncounterDefinition>($"{ResourcesRoot}/Encounters/{pair.Key}.asset");
            encounter.RewardDropTags = encounter.RewardDropTags
                .Where(tag => !tag.StartsWith("encounter_family_", StringComparison.Ordinal))
                .Append(pair.Value)
                .Distinct(StringComparer.Ordinal)
                .ToList();
            EditorUtility.SetDirty(encounter);
        }
    }

    private static void SeedElitePreviewLocalization(string encounterId, string koDescription, string enDescription)
    {
        var encounter = RequireBossDeckDefinition<EncounterDefinition>($"{ResourcesRoot}/Encounters/{encounterId}.asset");
        UpsertStringEntry(ContentLocalizationTables.Encounters, encounter.DescriptionKey, koDescription, enDescription);
    }

    private static void SeedBossDeckLocalization()
    {
        var entries = new[]
        {
            ("site_ashen_gate", "관문 수문장", "Gate Warden", "두 전열과 사냥꾼이 보스전의 기본 벽 문법을 가르친다.", "Two front bodies and a hunter teach the campaign's basic boss-wall grammar."),
            ("site_sunken_bastion", "침몰한 재정관", "Sunken Adjudicator", "밀집한 후열을 포격한 뒤 비다이브 집행리가 빈사 표적을 처형한다.", "Artillery punishes a clustered backline, then a non-diving executor finishes low-health targets."),
            ("site_tithe_road", "십일조 집전자", "Tithe Collector", "집전자의 지목이 집중 사격과 집행리의 다이브를 한 표적에 모은다.", "The collector's mark converges focus fire and an executor dive on one target."),
            ("site_ruined_crypts", "기록 성가대", "Archive Choir", "회복 전열이 버티는 동안 기록 소각이 전투를 결론으로 몰아간다.", "A healing wall endures while archive scorch drives the fight to a decision."),
            ("site_bone_orchard", "과수원 감시자", "Orchard Warden", "되받아치는 전열과 연소 맥동이 정화와 엔진 절단을 시험한다.", "A retaliating frontline and burn pulses test cleansing and engine removal."),
        };
        foreach (var entry in entries)
        {
            var encounter = RequireBossDeckDefinition<EncounterDefinition>($"{ResourcesRoot}/Encounters/{entry.Item1}_boss_1.asset");
            var squad = RequireBossDeckDefinition<EnemySquadTemplateDefinition>($"{ResourcesRoot}/EnemySquads/{entry.Item1}_boss_1_squad.asset");
            var overlayId = entry.Item1.Substring("site_".Length);
            var overlay = RequireBossDeckDefinition<BossOverlayDefinition>($"{ResourcesRoot}/BossOverlays/boss_overlay_{overlayId}.asset");
            UpsertStringEntry(ContentLocalizationTables.Encounters, encounter.NameKey, entry.Item2, entry.Item3);
            UpsertStringEntry(ContentLocalizationTables.Encounters, encounter.DescriptionKey, entry.Item4, entry.Item5);
            UpsertStringEntry(ContentLocalizationTables.Encounters, squad.NameKey, entry.Item2, entry.Item3);
            UpsertStringEntry(ContentLocalizationTables.Encounters, squad.DescriptionKey, entry.Item4, entry.Item5);
            UpsertStringEntry(ContentLocalizationTables.Encounters, overlay.NameKey, entry.Item2, entry.Item3);
            UpsertStringEntry(ContentLocalizationTables.Encounters, overlay.DescriptionKey, entry.Item4, entry.Item5);
        }
    }

    private static T RequireBossDeckDefinition<T>(string path) where T : ScriptableObject
    {
        var asset = LoadDefinition<T>(path);
        return asset != null
            ? asset
            : throw new InvalidOperationException($"Boss deck Build 2A requires an authored {typeof(T).Name} at {path}.");
    }
}
