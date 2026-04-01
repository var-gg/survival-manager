using System;
using System.Collections.Generic;
using System.Linq;
using SM.Content.Definitions;

namespace SM.Editor.Validation;

internal static class LoopBContractValidator
{
    public static void ValidateArchetype(UnitArchetypeDefinition archetype, string assetPath, ICollection<ContentValidationIssue> issues)
    {
        if (!archetype.IsRecruitable)
        {
            return;
        }

        if (archetype.IsSummonOnly || archetype.IsEventOnly || archetype.IsBossOnly || archetype.IsUnreleased || archetype.IsTestOnly)
        {
            AddError(issues, "loop_b.recruit.pool_flags", "Recruitable archetypes cannot be summon-only, event-only, boss-only, unreleased, or test-only.", assetPath);
        }

        var activePool = ResolveRecruitPool(archetype.RecruitFlexActivePool, archetype.FlexUtilitySkillPool, SkillSlotKindValue.UtilityActive);
        var passivePool = ResolveRecruitPool(archetype.RecruitFlexPassivePool, archetype.FlexSupportSkillPool, SkillSlotKindValue.Support);
        if (activePool.Count == 0 || passivePool.Count == 0)
        {
            AddError(issues, "loop_b.recruit.preview_pool", "Recruitable archetypes must expose flex active/passive pools for recruit previews.", assetPath);
            return;
        }

        var bannedPairs = new HashSet<string>(StringComparer.Ordinal);
        foreach (var pairing in archetype.RecruitBannedPairings ?? new List<RecruitBannedPairingDefinition>())
        {
            if (pairing == null
                || string.IsNullOrWhiteSpace(pairing.FlexActiveId)
                || string.IsNullOrWhiteSpace(pairing.FlexPassiveId))
            {
                AddError(issues, "loop_b.recruit.banned_pairing", "Recruit banned pairings must define both flex active and flex passive ids.", assetPath);
                continue;
            }

            if (string.Equals(pairing.FlexActiveId, pairing.FlexPassiveId, StringComparison.Ordinal))
            {
                AddError(issues, "loop_b.recruit.banned_pairing", "Recruit banned pairings cannot self-conflict on the same id.", assetPath);
            }

            var pairKey = $"{pairing.FlexActiveId}::{pairing.FlexPassiveId}";
            if (!bannedPairs.Add(pairKey))
            {
                AddError(issues, "loop_b.recruit.banned_pairing", $"Recruit banned pairing '{pairKey}' is duplicated.", assetPath);
            }
        }

        if (!HasValidRecruitPreview(archetype, activePool, passivePool))
        {
            AddError(issues, "loop_b.recruit.native_coherence", "Recruitable archetype cannot generate a native-coherent flex preview pair.", assetPath);
        }
    }

    private static List<SkillDefinitionAsset> ResolveRecruitPool(
        IEnumerable<SkillDefinitionAsset> primary,
        IEnumerable<SkillDefinitionAsset> legacy,
        SkillSlotKindValue expectedSlot)
    {
        var resolved = (primary ?? Array.Empty<SkillDefinitionAsset>())
            .Where(skill => skill != null)
            .ToList();
        if (resolved.Count == 0)
        {
            resolved = (legacy ?? Array.Empty<SkillDefinitionAsset>())
                .Where(skill => skill != null)
                .ToList();
        }

        return resolved
            .Where(skill => skill.SlotKind == expectedSlot)
            .Distinct()
            .ToList();
    }

    private static bool HasValidRecruitPreview(
        UnitArchetypeDefinition archetype,
        IReadOnlyList<SkillDefinitionAsset> activePool,
        IReadOnlyList<SkillDefinitionAsset> passivePool)
    {
        var nativeTags = ResolveArchetypeNativeTags(archetype);
        var signatureActiveFamily = archetype.Loadout?.SignatureActive?.EffectFamilyId
            ?? archetype.LockedSignatureActiveSkill?.EffectFamilyId
            ?? string.Empty;
        var signaturePassiveFamily = archetype.Loadout?.SignaturePassive?.EffectFamilyId
            ?? archetype.LockedSignaturePassiveSkill?.EffectFamilyId
            ?? string.Empty;

        foreach (var active in activePool)
        {
            foreach (var passive in passivePool)
            {
                if (IsBannedPairing(archetype, active.Id, passive.Id))
                {
                    continue;
                }

                if (!string.IsNullOrWhiteSpace(active.MutuallyExclusiveGroupId)
                    && string.Equals(active.MutuallyExclusiveGroupId, passive.MutuallyExclusiveGroupId, StringComparison.Ordinal))
                {
                    continue;
                }

                if (HasSameFamilyConflict(active.EffectFamilyId, signatureActiveFamily, signaturePassiveFamily)
                    || HasSameFamilyConflict(passive.EffectFamilyId, signatureActiveFamily, signaturePassiveFamily))
                {
                    continue;
                }

                var activeNative = ResolveSkillNativeTags(active, archetype);
                var passiveNative = ResolveSkillNativeTags(passive, archetype);
                if (!activeNative.Overlaps(nativeTags) && !passiveNative.Overlaps(nativeTags))
                {
                    continue;
                }

                return true;
            }
        }

        return false;
    }

    private static bool IsBannedPairing(UnitArchetypeDefinition archetype, string activeId, string passiveId)
    {
        return (archetype.RecruitBannedPairings ?? new List<RecruitBannedPairingDefinition>())
            .Any(pairing =>
                pairing != null
                && string.Equals(pairing.FlexActiveId, activeId, StringComparison.Ordinal)
                && string.Equals(pairing.FlexPassiveId, passiveId, StringComparison.Ordinal));
    }

    private static bool HasSameFamilyConflict(string familyId, string signatureActiveFamily, string signaturePassiveFamily)
    {
        return !string.IsNullOrWhiteSpace(familyId)
               && (string.Equals(familyId, signatureActiveFamily, StringComparison.Ordinal)
                   || string.Equals(familyId, signaturePassiveFamily, StringComparison.Ordinal));
    }

    private static HashSet<string> ResolveArchetypeNativeTags(UnitArchetypeDefinition archetype)
    {
        var tags = new HashSet<string>(StringComparer.Ordinal)
        {
            archetype.Race?.Id ?? string.Empty,
            archetype.Class?.Id ?? string.Empty,
            archetype.RoleTag ?? string.Empty,
        };

        if (archetype.Class != null)
        {
            switch (archetype.Class.Id)
            {
                case "vanguard":
                case "duelist":
                    tags.Add("frontline");
                    break;
                case "ranger":
                case "mystic":
                    tags.Add("backline");
                    break;
            }
        }

        if (archetype.BaseHealPower > 0f || string.Equals(archetype.RoleTag, "support", StringComparison.Ordinal))
        {
            tags.Add("support");
        }

        if (archetype.BaseMagPower > archetype.BasePhysPower)
        {
            tags.Add("magical");
        }
        else if (archetype.BasePhysPower > 0f)
        {
            tags.Add("physical");
        }

        return new HashSet<string>(tags.Where(tag => !string.IsNullOrWhiteSpace(tag)), StringComparer.Ordinal);
    }

    private static HashSet<string> ResolveSkillNativeTags(SkillDefinitionAsset skill, UnitArchetypeDefinition archetype)
    {
        var tags = new HashSet<string>(
            (skill.RecruitNativeTags ?? new List<StableTagDefinition>())
            .Where(tag => tag != null && !string.IsNullOrWhiteSpace(tag.Id))
            .Select(tag => tag.Id),
            StringComparer.Ordinal);

        if (tags.Count > 0)
        {
            return tags;
        }

        foreach (var classTag in (skill.RequiredClassTags ?? new List<StableTagDefinition>())
                     .Where(tag => tag != null && !string.IsNullOrWhiteSpace(tag.Id))
                     .Select(tag => tag.Id))
        {
            tags.Add(classTag);
            if (string.Equals(classTag, "vanguard", StringComparison.Ordinal) || string.Equals(classTag, "duelist", StringComparison.Ordinal))
            {
                tags.Add("frontline");
            }
            else if (string.Equals(classTag, "ranger", StringComparison.Ordinal) || string.Equals(classTag, "mystic", StringComparison.Ordinal))
            {
                tags.Add("backline");
            }
        }

        if (skill.DamageType == DamageTypeValue.Healing || skill.Kind == SkillKindValue.Heal)
        {
            tags.Add("support");
            tags.Add("heal");
        }
        else if (skill.DamageType == DamageTypeValue.Magical)
        {
            tags.Add("magical");
        }
        else if (skill.DamageType == DamageTypeValue.Physical || skill.DamageType == DamageTypeValue.True)
        {
            tags.Add("physical");
        }

        if (tags.Count == 0)
        {
            tags.UnionWith(ResolveArchetypeNativeTags(archetype));
        }

        return tags;
    }

    private static void AddError(ICollection<ContentValidationIssue> issues, string code, string message, string assetPath)
    {
        issues.Add(new ContentValidationIssue(ContentValidationSeverity.Error, code, message, assetPath));
    }
}
