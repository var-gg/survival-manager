using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using SM.Content.Definitions;
using SM.Core.Content;
using SM.Core.Contracts;
using SM.Core.Stats;
using UnityEngine;
using static SM.Unity.ContentParsing.YamlFieldExtractor;

namespace SM.Unity.ContentParsing;

internal static class CampaignFileParser
{
    internal static IReadOnlyList<CampaignChapterDefinition> LoadCampaignChapters()
    {
        return RuntimeCombatContentFileParser.LoadAssets("CampaignChapters", path =>
        {
            var lines = File.ReadAllLines(path);
            var definition = ScriptableObject.CreateInstance<CampaignChapterDefinition>();
            definition.Id = ExtractValue(lines, "Id:");
            definition.NameKey = ExtractValue(lines, "NameKey:");
            definition.DescriptionKey = ExtractValue(lines, "DescriptionKey:");
            definition.StoryOrder = ExtractInt(lines, "StoryOrder:");
            definition.SiteIds = ParseStringList(lines, "SiteIds:");
            definition.UnlocksEndlessOnClear = ExtractBool(lines, "UnlocksEndlessOnClear:");
            SetLegacyField(definition, "legacyDisplayName", ExtractValue(lines, "legacyDisplayName:"));
            SetLegacyField(definition, "legacyDescription", ExtractValue(lines, "legacyDescription:"));
            ApplyFallbackIdentity(definition, path);
            ApplyCampaignChapterFallbacks(definition);
            return definition;
        }).Values.ToList();
    }

    internal static IReadOnlyList<ExpeditionSiteDefinition> LoadExpeditionSites()
    {
        return RuntimeCombatContentFileParser.LoadAssets("ExpeditionSites", path =>
        {
            var lines = File.ReadAllLines(path);
            var definition = ScriptableObject.CreateInstance<ExpeditionSiteDefinition>();
            definition.Id = ExtractValue(lines, "Id:");
            definition.ChapterId = ExtractValue(lines, "ChapterId:");
            definition.NameKey = ExtractValue(lines, "NameKey:");
            definition.DescriptionKey = ExtractValue(lines, "DescriptionKey:");
            definition.SiteOrder = ExtractInt(lines, "SiteOrder:");
            definition.FactionId = ExtractValue(lines, "FactionId:");
            definition.EncounterIds = ParseStringList(lines, "EncounterIds:");
            definition.ExtractRewardSourceId = ExtractValue(lines, "ExtractRewardSourceId:");
            definition.ThreatTier = (ThreatTierValue)ExtractInt(lines, "ThreatTier:");
            SetLegacyField(definition, "legacyDisplayName", ExtractValue(lines, "legacyDisplayName:"));
            SetLegacyField(definition, "legacyDescription", ExtractValue(lines, "legacyDescription:"));
            ApplyFallbackIdentity(definition, path);
            ApplyExpeditionSiteFallbacks(definition);
            return definition;
        }).Values.ToList();
    }

    internal static IReadOnlyList<EncounterDefinition> LoadEncounters()
    {
        return RuntimeCombatContentFileParser.LoadAssets("Encounters", path =>
        {
            var lines = File.ReadAllLines(path);
            var definition = ScriptableObject.CreateInstance<EncounterDefinition>();
            definition.Id = ExtractValue(lines, "Id:");
            definition.NameKey = ExtractValue(lines, "NameKey:");
            definition.DescriptionKey = ExtractValue(lines, "DescriptionKey:");
            definition.Kind = (EncounterKindValue)ExtractInt(lines, "Kind:");
            definition.SiteId = ExtractValue(lines, "SiteId:");
            definition.EnemySquadTemplateId = ExtractValue(lines, "EnemySquadTemplateId:");
            definition.BossOverlayId = ExtractValue(lines, "BossOverlayId:");
            definition.RewardSourceId = ExtractValue(lines, "RewardSourceId:");
            definition.FactionId = ExtractValue(lines, "FactionId:");
            definition.ThreatTier = (ThreatTierValue)ExtractInt(lines, "ThreatTier:");
            definition.ThreatCost = ExtractInt(lines, "ThreatCost:");
            definition.ThreatSkulls = ExtractInt(lines, "ThreatSkulls:");
            definition.DifficultyBand = ExtractValue(lines, "DifficultyBand:");
            definition.RewardDropTags = ParseStringList(lines, "RewardDropTags:");
            SetLegacyField(definition, "legacyDisplayName", ExtractValue(lines, "legacyDisplayName:"));
            SetLegacyField(definition, "legacyDescription", ExtractValue(lines, "legacyDescription:"));
            ApplyFallbackIdentity(definition, path);
            ApplyEncounterFallbacks(definition);
            return definition;
        }).Values.ToList();
    }

    internal static IReadOnlyList<EnemySquadTemplateDefinition> LoadEnemySquads()
    {
        return RuntimeCombatContentFileParser.LoadAssets("EnemySquads", path =>
        {
            var lines = File.ReadAllLines(path);
            var definition = ScriptableObject.CreateInstance<EnemySquadTemplateDefinition>();
            definition.Id = ExtractValue(lines, "Id:");
            definition.NameKey = ExtractValue(lines, "NameKey:");
            definition.DescriptionKey = ExtractValue(lines, "DescriptionKey:");
            definition.FactionId = ExtractValue(lines, "FactionId:");
            definition.EnemyPosture = (TeamPostureTypeValue)ExtractInt(lines, "EnemyPosture:");
            definition.ThreatTier = (ThreatTierValue)ExtractInt(lines, "ThreatTier:");
            definition.ThreatCost = ExtractInt(lines, "ThreatCost:");
            definition.RewardDropTags = ParseStringList(lines, "RewardDropTags:");
            definition.Members = ParseEnemySquadMembers(lines);
            SetLegacyField(definition, "legacyDisplayName", ExtractValue(lines, "legacyDisplayName:"));
            SetLegacyField(definition, "legacyDescription", ExtractValue(lines, "legacyDescription:"));
            ApplyFallbackIdentity(definition, path);
            ApplyEnemySquadFallbacks(definition);
            return definition;
        }).Values.ToList();
    }

    internal static IReadOnlyList<BossOverlayDefinition> LoadBossOverlays()
    {
        return RuntimeCombatContentFileParser.LoadAssets("BossOverlays", path =>
        {
            var lines = File.ReadAllLines(path);
            var definition = ScriptableObject.CreateInstance<BossOverlayDefinition>();
            definition.Id = ExtractValue(lines, "Id:");
            definition.NameKey = ExtractValue(lines, "NameKey:");
            definition.DescriptionKey = ExtractValue(lines, "DescriptionKey:");
            definition.PhaseTrigger = (BossPhaseTriggerValue)ExtractInt(lines, "PhaseTrigger:");
            definition.ThreatCost = ExtractInt(lines, "ThreatCost:");
            definition.SignatureAuraTag = ExtractValue(lines, "SignatureAuraTag:");
            definition.SignatureUtilityTag = ExtractValue(lines, "SignatureUtilityTag:");
            definition.RewardDropTags = ParseStringList(lines, "RewardDropTags:");
            definition.AppliedStatuses = StatusFileParser.ParseStatusApplicationRules(lines, "AppliedStatuses:");
            SetLegacyField(definition, "legacyDisplayName", ExtractValue(lines, "legacyDisplayName:"));
            SetLegacyField(definition, "legacyDescription", ExtractValue(lines, "legacyDescription:"));
            return definition;
        }).Values.ToList();
    }

    internal static Dictionary<string, ExpeditionDefinition> LoadExpeditions(IReadOnlyDictionary<string, string> guidToPath)
    {
        return RuntimeCombatContentFileParser.LoadAssets("Expeditions", path =>
        {
            var lines = File.ReadAllLines(path);
            var definition = ScriptableObject.CreateInstance<ExpeditionDefinition>();
            definition.Id = ExtractValue(lines, "Id:");
            definition.NameKey = ExtractValue(lines, "NameKey:");
            definition.DescriptionKey = ExtractValue(lines, "DescriptionKey:");
            definition.Nodes = ParseExpeditionNodes(lines, guidToPath);
            SetLegacyField(definition, "legacyDisplayName", ExtractValue(lines, "legacyDisplayName:"));
            return definition;
        }, guidToPath);
    }

    internal static List<EnemySquadMemberDefinition> ParseEnemySquadMembers(string[] lines)
    {
        var result = new List<EnemySquadMemberDefinition>();
        var index = FindLineIndex(lines, "Members:");
        if (index < 0)
        {
            return result;
        }

        for (index++; index < lines.Length; index++)
        {
            var trimmed = lines[index].Trim();
            if (!trimmed.StartsWith("- Id:", StringComparison.Ordinal))
            {
                if (GetIndent(lines[index]) <= 2 && trimmed.EndsWith(":", StringComparison.Ordinal))
                {
                    break;
                }

                continue;
            }

            var member = new EnemySquadMemberDefinition
            {
                Id = trimmed["- Id:".Length..].Trim()
            };

            for (index++; index < lines.Length; index++)
            {
                trimmed = lines[index].Trim();
                if (trimmed.StartsWith("- Id:", StringComparison.Ordinal)
                    || (GetIndent(lines[index]) <= 2 && trimmed.EndsWith(":", StringComparison.Ordinal)))
                {
                    index--;
                    break;
                }

                if (trimmed.StartsWith("NameKey:", StringComparison.Ordinal))
                {
                    member.NameKey = trimmed["NameKey:".Length..].Trim();
                }
                else if (trimmed.StartsWith("ArchetypeId:", StringComparison.Ordinal))
                {
                    member.ArchetypeId = trimmed["ArchetypeId:".Length..].Trim();
                }
                else if (trimmed.StartsWith("Anchor:", StringComparison.Ordinal))
                {
                    member.Anchor = (DeploymentAnchorValue)ParseInt(trimmed["Anchor:".Length..].Trim());
                }
                else if (trimmed.StartsWith("Role:", StringComparison.Ordinal))
                {
                    member.Role = (EnemySquadMemberRoleValue)ParseInt(trimmed["Role:".Length..].Trim());
                }
                else if (trimmed.StartsWith("PositiveTraitId:", StringComparison.Ordinal))
                {
                    member.PositiveTraitId = trimmed["PositiveTraitId:".Length..].Trim();
                }
                else if (trimmed.StartsWith("NegativeTraitId:", StringComparison.Ordinal))
                {
                    member.NegativeTraitId = trimmed["NegativeTraitId:".Length..].Trim();
                }
                else if (string.Equals(trimmed, "RuleModifierTags: []", StringComparison.Ordinal))
                {
                    member.RuleModifierTags = new List<string>();
                }
                else if (string.Equals(trimmed, "RuleModifierTags:", StringComparison.Ordinal))
                {
                    member.RuleModifierTags = ParseIndentedStringList(lines, ref index, 4);
                }
                else if (trimmed.StartsWith("legacyDisplayName:", StringComparison.Ordinal))
                {
                    SetLegacyField(member, "legacyDisplayName", trimmed["legacyDisplayName:".Length..].Trim());
                }
            }

            result.Add(member);
        }

        return result;
    }

    internal static List<ExpeditionNodeDefinition> ParseExpeditionNodes(string[] lines, IReadOnlyDictionary<string, string> guidToPath)
    {
        var result = new List<ExpeditionNodeDefinition>();
        var index = FindLineIndex(lines, "Nodes:");
        if (index < 0)
        {
            return result;
        }

        for (index++; index < lines.Length; index++)
        {
            var trimmed = lines[index].Trim();
            if (!trimmed.StartsWith("- Id:", StringComparison.Ordinal))
            {
                if (GetIndent(lines[index]) <= 2 && trimmed.EndsWith(":", StringComparison.Ordinal))
                {
                    break;
                }

                continue;
            }

            var node = new ExpeditionNodeDefinition
            {
                Id = trimmed["- Id:".Length..].Trim()
            };

            for (index++; index < lines.Length; index++)
            {
                trimmed = lines[index].Trim();
                if (trimmed.StartsWith("- Id:", StringComparison.Ordinal)
                    || (GetIndent(lines[index]) <= 2 && trimmed.EndsWith(":", StringComparison.Ordinal)))
                {
                    index--;
                    break;
                }

                if (trimmed.StartsWith("LabelKey:", StringComparison.Ordinal))
                {
                    node.LabelKey = trimmed["LabelKey:".Length..].Trim();
                }
                else if (trimmed.StartsWith("DescriptionKey:", StringComparison.Ordinal))
                {
                    node.DescriptionKey = trimmed["DescriptionKey:".Length..].Trim();
                }
                else if (trimmed.StartsWith("RewardSummaryKey:", StringComparison.Ordinal))
                {
                    node.RewardSummaryKey = trimmed["RewardSummaryKey:".Length..].Trim();
                }
                else if (trimmed.StartsWith("RewardTable:", StringComparison.Ordinal))
                {
                    var tableGuid = ExtractGuid(trimmed);
                    if (!string.IsNullOrWhiteSpace(tableGuid) && guidToPath.TryGetValue(tableGuid, out var rewardTablePath))
                    {
                        node.RewardTable = ScriptableObject.CreateInstance<RewardTableDefinition>();
                        node.RewardTable.Id = Path.GetFileNameWithoutExtension(rewardTablePath)["rewardtable_".Length..];
                        node.RewardTable.name = Path.GetFileNameWithoutExtension(rewardTablePath);
                    }
                }
                else if (trimmed.StartsWith("legacyLabel:", StringComparison.Ordinal))
                {
                    SetLegacyField(node, "legacyLabel", trimmed["legacyLabel:".Length..].Trim());
                }
            }

            result.Add(node);
        }

        return result;
    }

    internal static void ApplyCampaignChapterFallbacks(CampaignChapterDefinition definition)
    {
        if (definition.StoryOrder <= 0)
        {
            definition.StoryOrder = definition.Id switch
            {
                "chapter_ashen_frontier" => 1,
                "chapter_ruined_crypts" => 2,
                "chapter_warren_depths" => 3,
                _ => 1,
            };
        }

        if (definition.SiteIds.Distinct(StringComparer.Ordinal).Count() != 2)
        {
            definition.SiteIds = definition.Id switch
            {
                "chapter_ashen_frontier" => new List<string> { "site_ashen_gate", "site_cinder_watch" },
                "chapter_warren_depths" => new List<string> { "site_forgotten_warren", "site_twisted_den" },
                "chapter_ruined_crypts" => new List<string> { "site_ruined_crypt", "site_grave_sanctum" },
                _ => definition.SiteIds,
            };
        }

        if (definition.StoryOrder == 3)
        {
            definition.UnlocksEndlessOnClear = true;
        }
    }

    internal static void ApplyExpeditionSiteFallbacks(ExpeditionSiteDefinition definition)
    {
        if (TryGetCampaignSiteFallbackSpec(definition.Id, out var spec))
        {
            definition.ChapterId = CoalesceId(definition.ChapterId, spec.ChapterId);
            definition.FactionId = Coalesce(definition.FactionId, spec.FactionId);
            if (definition.EncounterIds.Distinct(StringComparer.Ordinal).Count() != 4)
            {
                definition.EncounterIds = BuildSiteEncounterIds(spec.SiteId);
            }

            definition.ExtractRewardSourceId = Coalesce(definition.ExtractRewardSourceId, "reward_source_extract");
            definition.ThreatTier = definition.ThreatTier == 0 ? spec.ThreatTier : definition.ThreatTier;
        }

        definition.SiteOrder = Math.Max(definition.SiteOrder, definition.Id switch
        {
            "site_ashen_gate" => 1,
            "site_cinder_watch" => 2,
            "site_forgotten_warren" => 3,
            "site_grave_sanctum" => 4,
            "site_ruined_crypt" => 5,
            "site_twisted_den" => 6,
            _ => 1,
        });
    }

    internal static void ApplyEncounterFallbacks(EncounterDefinition definition)
    {
        if (string.IsNullOrWhiteSpace(definition.SiteId) && TryGetSiteIdFromEncounterId(definition.Id, out var siteId))
        {
            definition.SiteId = siteId;
        }

        if (string.IsNullOrWhiteSpace(definition.RewardSourceId))
        {
            definition.RewardSourceId = definition.Id.Contains("_boss_", StringComparison.Ordinal)
                ? "reward_source_boss"
                : definition.Id.Contains("_elite_", StringComparison.Ordinal)
                    ? "reward_source_elite"
                    : "reward_source_skirmish";
        }

        if (TryGetEncounterKindFromId(definition.Id, out var kind))
        {
            definition.Kind = definition.Kind == 0 && !definition.Id.Contains("_skirmish_", StringComparison.Ordinal) ? kind : definition.Kind;
            definition.DifficultyBand = Coalesce(definition.DifficultyBand, kind switch
            {
                EncounterKindValue.Boss => "site_boss",
                EncounterKindValue.Elite => "site_mid",
                _ => "chapter_entry",
            });

            if (definition.ThreatCost <= 0)
            {
                definition.ThreatCost = kind switch
                {
                    EncounterKindValue.Boss => 3,
                    EncounterKindValue.Elite => 2,
                    _ => 1,
                };
            }

            if (definition.ThreatSkulls <= 0)
            {
                definition.ThreatSkulls = definition.ThreatCost;
            }

            if (definition.ThreatTier == 0)
            {
                definition.ThreatTier = kind switch
                {
                    EncounterKindValue.Boss => ThreatTierValue.Tier3,
                    EncounterKindValue.Elite => ThreatTierValue.Tier2,
                    _ => ThreatTierValue.Tier1,
                };
            }

            definition.EnemySquadTemplateId = CoalesceId(definition.EnemySquadTemplateId, $"{definition.Id}_squad");
            if (definition.RewardDropTags.Count == 0)
            {
                definition.RewardDropTags = kind switch
                {
                    EncounterKindValue.Boss => new List<string> { "boss", "combat", "reward" },
                    EncounterKindValue.Elite => new List<string> { "elite", "combat", "reward" },
                    _ => new List<string> { "skirmish", "combat", "reward" },
                };
            }
        }

        if (TryGetCampaignSiteFallbackSpec(definition.SiteId, out var spec))
        {
            definition.FactionId = Coalesce(definition.FactionId, spec.FactionId);
            if (definition.Kind == EncounterKindValue.Boss)
            {
                definition.BossOverlayId = CoalesceId(definition.BossOverlayId, spec.BossOverlayId);
            }
        }
    }

    internal static void ApplyEnemySquadFallbacks(EnemySquadTemplateDefinition definition)
    {
        if (!TryGetSiteIdFromSquadId(definition.Id, out var siteId) || !TryGetCampaignSiteFallbackSpec(siteId, out var spec))
        {
            return;
        }

        definition.FactionId = Coalesce(definition.FactionId, spec.FactionId);
        if (definition.RewardDropTags.Count == 0)
        {
            definition.RewardDropTags = definition.Id.Contains("_boss_", StringComparison.Ordinal)
                ? new List<string> { "boss", "combat", "reward" }
                : definition.Id.Contains("_elite_", StringComparison.Ordinal)
                    ? new List<string> { "elite", "combat", "reward" }
                    : new List<string> { "skirmish", "combat", "reward" };
        }

        if (definition.Members.Count == 0)
        {
            definition.Members = BuildEnemySquadMembers(spec, definition.Id);
        }
    }

    internal static bool TryGetSiteIdFromEncounterId(string encounterId, out string siteId)
    {
        foreach (var marker in new[] { "_skirmish_", "_elite_", "_boss_" })
        {
            var index = encounterId.IndexOf(marker, StringComparison.Ordinal);
            if (index > 0)
            {
                siteId = encounterId[..index];
                return true;
            }
        }

        siteId = string.Empty;
        return false;
    }

    internal static bool TryGetSiteIdFromSquadId(string squadId, out string siteId)
    {
        if (squadId.EndsWith("_squad", StringComparison.Ordinal))
        {
            return TryGetSiteIdFromEncounterId(squadId[..^"_squad".Length], out siteId);
        }

        siteId = string.Empty;
        return false;
    }

    internal static bool TryGetEncounterKindFromId(string encounterId, out EncounterKindValue kind)
    {
        if (encounterId.Contains("_boss_", StringComparison.Ordinal))
        {
            kind = EncounterKindValue.Boss;
            return true;
        }

        if (encounterId.Contains("_elite_", StringComparison.Ordinal))
        {
            kind = EncounterKindValue.Elite;
            return true;
        }

        if (encounterId.Contains("_skirmish_", StringComparison.Ordinal))
        {
            kind = EncounterKindValue.Skirmish;
            return true;
        }

        kind = EncounterKindValue.Skirmish;
        return false;
    }

    internal static List<EnemySquadMemberDefinition> BuildEnemySquadMembers(CampaignSiteFallbackSpec spec, string squadId)
    {
        var archetypes = squadId.Contains("_boss_", StringComparison.Ordinal)
            ? new[] { spec.BossCaptain, spec.BossEscorts[0], spec.BossEscorts[1] }
            : squadId.Contains("_elite_", StringComparison.Ordinal)
                ? spec.Elite.ToArray()
                : squadId.Contains("_skirmish_2", StringComparison.Ordinal)
                    ? spec.SkirmishB.ToArray()
                    : spec.SkirmishA.ToArray();
        var anchors = new[]
        {
            DeploymentAnchorValue.FrontCenter,
            DeploymentAnchorValue.FrontTop,
            DeploymentAnchorValue.FrontBottom,
            DeploymentAnchorValue.BackCenter,
        };
        var members = new List<EnemySquadMemberDefinition>(archetypes.Length);
        for (var i = 0; i < archetypes.Length; i++)
        {
            members.Add(new EnemySquadMemberDefinition
            {
                Id = $"{squadId}.member.{i + 1}",
                NameKey = ContentLocalizationTables.BuildEnemySquadNameKey($"{squadId}.member.{i + 1}"),
                ArchetypeId = archetypes[i],
                Anchor = anchors[Math.Min(i, anchors.Length - 1)],
                Role = squadId.Contains("_boss_", StringComparison.Ordinal)
                    ? i == 0 ? EnemySquadMemberRoleValue.Captain : EnemySquadMemberRoleValue.Escort
                    : EnemySquadMemberRoleValue.Unit,
            });
        }

        return members;
    }

    internal static List<string> BuildSiteEncounterIds(string siteId)
    {
        return new List<string>
        {
            $"{siteId}_skirmish_1",
            $"{siteId}_skirmish_2",
            $"{siteId}_elite_1",
            $"{siteId}_boss_1",
        };
    }

    internal static bool TryGetCampaignSiteFallbackSpec(string siteId, out CampaignSiteFallbackSpec spec)
    {
        spec = siteId switch
        {
            "site_ashen_gate" => new CampaignSiteFallbackSpec(
                "site_ashen_gate",
                "chapter_ashen_frontier",
                1,
                "faction_ashen_vanguard",
                ThreatTierValue.Tier1,
                "boss_overlay_ashen_gate",
                new[] { "warden", "hunter", "hexer", "raider" },
                new[] { "guardian", "scout", "hexer", "hunter" },
                new[] { "bulwark", "hunter", "hexer", "warden" },
                "bulwark",
                new[] { "hunter", "hexer" }),
            "site_cinder_watch" => new CampaignSiteFallbackSpec(
                "site_cinder_watch",
                "chapter_ashen_frontier",
                2,
                "faction_cinder_watch",
                ThreatTierValue.Tier1,
                "boss_overlay_cinder_watch",
                new[] { "hunter", "scout", "raider", "priest" },
                new[] { "marksman", "scout", "guardian", "hexer" },
                new[] { "marksman", "bulwark", "scout", "hexer" },
                "marksman",
                new[] { "scout", "priest" }),
            "site_forgotten_warren" => new CampaignSiteFallbackSpec(
                "site_forgotten_warren",
                "chapter_warren_depths",
                1,
                "faction_warren_pack",
                ThreatTierValue.Tier2,
                "boss_overlay_forgotten_warren",
                new[] { "raider", "scout", "shaman", "hunter" },
                new[] { "reaver", "scout", "shaman", "hunter" },
                new[] { "reaver", "raider", "scout", "shaman" },
                "reaver",
                new[] { "scout", "shaman" }),
            "site_twisted_den" => new CampaignSiteFallbackSpec(
                "site_twisted_den",
                "chapter_warren_depths",
                2,
                "faction_twisted_den",
                ThreatTierValue.Tier2,
                "boss_overlay_twisted_den",
                new[] { "slayer", "raider", "scout", "shaman" },
                new[] { "slayer", "hunter", "scout", "priest" },
                new[] { "slayer", "reaver", "scout", "priest" },
                "slayer",
                new[] { "scout", "priest" }),
            "site_ruined_crypt" => new CampaignSiteFallbackSpec(
                "site_ruined_crypt",
                "chapter_ruined_crypts",
                1,
                "faction_bone_host",
                ThreatTierValue.Tier3,
                "boss_overlay_ruined_crypt",
                new[] { "guardian", "hexer", "priest", "hunter" },
                new[] { "bulwark", "hexer", "priest", "marksman" },
                new[] { "bulwark", "hexer", "priest", "marksman" },
                "hexer",
                new[] { "priest", "guardian" }),
            "site_grave_sanctum" => new CampaignSiteFallbackSpec(
                "site_grave_sanctum",
                "chapter_ruined_crypts",
                2,
                "faction_grave_sanctum",
                ThreatTierValue.Tier3,
                "boss_overlay_grave_sanctum",
                new[] { "guardian", "hexer", "shaman", "marksman" },
                new[] { "bulwark", "hexer", "shaman", "hunter" },
                new[] { "bulwark", "guardian", "hexer", "shaman" },
                "shaman",
                new[] { "guardian", "hexer" }),
            _ => null!,
        };

        return spec != null;
    }

    internal sealed record CampaignSiteFallbackSpec(
        string SiteId,
        string ChapterId,
        int SiteOrder,
        string FactionId,
        ThreatTierValue ThreatTier,
        string BossOverlayId,
        IReadOnlyList<string> SkirmishA,
        IReadOnlyList<string> SkirmishB,
        IReadOnlyList<string> Elite,
        string BossCaptain,
        IReadOnlyList<string> BossEscorts);
}
