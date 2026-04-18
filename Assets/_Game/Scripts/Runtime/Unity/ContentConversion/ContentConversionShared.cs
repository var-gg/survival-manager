using System;
using System.Collections.Generic;
using System.Linq;
using SM.Combat.Model;
using SM.Content.Definitions;
using SM.Core.Content;
using SM.Core.Contracts;
using SM.Core.Stats;
using SM.Meta.Model;

namespace SM.Unity.ContentConversion;

internal static class ContentConversionShared
{
    internal static ContentGovernanceSummary? BuildGovernanceSummary(BudgetCard? budgetCard)
    {
        if (budgetCard == null)
        {
            return null;
        }

        return new ContentGovernanceSummary(
            budgetCard.Rarity.ToString(),
            budgetCard.PowerBand.ToString(),
            budgetCard.RoleProfile.ToString(),
            budgetCard.Vector?.FinalScore ?? 0,
            budgetCard.DeclaredThreatPatterns?.Select(pattern => pattern.ToString()).ToList() ?? new List<string>(),
            budgetCard.DeclaredCounterTools?.Select(tool => new CompiledCounterToolContribution(tool.Tool.ToString(), (int)tool.Strength)).ToList()
                ?? new List<CompiledCounterToolContribution>(),
            budgetCard.DeclaredFeatureFlags.ToString());
    }

    internal static StatModifier BuildStatModifier(SerializableStatModifier modifier, ModifierSource source, string sourceId)
    {
        return new StatModifier(
            ToStatKey(modifier.StatId),
            modifier.Op,
            modifier.Value,
            source,
            sourceId);
    }

    internal static StatKey ToStatKey(string statId)
    {
        if (StatKey.TryResolve(statId, out var key))
        {
            return key;
        }

        throw new InvalidOperationException($"알 수 없는 StatId '{statId}'");
    }

    internal static Dictionary<StatKey, float> BuildBaseStats(UnitArchetypeDefinition definition)
    {
        return new Dictionary<StatKey, float>
        {
            [StatKey.MaxHealth] = definition.BaseMaxHealth,
            [StatKey.Armor] = PreferPrimaryOrFallback(definition.BaseArmor, definition.BaseDefense),
            [StatKey.Resist] = definition.BaseResist,
            [StatKey.BarrierPower] = definition.BaseBarrierPower,
            [StatKey.Tenacity] = definition.BaseTenacity,
            [StatKey.HealPower] = definition.BaseHealPower,
            [StatKey.PhysPower] = PreferPrimaryOrFallback(definition.BasePhysPower, definition.BaseAttack),
            [StatKey.MagPower] = definition.BaseMagPower,
            [StatKey.AttackSpeed] = PreferPrimaryOrFallback(definition.BaseAttackSpeed, definition.BaseSpeed),
            [StatKey.MoveSpeed] = definition.BaseMoveSpeed,
            [StatKey.AttackRange] = definition.BaseAttackRange,
            [StatKey.SkillHaste] = PreferPrimaryOrFallback(definition.BaseSkillHaste, definition.BaseCooldownRecovery),
            [StatKey.ManaMax] = definition.BaseManaMax,
            [StatKey.ManaGainOnAttack] = definition.BaseManaGainOnAttack,
            [StatKey.ManaGainOnHit] = definition.BaseManaGainOnHit,
            [StatKey.CooldownRecovery] = definition.BaseCooldownRecovery,
            [StatKey.CritChance] = definition.BaseCritChance,
            [StatKey.CritMultiplier] = definition.BaseCritMultiplier,
            [StatKey.PhysPen] = definition.BasePhysPen,
            [StatKey.MagPen] = definition.BaseMagPen,
            [StatKey.AggroRadius] = definition.BaseAggroRadius,
            [StatKey.LeashDistance] = definition.BaseLeashDistance,
            [StatKey.TargetSwitchDelay] = definition.BaseTargetSwitchDelay,
            [StatKey.PreferredDistance] = PreferPrimaryOrFallback(definition.BasePreferredDistance, definition.BaseAttackRange),
            [StatKey.ProtectRadius] = definition.BaseProtectRadius,
            [StatKey.AttackWindup] = definition.BaseAttackWindup,
            [StatKey.CastWindup] = definition.BaseCastWindup,
            [StatKey.ProjectileSpeed] = definition.BaseProjectileSpeed,
            [StatKey.CollisionRadius] = definition.BaseCollisionRadius,
            [StatKey.RepositionCooldown] = definition.BaseRepositionCooldown,
            [StatKey.AttackCooldown] = definition.BaseAttackCooldown,
        };
    }

    internal static float PreferPrimaryOrFallback(float primary, float fallback)
    {
        return primary != 0f ? primary : fallback;
    }

    internal static List<string> ExtractTagIds(IEnumerable<StableTagDefinition> tags)
    {
        return Enumerate(tags)
            .Where(tag => tag != null && !string.IsNullOrWhiteSpace(tag.Id))
            .Select(tag => tag.Id)
            .ToList();
    }

    internal static IEnumerable<T> Enumerate<T>(IEnumerable<T> values)
    {
        return values ?? Array.Empty<T>();
    }

    internal static T BuildSection<T>(string label, Func<T> factory)
    {
        try
        {
            return factory();
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to build {label}.", ex);
        }
    }

    internal static string ResolveLegacyName(string nameKey, string legacyDisplayName, string fallback)
    {
        if (!string.IsNullOrWhiteSpace(legacyDisplayName))
        {
            return legacyDisplayName;
        }

        if (!string.IsNullOrWhiteSpace(nameKey))
        {
            return nameKey;
        }

        return fallback;
    }

    internal static string ResolveTraitPoolArchetypeId(TraitPoolDefinition traitPool)
    {
        if (!string.IsNullOrWhiteSpace(traitPool.ArchetypeId))
        {
            return traitPool.ArchetypeId;
        }

        if (!string.IsNullOrWhiteSpace(traitPool.Id) && traitPool.Id.StartsWith("traitpool_", StringComparison.Ordinal))
        {
            return traitPool.Id["traitpool_".Length..];
        }

        if (!string.IsNullOrWhiteSpace(traitPool.name) && traitPool.name.StartsWith("traitpool_", StringComparison.Ordinal))
        {
            return traitPool.name["traitpool_".Length..];
        }

        return string.Empty;
    }

    internal static CombatRuleModifierPackage? BuildRulePackage(
        string sourceId,
        ModifierSource source,
        IEnumerable<StableTagDefinition> tags)
    {
        var modifiers = tags
            .Where(tag => tag != null && !string.IsNullOrWhiteSpace(tag.Id))
            .Select(tag => new RuleModifier(RuleModifierKind.BehaviorTag, tag.Id))
            .ToList();
        return modifiers.Count == 0
            ? null
            : new CombatRuleModifierPackage(sourceId, source, modifiers);
    }
}
