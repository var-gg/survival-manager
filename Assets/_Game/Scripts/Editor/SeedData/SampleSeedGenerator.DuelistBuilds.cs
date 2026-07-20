using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using SM.Content.Definitions;
using SM.Core.Content;
using SM.Core.Contracts;
using SM.Core.Stats;
using UnityEditor;
using UnityEngine;

namespace SM.Editor.SeedData;

/// <summary>
/// Duelist build content is kept apart from the broad sample seed so the 24-node board can be reauthored and
/// synchronized without touching unrelated authored content.
/// </summary>
public static partial class SampleSeedGenerator
{
    private const string DuelistBoardId = "board_duelist";

    // Preserve the established board reference order byte-for-byte: the six late safe-target nodes were appended
    // after the original 18-node board and must not be silently reordered by the targeted seed.
    private static readonly string[] DuelistBoardNodeOrder = new[]
    {
        "small_01", "small_02", "small_03", "small_04", "small_05", "small_06", "small_07",
        "small_08", "small_09", "small_10", "small_11", "small_12",
        "notable_01", "notable_02", "notable_03", "notable_04", "notable_05", "keystone_01",
        "small_13", "small_14", "notable_06", "notable_07", "notable_08", "keystone_02",
    }.Select(suffix => $"passive_duelist_{suffix}").ToArray();

    private static readonly (string Id, string EnName, string KoName)[] DuelistBuildStableTags =
    {
        ("duelist_dive_commit", "Duelist Dive Commit", "결투가 다이브 헌신"),
        ("duelist_hold_bruiser", "Duelist Hold Bruiser", "결투가 전선 브루저"),
        ("duelist_peel", "Duelist Peel", "결투가 요격"),
        ("execute_low_hp", "Low-HP Execute", "빈사 처형"),
        ("dive_assassin_keystone", "Dive Assassin Keystone", "다이브 암살 키스톤"),
        ("tag_duelist_build_gate", "Duelist Build Gate", "결투가 빌드 게이트"),
    };

    [MenuItem("SM/Internal/Content/Sync Duelist Build Content")]
    public static void SyncDuelistBuildContent()
    {
        EnsureFolders();
        EnsureDuelistBuildStableTags();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);

        var stableTags = LoadDefinitionsById<StableTagDefinition>($"{ResourcesRoot}/StableTags");
        CreateDuelistBuildContent(stableTags);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
        NormalizeEmptyDuelistGrantedSkillYaml();
        Debug.Log("[SampleSeedGenerator] synchronized duelist build content: 24 nodes, 6 tags, 2 skills.");
    }

    private static void EnsureDuelistBuildStableTags()
    {
        foreach (var definition in DuelistBuildStableTags)
        {
            CreateAsset<StableTagDefinition>(
                $"{ResourcesRoot}/StableTags/tag_{definition.Id}.asset",
                asset =>
                {
                    asset.Id = definition.Id;
                    asset.NameKey = $"content.tag.{ContentLocalizationTables.NormalizeId(definition.Id)}.name";
                    UpsertStringEntry(ContentLocalizationTables.Tags, asset.NameKey, definition.KoName, definition.EnName);
                });
        }
    }

    private static void CreateDuelistBuildContent(IReadOnlyDictionary<string, StableTagDefinition> tags)
    {
        ValidateDuelistBuildTags(tags);
        CreateDuelistBuildSkills(tags);

        var board = LoadDefinition<PassiveBoardDefinition>($"{ResourcesRoot}/PassiveBoards/{DuelistBoardId}.asset")
                    ?? throw new InvalidOperationException($"Missing canonical passive board '{DuelistBoardId}'.");
        var nodes = new List<PassiveNodeDefinition>();
        foreach (var seed in BuildDuelistNodeSeeds())
        {
            var node = CreateAsset<PassiveNodeDefinition>(
                $"{ResourcesRoot}/PassiveNodes/{seed.Id}.asset",
                asset =>
                {
                    asset.Id = seed.Id;
                    asset.BoardId = DuelistBoardId;
                    asset.NameKey = ContentLocalizationTables.BuildPassiveNodeNameKey(seed.Id);
                    asset.DescriptionKey = ContentLocalizationTables.BuildPassiveNodeDescriptionKey(seed.Id);
                    asset.NodeKind = seed.Kind;
                    asset.PrerequisiteNodeIds = seed.PrerequisiteIds.ToList();
                    asset.MutualExclusionTags = ResolveRequiredTags(tags, seed.MutualExclusionTagIds);
                    asset.BoardDepth = seed.Depth;
                    asset.CompileTags = ResolveRequiredTags(tags, seed.CompileTagIds);
                    asset.RuleModifierTags = ResolveRequiredTags(tags, seed.RuleModifierTagIds);
                    asset.Modifiers = seed.Modifiers
                        .Select(modifier => new SerializableStatModifier
                        {
                            StatId = modifier.StatId,
                            Op = modifier.Op,
                            Value = modifier.Value,
                        })
                        .ToList();
                    asset.GrantedSkillId = seed.GrantedSkillId;
                    UpsertStringEntry(ContentLocalizationTables.Passives, asset.NameKey, seed.KoName, seed.EnName);
                    UpsertStringEntry(ContentLocalizationTables.Passives, asset.DescriptionKey, seed.KoDescription, seed.EnDescription);
                });
            PatchSerializedPassiveNodeTags(node);
            nodes.Add(node);
        }

        if (nodes.Count != 24 || nodes.Select(node => node.Id).Distinct(StringComparer.Ordinal).Count() != 24)
        {
            throw new InvalidOperationException("Duelist build seed must resolve exactly 24 unique nodes.");
        }

        var nodesById = nodes.ToDictionary(node => node.Id, StringComparer.Ordinal);
        board.Nodes = DuelistBoardNodeOrder.Select(id => nodesById[id]).ToList();
        EditorUtility.SetDirty(board);
    }

    private static void NormalizeEmptyDuelistGrantedSkillYaml()
    {
        foreach (var seed in BuildDuelistNodeSeeds().Where(seed => string.IsNullOrEmpty(seed.GrantedSkillId)))
        {
            var path = $"{ResourcesRoot}/PassiveNodes/{seed.Id}.asset";
            var lines = File.ReadAllLines(path);
            var normalized = lines
                .Where(line => !string.Equals(line.Trim(), "GrantedSkillId:", StringComparison.Ordinal))
                .ToArray();
            if (normalized.Length != lines.Length)
            {
                File.WriteAllLines(path, normalized);
            }
        }
    }

    private static void CreateDuelistBuildSkills(IReadOnlyDictionary<string, StableTagDefinition> tags)
    {
        var sunder = CreateAsset<SkillDefinitionAsset>($"{ResourcesRoot}/Skills/skill_sunder_rhythm.asset", asset =>
        {
            ResetDuelistGhostSkill(asset, "skill_sunder_rhythm", SkillTemplateTypeValue.AllyBuffAuraPulse,
                SkillKindValue.Buff, SkillSlotKindValue.Support, SkillTargetRuleValue.MostExposedEnemy);
            asset.CompileTags = ResolveRequiredTags(tags, new[] { "duelist", "support", "sunder" });
            asset.SupportAllowedTags = ResolveRequiredTags(tags, new[] { "strike" });
            asset.SupportBlockedTags = ResolveRequiredTags(tags, new[] { "dash", "heal", "shield_skill" });
            asset.RequiredWeaponTags = ResolveRequiredTags(tags, new[] { "blade" });
            asset.RequiredClassTags = ResolveRequiredTags(tags, new[] { "duelist" });
            asset.EffectFamilyId = "duelist_sunder_rhythm";
            asset.VfxHookId = "vfx.skill_sunder_rhythm";
            asset.SfxHookId = "sfx.skill_sunder_rhythm";
            asset.SupportModifier = new SupportModifierSpec
            {
                AddedStatuses = new List<StatusApplicationRule>
                {
                    new()
                    {
                        Id = "skill_sunder_rhythm:sunder",
                        StatusId = "sunder",
                        DurationSeconds = 3.5f,
                        Magnitude = 0.06f,
                        MaxStacks = 3,
                        StackCap = 3,
                        RefreshDurationOnReapply = true,
                    },
                },
            };
            UpsertStringEntry(ContentLocalizationTables.Skills, asset.NameKey, "파쇄 리듬", "Sunder Rhythm");
            UpsertStringEntry(
                ContentLocalizationTables.Skills,
                asset.DescriptionKey,
                 "근접 타격이 방어를 6%씩 파쇄하며 3회까지 중첩되고 재타격 시 3.5초 지속시간이 갱신됩니다.",
                 "Melee strikes shred 6% defense, stack up to three times, and refresh the 3.5-second duration on hit.");
            ApplyLoopCSkillGovernance(asset);
        });
        PatchDuelistGhostSkillReferences(sunder);

        var lastBastion = CreateAsset<SkillDefinitionAsset>($"{ResourcesRoot}/Skills/skill_last_bastion.asset", asset =>
        {
            ResetDuelistGhostSkill(asset, "skill_last_bastion", SkillTemplateTypeValue.TriggerPassive,
                SkillKindValue.Utility, SkillSlotKindValue.Passive, SkillTargetRuleValue.Self);
            asset.CompileTags = ResolveRequiredTags(tags, new[] { "duelist", "frontline", "guard" });
            asset.RequiredClassTags = ResolveRequiredTags(tags, new[] { "duelist" });
            asset.EffectFamilyId = "duelist_last_bastion";
            asset.VfxHookId = "vfx.skill_last_bastion";
            asset.SfxHookId = "sfx.skill_last_bastion";
            asset.TargetRuleData = new TargetRule
            {
                Domain = TargetDomain.Self,
                PrimarySelector = TargetSelector.Self,
                FallbackPolicy = TargetFallbackPolicy.Self,
                Filters = TargetFilterFlags.None,
            };
            asset.TriggeredEffects = new List<TriggeredEffectSpec>
            {
                new()
                {
                    Trigger = CombatTriggerKind.OnHpBelow,
                    Op = TriggeredEffectOp.Barrier,
                    Scope = EffectScope.Self,
                    Magnitude = 6f,
                    ThresholdRatio = 0.40f,
                    MaxStacks = 1,
                },
                new()
                {
                    Trigger = CombatTriggerKind.OnHpBelow,
                    Op = TriggeredEffectOp.ApplyStatus,
                    Scope = EffectScope.Self,
                    Magnitude = 1f,
                    ThresholdRatio = 0.40f,
                    StatusId = "guarded",
                    DurationSeconds = 2.5f,
                    MaxStacks = 1,
                },
            };
            UpsertStringEntry(ContentLocalizationTables.Skills, asset.NameKey, "최후의 보루", "Last Bastion");
            UpsertStringEntry(
                ContentLocalizationTables.Skills,
                asset.DescriptionKey,
                 "체력이 처음 40% 이하가 되면 보호막 6과 2.5초 수호를 얻습니다.",
                 "The first time health reaches 40% or lower, gain a 6 barrier and Guarded for 2.5 seconds.");
            ApplyLoopCSkillGovernance(asset);
        });
        PatchDuelistGhostSkillReferences(lastBastion);
    }

    private static void ResetDuelistGhostSkill(
        SkillDefinitionAsset asset,
        string id,
        SkillTemplateTypeValue templateType,
        SkillKindValue kind,
        SkillSlotKindValue slotKind,
        SkillTargetRuleValue targetRule)
    {
        asset.Id = id;
        asset.NameKey = ContentLocalizationTables.BuildSkillNameKey(id);
        asset.DescriptionKey = ContentLocalizationTables.BuildSkillDescriptionKey(id);
        asset.TemplateType = templateType;
        asset.Kind = kind;
        asset.SlotKind = slotKind;
        asset.DamageType = DamageTypeValue.Physical;
        asset.Delivery = SkillDeliveryValue.Melee;
        asset.TargetRule = targetRule;
        asset.Power = 0f;
        asset.Range = 0f;
        asset.RangeMin = 0f;
        asset.RangeMax = -1f;
        asset.Radius = 0f;
        asset.Width = 0f;
        asset.ArcDegrees = 0f;
        asset.PowerFlat = 0f;
        asset.PhysCoeff = 0f;
        asset.MagCoeff = 0f;
        asset.HealCoeff = 0f;
        asset.HealthCoeff = 0f;
        asset.CanCrit = false;
        asset.ActivationModel = ActivationModel.Passive;
        asset.Lane = ActionLane.Primary;
        asset.LockRule = ActionLockRule.HardCommit;
        asset.AuthorityLayer = AuthorityLayer.Skill;
        asset.ManaCost = 0f;
        asset.ResourceCost = -1f;
        asset.BaseCooldownSeconds = 0f;
        asset.CooldownSeconds = -1f;
        asset.CastWindupSeconds = 0f;
        asset.RecoverySeconds = -1f;
        asset.AnimationHookId = string.Empty;
        asset.IconId = $"skill_icon_{id["skill_".Length..]}";
        asset.VfxHookId = string.Empty;
        asset.SfxHookId = string.Empty;
        asset.LearnSource = SkillLearnSourceValue.RecruitFlex;
        asset.MutuallyExclusiveGroupId = string.Empty;
        asset.RecruitNativeTags = new List<StableTagDefinition>();
        asset.RecruitPlanTags = new List<StableTagDefinition>();
        asset.RecruitScoutTags = new List<StableTagDefinition>();
        asset.TargetRuleData = new TargetRule();
        asset.Effects = new List<EffectDescriptor>();
        asset.CompileTags = new List<StableTagDefinition>();
        asset.RuleModifierTags = new List<StableTagDefinition>();
        asset.SupportAllowedTags = new List<StableTagDefinition>();
        asset.SupportBlockedTags = new List<StableTagDefinition>();
        asset.RequiredWeaponTags = new List<StableTagDefinition>();
        asset.RequiredClassTags = new List<StableTagDefinition>();
        asset.AppliedStatuses = new List<StatusApplicationRule>();
        asset.CleanseProfileId = string.Empty;
        asset.TriggeredEffects = new List<TriggeredEffectSpec>();
        asset.SupportModifier = new SupportModifierSpec();
    }

    private static void PatchDuelistGhostSkillReferences(SkillDefinitionAsset skill)
    {
        var serialized = new SerializedObject(skill);
        SetObjectReferenceArray(serialized.FindProperty(nameof(SkillDefinitionAsset.CompileTags)), skill.CompileTags);
        SetObjectReferenceArray(serialized.FindProperty(nameof(SkillDefinitionAsset.RuleModifierTags)), skill.RuleModifierTags);
        SetObjectReferenceArray(serialized.FindProperty(nameof(SkillDefinitionAsset.SupportAllowedTags)), skill.SupportAllowedTags);
        SetObjectReferenceArray(serialized.FindProperty(nameof(SkillDefinitionAsset.SupportBlockedTags)), skill.SupportBlockedTags);
        SetObjectReferenceArray(serialized.FindProperty(nameof(SkillDefinitionAsset.RequiredWeaponTags)), skill.RequiredWeaponTags);
        SetObjectReferenceArray(serialized.FindProperty(nameof(SkillDefinitionAsset.RequiredClassTags)), skill.RequiredClassTags);
        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(skill);
    }

    private static void ValidateDuelistBuildTags(IReadOnlyDictionary<string, StableTagDefinition> tags)
    {
        var required = DuelistBuildStableTags.Select(definition => definition.Id)
            .Concat(new[]
            {
                "duelist", "frontline", "physical", "melee", "strike", "dash", "execute", "guard",
                "sunder", "support", "heal", "shield_skill", "blade",
            })
            .Distinct(StringComparer.Ordinal)
            .Where(id => !tags.ContainsKey(id))
            .ToArray();
        if (required.Length > 0)
        {
            throw new InvalidOperationException($"Duelist build seed is missing stable tags: {string.Join(", ", required)}");
        }
    }

    private static List<StableTagDefinition> ResolveRequiredTags(
        IReadOnlyDictionary<string, StableTagDefinition> tags,
        IEnumerable<string> tagIds)
    {
        var ids = tagIds.Distinct(StringComparer.Ordinal).ToArray();
        var missing = ids.Where(id => !tags.ContainsKey(id)).ToArray();
        if (missing.Length > 0)
        {
            throw new InvalidOperationException($"Duelist build tag references are missing: {string.Join(", ", missing)}");
        }

        return ids.Select(id => tags[id]).ToList();
    }

    private static IReadOnlyList<DuelistNodeSeed> BuildDuelistNodeSeeds()
    {
        var duelistPhysical = new[] { "duelist", "physical", "strike" };
        var duelistDive = new[] { "duelist", "frontline", "dash", "execute" };
        var duelistBruiser = new[] { "duelist", "frontline", "guard" };
        return new[]
        {
            Node("small_01", "단련된 칼끝", "Honed Edge", "물리 출력에 먼저 투자해 초반 타격 선택을 강화합니다.", "Invest in physical output first to strengthen early strikes.", 0, PassiveNodeKindValue.Small, null, duelistPhysical, null, null, null, Mod("phys_power", ModifierOp.Flat, 0.8f)),
            Node("small_02", "가벼운 손목", "Light Wrist", "공격 속도에 먼저 투자해 초반 템포 선택을 강화합니다.", "Invest in attack speed first to strengthen early tempo.", 0, PassiveNodeKindValue.Small, null, new[] { "duelist", "melee", "strike" }, null, null, null, Mod("attack_speed", ModifierOp.Flat, 0.08f)),
            Node("small_03", "피의 습관", "Blood Habit", "흡혈을 얻어 방패 서약으로 향하는 지속력 관문을 엽니다.", "Gain lifesteal and open the sustain path toward Shield Oath.", 1, PassiveNodeKindValue.Small, new[] { "small_01" }, duelistBruiser, null, null, null, Mod("lifesteal", ModifierOp.Flat, 0.02f)),
            Node("small_04", "보폭", "Stride", "이동 속도를 얻어 사냥 개시로 향하는 접근 관문을 엽니다.", "Gain movement speed and open the approach path toward Hunt Begins.", 1, PassiveNodeKindValue.Small, new[] { "small_02" }, duelistDive, null, null, null, Mod("move_speed", ModifierOp.Flat, 0.05f)),
            Node("small_05", "질주", "Rush", "후열 접촉 시간을 줄여 위험 기술보다 먼저 도착할 가능성을 높입니다.", "Reach the backline sooner and improve the chance to arrive before dangerous skills land.", 3, PassiveNodeKindValue.Small, new[] { "notable_01" }, duelistDive, null, null, null, Mod("move_speed", ModifierOp.Flat, 0.07f)),
            Node("small_06", "급소 감각", "Vital Sense", "도착 후 치명타 확률을 높여 다이브 화력을 키웁니다.", "Raise critical chance after arrival to strengthen dive damage.", 3, PassiveNodeKindValue.Small, new[] { "notable_01" }, duelistDive, null, null, null, Mod("crit_chance", ModifierOp.Flat, 0.03f)),
            Node("small_07", "망설임 없는 칼", "Unhesitating Blade", "공격 예열을 줄여 다이브 커밋 동안 한 타를 더 노립니다.", "Shorten attack windup to seek one more hit during the dive commitment.", 4, PassiveNodeKindValue.Small, new[] { "small_05" }, duelistDive, null, null, null, Mod("attack_windup", ModifierOp.Flat, -0.06f)),
            Node("small_08", "치명의 각도", "Killing Angle", "치명타 배율을 높여 처형 선고 경로의 고점을 엽니다.", "Raise critical multiplier and open the peak-damage path to Execution Sentence.", 4, PassiveNodeKindValue.Small, new[] { "small_06" }, duelistDive, null, null, null, Mod("crit_multiplier", ModifierOp.Flat, 0.05f)),
            Node("small_09", "두꺼운 살갗", "Thick Hide", "원본 체력을 늘려 순간 폭딜을 견디는 브루저 경로를 엽니다.", "Increase base health and open the bruiser path against burst damage.", 3, PassiveNodeKindValue.Small, new[] { "notable_02" }, duelistBruiser, null, null, null, Mod("max_health", ModifierOp.Increased, 0.07f)),
            Node("small_10", "피의 흡수", "Blood Absorption", "흡혈을 늘려 장기전에 특화된 브루저 경로를 엽니다.", "Increase lifesteal and open the bruiser path for long fights.", 3, PassiveNodeKindValue.Small, new[] { "notable_02" }, duelistBruiser, null, null, null, Mod("lifesteal", ModifierOp.Flat, 0.04f)),
            Node("small_11", "강철 어깨", "Steel Shoulder", "방어를 늘려 물리 공격 덱에 맞서는 선택을 강화합니다.", "Increase armor to strengthen the choice against physical attackers.", 4, PassiveNodeKindValue.Small, new[] { "small_09" }, duelistBruiser, null, null, null, Mod("armor", ModifierOp.Flat, 1f)),
            Node("small_12", "꿰뚫는 일격", "Piercing Blow", "물리 관통을 얻어 단단한 표적을 오래 때리는 선택을 보상합니다.", "Gain physical penetration to reward staying on durable targets.", 4, PassiveNodeKindValue.Small, new[] { "small_10" }, new[] { "duelist", "frontline", "sunder" }, null, null, null, Mod("phys_pen", ModifierOp.Flat, 0.7f)),
            Node("small_13", "시선 고정", "Fixed Gaze", "첫 처치 뒤 다음 표적으로 더 빨리 전환해 연쇄 킬을 준비합니다.", "Switch targets faster after the first kill to prepare a kill chain.", 4, PassiveNodeKindValue.Small, new[] { "small_06" }, duelistDive, null, null, null, Mod("crit_chance", ModifierOp.Flat, 0.02f), Mod("target_switch_delay", ModifierOp.Flat, -0.04f)),
            Node("small_14", "굳센 심지", "Steadfast Core", "강인함을 높여 제어가 많은 적을 상대하는 선택을 엽니다.", "Raise tenacity to open a counter-pick against control-heavy enemies.", 4, PassiveNodeKindValue.Small, new[] { "small_10" }, duelistBruiser, null, null, null, Mod("tenacity", ModifierOp.Flat, 0.15f)),
            Node("notable_01", "사냥 개시", "Hunt Begins", "기본 전진 태세에서도 적 후열을 노리고 뛰어듭니다. 체력과 지속력 상한을 대가로 치명타를 얻습니다.", "Dive toward enemy backliners even under Standard Advance. Trade health and sustain caps for critical chance.", 2, PassiveNodeKindValue.Notable, new[] { "small_04" }, duelistDive, new[] { "duelist_dive_commit" }, new[] { "tag_duelist_build_gate" }, null, Mod("crit_chance", ModifierOp.Flat, 0.03f), Mod("max_health", ModifierOp.More, -0.06f), Mod("lifesteal", ModifierOp.ClampMax, 0.05f), Mod("phys_pen", ModifierOp.ClampMax, 1f)),
            Node("notable_02", "방패 서약", "Shield Oath", "어떤 태세에서도 다이브하지 않고 전선을 지킵니다. 치명타 고점과 이동 속도를 대가로 체력과 방어를 얻습니다.", "Never dive in any posture and hold the frontline. Trade critical peaks and movement speed for health and armor.", 2, PassiveNodeKindValue.Notable, new[] { "small_03" }, duelistBruiser, new[] { "duelist_hold_bruiser" }, new[] { "tag_duelist_build_gate" }, null, Mod("max_health", ModifierOp.Increased, 0.08f), Mod("armor", ModifierOp.Flat, 1f), Mod("crit_chance", ModifierOp.ClampMax, 0.08f), Mod("crit_multiplier", ModifierOp.ClampMax, 0.75f), Mod("move_speed", ModifierOp.More, -0.05f)),
            Node("notable_03", "처형 선고", "Execution Sentence", "체력 35% 이하 표적을 우선하고 피해를 25% 더 줍니다. 치명타 배율도 높입니다.", "Prioritize targets at 35% health or lower and deal 25% more damage to them. Also raise critical multiplier.", 5, PassiveNodeKindValue.Notable, new[] { "small_08" }, duelistDive, new[] { "execute_low_hp" }, null, null, Mod("crit_multiplier", ModifierOp.Flat, 0.05f)),
            Node("notable_04", "그림자 걸음", "Shadow Step", "처치가 에너지 20을 돌려줘 다음 시그니처를 앞당깁니다. 이동과 공격 예열도 개선합니다.", "Kills restore 20 energy to accelerate the next signature. Movement and attack windup also improve.", 5, PassiveNodeKindValue.Notable, new[] { "small_07" }, duelistDive, null, null, "skill_edge_of_sentence", Mod("move_speed", ModifierOp.Flat, 0.05f), Mod("attack_windup", ModifierOp.Flat, -0.04f)),
            Node("notable_05", "파쇄 리듬", "Sunder Rhythm", "근접 코어 타격이 방어 파쇄를 3회까지 쌓아 단단한 표적에 집중할 이유를 만듭니다.", "Melee core strikes stack defense shred up to three times, rewarding focus on durable targets.", 5, PassiveNodeKindValue.Notable, new[] { "small_12" }, new[] { "duelist", "frontline", "sunder" }, null, null, "skill_sunder_rhythm", Mod("phys_power", ModifierOp.Flat, 0.6f)),
            Node("notable_06", "간격 통제", "Spacing Control", "아군 후열을 문 적 다이버를 3m 안에서 요격합니다. 체력과 보호 반경도 늘어납니다.", "Intercept enemy divers threatening the backline within 3 meters. Health and protect radius also increase.", 5, PassiveNodeKindValue.Notable, new[] { "small_11" }, duelistBruiser, new[] { "duelist_peel" }, null, null, Mod("protect_radius", ModifierOp.Flat, 0.6f), Mod("max_health", ModifierOp.Increased, 0.05f)),
            Node("notable_07", "추격 본능", "Pursuit Instinct", "긴 타깃 락 없이 이동과 공격 예열을 개선해 유연한 암살 경로를 만듭니다.", "Improve movement and attack windup without a long target lock to form a flexible assassin path.", 5, PassiveNodeKindValue.Notable, new[] { "small_13" }, duelistDive, null, null, null, Mod("move_speed", ModifierOp.Flat, 0.05f), Mod("attack_windup", ModifierOp.Flat, -0.04f), Mod("crit_chance", ModifierOp.Flat, 0.02f)),
            Node("notable_08", "피의 대가", "Price of Blood", "처치 시 2.5초 수호를 얻어 난전에서 킬을 딸수록 단단해집니다.", "Gain Guarded for 2.5 seconds on kill, becoming tougher as you secure kills in a brawl.", 5, PassiveNodeKindValue.Notable, new[] { "small_14" }, duelistBruiser, null, null, "skill_bloodless_form", Mod("lifesteal", ModifierOp.Flat, 0.03f), Mod("phys_pen", ModifierOp.Flat, 0.6f)),
            Node("keystone_01", "고립 선고", "Isolation Sentence", "다이브 표적을 2.5초 동안 놓지 않습니다. 처치 시 주변 적을 노출하지만 최대 체력이 더 줄어듭니다.", "Hold a dive target for 2.5 seconds. Kills expose nearby enemies, but maximum health falls further.", 6, PassiveNodeKindValue.Keystone, new[] { "notable_04" }, duelistDive, new[] { "dive_assassin_keystone" }, null, "skill_shard_memory", Mod("crit_chance", ModifierOp.Flat, 0.02f), Mod("max_health", ModifierOp.More, -0.065f)),
            Node("keystone_02", "최후의 보루", "Last Bastion", "체력이 처음 40% 이하가 되면 보호막 6과 2.5초 수호를 얻습니다. 이동 속도를 추가로 희생합니다.", "The first time health reaches 40% or lower, gain a 6 barrier and Guarded for 2.5 seconds. Sacrifice more movement speed.", 6, PassiveNodeKindValue.Keystone, new[] { "notable_06" }, duelistBruiser, null, null, "skill_last_bastion", Mod("armor", ModifierOp.Flat, 1f), Mod("lifesteal", ModifierOp.Flat, 0.02f), Mod("move_speed", ModifierOp.More, -0.05f)),
        };
    }

    private static DuelistNodeSeed Node(
        string suffix,
        string koName,
        string enName,
        string koDescription,
        string enDescription,
        int depth,
        PassiveNodeKindValue kind,
        IReadOnlyList<string>? prerequisiteSuffixes,
        IReadOnlyList<string> compileTagIds,
        IReadOnlyList<string>? ruleModifierTagIds,
        IReadOnlyList<string>? mutualExclusionTagIds,
        string? grantedSkillId,
        params DuelistStatSeed[] modifiers)
    {
        static string FullId(string value) => $"passive_duelist_{value}";
        return new DuelistNodeSeed(
            FullId(suffix),
            koName,
            enName,
            koDescription,
            enDescription,
            depth,
            kind,
            (prerequisiteSuffixes ?? Array.Empty<string>()).Select(FullId).ToArray(),
            compileTagIds,
            ruleModifierTagIds ?? Array.Empty<string>(),
            mutualExclusionTagIds ?? Array.Empty<string>(),
            grantedSkillId ?? string.Empty,
            modifiers);
    }

    private static DuelistStatSeed Mod(string statId, ModifierOp op, float value) => new(statId, op, value);

    private sealed record DuelistNodeSeed(
        string Id,
        string KoName,
        string EnName,
        string KoDescription,
        string EnDescription,
        int Depth,
        PassiveNodeKindValue Kind,
        IReadOnlyList<string> PrerequisiteIds,
        IReadOnlyList<string> CompileTagIds,
        IReadOnlyList<string> RuleModifierTagIds,
        IReadOnlyList<string> MutualExclusionTagIds,
        string GrantedSkillId,
        IReadOnlyList<DuelistStatSeed> Modifiers);

    private sealed record DuelistStatSeed(string StatId, ModifierOp Op, float Value);
}
