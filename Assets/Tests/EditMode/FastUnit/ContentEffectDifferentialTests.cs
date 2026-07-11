using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using NUnit.Framework;
using SM.Combat.Model;
using SM.Combat.Services;
using SM.Core.Contracts;
using SM.Core.Stats;
using SM.Meta;
using SM.Meta.Model;
using SM.Meta.Services;

namespace SM.Tests.EditMode;

/// <summary>
/// differential 하네스 1차 — "콘텐츠가 sim에서 실제로 뭔가 바꾸는가"를 단언한다.
/// 배경: 2026-07 준비도 감사에서 콘텐츠→sim 효과 사슬 assertion이 0으로 판명
/// (affix의 CompileTags/RequiredTags/RuleModifierTags 전량 드롭, 조건부 affix가
/// 무조건 affix로 동작). 이 파일은 compile 출력을 실 BattleSimulator에 넣어
/// 장착 유무가 전투 결과를 바꾸는지까지 검증하는 최초의 compile→sim 연결이다.
/// </summary>
[Category("FastUnit")]
public sealed class ContentEffectDifferentialTests
{
    private const int Seed = 7;
    private const int MaxSteps = 600;

    [Test]
    public void RuleAffix_CompileTagsAndRulePackage_ReachCompiledUnit()
    {
        var compiled = CompileSingleHero(affixIds: new[] { "affix.tempo" });
        var ally = compiled.Allies.Single();

        Assert.That(ally.CompileTags, Does.Contain("tempo"),
            "affix의 CompileTags는 유닛 태그로 전파돼야 한다 — 드롭되면 태그 기반 계약(조건/시너지 확장)이 전부 무효화된다");
        Assert.That(ally.CompileTags, Does.Contain("execute"));
        Assert.That(
            (ally.RulePackages ?? Array.Empty<CombatRuleModifierPackage>())
                .Any(package => package.SourceId == "affix.tempo"
                    && package.Modifiers.Any(modifier => modifier.Kind == RuleModifierKind.BehaviorTag && modifier.Value == "tempo")),
            Is.True,
            "affix RuleModifierTags는 skill/augment/passive와 동일하게 RulePackages로 컴파일돼야 한다");
    }

    [Test]
    public void ConditionalAffix_UnmetRequirement_IsFullyInert_InCompileAndSim()
    {
        var withUnmetConditional = CompileSingleHero(affixIds: new[] { "affix.cond_unmet" });
        var withoutAffix = CompileSingleHero(affixIds: Array.Empty<string>());

        Assert.That(
            withUnmetConditional.Allies.Single().NumericPackages.Any(package => package.SourceId == "affix.cond_unmet"),
            Is.False,
            "RequiredTags 미충족 조건부 affix의 수치는 적용되면 안 된다 — 현재는 조건 무시로 무조건 적용되는 버그");

        var unmetRun = RunCompiledAllyVersusRaider(withUnmetConditional);
        var baselineRun = RunCompiledAllyVersusRaider(withoutAffix);
        Assert.That(unmetRun.Stream, Is.EqualTo(baselineRun.Stream),
            "미충족 조건부 affix 장착은 미장착과 전투 결과가 완전히 동일해야 한다(수치 누출 0)");
    }

    [Test]
    public void ConditionalAffix_MetRequirement_AppliesNumeric_AndChangesSimOutcome()
    {
        var withMetConditional = CompileSingleHero(affixIds: new[] { "affix.cond_met" });
        var withoutAffix = CompileSingleHero(affixIds: Array.Empty<string>());

        Assert.That(
            withMetConditional.Allies.Single().NumericPackages.Any(package => package.SourceId == "affix.cond_met"),
            Is.True,
            "RequiredTags 충족(vanguard) 조건부 affix는 수치가 적용돼야 한다 — 게이트가 과잉 차단하면 안 된다");

        var metRun = RunCompiledAllyVersusRaider(withMetConditional);
        var baselineRun = RunCompiledAllyVersusRaider(withoutAffix);
        Assert.That(metRun.AllySurvivedSteps, Is.GreaterThan(baselineRun.AllySurvivedSteps),
            "+200 MaxHealth 충족 affix를 장착한 유닛은 실 sim에서 뚜렷하게 오래 생존해야 한다");
    }

    [Test]
    public void NumericAffix_EquippedVsNot_SurvivesLonger_InRealSim()
    {
        var equipped = CompileSingleHero(affixIds: new[] { "affix.hearty" });
        var bare = CompileSingleHero(affixIds: Array.Empty<string>());

        var equippedRun = RunCompiledAllyVersusRaider(equipped);
        var bareRun = RunCompiledAllyVersusRaider(bare);

        Assert.That(equippedRun.AllySurvivedSteps, Is.GreaterThan(bareRun.AllySurvivedSteps),
            "장착 vs 미장착이 실 BattleSimulator 결과를 바꿔야 한다 — 이 단언이 실 콘텐츠 사슬의 silent 드롭을 잡는 1차 가드다");
    }

    [Test]
    public void PassiveSkillTriggeredEffect_ReachesUnit_AndChangesSimOutcome()
    {
        var withTrigger = CompileSingleHero(
            affixIds: Array.Empty<string>(),
            mutatePassive: skill => skill with
            {
                TriggeredEffects = new[]
                {
                    new CombatTriggeredEffect(skill.Id, CombatTriggerKind.BattleStart, TriggeredEffectOp.Barrier, EffectScope.Self, Magnitude: 60f),
                },
            });
        var without = CompileSingleHero(affixIds: Array.Empty<string>());

        Assert.That(
            withTrigger.Allies.Single().EffectiveTriggeredEffects
                .Any(effect => effect.SourceId == "skill.vanguard.passive" && effect.Op == TriggeredEffectOp.Barrier),
            Is.True,
            "패시브 슬롯 스킬의 TriggeredEffects는 유닛 트리거 채널로 합류해야 한다(증강과 동일 계약)");
        Assert.That(
            without.Allies.Single().EffectiveTriggeredEffects
                .Any(effect => effect.SourceId == "skill.vanguard.passive"),
            Is.False,
            "음성 대조군: 발동 효과가 없는 패시브는 트리거 채널에 아무 것도 남기지 않아야 한다");

        var withRun = RunCompiledAllyVersusRaider(withTrigger);
        var withoutRun = RunCompiledAllyVersusRaider(without);
        Assert.That(withRun.AllySurvivedSteps, Is.GreaterThan(withoutRun.AllySurvivedSteps),
            "BattleStart Barrier(60)를 발동한 유닛은 실 sim에서 뚜렷하게 오래 생존해야 한다 — 패시브 슬롯 실전투 효과의 1차 가드");
    }

    [Test]
    public void SupportGem_TransformsMatchedActive_InCompile()
    {
        BattleSkillSpec MutateCore(BattleSkillSpec skill) => skill with { CompileTags = new[] { "strike" } };

        // 매칭: allowed(strike)가 코어와 교집합 → Power ×2 + marked 부여, 비매칭 유틸리티는 무변.
        var matched = CompileSingleHero(
            Array.Empty<string>(),
            mutateCore: MutateCore,
            mutateSupport: skill => skill with
            {
                SupportAllowedTags = new[] { "strike" },
                SupportModifier = new BattleSupportModifierSpec(
                    PowerMultiplier: 2f,
                    AddedStatuses: new[] { new StatusApplicationSpec("gem:marked", "marked", 3f, 1f) }),
            });
        var matchedCore = matched.Allies.Single().Skills.Single(skill => skill.Id == "skill.warden.core");
        Assert.That(matchedCore.Power, Is.EqualTo(9f).Within(0.0001f),
            "매칭된 서포트 젬은 페어 액티브의 위력을 변조해야 한다 (4.5 × 2)");
        Assert.That(
            (matchedCore.AppliedStatuses ?? Array.Empty<StatusApplicationSpec>()).Any(status => status.StatusId == "marked"),
            Is.True,
            "젬의 AddedStatuses가 매칭 액티브에 부여돼야 한다");
        var matchedUtility = matched.Allies.Single().Skills.Single(skill => skill.Id == "skill.warden.utility");
        Assert.That(matchedUtility.Power, Is.EqualTo(0f).Within(0.0001f), "비매칭(태그 없음) 액티브는 무변");

        // 비매칭: allowed(projectile)가 어느 액티브와도 교집합 없음 → 전부 무변.
        var unmatched = CompileSingleHero(
            Array.Empty<string>(),
            mutateCore: MutateCore,
            mutateSupport: skill => skill with
            {
                SupportAllowedTags = new[] { "projectile" },
                SupportModifier = new BattleSupportModifierSpec(PowerMultiplier: 2f),
            });
        Assert.That(
            unmatched.Allies.Single().Skills.Single(skill => skill.Id == "skill.warden.core").Power,
            Is.EqualTo(4.5f).Within(0.0001f),
            "allowed 태그 비매칭이면 변조가 없어야 한다");

        // 클래스 게이트: RequiredClassTags(mystic)를 warden(vanguard)이 불충족 → 무변.
        var classGated = CompileSingleHero(
            Array.Empty<string>(),
            mutateCore: MutateCore,
            mutateSupport: skill => skill with
            {
                SupportAllowedTags = new[] { "strike" },
                RequiredClassTags = new[] { "mystic" },
                SupportModifier = new BattleSupportModifierSpec(PowerMultiplier: 2f),
            });
        Assert.That(
            classGated.Allies.Single().Skills.Single(skill => skill.Id == "skill.warden.core").Power,
            Is.EqualTo(4.5f).Within(0.0001f),
            "RequiredClassTags 불충족 젬은 변조하지 않아야 한다");
    }

    [Test]
    public void SupportGem_WeaponGate_RequiresMatchingWeaponFamily()
    {
        BattleSkillSpec MutateCore(BattleSkillSpec skill) => skill with { CompileTags = new[] { "strike" } };
        BattleSkillSpec MutateSupport(BattleSkillSpec skill) => skill with
        {
            SupportAllowedTags = new[] { "strike" },
            RequiredWeaponTags = new[] { "bow" },
            SupportModifier = new BattleSupportModifierSpec(PowerMultiplier: 2f),
        };

        // 활(bow) 미장착 → 무기 게이트가 젬을 차단(변조 없음).
        var withoutBow = CompileSingleHero(
            Array.Empty<string>(), mutateCore: MutateCore, mutateSupport: MutateSupport);
        Assert.That(
            withoutBow.Allies.Single().Skills.Single(skill => skill.Id == "skill.warden.core").Power,
            Is.EqualTo(4.5f).Within(0.0001f),
            "RequiredWeaponTags(bow) 미충족 젬은 변조하지 않아야 한다");

        // 활 장착 → 무기 family 태그가 유닛에 전파되고 게이트 통과(변조 적용).
        var withBow = CompileSingleHero(
            Array.Empty<string>(),
            mutateCore: MutateCore,
            mutateSupport: MutateSupport,
            itemIds: new[] { "item.shield", "item.bow" });
        var unit = withBow.Allies.Single();
        Assert.That(unit.CompileTags, Does.Contain("bow"),
            "아이템의 weapon family 태그가 유닛 태그로 전파돼야 한다");
        Assert.That(
            unit.Skills.Single(skill => skill.Id == "skill.warden.core").Power,
            Is.EqualTo(9f).Within(0.0001f),
            "무기 게이트 충족 시 젬 변조가 적용돼야 한다 (4.5 × 2)");
    }

    [Test]
    public void SupportGem_OwnerModifiers_ChangeSimOutcome()
    {
        var withGem = CompileSingleHero(
            Array.Empty<string>(),
            mutateSupport: skill => skill with
            {
                SupportModifier = new BattleSupportModifierSpec(
                    OwnerModifiers: new[]
                    {
                        new StatModifier(StatKey.MaxHealth, ModifierOp.Flat, 200f, ModifierSource.Skill, "gem.owner"),
                    }),
            });
        var without = CompileSingleHero(Array.Empty<string>());

        Assert.That(
            withGem.Allies.Single().NumericPackages.Any(package => package.SourceId == "support:skill.vanguard.support"),
            Is.True,
            "젬의 OwnerModifiers는 유닛 numeric package로 합류해야 한다");

        var withRun = RunCompiledAllyVersusRaider(withGem);
        var withoutRun = RunCompiledAllyVersusRaider(without);
        Assert.That(withRun.AllySurvivedSteps, Is.GreaterThan(withoutRun.AllySurvivedSteps),
            "젬 OwnerModifiers(+200 MaxHealth)가 실 sim 생존을 바꿔야 한다");
    }

    [Test]
    public void GuardedIncomingDamageDelta_IsContentTunable_InRealSim()
    {
        // 같은 전투를 상태 규칙만 바꿔 두 번 — guarded delta는 이제 콘텐츠 튜닝값이다.
        // 프로브 유닛에 개전 시 장시간 guarded를 발동시켜(slic A 트리거 채널) delta가 매 피격에 적용되게 한다.
        var compiled = CompileSingleHero(
            Array.Empty<string>(),
            mutatePassive: skill => skill with
            {
                TriggeredEffects = new[]
                {
                    new CombatTriggeredEffect(
                        skill.Id,
                        CombatTriggerKind.BattleStart,
                        TriggeredEffectOp.ApplyStatus,
                        EffectScope.Self,
                        Magnitude: 1f,
                        StatusId: "guarded",
                        DurationSeconds: 600f),
                },
            });

        var defaultRun = RunCompiledAllyVersusRaider(compiled, CombatStatusRules.Default);
        var strongGuard = new CombatStatusRules(
            CombatStatusRules.Default.StatusFamilies
                .ToDictionary(pair => pair.Key, pair => pair.Key == "guarded"
                    ? pair.Value with { IncomingDamageDelta = -0.6f }
                    : pair.Value, StringComparer.Ordinal),
            null,
            null);
        var strongRun = RunCompiledAllyVersusRaider(compiled, strongGuard);

        Assert.That(strongRun.AllySurvivedSteps, Is.GreaterThan(defaultRun.AllySurvivedSteps),
            "guarded 받는피해 delta를 콘텐츠 값(-0.6)으로 키우면 실 sim 생존이 뚜렷하게 늘어야 한다 — 숫자 콘텐츠화의 1차 가드");
    }

    [Test]
    public void AffixTemplateCompile_SameScenarioTwice_StableHash()
    {
        var first = CompileSingleHero(affixIds: new[] { "affix.tempo", "affix.cond_met", "affix.cond_unmet" });
        var second = CompileSingleHero(affixIds: new[] { "affix.tempo", "affix.cond_met", "affix.cond_unmet" });

        Assert.That(first.CompileHash, Is.EqualTo(second.CompileHash),
            "affix 템플릿(태그/조건/rule) 경로가 추가돼도 컴파일은 결정적이어야 한다");
    }

    private sealed record SimRun(string Stream, int AllySurvivedSteps);

    private static SimRun RunCompiledAllyVersusRaider(BattleLoadoutSnapshot compiled, CombatStatusRules? statusRules = null)
    {
        var enemy = CombatTestFactory.CreateUnit(
            "enemy.raider",
            race: "undead",
            classId: "duelist",
            anchor: DeploymentAnchorId.FrontBottom,
            hp: 500f,
            attack: 8f);
        var simulator = new BattleSimulator(
            CombatTestFactory.CreateBattleState(compiled.Allies, new[] { enemy }, seed: Seed, statusRules: statusRules),
            MaxSteps);

        var sb = new StringBuilder();
        var allySurvivedSteps = 0;
        var steps = 0;
        var guard = 0;
        while (!simulator.IsFinished && guard++ < 10000)
        {
            var step = simulator.Step();
            steps++;
            foreach (var unit in step.Units)
            {
                sb.Append(unit.Id).Append(':')
                    .Append(unit.CurrentHealth.ToString("R", CultureInfo.InvariantCulture)).Append(',')
                    .Append(unit.IsAlive ? '1' : '0').Append(';');
                // BattleFactory id 계약: ally_{index}_{loadoutId} (BattleFactory.cs:31)
                if (unit.Id == "ally_0_hero.warden" && unit.IsAlive)
                {
                    allySurvivedSteps = steps;
                }
            }

            sb.Append('\n');
        }

        return new SimRun(sb.ToString(), allySurvivedSteps);
    }

    private static BattleLoadoutSnapshot CompileSingleHero(
        IReadOnlyList<string> affixIds,
        Func<BattleSkillSpec, BattleSkillSpec>? mutatePassive = null,
        Func<BattleSkillSpec, BattleSkillSpec>? mutateCore = null,
        Func<BattleSkillSpec, BattleSkillSpec>? mutateSupport = null,
        IReadOnlyList<string>? itemIds = null)
    {
        itemIds ??= new[] { "item.shield" };
        var content = BuildContentSnapshot(mutatePassive, mutateCore, mutateSupport);
        var archetype = content.Archetypes["warden"];
        var heroes = new List<HeroRecord>
        {
            new("hero.warden", "hero.warden", archetype.Id, archetype.RaceId, archetype.ClassId, string.Empty, string.Empty),
        };
        var itemInstances = new Dictionary<string, ItemInstanceState>(StringComparer.Ordinal);
        var itemInstanceIds = new List<string>();
        for (var index = 0; index < itemIds.Count; index++)
        {
            var instanceId = $"hero.warden.item.{index}";
            // affix는 첫 아이템에만 부착(기존 시나리오 보존).
            itemInstances[instanceId] = new(instanceId, itemIds[index], index == 0 ? affixIds : Array.Empty<string>(), "hero.warden");
            itemInstanceIds.Add(instanceId);
        }

        var heroLoadouts = new Dictionary<string, HeroLoadoutState>(StringComparer.Ordinal)
        {
            ["hero.warden"] = new(
                "hero.warden",
                itemInstanceIds,
                Array.Empty<string>(),
                "board.vanguard",
                Array.Empty<string>(),
                Array.Empty<string>()),
        };
        var heroProgressions = new Dictionary<string, HeroProgressionState>(StringComparer.Ordinal)
        {
            ["hero.warden"] = new("hero.warden", 1, 0, Array.Empty<string>(), archetype.Skills.Select(skill => skill.Id).ToList()),
        };

        return new LoadoutCompiler().Compile(
            heroes,
            heroLoadouts,
            heroProgressions,
            itemInstances,
            new Dictionary<string, SkillInstanceState>(StringComparer.Ordinal),
            new Dictionary<string, PassiveBoardSelectionState>(StringComparer.Ordinal),
            new PermanentAugmentLoadoutState("bp.differential", Array.Empty<string>()),
            new SquadBlueprintState(
                "bp.differential",
                "bp.differential",
                TeamPostureType.StandardAdvance,
                "team_tactic_standard_advance",
                new Dictionary<DeploymentAnchorId, string> { [DeploymentAnchorId.FrontCenter] = "hero.warden" },
                new[] { "hero.warden" },
                new Dictionary<string, string>(StringComparer.Ordinal),
                null),
            new RunOverlayState(0, Array.Empty<string>(), Array.Empty<string>(), LoadoutCompiler.CurrentCompileVersion, string.Empty),
            content);
    }

    private static CombatContentSnapshot BuildContentSnapshot(
        Func<BattleSkillSpec, BattleSkillSpec>? mutatePassive = null,
        Func<BattleSkillSpec, BattleSkillSpec>? mutateCore = null,
        Func<BattleSkillSpec, BattleSkillSpec>? mutateSupport = null)
    {
        var baseRules = new[]
        {
            new TacticRule(0, TacticConditionType.Fallback, 0f, BattleActionType.WaitDefend, TargetSelectorType.Self),
        };
        var coreSkill = CreateSkill("skill.warden.core", CompiledSkillSlots.CoreActive, 4.5f);
        if (mutateCore != null)
        {
            coreSkill = mutateCore(coreSkill);
        }

        var passiveSkill = CreateSkill("skill.vanguard.passive", CompiledSkillSlots.Passive, 0f, SkillKind.Buff);
        if (mutatePassive != null)
        {
            passiveSkill = mutatePassive(passiveSkill);
        }

        var supportSkill = CreateSkill("skill.vanguard.support", CompiledSkillSlots.Support, 0f, SkillKind.Buff);
        if (mutateSupport != null)
        {
            supportSkill = mutateSupport(supportSkill);
        }

        var wardenSkills = new[]
        {
            coreSkill,
            CreateSkill("skill.warden.utility", CompiledSkillSlots.UtilityActive, 0f, SkillKind.Utility),
            passiveSkill,
            supportSkill,
        };

        return new CombatContentSnapshot(
            new Dictionary<string, CombatArchetypeTemplate>(StringComparer.Ordinal)
            {
                ["warden"] = new CombatArchetypeTemplate(
                    "warden",
                    "Warden",
                    "human",
                    "vanguard",
                    DeploymentAnchorId.FrontCenter,
                    CreateBaseStats(30f, 1f, 2f),
                    baseRules,
                    wardenSkills,
                    "anchor",
                    null,
                    null,
                    null,
                    0.25f,
                    1.2f,
                    new ManaEnvelope(0f, 0f, 0f)),
            },
            new Dictionary<string, CombatModifierPackage>(StringComparer.Ordinal),
            new Dictionary<string, CombatModifierPackage>(StringComparer.Ordinal)
            {
                ["item.shield"] = new CombatModifierPackage("item.shield", ModifierSource.Item, new[]
                {
                    new StatModifier(StatKey.MaxHealth, ModifierOp.Flat, 3f, ModifierSource.Item, "item.shield"),
                }),
                ["item.bow"] = new CombatModifierPackage("item.bow", ModifierSource.Item, new[]
                {
                    new StatModifier(StatKey.AttackRange, ModifierOp.Flat, 0.5f, ModifierSource.Item, "item.bow"),
                }),
            },
            new Dictionary<string, CombatModifierPackage>(StringComparer.Ordinal)
            {
                ["affix.hearty"] = new CombatModifierPackage("affix.hearty", ModifierSource.Item, new[]
                {
                    new StatModifier(StatKey.MaxHealth, ModifierOp.Flat, 200f, ModifierSource.Item, "affix.hearty"),
                }),
                ["affix.cond_unmet"] = new CombatModifierPackage("affix.cond_unmet", ModifierSource.Item, new[]
                {
                    new StatModifier(StatKey.MaxHealth, ModifierOp.Flat, 200f, ModifierSource.Item, "affix.cond_unmet"),
                }),
                ["affix.cond_met"] = new CombatModifierPackage("affix.cond_met", ModifierSource.Item, new[]
                {
                    new StatModifier(StatKey.MaxHealth, ModifierOp.Flat, 200f, ModifierSource.Item, "affix.cond_met"),
                }),
                ["affix.tempo"] = new CombatModifierPackage("affix.tempo", ModifierSource.Item, new[]
                {
                    new StatModifier(StatKey.AttackSpeed, ModifierOp.Increased, 0.06f, ModifierSource.Item, "affix.tempo"),
                }),
            },
            new Dictionary<string, CombatModifierPackage>(StringComparer.Ordinal),
            wardenSkills.ToDictionary(skill => skill.Id, skill => skill, StringComparer.Ordinal),
            new Dictionary<string, TeamTacticTemplate>(StringComparer.Ordinal)
            {
                ["team_tactic_standard_advance"] = new TeamTacticTemplate(
                    "team_tactic_standard_advance",
                    new TeamTacticProfile("team_tactic_standard_advance", "Standard", TeamPostureType.StandardAdvance, 1f, 0f, 0f, 0f, 0f, 0f)),
            },
            new Dictionary<string, RoleInstructionTemplate>(StringComparer.Ordinal),
            new Dictionary<string, PassiveNodeTemplate>(StringComparer.Ordinal),
            new Dictionary<string, AugmentCatalogEntry>(StringComparer.Ordinal),
            new Dictionary<string, SynergyTierTemplate>(StringComparer.Ordinal),
            new Dictionary<string, IReadOnlyList<BattleSkillSpec>>(StringComparer.Ordinal),
            ItemCatalog: new Dictionary<string, ItemTemplate>(StringComparer.Ordinal)
            {
                ["item.shield"] = new ItemTemplate("item.shield", Array.Empty<string>(), "shield"),
                ["item.bow"] = new ItemTemplate("item.bow", Array.Empty<string>(), "bow"),
            },
            AffixCatalog: new Dictionary<string, AffixTemplate>(StringComparer.Ordinal)
            {
                // rule affix: 태그 + rule package 보유(계약: EquipmentContentV1 BuildShaping 계열의 축소판)
                ["affix.tempo"] = new AffixTemplate(
                    "affix.tempo",
                    new[] { "tempo", "execute" },
                    Array.Empty<string>(),
                    Array.Empty<string>(),
                    new CombatRuleModifierPackage("affix.tempo", ModifierSource.Item, new[]
                    {
                        new RuleModifier(RuleModifierKind.BehaviorTag, "tempo"),
                        new RuleModifier(RuleModifierKind.BehaviorTag, "execute"),
                    })),
                // 조건부: warden(vanguard)이 절대 갖지 않는 태그를 요구 → 미충족
                ["affix.cond_unmet"] = new AffixTemplate(
                    "affix.cond_unmet",
                    new[] { "backline" },
                    new[] { "projectile" },
                    Array.Empty<string>(),
                    null),
                // 조건부: 클래스 raw 태그(vanguard)를 요구 → 충족
                ["affix.cond_met"] = new AffixTemplate(
                    "affix.cond_met",
                    Array.Empty<string>(),
                    new[] { "vanguard" },
                    Array.Empty<string>(),
                    null),
                // affix.hearty는 의도적으로 템플릿 없음 — 레거시(numeric-only) 경로 parity 확인
            });
    }

    private static BattleSkillSpec CreateSkill(string id, string slotKind, float power, SkillKind kind = SkillKind.Strike)
    {
        return new BattleSkillSpec(
            id,
            id,
            kind,
            power,
            1.5f,
            slotKind,
            Array.Empty<string>(),
            kind == SkillKind.Utility || kind == SkillKind.Buff ? DamageType.Healing : DamageType.Physical,
            power,
            1f,
            0f,
            kind == SkillKind.Buff ? 0.5f : 0f,
            0f,
            1.2f,
            0.2f,
            Array.Empty<string>(),
            0f,
            false,
            kind == SkillKind.Utility || kind == SkillKind.Buff ? SkillDelivery.Aura : SkillDelivery.Melee,
            kind == SkillKind.Buff ? SkillTargetRule.Self : SkillTargetRule.NearestEnemy,
            Array.Empty<string>(),
            Array.Empty<string>(),
            Array.Empty<string>());
    }

    private static Dictionary<StatKey, float> CreateBaseStats(float health, float armor, float attackSpeed)
    {
        return new Dictionary<StatKey, float>
        {
            [StatKey.MaxHealth] = health,
            [StatKey.Armor] = armor,
            [StatKey.AttackSpeed] = attackSpeed,
            [StatKey.AttackRange] = 1.5f,
            [StatKey.AttackWindup] = 0.2f,
            [StatKey.AttackCooldown] = 1f,
            [StatKey.LeashDistance] = 5f,
            [StatKey.TargetSwitchDelay] = 0.2f,
            [StatKey.PhysPower] = 4f,
        };
    }
}
