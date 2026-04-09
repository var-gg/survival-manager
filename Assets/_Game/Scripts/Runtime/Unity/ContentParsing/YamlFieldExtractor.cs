using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Reflection;
using SM.Content.Definitions;
using SM.Core.Contracts;
using SM.Core.Stats;
using UnityEngine;

namespace SM.Unity.ContentParsing;

internal static class YamlFieldExtractor
{
    internal static readonly Regex GuidRegex = new(@"guid:\s*([0-9a-fA-F]+)", RegexOptions.Compiled);

    internal static string ExtractLine(string[] lines, string key)
    {
        return lines.FirstOrDefault(line => line.TrimStart().StartsWith(key, StringComparison.Ordinal)) ?? string.Empty;
    }

    internal static string ExtractValue(string[] lines, string key)
    {
        var line = ExtractLine(lines, key);
        if (string.IsNullOrWhiteSpace(line))
        {
            return string.Empty;
        }

        return line.TrimStart()[key.Length..].Trim();
    }

    internal static int ExtractInt(string[] lines, string key)
    {
        return ParseInt(ExtractValue(lines, key));
    }

    internal static float ExtractFloat(string[] lines, string key)
    {
        return ParseFloat(ExtractValue(lines, key));
    }

    internal static bool ExtractBool(string[] lines, string key)
    {
        return ParseBool(ExtractValue(lines, key));
    }

    internal static string[] ExtractSection(string[] lines, string sectionHeader, int sectionIndent)
    {
        var index = FindLineIndex(lines, sectionHeader);
        if (index < 0)
        {
            return Array.Empty<string>();
        }

        var collected = new List<string> { lines[index] };
        for (var lineIndex = index + 1; lineIndex < lines.Length; lineIndex++)
        {
            var line = lines[lineIndex];
            var trimmed = line.Trim();
            if (GetIndent(line) <= sectionIndent && !string.IsNullOrWhiteSpace(trimmed))
            {
                break;
            }

            collected.Add(line);
        }

        return collected.ToArray();
    }

    internal static string ExtractDefinitionId(UnityEngine.Object definition)
    {
        return definition switch
        {
            StatDefinition stat => stat.Id,
            RaceDefinition race => race.Id,
            ClassDefinition @class => @class.Id,
            CharacterDefinition character => character.Id,
            SkillDefinitionAsset skill => skill.Id,
            TraitPoolDefinition traitPool => traitPool.Id,
            ItemBaseDefinition item => item.Id,
            AffixDefinition affix => affix.Id,
            AugmentDefinition augment => augment.Id,
            StableTagDefinition stableTag => stableTag.Id,
            TeamTacticDefinition teamTactic => teamTactic.Id,
            RoleInstructionDefinition roleInstruction => roleInstruction.Id,
            PassiveBoardDefinition passiveBoard => passiveBoard.Id,
            PassiveNodeDefinition passiveNode => passiveNode.Id,
            SynergyDefinition synergy => synergy.Id,
            SynergyTierDefinition synergyTier => synergyTier.Id,
            ExpeditionDefinition expedition => expedition.Id,
            RewardTableDefinition rewardTable => rewardTable.Id,
            UnitArchetypeDefinition archetype => archetype.Id,
            CampaignChapterDefinition chapter => chapter.Id,
            ExpeditionSiteDefinition site => site.Id,
            EncounterDefinition encounter => encounter.Id,
            EnemySquadTemplateDefinition squad => squad.Id,
            BossOverlayDefinition overlay => overlay.Id,
            StatusFamilyDefinition statusFamily => statusFamily.Id,
            CleanseProfileDefinition cleanseProfile => cleanseProfile.Id,
            ControlDiminishingRuleDefinition controlRule => controlRule.Id,
            RewardSourceDefinition rewardSource => rewardSource.Id,
            DropTableDefinition dropTable => dropTable.Id,
            LootBundleDefinition lootBundle => lootBundle.Id,
            TraitTokenDefinition traitToken => traitToken.Id,
            _ => string.Empty
        };
    }

    internal static string ExtractGuid(string line)
    {
        if (string.IsNullOrWhiteSpace(line))
        {
            return string.Empty;
        }

        var match = GuidRegex.Match(line);
        return match.Success ? match.Groups[1].Value : string.Empty;
    }

    internal static List<string> ParseGuidList(string[] lines, string sectionHeader)
    {
        var result = new List<string>();
        var index = FindLineIndex(lines, sectionHeader);
        if (index < 0)
        {
            return result;
        }

        for (index++; index < lines.Length; index++)
        {
            var trimmed = lines[index].Trim();
            if (!trimmed.StartsWith("- {", StringComparison.Ordinal))
            {
                if (GetIndent(lines[index]) <= 2 && trimmed.EndsWith(":", StringComparison.Ordinal))
                {
                    break;
                }

                continue;
            }

            var guid = ExtractGuid(trimmed);
            if (!string.IsNullOrWhiteSpace(guid))
            {
                result.Add(guid);
            }
        }

        return result;
    }

    internal static List<string> ParseStringList(string[] lines, string sectionHeader)
    {
        var result = new List<string>();
        var index = FindLineIndex(lines, sectionHeader);
        if (index < 0)
        {
            return result;
        }

        for (index++; index < lines.Length; index++)
        {
            var trimmed = lines[index].Trim();
            if (!trimmed.StartsWith("-", StringComparison.Ordinal))
            {
                if (GetIndent(lines[index]) <= 2 && trimmed.EndsWith(":", StringComparison.Ordinal))
                {
                    break;
                }

                continue;
            }

            var value = trimmed[1..].Trim();
            if (!string.IsNullOrWhiteSpace(value))
            {
                result.Add(value);
            }
        }

        return result;
    }

    internal static List<string> ParseIndentedStringList(string[] lines, ref int index, int breakIndent)
    {
        var result = new List<string>();
        for (index++; index < lines.Length; index++)
        {
            var trimmed = lines[index].Trim();
            if (!trimmed.StartsWith("-", StringComparison.Ordinal))
            {
                if (GetIndent(lines[index]) <= breakIndent && trimmed.EndsWith(":", StringComparison.Ordinal))
                {
                    index--;
                    break;
                }

                continue;
            }

            var value = trimmed[1..].Trim();
            if (!string.IsNullOrWhiteSpace(value))
            {
                result.Add(value);
            }
        }

        return result;
    }

    internal static List<SerializableStatModifier> ParseModifiers(string[] lines, string sectionHeader)
    {
        var index = FindLineIndex(lines, sectionHeader);
        if (index < 0)
        {
            return new List<SerializableStatModifier>();
        }

        return ParseNestedModifiers(lines, ref index);
    }

    internal static List<SerializableStatModifier> ParseNestedModifiers(string[] lines, ref int index)
    {
        var result = new List<SerializableStatModifier>();
        for (index++; index < lines.Length; index++)
        {
            var trimmed = lines[index].Trim();
            if (!trimmed.StartsWith("- StatId:", StringComparison.Ordinal))
            {
                if (GetIndent(lines[index]) <= 4 && trimmed.EndsWith(":", StringComparison.Ordinal))
                {
                    index--;
                    break;
                }

                continue;
            }

            var modifier = new SerializableStatModifier
            {
                StatId = trimmed["- StatId:".Length..].Trim()
            };

            for (index++; index < lines.Length; index++)
            {
                trimmed = lines[index].Trim();
                if (trimmed.StartsWith("- StatId:", StringComparison.Ordinal)
                    || (GetIndent(lines[index]) <= 4 && trimmed.EndsWith(":", StringComparison.Ordinal))
                    || trimmed.StartsWith("- Id:", StringComparison.Ordinal))
                {
                    index--;
                    break;
                }

                if (trimmed.StartsWith("Op:", StringComparison.Ordinal))
                {
                    modifier.Op = (ModifierOp)ParseInt(trimmed["Op:".Length..].Trim());
                }
                else if (trimmed.StartsWith("Value:", StringComparison.Ordinal))
                {
                    modifier.Value = ParseFloat(trimmed["Value:".Length..].Trim());
                }
            }

            result.Add(modifier);
        }

        return result;
    }

    internal static BudgetCard? ParseBudgetCard(string[] lines, string sectionHeader, int sectionIndent = 2)
    {
        var index = FindLineIndex(lines, sectionHeader);
        if (index < 0)
        {
            return null;
        }

        var card = new BudgetCard();
        for (index++; index < lines.Length; index++)
        {
            var line = lines[index];
            var trimmed = line.Trim();
            var indent = GetIndent(line);
            if (indent <= sectionIndent && trimmed.EndsWith(":", StringComparison.Ordinal))
            {
                index--;
                break;
            }

            if (indent <= sectionIndent && !string.IsNullOrWhiteSpace(trimmed))
            {
                index--;
                break;
            }

            if (trimmed.StartsWith("Domain:", StringComparison.Ordinal))
            {
                card.Domain = (BudgetDomain)ParseInt(trimmed["Domain:".Length..].Trim());
            }
            else if (trimmed.StartsWith("Rarity:", StringComparison.Ordinal))
            {
                card.Rarity = (ContentRarity)ParseInt(trimmed["Rarity:".Length..].Trim());
            }
            else if (trimmed.StartsWith("PowerBand:", StringComparison.Ordinal))
            {
                card.PowerBand = (PowerBand)ParseInt(trimmed["PowerBand:".Length..].Trim());
            }
            else if (trimmed.StartsWith("RoleProfile:", StringComparison.Ordinal))
            {
                card.RoleProfile = (CombatRoleBudgetProfile)ParseInt(trimmed["RoleProfile:".Length..].Trim());
            }
            else if (string.Equals(trimmed, "Vector:", StringComparison.Ordinal))
            {
                card.Vector = ParseBudgetVector(lines, ref index, indent);
            }
            else if (trimmed.StartsWith("KeywordCount:", StringComparison.Ordinal))
            {
                card.KeywordCount = ParseInt(trimmed["KeywordCount:".Length..].Trim());
            }
            else if (trimmed.StartsWith("ConditionClauseCount:", StringComparison.Ordinal))
            {
                card.ConditionClauseCount = ParseInt(trimmed["ConditionClauseCount:".Length..].Trim());
            }
            else if (trimmed.StartsWith("RuleExceptionCount:", StringComparison.Ordinal))
            {
                card.RuleExceptionCount = ParseInt(trimmed["RuleExceptionCount:".Length..].Trim());
            }
            else if (trimmed.StartsWith("DeclaredThreatPatterns:", StringComparison.Ordinal))
            {
                var packed = trimmed["DeclaredThreatPatterns:".Length..].Trim();
                card.DeclaredThreatPatterns = string.IsNullOrWhiteSpace(packed)
                    ? Array.Empty<ThreatPattern>()
                    : ParsePackedEnumList<ThreatPattern>(packed).ToArray();
            }
            else if (trimmed.StartsWith("DeclaredCounterTools:", StringComparison.Ordinal))
            {
                card.DeclaredCounterTools = ParseCounterToolContributions(lines, ref index, indent).ToArray();
            }
            else if (trimmed.StartsWith("DeclaredFeatureFlags:", StringComparison.Ordinal))
            {
                card.DeclaredFeatureFlags = (ContentFeatureFlag)ParseInt(trimmed["DeclaredFeatureFlags:".Length..].Trim());
            }
        }

        card.Vector ??= new BudgetVector();
        card.DeclaredThreatPatterns ??= Array.Empty<ThreatPattern>();
        card.DeclaredCounterTools ??= Array.Empty<CounterToolContribution>();
        return card;
    }

    internal static BudgetVector ParseBudgetVector(string[] lines, ref int index, int sectionIndent)
    {
        var vector = new BudgetVector();
        for (index++; index < lines.Length; index++)
        {
            var line = lines[index];
            var trimmed = line.Trim();
            var indent = GetIndent(line);
            if (indent <= sectionIndent && trimmed.EndsWith(":", StringComparison.Ordinal))
            {
                index--;
                break;
            }

            if (indent <= sectionIndent && !string.IsNullOrWhiteSpace(trimmed))
            {
                index--;
                break;
            }

            if (trimmed.StartsWith("SustainedDamage:", StringComparison.Ordinal))
            {
                vector.SustainedDamage = ParseInt(trimmed["SustainedDamage:".Length..].Trim());
            }
            else if (trimmed.StartsWith("BurstDamage:", StringComparison.Ordinal))
            {
                vector.BurstDamage = ParseInt(trimmed["BurstDamage:".Length..].Trim());
            }
            else if (trimmed.StartsWith("Durability:", StringComparison.Ordinal))
            {
                vector.Durability = ParseInt(trimmed["Durability:".Length..].Trim());
            }
            else if (trimmed.StartsWith("Control:", StringComparison.Ordinal))
            {
                vector.Control = ParseInt(trimmed["Control:".Length..].Trim());
            }
            else if (trimmed.StartsWith("Mobility:", StringComparison.Ordinal))
            {
                vector.Mobility = ParseInt(trimmed["Mobility:".Length..].Trim());
            }
            else if (trimmed.StartsWith("Support:", StringComparison.Ordinal))
            {
                vector.Support = ParseInt(trimmed["Support:".Length..].Trim());
            }
            else if (trimmed.StartsWith("CounterCoverage:", StringComparison.Ordinal))
            {
                vector.CounterCoverage = ParseInt(trimmed["CounterCoverage:".Length..].Trim());
            }
            else if (trimmed.StartsWith("Reliability:", StringComparison.Ordinal))
            {
                vector.Reliability = ParseInt(trimmed["Reliability:".Length..].Trim());
            }
            else if (trimmed.StartsWith("Economy:", StringComparison.Ordinal))
            {
                vector.Economy = ParseInt(trimmed["Economy:".Length..].Trim());
            }
            else if (trimmed.StartsWith("DrawbackCredit:", StringComparison.Ordinal))
            {
                vector.DrawbackCredit = ParseInt(trimmed["DrawbackCredit:".Length..].Trim());
            }
        }

        return vector;
    }

    internal static List<CounterToolContribution> ParseCounterToolContributions(string[] lines, ref int index, int sectionIndent)
    {
        var result = new List<CounterToolContribution>();
        if (lines[index].TrimEnd().EndsWith("[]", StringComparison.Ordinal))
        {
            return result;
        }

        for (index++; index < lines.Length; index++)
        {
            var line = lines[index];
            var trimmed = line.Trim();
            var indent = GetIndent(line);
            if (indent <= sectionIndent && trimmed.EndsWith(":", StringComparison.Ordinal))
            {
                index--;
                break;
            }

            if (!trimmed.StartsWith("- Tool:", StringComparison.Ordinal))
            {
                continue;
            }

            var contribution = new CounterToolContribution
            {
                Tool = (CounterTool)ParseInt(trimmed["- Tool:".Length..].Trim())
            };

            for (index++; index < lines.Length; index++)
            {
                line = lines[index];
                trimmed = line.Trim();
                indent = GetIndent(line);
                if (trimmed.StartsWith("- Tool:", StringComparison.Ordinal)
                    || (indent <= sectionIndent && trimmed.EndsWith(":", StringComparison.Ordinal)))
                {
                    index--;
                    break;
                }

                if (trimmed.StartsWith("Strength:", StringComparison.Ordinal))
                {
                    contribution.Strength = (CounterCoverageStrength)ParseInt(trimmed["Strength:".Length..].Trim());
                }
            }

            result.Add(contribution);
        }

        return result;
    }

    internal static TargetRule ParseTargetRule(string[] lines, string sectionHeader, int sectionIndent = 2)
    {
        var index = FindLineIndex(lines, sectionHeader);
        return index < 0 ? new TargetRule() : ParseTargetRule(lines, ref index, sectionIndent);
    }

    internal static TargetRule ParseTargetRule(string[] lines, ref int index, int sectionIndent)
    {
        var rule = new TargetRule();
        for (index++; index < lines.Length; index++)
        {
            var line = lines[index];
            var trimmed = line.Trim();
            var indent = GetIndent(line);
            if (indent <= sectionIndent && trimmed.EndsWith(":", StringComparison.Ordinal))
            {
                index--;
                break;
            }

            if (indent <= sectionIndent && !string.IsNullOrWhiteSpace(trimmed))
            {
                index--;
                break;
            }

            if (trimmed.StartsWith("Domain:", StringComparison.Ordinal))
            {
                rule.Domain = (TargetDomain)ParseInt(trimmed["Domain:".Length..].Trim());
            }
            else if (trimmed.StartsWith("PrimarySelector:", StringComparison.Ordinal))
            {
                rule.PrimarySelector = (TargetSelector)ParseInt(trimmed["PrimarySelector:".Length..].Trim());
            }
            else if (trimmed.StartsWith("FallbackPolicy:", StringComparison.Ordinal))
            {
                rule.FallbackPolicy = (TargetFallbackPolicy)ParseInt(trimmed["FallbackPolicy:".Length..].Trim());
            }
            else if (trimmed.StartsWith("Filters:", StringComparison.Ordinal))
            {
                rule.Filters = (TargetFilterFlags)ParseInt(trimmed["Filters:".Length..].Trim());
            }
            else if (trimmed.StartsWith("ReevaluateIntervalSeconds:", StringComparison.Ordinal))
            {
                rule.ReevaluateIntervalSeconds = ParseFloat(trimmed["ReevaluateIntervalSeconds:".Length..].Trim());
            }
            else if (trimmed.StartsWith("MinimumCommitSeconds:", StringComparison.Ordinal))
            {
                rule.MinimumCommitSeconds = ParseFloat(trimmed["MinimumCommitSeconds:".Length..].Trim());
            }
            else if (trimmed.StartsWith("MaxAcquireRange:", StringComparison.Ordinal))
            {
                rule.MaxAcquireRange = ParseFloat(trimmed["MaxAcquireRange:".Length..].Trim());
            }
            else if (trimmed.StartsWith("PreferredMinTargets:", StringComparison.Ordinal))
            {
                rule.PreferredMinTargets = ParseInt(trimmed["PreferredMinTargets:".Length..].Trim());
            }
            else if (trimmed.StartsWith("ClusterRadius:", StringComparison.Ordinal))
            {
                rule.ClusterRadius = ParseFloat(trimmed["ClusterRadius:".Length..].Trim());
            }
            else if (trimmed.StartsWith("LockTargetAtCastStart:", StringComparison.Ordinal))
            {
                rule.LockTargetAtCastStart = ParseBool(trimmed["LockTargetAtCastStart:".Length..].Trim());
            }
            else if (trimmed.StartsWith("RetargetLockMode:", StringComparison.Ordinal))
            {
                rule.RetargetLockMode = (RetargetLockMode)ParseInt(trimmed["RetargetLockMode:".Length..].Trim());
            }
        }

        return rule;
    }

    internal static SummonProfile? ParseSummonProfile(string[] lines, string sectionHeader, int sectionIndent = 2)
    {
        var index = FindLineIndex(lines, sectionHeader);
        if (index < 0)
        {
            return null;
        }

        return ParseSummonProfile(lines, ref index, sectionIndent);
    }

    internal static SummonProfile ParseSummonProfile(string[] lines, ref int index, int sectionIndent)
    {
        var profile = new SummonProfile();
        for (index++; index < lines.Length; index++)
        {
            var line = lines[index];
            var trimmed = line.Trim();
            var indent = GetIndent(line);
            if (indent <= sectionIndent && trimmed.EndsWith(":", StringComparison.Ordinal))
            {
                index--;
                break;
            }

            if (indent <= sectionIndent && !string.IsNullOrWhiteSpace(trimmed))
            {
                index--;
                break;
            }

            if (trimmed.StartsWith("EntityKind:", StringComparison.Ordinal))
            {
                profile.EntityKind = (CombatEntityKind)ParseInt(trimmed["EntityKind:".Length..].Trim());
            }
            else if (trimmed.StartsWith("BehaviorKind:", StringComparison.Ordinal))
            {
                profile.BehaviorKind = (SummonBehaviorKind)ParseInt(trimmed["BehaviorKind:".Length..].Trim());
            }
            else if (trimmed.StartsWith("Eligibility:", StringComparison.Ordinal))
            {
                profile.Eligibility = (CombatantEligibilityFlags)ParseInt(trimmed["Eligibility:".Length..].Trim());
            }
            else if (trimmed.StartsWith("CreditPolicy:", StringComparison.Ordinal))
            {
                profile.CreditPolicy = (CombatCreditFlags)ParseInt(trimmed["CreditPolicy:".Length..].Trim());
            }
            else if (trimmed.StartsWith("MaxConcurrentPerSource:", StringComparison.Ordinal))
            {
                profile.MaxConcurrentPerSource = ParseInt(trimmed["MaxConcurrentPerSource:".Length..].Trim());
            }
            else if (trimmed.StartsWith("MaxConcurrentPerOwner:", StringComparison.Ordinal))
            {
                profile.MaxConcurrentPerOwner = ParseInt(trimmed["MaxConcurrentPerOwner:".Length..].Trim());
            }
            else if (trimmed.StartsWith("DespawnOnOwnerDeath:", StringComparison.Ordinal))
            {
                profile.DespawnOnOwnerDeath = ParseBool(trimmed["DespawnOnOwnerDeath:".Length..].Trim());
            }
            else if (trimmed.StartsWith("OwnerDeathDespawnDelaySeconds:", StringComparison.Ordinal))
            {
                profile.OwnerDeathDespawnDelaySeconds = ParseFloat(trimmed["OwnerDeathDespawnDelaySeconds:".Length..].Trim());
            }
            else if (trimmed.StartsWith("InheritOwnerTarget:", StringComparison.Ordinal))
            {
                profile.InheritOwnerTarget = ParseBool(trimmed["InheritOwnerTarget:".Length..].Trim());
            }
            else if (trimmed.StartsWith("IsPersistent:", StringComparison.Ordinal))
            {
                profile.IsPersistent = ParseBool(trimmed["IsPersistent:".Length..].Trim());
            }
            else if (string.Equals(trimmed, "Inheritance:", StringComparison.Ordinal))
            {
                profile.Inheritance = ParseStatInheritanceProfile(lines, ref index, indent);
            }
        }

        return profile;
    }

    internal static StatInheritanceProfile ParseStatInheritanceProfile(string[] lines, ref int index, int sectionIndent)
    {
        var profile = StatInheritanceProfile.DefaultOwnedSummon;
        for (index++; index < lines.Length; index++)
        {
            var line = lines[index];
            var trimmed = line.Trim();
            var indent = GetIndent(line);
            if (indent <= sectionIndent && trimmed.EndsWith(":", StringComparison.Ordinal))
            {
                index--;
                break;
            }

            if (trimmed.StartsWith("OffenseBonusScalar:", StringComparison.Ordinal))
            {
                profile.OffenseBonusScalar = ParseFloat(trimmed["OffenseBonusScalar:".Length..].Trim());
            }
            else if (trimmed.StartsWith("DefenseBonusScalar:", StringComparison.Ordinal))
            {
                profile.DefenseBonusScalar = ParseFloat(trimmed["DefenseBonusScalar:".Length..].Trim());
            }
            else if (trimmed.StartsWith("UtilityBonusScalar:", StringComparison.Ordinal))
            {
                profile.UtilityBonusScalar = ParseFloat(trimmed["UtilityBonusScalar:".Length..].Trim());
            }
            else if (trimmed.StartsWith("InheritCritChance:", StringComparison.Ordinal))
            {
                profile.InheritCritChance = ParseBool(trimmed["InheritCritChance:".Length..].Trim());
            }
            else if (trimmed.StartsWith("InheritDodge:", StringComparison.Ordinal))
            {
                profile.InheritDodge = ParseBool(trimmed["InheritDodge:".Length..].Trim());
            }
            else if (trimmed.StartsWith("InheritBlock:", StringComparison.Ordinal))
            {
                profile.InheritBlock = ParseBool(trimmed["InheritBlock:".Length..].Trim());
            }
        }

        return profile;
    }

    internal static List<EffectDescriptor> ParseEffectDescriptors(string[] lines, string sectionHeader, int sectionIndent = 2)
    {
        var result = new List<EffectDescriptor>();
        var index = FindLineIndex(lines, sectionHeader);
        if (index < 0)
        {
            return result;
        }

        for (index++; index < lines.Length; index++)
        {
            var line = lines[index];
            var trimmed = line.Trim();
            var indent = GetIndent(line);
            if (indent <= sectionIndent && trimmed.EndsWith(":", StringComparison.Ordinal))
            {
                break;
            }

            if (!trimmed.StartsWith("- Layer:", StringComparison.Ordinal))
            {
                continue;
            }

            var effect = new EffectDescriptor
            {
                Layer = (AuthorityLayer)ParseInt(trimmed["- Layer:".Length..].Trim())
            };

            for (index++; index < lines.Length; index++)
            {
                line = lines[index];
                trimmed = line.Trim();
                indent = GetIndent(line);
                if (trimmed.StartsWith("- Layer:", StringComparison.Ordinal)
                    || (indent <= sectionIndent && trimmed.EndsWith(":", StringComparison.Ordinal)))
                {
                    index--;
                    break;
                }

                if (trimmed.StartsWith("Scope:", StringComparison.Ordinal))
                {
                    effect.Scope = (EffectScope)ParseInt(trimmed["Scope:".Length..].Trim());
                }
                else if (trimmed.StartsWith("Capabilities:", StringComparison.Ordinal))
                {
                    effect.Capabilities = (EffectCapability)ParseInt(trimmed["Capabilities:".Length..].Trim());
                }
                else if (trimmed.StartsWith("AllowMirroredOwnedSummonKill:", StringComparison.Ordinal))
                {
                    effect.AllowMirroredOwnedSummonKill = ParseBool(trimmed["AllowMirroredOwnedSummonKill:".Length..].Trim());
                }
                else if (trimmed.StartsWith("AllowsPersistentSummonChain:", StringComparison.Ordinal))
                {
                    effect.AllowsPersistentSummonChain = ParseBool(trimmed["AllowsPersistentSummonChain:".Length..].Trim());
                }
                else if (trimmed.StartsWith("LoadoutTopologyDelta:", StringComparison.Ordinal))
                {
                    effect.LoadoutTopologyDelta = ParseInt(trimmed["LoadoutTopologyDelta:".Length..].Trim());
                }
            }

            result.Add(effect);
        }

        return result;
    }

    internal static List<T> ParseReferenceList<T>(
        string[] lines,
        string sectionHeader,
        IReadOnlyDictionary<string, string> guidToPath,
        IReadOnlyDictionary<string, T> definitions)
        where T : UnityEngine.Object
    {
        return ParseGuidList(lines, sectionHeader)
            .Select(guid => ResolveGuid(guid, guidToPath, definitions))
            .Where(asset => asset != null)
            .Select(asset => asset!)
            .ToList();
    }

    internal static SkillAiScoreHints ParseSkillAiScoreHints(string[] lines, string sectionHeader, int sectionIndent = 2)
    {
        var index = FindLineIndex(lines, sectionHeader);
        var hints = new SkillAiScoreHints();
        if (index < 0)
        {
            return hints;
        }

        for (index++; index < lines.Length; index++)
        {
            var line = lines[index];
            var trimmed = line.Trim();
            var indent = GetIndent(line);
            if (indent <= sectionIndent && trimmed.EndsWith(":", StringComparison.Ordinal))
            {
                index--;
                break;
            }

            if (trimmed.StartsWith("BurstBias:", StringComparison.Ordinal))
            {
                hints.BurstBias = ParseFloat(trimmed["BurstBias:".Length..].Trim());
            }
            else if (trimmed.StartsWith("ProtectBias:", StringComparison.Ordinal))
            {
                hints.ProtectBias = ParseFloat(trimmed["ProtectBias:".Length..].Trim());
            }
            else if (trimmed.StartsWith("MaintainRangeBias:", StringComparison.Ordinal))
            {
                hints.MaintainRangeBias = ParseFloat(trimmed["MaintainRangeBias:".Length..].Trim());
            }
            else if (trimmed.StartsWith("ExecuteBias:", StringComparison.Ordinal))
            {
                hints.ExecuteBias = ParseFloat(trimmed["ExecuteBias:".Length..].Trim());
            }
            else if (trimmed.StartsWith("ControlBias:", StringComparison.Ordinal))
            {
                hints.ControlBias = ParseFloat(trimmed["ControlBias:".Length..].Trim());
            }
            else if (trimmed.StartsWith("MinimumTargetHealthRatio:", StringComparison.Ordinal))
            {
                hints.MinimumTargetHealthRatio = ParseFloat(trimmed["MinimumTargetHealthRatio:".Length..].Trim());
            }
            else if (trimmed.StartsWith("MaximumTargetHealthRatio:", StringComparison.Ordinal))
            {
                hints.MaximumTargetHealthRatio = ParseFloat(trimmed["MaximumTargetHealthRatio:".Length..].Trim());
            }
            else if (trimmed.StartsWith("MinimumDistance:", StringComparison.Ordinal))
            {
                hints.MinimumDistance = ParseFloat(trimmed["MinimumDistance:".Length..].Trim());
            }
            else if (trimmed.StartsWith("MaximumDistance:", StringComparison.Ordinal))
            {
                hints.MaximumDistance = ParseFloat(trimmed["MaximumDistance:".Length..].Trim());
            }
        }

        return hints;
    }

    internal static int ParseInt(string value)
    {
        return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) ? parsed : 0;
    }

    internal static float ParseFloat(string value)
    {
        return float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed) ? parsed : 0f;
    }

    internal static bool ParseBool(string value)
    {
        return bool.TryParse(value, out var parsed)
            ? parsed
            : ParseInt(value) != 0;
    }

    internal static int FindLineIndex(string[] lines, string key)
    {
        for (var i = 0; i < lines.Length; i++)
        {
            if (string.Equals(lines[i].Trim(), key, StringComparison.Ordinal))
            {
                return i;
            }
        }

        return -1;
    }

    internal static int GetIndent(string line)
    {
        var count = 0;
        while (count < line.Length && line[count] == ' ')
        {
            count++;
        }

        return count;
    }

    internal static string ToUnityPath(string path)
    {
        return path.Replace('\\', '/');
    }

    internal static BudgetCard BuildBudgetCard(
        BudgetDomain domain,
        ContentRarity rarity,
        PowerBand powerBand,
        CombatRoleBudgetProfile roleProfile,
        BudgetVector vector,
        int keywordCount,
        int conditionClauseCount,
        int ruleExceptionCount,
        ThreatPattern[] threats,
        CounterToolContribution[] counters)
    {
        return new BudgetCard
        {
            Domain = domain,
            Rarity = rarity,
            PowerBand = powerBand,
            RoleProfile = roleProfile,
            Vector = vector,
            KeywordCount = keywordCount,
            ConditionClauseCount = conditionClauseCount,
            RuleExceptionCount = ruleExceptionCount,
            DeclaredThreatPatterns = threats,
            DeclaredCounterTools = counters,
            DeclaredFeatureFlags = ContentFeatureFlag.None,
        };
    }

    internal static BudgetVector MakeBudgetVector(
        int sustainedDamage,
        int burstDamage,
        int durability,
        int control,
        int mobility,
        int support,
        int counterCoverage,
        int reliability,
        int economy = 0,
        int drawbackCredit = 0)
    {
        return new BudgetVector
        {
            SustainedDamage = sustainedDamage,
            BurstDamage = burstDamage,
            Durability = durability,
            Control = control,
            Mobility = mobility,
            Support = support,
            CounterCoverage = counterCoverage,
            Reliability = reliability,
            Economy = economy,
            DrawbackCredit = drawbackCredit,
        };
    }

    internal static void AdjustBudgetFinalScore(BudgetVector vector, int target)
    {
        var delta = target - vector.FinalScore;
        vector.Reliability += delta;
        if (vector.Reliability < 0)
        {
            vector.DrawbackCredit += -vector.Reliability;
            vector.Reliability = 0;
        }
    }

    internal static CounterToolContribution MakeCounter(CounterTool tool, CounterCoverageStrength strength)
    {
        return new CounterToolContribution
        {
            Tool = tool,
            Strength = strength,
        };
    }

    internal static void ApplyFallbackIdentity(UnityEngine.Object definition, string assetPath)
    {
        switch (definition)
        {
            case StatDefinition stat:
                stat.Id = CoalesceId(stat.Id, DeriveId(assetPath, "stat_"));
                stat.NameKey = Coalesce(stat.NameKey, ContentLocalizationTables.BuildStatNameKey(stat.Id));
                stat.DescriptionKey = Coalesce(stat.DescriptionKey, ContentLocalizationTables.BuildStatDescriptionKey(stat.Id));
                break;
            case RaceDefinition race:
                race.Id = CoalesceId(race.Id, DeriveId(assetPath, "race_"));
                race.NameKey = Coalesce(race.NameKey, ContentLocalizationTables.BuildRaceNameKey(race.Id));
                race.DescriptionKey = Coalesce(race.DescriptionKey, ContentLocalizationTables.BuildRaceDescriptionKey(race.Id));
                break;
            case ClassDefinition @class:
                @class.Id = CoalesceId(@class.Id, DeriveId(assetPath, "class_"));
                @class.NameKey = Coalesce(@class.NameKey, ContentLocalizationTables.BuildClassNameKey(@class.Id));
                @class.DescriptionKey = Coalesce(@class.DescriptionKey, ContentLocalizationTables.BuildClassDescriptionKey(@class.Id));
                break;
            case CharacterDefinition character:
                character.Id = CoalesceId(character.Id, DeriveId(assetPath, "character_"));
                character.NameKey = Coalesce(character.NameKey, ContentLocalizationTables.BuildCharacterNameKey(character.Id));
                character.DescriptionKey = Coalesce(character.DescriptionKey, ContentLocalizationTables.BuildCharacterDescriptionKey(character.Id));
                break;
            case SkillDefinitionAsset skill:
                skill.Id = CoalesceId(skill.Id, DeriveId(assetPath));
                skill.NameKey = Coalesce(skill.NameKey, ContentLocalizationTables.BuildSkillNameKey(skill.Id));
                skill.DescriptionKey = Coalesce(skill.DescriptionKey, ContentLocalizationTables.BuildSkillDescriptionKey(skill.Id));
                break;
            case TraitPoolDefinition traitPool:
                traitPool.Id = CoalesceId(traitPool.Id, DeriveId(assetPath));
                traitPool.ArchetypeId = CoalesceId(traitPool.ArchetypeId, DeriveId(assetPath, "traitpool_"));
                break;
            case ItemBaseDefinition item:
                item.Id = CoalesceId(item.Id, DeriveId(assetPath));
                item.NameKey = Coalesce(item.NameKey, ContentLocalizationTables.BuildItemNameKey(item.Id));
                item.DescriptionKey = Coalesce(item.DescriptionKey, ContentLocalizationTables.BuildItemDescriptionKey(item.Id));
                break;
            case AffixDefinition affix:
                affix.Id = CoalesceId(affix.Id, DeriveId(assetPath));
                affix.NameKey = Coalesce(affix.NameKey, ContentLocalizationTables.BuildAffixNameKey(affix.Id));
                affix.DescriptionKey = Coalesce(affix.DescriptionKey, ContentLocalizationTables.BuildAffixDescriptionKey(affix.Id));
                break;
            case AugmentDefinition augment:
                augment.Id = CoalesceId(augment.Id, DeriveId(assetPath));
                augment.NameKey = Coalesce(augment.NameKey, ContentLocalizationTables.BuildAugmentNameKey(augment.Id));
                augment.DescriptionKey = Coalesce(augment.DescriptionKey, ContentLocalizationTables.BuildAugmentDescriptionKey(augment.Id));
                break;
            case StableTagDefinition stableTag:
                stableTag.Id = CoalesceId(stableTag.Id, DeriveId(assetPath, "tag_"));
                stableTag.NameKey = Coalesce(stableTag.NameKey, $"content.tag.{ContentLocalizationTables.NormalizeId(stableTag.Id)}.name");
                break;
            case TeamTacticDefinition teamTactic:
                teamTactic.Id = CoalesceId(teamTactic.Id, DeriveId(assetPath));
                teamTactic.NameKey = Coalesce(teamTactic.NameKey, ContentLocalizationTables.BuildTeamTacticNameKey(teamTactic.Id));
                break;
            case RoleInstructionDefinition roleInstruction:
                roleInstruction.Id = CoalesceId(roleInstruction.Id, DeriveId(assetPath));
                roleInstruction.NameKey = Coalesce(roleInstruction.NameKey, ContentLocalizationTables.BuildRoleNameKey(roleInstruction.Id));
                roleInstruction.RoleTag = Coalesce(roleInstruction.RoleTag, roleInstruction.Id);
                break;
            case PassiveBoardDefinition passiveBoard:
                passiveBoard.Id = CoalesceId(passiveBoard.Id, DeriveId(assetPath));
                passiveBoard.NameKey = Coalesce(passiveBoard.NameKey, ContentLocalizationTables.BuildPassiveBoardNameKey(passiveBoard.Id));
                passiveBoard.DescriptionKey = Coalesce(passiveBoard.DescriptionKey, ContentLocalizationTables.BuildPassiveBoardDescriptionKey(passiveBoard.Id));
                break;
            case PassiveNodeDefinition passiveNode:
                passiveNode.Id = CoalesceId(passiveNode.Id, DeriveId(assetPath));
                passiveNode.NameKey = Coalesce(passiveNode.NameKey, ContentLocalizationTables.BuildPassiveNodeNameKey(passiveNode.Id));
                passiveNode.DescriptionKey = Coalesce(passiveNode.DescriptionKey, ContentLocalizationTables.BuildPassiveNodeDescriptionKey(passiveNode.Id));
                break;
            case SynergyDefinition synergy:
                synergy.Id = CoalesceId(synergy.Id, DeriveId(assetPath));
                synergy.NameKey = Coalesce(synergy.NameKey, ContentLocalizationTables.BuildSynergyNameKey(synergy.Id));
                synergy.DescriptionKey = Coalesce(synergy.DescriptionKey, ContentLocalizationTables.BuildSynergyDescriptionKey(synergy.Id));
                break;
            case SynergyTierDefinition synergyTier:
                synergyTier.Id = CoalesceId(synergyTier.Id, DeriveId(assetPath));
                synergyTier.NameKey = Coalesce(synergyTier.NameKey, ContentLocalizationTables.BuildSynergyNameKey(synergyTier.Id));
                synergyTier.DescriptionKey = Coalesce(synergyTier.DescriptionKey, ContentLocalizationTables.BuildSynergyDescriptionKey(synergyTier.Id));
                break;
            case ExpeditionDefinition expedition:
                expedition.Id = CoalesceId(expedition.Id, DeriveId(assetPath));
                expedition.NameKey = Coalesce(expedition.NameKey, ContentLocalizationTables.BuildExpeditionNameKey(expedition.Id));
                expedition.DescriptionKey = Coalesce(expedition.DescriptionKey, ContentLocalizationTables.BuildExpeditionDescriptionKey(expedition.Id));
                break;
            case RewardTableDefinition rewardTable:
                rewardTable.Id = CoalesceId(rewardTable.Id, DeriveId(assetPath));
                rewardTable.NameKey = Coalesce(rewardTable.NameKey, ContentLocalizationTables.BuildRewardTableNameKey(rewardTable.Id));
                rewardTable.DescriptionKey = Coalesce(rewardTable.DescriptionKey, $"content.reward_table.{ContentLocalizationTables.NormalizeId(rewardTable.Id)}.desc");
                break;
            case UnitArchetypeDefinition archetype:
                archetype.Id = CoalesceId(archetype.Id, DeriveId(assetPath, "archetype_"));
                archetype.NameKey = Coalesce(archetype.NameKey, ContentLocalizationTables.BuildArchetypeNameKey(archetype.Id));
                break;
            case CampaignChapterDefinition chapter:
                chapter.Id = CoalesceId(chapter.Id, DeriveId(assetPath));
                chapter.NameKey = Coalesce(chapter.NameKey, ContentLocalizationTables.BuildCampaignChapterNameKey(chapter.Id));
                chapter.DescriptionKey = Coalesce(chapter.DescriptionKey, ContentLocalizationTables.BuildCampaignChapterDescriptionKey(chapter.Id));
                break;
            case ExpeditionSiteDefinition site:
                site.Id = CoalesceId(site.Id, DeriveId(assetPath));
                site.NameKey = Coalesce(site.NameKey, ContentLocalizationTables.BuildExpeditionSiteNameKey(site.Id));
                site.DescriptionKey = Coalesce(site.DescriptionKey, ContentLocalizationTables.BuildExpeditionSiteDescriptionKey(site.Id));
                break;
            case EncounterDefinition encounter:
                encounter.Id = CoalesceId(encounter.Id, DeriveId(assetPath));
                encounter.NameKey = Coalesce(encounter.NameKey, ContentLocalizationTables.BuildEncounterNameKey(encounter.Id));
                encounter.DescriptionKey = Coalesce(encounter.DescriptionKey, ContentLocalizationTables.BuildEncounterDescriptionKey(encounter.Id));
                break;
            case EnemySquadTemplateDefinition squad:
                squad.Id = CoalesceId(squad.Id, DeriveId(assetPath));
                squad.NameKey = Coalesce(squad.NameKey, ContentLocalizationTables.BuildEnemySquadNameKey(squad.Id));
                squad.DescriptionKey = Coalesce(squad.DescriptionKey, ContentLocalizationTables.BuildEnemySquadDescriptionKey(squad.Id));
                break;
            case BossOverlayDefinition overlay:
                overlay.Id = CoalesceId(overlay.Id, DeriveId(assetPath));
                overlay.NameKey = Coalesce(overlay.NameKey, ContentLocalizationTables.BuildBossOverlayNameKey(overlay.Id));
                overlay.DescriptionKey = Coalesce(overlay.DescriptionKey, ContentLocalizationTables.BuildBossOverlayDescriptionKey(overlay.Id));
                break;
            case StatusFamilyDefinition statusFamily:
                statusFamily.Id = CoalesceId(statusFamily.Id, DeriveId(assetPath, "status_family_"));
                statusFamily.NameKey = Coalesce(statusFamily.NameKey, ContentLocalizationTables.BuildStatusNameKey(statusFamily.Id));
                statusFamily.DescriptionKey = Coalesce(statusFamily.DescriptionKey, ContentLocalizationTables.BuildStatusDescriptionKey(statusFamily.Id));
                break;
            case CleanseProfileDefinition cleanseProfile:
                cleanseProfile.Id = CoalesceId(cleanseProfile.Id, DeriveId(assetPath, "cleanse_profile_"));
                cleanseProfile.NameKey = Coalesce(cleanseProfile.NameKey, ContentLocalizationTables.BuildCleanseProfileNameKey(cleanseProfile.Id));
                cleanseProfile.DescriptionKey = Coalesce(cleanseProfile.DescriptionKey, ContentLocalizationTables.BuildCleanseProfileDescriptionKey(cleanseProfile.Id));
                break;
            case ControlDiminishingRuleDefinition controlRule:
                controlRule.Id = CoalesceId(controlRule.Id, DeriveId(assetPath));
                controlRule.NameKey = Coalesce(controlRule.NameKey, ContentLocalizationTables.BuildControlDiminishingNameKey(controlRule.Id));
                controlRule.DescriptionKey = Coalesce(controlRule.DescriptionKey, ContentLocalizationTables.BuildControlDiminishingDescriptionKey(controlRule.Id));
                break;
            case RewardSourceDefinition rewardSource:
                rewardSource.Id = CoalesceId(rewardSource.Id, DeriveId(assetPath));
                rewardSource.NameKey = Coalesce(rewardSource.NameKey, ContentLocalizationTables.BuildRewardSourceNameKey(rewardSource.Id));
                rewardSource.DescriptionKey = Coalesce(rewardSource.DescriptionKey, ContentLocalizationTables.BuildRewardSourceDescriptionKey(rewardSource.Id));
                break;
            case DropTableDefinition dropTable:
                dropTable.Id = CoalesceId(dropTable.Id, DeriveId(assetPath));
                dropTable.NameKey = Coalesce(dropTable.NameKey, ContentLocalizationTables.BuildDropTableNameKey(dropTable.Id));
                dropTable.DescriptionKey = Coalesce(dropTable.DescriptionKey, ContentLocalizationTables.BuildDropTableDescriptionKey(dropTable.Id));
                break;
            case LootBundleDefinition lootBundle:
                lootBundle.Id = CoalesceId(lootBundle.Id, DeriveId(assetPath));
                lootBundle.NameKey = Coalesce(lootBundle.NameKey, ContentLocalizationTables.BuildLootBundleNameKey(lootBundle.Id));
                lootBundle.DescriptionKey = Coalesce(lootBundle.DescriptionKey, ContentLocalizationTables.BuildLootBundleDescriptionKey(lootBundle.Id));
                break;
            case TraitTokenDefinition traitToken:
                traitToken.Id = CoalesceId(traitToken.Id, DeriveId(assetPath));
                traitToken.NameKey = Coalesce(traitToken.NameKey, ContentLocalizationTables.BuildTraitTokenNameKey(traitToken.Id));
                traitToken.DescriptionKey = Coalesce(traitToken.DescriptionKey, ContentLocalizationTables.BuildTraitTokenDescriptionKey(traitToken.Id));
                break;
        }
    }

    internal static string DeriveId(string assetPath, string prefix = "")
    {
        var fileName = Path.GetFileNameWithoutExtension(assetPath);
        if (!string.IsNullOrWhiteSpace(prefix) && fileName.StartsWith(prefix, StringComparison.Ordinal))
        {
            return fileName[prefix.Length..];
        }

        return fileName;
    }

    internal static string CoalesceId(string current, string fallback)
    {
        return string.IsNullOrWhiteSpace(current) ? fallback : current;
    }

    internal static string Coalesce(string current, string fallback)
    {
        return string.IsNullOrWhiteSpace(current) || current.Contains(".unknown.", StringComparison.Ordinal)
            ? fallback
            : current;
    }

    internal static void SetLegacyField(object instance, string fieldName, string value)
    {
        var field = instance.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        if (field != null)
        {
            field.SetValue(instance, value);
        }
    }

    internal static T ResolveReference<T>(
        string[] lines,
        string key,
        IReadOnlyDictionary<string, string> guidToPath,
        IReadOnlyDictionary<string, T> definitions)
        where T : UnityEngine.Object
    {
        var guid = ExtractGuid(ExtractLine(lines, key));
        return ResolveGuid(guid, guidToPath, definitions)!;
    }

    internal static T? ResolveReferenceFromLine<T>(
        string line,
        IReadOnlyDictionary<string, string> guidToPath,
        IReadOnlyDictionary<string, T> definitions)
        where T : UnityEngine.Object
    {
        var guid = ExtractGuid(line);
        return ResolveGuid(guid, guidToPath, definitions);
    }

    internal static T? ResolveGuid<T>(
        string? guid,
        IReadOnlyDictionary<string, string> guidToPath,
        IReadOnlyDictionary<string, T> definitions)
        where T : UnityEngine.Object
    {
        if (string.IsNullOrWhiteSpace(guid) || !guidToPath.TryGetValue(guid, out var path))
        {
            return null;
        }

        var fileName = Path.GetFileNameWithoutExtension(path);
        var candidateId = fileName[(fileName.IndexOf('_') + 1)..];
        if (definitions.TryGetValue(candidateId, out var byId))
        {
            return byId;
        }

        return definitions.Values.FirstOrDefault(definition => string.Equals(definition.name, fileName, StringComparison.Ordinal));
    }

    internal static List<TEnum> ParsePackedEnumList<TEnum>(string packedHex) where TEnum : struct, Enum
    {
        var result = new List<TEnum>();
        if (string.IsNullOrWhiteSpace(packedHex) || packedHex.Length < 8)
        {
            return result;
        }

        for (var index = 0; index + 8 <= packedHex.Length; index += 8)
        {
            var chunk = packedHex.Substring(index, 8);
            try
            {
                var bytes = new byte[4];
                for (var byteIndex = 0; byteIndex < 4; byteIndex++)
                {
                    bytes[byteIndex] = byte.Parse(
                        chunk.Substring(byteIndex * 2, 2),
                        NumberStyles.HexNumber,
                        CultureInfo.InvariantCulture);
                }

                var value = BitConverter.ToInt32(bytes, 0);
                result.Add((TEnum)Enum.ToObject(typeof(TEnum), value));
            }
            catch
            {
                continue;
            }
        }

        return result;
    }
}
