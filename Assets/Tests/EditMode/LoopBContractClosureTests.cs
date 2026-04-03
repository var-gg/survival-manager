using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using SM.Combat.Model;
using SM.Content.Definitions;
using SM.Core.Contracts;
using SM.Editor.Validation;
using SM.Meta.Model;
using SM.Meta.Services;
using SM.Persistence.Abstractions.Models;
using SM.Unity;
using UnityEditor;
using UnityEngine;

namespace SM.Tests.EditMode;

[Category("BatchOnly")]
public sealed class LoopBContractClosureTests
{
    private const string TempRoot = "Assets/Resources/_Game/Content/Definitions/__LoopBContractTemp";

    [SetUp]
    public void SetUp()
    {
        DeleteTempRoot();
    }

    [TearDown]
    public void TearDown()
    {
        DeleteTempRoot();
    }

    [Test]
    public void ContentDefinitionValidator_FlagsLoopBRecruitmentViolations()
    {
        EnsureFolder(TempRoot);
        var assets = new List<ScriptableObject>();

        var race = CreateTempAsset<RaceDefinition>("race_loop_b.asset", asset =>
        {
            asset.Id = "loop_b_race";
            asset.NameKey = "content.race.loop_b.name";
        });
        assets.Add(race);

        var @class = CreateTempAsset<ClassDefinition>("class_loop_b.asset", asset =>
        {
            asset.Id = "vanguard";
            asset.NameKey = "content.class.loop_b.name";
        });
        assets.Add(@class);

        var traitPool = CreateTempAsset<TraitPoolDefinition>("trait_pool_loop_b.asset", asset =>
        {
            asset.Id = "loop_b_traits";
            asset.ArchetypeId = "loop_b_invalid";
            asset.PositiveTraits = new List<TraitEntry>
            {
                MakeTrait("loop_b_pos_1", "max_health", 1f),
                MakeTrait("loop_b_pos_2", "armor", 1f),
                MakeTrait("loop_b_pos_3", "attack_speed", 1f),
            };
            asset.NegativeTraits = new List<TraitEntry>
            {
                MakeTrait("loop_b_neg_1", "max_health", -1f),
                MakeTrait("loop_b_neg_2", "armor", -1f),
                MakeTrait("loop_b_neg_3", "attack_speed", -1f),
            };
        });
        assets.Add(traitPool);

        var mysticTag = CreateTempAsset<StableTagDefinition>("tag_mystic.asset", asset =>
        {
            asset.Id = "mystic";
            asset.NameKey = "content.tag.mystic.name";
        });
        assets.Add(mysticTag);

        var utility = CreateTempAsset<SkillDefinitionAsset>("skill_loop_b_utility.asset", asset =>
        {
            asset.Id = "skill_loop_b_utility";
            asset.NameKey = "content.skill.loop_b_utility.name";
            asset.DescriptionKey = "content.skill.loop_b_utility.desc";
            asset.Kind = SkillKindValue.Utility;
            asset.SlotKind = SkillSlotKindValue.UtilityActive;
            asset.DamageType = DamageTypeValue.Magical;
            asset.Delivery = SkillDeliveryValue.Projectile;
            asset.TargetRule = SkillTargetRuleValue.NearestEnemy;
            asset.RecruitNativeTags = new List<StableTagDefinition> { mysticTag };
            asset.MutuallyExclusiveGroupId = "loop_b_group";
        });
        assets.Add(utility);

        var passive = CreateTempAsset<SkillDefinitionAsset>("skill_loop_b_support.asset", asset =>
        {
            asset.Id = "skill_loop_b_support";
            asset.NameKey = "content.skill.loop_b_support.name";
            asset.DescriptionKey = "content.skill.loop_b_support.desc";
            asset.Kind = SkillKindValue.Buff;
            asset.SlotKind = SkillSlotKindValue.Support;
            asset.DamageType = DamageTypeValue.Magical;
            asset.Delivery = SkillDeliveryValue.Aura;
            asset.TargetRule = SkillTargetRuleValue.Self;
            asset.RecruitNativeTags = new List<StableTagDefinition> { mysticTag };
            asset.MutuallyExclusiveGroupId = "loop_b_group";
        });
        assets.Add(passive);

        assets.Add(CreateTempAsset<UnitArchetypeDefinition>("archetype_loop_b_invalid.asset", asset =>
        {
            asset.Id = "loop_b_invalid";
            asset.NameKey = "content.archetype.loop_b_invalid.name";
            asset.Race = race;
            asset.Class = @class;
            asset.TraitPool = traitPool;
            asset.IsRecruitable = true;
            asset.IsSummonOnly = true;
            asset.Skills = new List<SkillDefinitionAsset> { utility, passive };
            asset.FlexUtilitySkillPool = new List<SkillDefinitionAsset> { utility };
            asset.FlexSupportSkillPool = new List<SkillDefinitionAsset> { passive };
            asset.RecruitBannedPairings = new List<RecruitBannedPairingDefinition>
            {
                new() { FlexActiveId = utility.Id, FlexPassiveId = utility.Id },
            };
        }));

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);

        var report = ContentDefinitionValidator.BuildValidationReport(assets);
        var codes = report.Issues.Select(issue => issue.Code).ToHashSet(StringComparer.Ordinal);

        Assert.That(codes, Contains.Item("loop_b.recruit.pool_flags"));
        Assert.That(codes, Contains.Item("loop_b.recruit.banned_pairing"));
        Assert.That(codes, Contains.Item("loop_b.recruit.native_coherence"));
    }

    [Test]
    public void RecruitPackGenerator_GuaranteesOnPlanSlotAndPreventsDuplicatesAcrossSimulation()
    {
        var lookup = new RuntimeCombatContentLookup();
        var snapshot = lookup.Snapshot;
        var session = CreateBoundSession(lookup);
        var roster = ToHeroRecords(session.Profile.Heroes);
        var pity = new RecruitPityState();
        var phase = new RecruitPhaseState();

        for (var seed = 1; seed <= 1000; seed++)
        {
            var pool = RecruitmentTemplateResolver.GetRecruitPool(snapshot.Archetypes, roster);
            var plan = TeamPlanEvaluator.Evaluate(roster, snapshot.Archetypes, snapshot, Array.Empty<string>(), Array.Empty<string>());
            var evaluations = pool
                .Select(template => RecruitCandidateScoringService.Evaluate(template, plan, phase.PendingScoutDirective, roster))
                .ToList();
            var onPlanExists = evaluations.Any(evaluation => evaluation.PlanFit == CandidatePlanFit.OnPlan);

            var result = RecruitPackGenerator.GeneratePack(
                snapshot.Archetypes,
                snapshot,
                roster,
                Array.Empty<string>(),
                Array.Empty<string>(),
                pity,
                phase,
                seed);

            Assert.That(result.Offers.Count, Is.EqualTo(4));
            Assert.That(result.Offers.Select(offer => offer.UnitBlueprintId).Distinct(StringComparer.Ordinal).Count(), Is.EqualTo(4));
            if (onPlanExists)
            {
                Assert.That(result.Offers.Single(offer => offer.Metadata.SlotType == RecruitOfferSlotType.OnPlan).Metadata.PlanFit, Is.EqualTo(CandidatePlanFit.OnPlan));
            }

            pity = result.UpdatedPity;
            phase = result.UpdatedPhase;
        }
    }

    [Test]
    public void RecruitPackGenerator_ProtectedSlotHonorsRareAndEpicPityFloors()
    {
        var lookup = new RuntimeCombatContentLookup();
        var snapshot = lookup.Snapshot;
        var session = CreateBoundSession(lookup);
        var roster = ToHeroRecords(session.Profile.Heroes);

        var rareFloorPack = RecruitPackGenerator.GeneratePack(
            snapshot.Archetypes,
            snapshot,
            roster,
            Array.Empty<string>(),
            Array.Empty<string>(),
            new RecruitPityState { PacksSinceRarePlusSeen = 3, PacksSinceEpicSeen = 0 },
            new RecruitPhaseState(),
            41);
        var rareProtected = rareFloorPack.Offers.Single(offer => offer.Metadata.SlotType == RecruitOfferSlotType.Protected);

        var epicFloorPack = RecruitPackGenerator.GeneratePack(
            snapshot.Archetypes,
            snapshot,
            roster,
            Array.Empty<string>(),
            Array.Empty<string>(),
            new RecruitPityState { PacksSinceRarePlusSeen = 8, PacksSinceEpicSeen = 8 },
            new RecruitPhaseState(),
            87);
        var epicProtected = epicFloorPack.Offers.Single(offer => offer.Metadata.SlotType == RecruitOfferSlotType.Protected);

        Assert.That(rareProtected.Metadata.Tier >= RecruitTier.Rare, Is.True);
        Assert.That(rareProtected.Metadata.ProtectedByPity, Is.True);
        Assert.That(epicProtected.Metadata.Tier >= RecruitTier.Epic, Is.True);
        Assert.That(epicProtected.Metadata.ProtectedByPity, Is.True);
    }

    [Test]
    public void RetrainService_BlocksImmediateRecurrence_AndPityForcesPlanCoherentResult()
    {
        var lookup = new RuntimeCombatContentLookup();
        var template = lookup.Snapshot.Archetypes["hunter"];
        var plan = new TeamPlanProfile
        {
            TopSynergyTagIds = new List<string> { "mark" },
            PrefersPhysical = true,
            NeedsBackline = true,
        };

        var currentActive = template.RecruitFlexActivePool![0].Id;
        var currentPassive = template.RecruitFlexPassivePool![0].Id;
        var state = new UnitRetrainState();

        for (var index = 0; index < 500; index++)
        {
            var result = RetrainService.Retrain(
                template,
                currentActive,
                currentPassive,
                RetrainOperationKind.FullRetrain,
                state,
                plan,
                RecruitmentBalanceCatalog.DefaultRetrainCosts,
                index + 1);

            Assert.That(result.FlexActiveId, Is.Not.EqualTo(currentActive));
            Assert.That(result.FlexPassiveId, Is.Not.EqualTo(currentPassive));
            Assert.That(result.FlexActiveId, Is.Not.EqualTo(state.PreviousFlexActiveId));
            Assert.That(result.FlexPassiveId, Is.Not.EqualTo(state.PreviousFlexPassiveId));

            var active = template.RecruitFlexActivePool.First(skill => skill.Id == result.FlexActiveId);
            var passive = template.RecruitFlexPassivePool.First(skill => skill.Id == result.FlexPassiveId);
            Assert.That(
                RecruitmentTemplateResolver.IsNativeCoherent(template, active)
                || RecruitmentTemplateResolver.IsNativeCoherent(template, passive),
                Is.True);

            currentActive = result.FlexActiveId;
            currentPassive = result.FlexPassiveId;
            state = result.RetrainState;
        }

        var pityState = new UnitRetrainState
        {
            RetrainCount = 2,
            PreviousFlexActiveId = currentActive,
            PreviousFlexPassiveId = currentPassive,
            ConsecutivePlanIncoherentRetrains = 2,
        };
        var pityResult = RetrainService.Retrain(
            template,
            currentActive,
            currentPassive,
            RetrainOperationKind.FullRetrain,
            pityState,
            plan,
            RecruitmentBalanceCatalog.DefaultRetrainCosts,
            9001);

        Assert.That(pityResult.UsedPity, Is.True);
        Assert.That(pityResult.IsPlanCoherent, Is.True);
    }

    [Test]
    public void GameSessionState_DuplicateGrantScoutRetrainAndDismissHonorLoopBEconomy()
    {
        var lookup = new RuntimeCombatContentLookup();
        var session = CreateBoundSession(lookup);
        var duplicateHero = session.Profile.Heroes[0];
        var duplicateTier = lookup.Snapshot.Archetypes[duplicateHero.ArchetypeId].RecruitTier;
        var expectedDuplicateEcho = RecruitmentBalanceCatalog.DefaultDuplicateEchoValues.GetValue(duplicateTier);
        var heroCountBefore = session.Profile.Heroes.Count;
        var echoBeforeDuplicate = session.Profile.Currencies.Echo;

        for (var index = 0; index < 100; index++)
        {
            var result = session.GrantHeroDirect(duplicateHero.ArchetypeId, RecruitOfferSource.DirectGrant);
            Assert.That(result.IsSuccess, Is.True, result.Error);
        }

        Assert.That(session.Profile.Heroes.Count, Is.EqualTo(heroCountBefore));
        Assert.That(session.Profile.Currencies.Echo, Is.EqualTo(echoBeforeDuplicate + (expectedDuplicateEcho * 100)));

        var scoutEchoBefore = session.Profile.Currencies.Echo;
        var scoutResult = session.UseScout(new ScoutDirective { Kind = ScoutDirectiveKind.Backline });
        Assert.That(scoutResult.IsSuccess, Is.True, scoutResult.Error);
        Assert.That(session.Profile.Currencies.Echo, Is.EqualTo(scoutEchoBefore - RecruitmentBalanceCatalog.ScoutEchoCost));
        Assert.That(session.RecruitPhase.ScoutUsedThisPhase, Is.True);
        Assert.That(session.RecruitPhase.PendingScoutDirective.Kind, Is.EqualTo(ScoutDirectiveKind.Backline));

        var retrainHero = session.Profile.Heroes[1];
        var goldBeforeRetrain = session.Profile.Currencies.Gold;
        var retrainEchoBefore = session.Profile.Currencies.Echo;
        var retrainStateBefore = retrainHero.RetrainState?.Clone() ?? new UnitRetrainState();
        var retrainCost = RecruitmentBalanceCatalog.DefaultRetrainCosts.GetTotalCost(RetrainOperationKind.FullRetrain, retrainStateBefore);
        var retrainResult = session.RetrainHero(retrainHero.HeroId, RetrainOperationKind.FullRetrain);
        Assert.That(retrainResult.IsSuccess, Is.True, retrainResult.Error);
        Assert.That(session.Profile.Currencies.Gold, Is.EqualTo(goldBeforeRetrain));
        Assert.That(session.Profile.Currencies.Echo, Is.EqualTo(retrainEchoBefore - retrainCost));
        Assert.That(retrainHero.EconomyFootprint.RetrainEchoPaid, Is.GreaterThanOrEqualTo(retrainCost));

        var dismissHero = session.Profile.Heroes[2];
        dismissHero.EconomyFootprint = new UnitEconomyFootprint
        {
            RecruitGoldPaid = 10,
            RetrainEchoPaid = 20,
        };
        var equippedItemIds = dismissHero.EquippedItemIds.ToList();
        var goldBeforeDismiss = session.Profile.Currencies.Gold;
        var echoBeforeDismiss = session.Profile.Currencies.Echo;
        var dismissResult = session.DismissHero(dismissHero.HeroId);
        Assert.That(dismissResult.IsSuccess, Is.True, dismissResult.Error);
        Assert.That(session.Profile.Currencies.Gold, Is.EqualTo(goldBeforeDismiss + 5));
        Assert.That(session.Profile.Currencies.Echo, Is.EqualTo(echoBeforeDismiss + 10));
        Assert.That(session.Profile.Heroes.Any(hero => hero.HeroId == dismissHero.HeroId), Is.False);
        Assert.That(session.Profile.Inventory.Where(item => equippedItemIds.Contains(item.ItemInstanceId)).All(item => string.IsNullOrWhiteSpace(item.EquippedHeroId)), Is.True);
    }

    [Test]
    public void GameSessionState_TownOnlyEconomyCommandsRejectOutsideTown()
    {
        var session = CreateBoundSession(new RuntimeCombatContentLookup());
        session.SetCurrentScene(SceneNames.Expedition);

        Assert.That(session.RerollRecruitOffers().IsSuccess, Is.False);
        Assert.That(session.Recruit(0).IsSuccess, Is.False);
        Assert.That(session.UseScout(new ScoutDirective { Kind = ScoutDirectiveKind.Support }).IsSuccess, Is.False);
        Assert.That(session.RetrainHero(session.Profile.Heroes[0].HeroId, RetrainOperationKind.RerollFlexActive).IsSuccess, Is.False);
        Assert.That(session.DismissHero(session.Profile.Heroes[0].HeroId).IsSuccess, Is.False);
    }

    private static GameSessionState CreateBoundSession(RuntimeCombatContentLookup lookup)
    {
        var session = new GameSessionState(lookup);
        session.BindProfile(new SaveProfile());
        session.SetCurrentScene(SceneNames.Town);
        return session;
    }

    private static List<HeroRecord> ToHeroRecords(IEnumerable<HeroInstanceRecord> heroes)
    {
        return heroes.Select(hero => new HeroRecord(
            hero.HeroId,
            hero.Name,
            hero.ArchetypeId,
            hero.RaceId,
            hero.ClassId,
            hero.PositiveTraitId,
            hero.NegativeTraitId,
            hero.FlexActiveId,
            hero.FlexPassiveId,
            hero.RecruitTier,
            hero.RecruitSource,
            hero.RetrainState?.Clone() ?? new UnitRetrainState(),
            hero.EconomyFootprint?.Clone() ?? new UnitEconomyFootprint()))
            .ToList();
    }

    private static TraitEntry MakeTrait(string id, string statId, float value)
    {
        return new TraitEntry
        {
            Id = id,
            NameKey = $"content.trait.{id}.name",
            DescriptionKey = $"content.trait.{id}.desc",
            Modifiers = new List<SerializableStatModifier>
            {
                new() { StatId = statId, Value = value }
            }
        };
    }

    private static T CreateTempAsset<T>(string fileName, Action<T> configure) where T : ScriptableObject
    {
        var path = $"{TempRoot}/{fileName}";
        var asset = ScriptableObject.CreateInstance<T>();
        configure(asset);
        AssetDatabase.CreateAsset(asset, path);
        EditorUtility.SetDirty(asset);
        return asset;
    }

    private static void EnsureFolder(string folder)
    {
        if (AssetDatabase.IsValidFolder(folder))
        {
            return;
        }

        var parent = Path.GetDirectoryName(folder)!.Replace('\\', '/');
        if (!AssetDatabase.IsValidFolder(parent))
        {
            EnsureFolder(parent);
        }

        AssetDatabase.CreateFolder(parent, Path.GetFileName(folder));
    }

    private static void DeleteTempRoot()
    {
        if (!AssetDatabase.IsValidFolder(TempRoot))
        {
            return;
        }

        AssetDatabase.DeleteAsset(TempRoot);
        AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
    }
}
