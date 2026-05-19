using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using SM.Combat.Model;
using SM.Core.Content;
using SM.Core.Contracts;
using SM.Meta.Model;
using SM.Tests.EditMode.Fakes;
using SM.Unity;
using SM.Unity.UI.Battle;
using UnityEngine;

namespace SM.Tests.EditMode;

[Category("BatchOnly")]
public sealed class BattleUnitDetailSheetV2Tests
{
    [Test]
    public void BuildSelectedUnitPanel_ProducesStructuredV2Sections()
    {
            var go = new GameObject("BattleUnitDetailSheetV2Tests");
        try
        {
            var localization = go.AddComponent<GameLocalizationController>();
            var formatter = new BattleUnitMetadataFormatter(localization, new FakeCombatContentLookup(snapshot: CreateContentSnapshot()));
            var unit = CreateUnit(
                "ally.dawn",
                DeploymentAnchorId.FrontCenter,
                new[] { "poison", "marked" });
            var teammate = CreateUnit(
                "ally.hexer",
                DeploymentAnchorId.BackCenter,
                System.Array.Empty<string>());
            var tactic = new TeamTacticProfile(
                "team_tactic_wide_spread",
                "Wide Spread",
                TeamPostureType.StandardAdvance,
                CombatPace: 1.1f,
                FocusModeBias: 0.2f,
                FrontSpacingBias: 0.4f,
                BackSpacingBias: 0.6f,
                ProtectCarryBias: 0.3f,
                TargetSwitchPenalty: 1.4f,
                Compactness: 0.25f,
                Width: 1.35f,
                Depth: 0.8f,
                LineSpacing: 1.2f,
                FlankBias: 0.5f);

            var selected = formatter.BuildSelectedUnitPanel(
                unit,
                isVisible: true,
                activeTab: BattleUnitDetailTab.Status,
                teamTactic: tactic,
                teamUnits: new[] { teammate, unit });

            Assert.That(selected.StatLines!.Select(line => line.Category).Distinct(), Is.SupersetOf(new[]
            {
                BattleStatLineCategory.Vital,
                BattleStatLineCategory.Combat,
                BattleStatLineCategory.Defense,
                BattleStatLineCategory.Resource,
                BattleStatLineCategory.Movement,
                BattleStatLineCategory.Targeting,
            }));
            var handLine = selected.StatLines!.Single(line => line.Label == "Dominant Hand");
            Assert.That(handLine.Value, Is.EqualTo("Left"));
            Assert.That(handLine.Tooltip, Does.Contain("does not change damage"));
            Assert.That(handLine.Tooltip, Does.Not.Contain("0."));
            Assert.That(selected.SkillSlots!.Count, Is.EqualTo(4));
            var signature = selected.SkillSlots.Single(slot => slot.SkillId == "skill_priest_core");
            Assert.That(signature.IsActiveSlot, Is.True);
            Assert.That(signature.IsSignatureSlot, Is.True);
            Assert.That(signature.HasResolvedDefinition, Is.True);
            Assert.That(signature.TimingText, Does.Contain("Cooldown 6"));
            Assert.That(signature.TimingText, Does.Contain("Windup 0.4"));
            Assert.That(signature.TimingText, Does.Contain("Energy 12"));
            Assert.That(signature.EffectSummary, Does.Contain("Lowest HP Ally"));
            Assert.That(signature.ScalingSummary, Does.Contain("Heal x1.2"));
            Assert.That(signature.Tags, Does.Contain("cleanse"));
            var flex = selected.SkillSlots.Single(slot => slot.SkillId == "skill_minor_heal");
            Assert.That(flex.IsFlexSlot, Is.True);
            Assert.That(flex.PresentationStyle, Is.EqualTo("heal"));
            Assert.That(selected.StatusEffects!.First().Section, Is.EqualTo(BattleStatusEffectSection.Permanent));
            Assert.That(selected.StatusEffects.Any(chip => chip.Section == BattleStatusEffectSection.BattleScoped && chip.StatusId == "marked"), Is.True);
            Assert.That(selected.StatusEffects.Any(chip => chip.StatusId == "windup" && chip.MaxDurationSeconds > 0f), Is.True);
            Assert.That(selected.StatusEffects.First(chip => chip.StatusId == "marked").CleanseText, Does.Contain("Cleanse"));
            Assert.That(selected.StatusEffects.First(chip => chip.StatusId == "windup").DurationText, Does.Contain("s /"));
            Assert.That(selected.StatusBody, Does.Contain("Battle Scoped"));
            Assert.That(selected.StatusBody, Does.Not.Contain("marked, poison"));
            Assert.That(selected.TacticSummary!.PresetName, Is.EqualTo("Wide Spread"));
            Assert.That(selected.TacticSummary.Dials.Count, Is.EqualTo(12));
            Assert.That(selected.TacticSummary.PriorityRules!, Does.Contain("P010 EnemyInRange -> BasicAttack / NearestEnemy threshold=0"));
            Assert.That(
                selected.TacticSummary.Dials.First(dial => dial.Label == "Compactness").NormalizedValue,
                Is.EqualTo(0.25f).Within(0.001f));
            Assert.That(selected.PositionSummary!.HomeAnchor, Is.EqualTo(DeploymentAnchorId.FrontCenter));
            Assert.That(
                selected.PositionSummary.TeammateAnchors,
                Is.EqualTo(new[] { DeploymentAnchorId.FrontCenter, DeploymentAnchorId.BackCenter }));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(go);
        }
    }

    private static CombatContentSnapshot CreateContentSnapshot()
    {
        var emptyPackages = new Dictionary<string, CombatModifierPackage>(StringComparer.Ordinal);
        return new CombatContentSnapshot(
            Archetypes: new Dictionary<string, CombatArchetypeTemplate>(StringComparer.Ordinal),
            TraitPackages: emptyPackages,
            ItemPackages: emptyPackages,
            AffixPackages: emptyPackages,
            AugmentPackages: emptyPackages,
            SkillCatalog: new Dictionary<string, BattleSkillSpec>(StringComparer.Ordinal)
            {
                ["skill_priest_core"] = new(
                    "skill_priest_core",
                    "Priest Core",
                    SkillKind.Heal,
                    18f,
                    4f,
                    DamageType: DamageType.Healing,
                    ManaCost: 12f,
                    BaseCooldownSeconds: 6f,
                    CastWindupSeconds: 0.4f,
                    HealCoeff: 1.2f,
                    Delivery: SkillDelivery.Aura,
                    TargetRule: SkillTargetRule.LowestHpAlly,
                    CompileTags: new[] { "cleanse", "healing" },
                    RequiredClassTags: new[] { "vanguard" },
                    ResolvedSlotKind: ActionSlotKind.SignatureActive,
                    PresentationProfile: new BattleSkillPresentationProfile(
                        SkillPresentationFamily.Heal,
                        SkillPresentationSkin.HealGold,
                        SkillPresentationGesture.SpellOmni,
                        SkillPresentationCueSequence.HealPulse)),
                ["skill_minor_heal"] = new(
                    "skill_minor_heal",
                    "Minor Heal",
                    SkillKind.Heal,
                    8f,
                    3f,
                    DamageType: DamageType.Healing,
                    BaseCooldownSeconds: 3f,
                    Delivery: SkillDelivery.Projectile,
                    TargetRule: SkillTargetRule.LowestHpAlly,
                    ResolvedSlotKind: ActionSlotKind.FlexActive,
                    PresentationProfile: new BattleSkillPresentationProfile(
                        SkillPresentationFamily.Heal,
                        SkillPresentationSkin.HealGold,
                        SkillPresentationGesture.SpellOmni,
                        SkillPresentationCueSequence.HealPulse)),
            },
            TeamTactics: new Dictionary<string, TeamTacticTemplate>(StringComparer.Ordinal),
            RoleInstructions: new Dictionary<string, RoleInstructionTemplate>(StringComparer.Ordinal),
            PassiveNodes: new Dictionary<string, PassiveNodeTemplate>(StringComparer.Ordinal),
            AugmentCatalog: new Dictionary<string, AugmentCatalogEntry>(StringComparer.Ordinal),
            SynergyCatalog: new Dictionary<string, SynergyTierTemplate>(StringComparer.Ordinal));
    }

    private static BattleUnitReadModel CreateUnit(
        string id,
        DeploymentAnchorId anchor,
        string[] statusIds)
    {
        return new BattleUnitReadModel(
            Id: id,
            Name: id,
            Side: TeamSide.Ally,
            Anchor: anchor,
            RaceId: "human",
            ClassId: "vanguard",
            Position: new CombatVector2(1.25f, -0.5f),
            CurrentHealth: 15f,
            MaxHealth: 24f,
            IsAlive: true,
            ActionState: CombatActionState.ExecuteAction,
            PendingActionType: BattleActionType.ActiveSkill,
            TargetId: "enemy.grey",
            TargetName: "Grey Fang",
            WindupProgress: 0.4f,
            CooldownRemaining: 1.2f,
            CurrentEnergy: 30f,
            MaxEnergy: 100f,
            IsDefending: false,
            Barrier: 5f,
            StatusIds: statusIds,
            PreferredRangeMin: 1.5f,
            PreferredRangeMax: 3.2f,
            CurrentSelector: "NearestEnemy",
            CurrentFallback: "KeepCurrent",
            RetargetLockRemaining: 0.5f,
            ArchetypeId: "dawn_priest",
            CharacterId: "hero_dawn_priest",
            RoleInstructionId: "frontline",
            RoleTag: "frontline",
            SignatureActiveId: "skill_priest_core",
            SignatureActiveName: "Priest Core",
            FlexActiveId: "skill_minor_heal",
            FlexActiveName: "Minor Heal",
            SignaturePassiveId: "support_purifying",
            SignaturePassiveName: "Purifying",
            FlexPassiveId: "support_brutal",
            FlexPassiveName: "Brutal",
            TacticRuleSummaries: new[] { "P010 EnemyInRange -> BasicAttack / NearestEnemy threshold=0" },
            DominantHand: DominantHand.Left);
    }
}
