using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using SM.Combat.Model;
using SM.Content.Definitions;
using SM.Core.Stats;
using SM.Meta.Model;

namespace SM.Meta.Services;

public sealed class EncounterResolutionService
{
    private const int BattleNodesPerSite = 4;
    private readonly CombatContentSnapshot _content;

    public EncounterResolutionService(CombatContentSnapshot content)
    {
        _content = content;
    }

    public bool HasAuthoredCatalog =>
        _content.CampaignChapters is { Count: > 0 } &&
        _content.ExpeditionSites is { Count: > 0 } &&
        _content.Encounters is { Count: > 0 } &&
        _content.EnemySquads is { Count: > 0 };

    public CampaignProgressState NormalizeCampaignProgress(CampaignProgressState progress)
    {
        if (!HasAuthoredCatalog)
        {
            return new CampaignProgressState(
                progress.SelectedChapterId,
                progress.SelectedSiteId,
                progress.ClearedChapterIds,
                progress.ClearedSiteIds,
                progress.StoryCleared,
                progress.EndlessUnlocked);
        }

        var chapters = _content.CampaignChapters!.Values
            .OrderBy(chapter => chapter.StoryOrder)
            .ThenBy(chapter => chapter.Id, StringComparer.Ordinal)
            .ToList();
        var selectedChapterId = chapters.Any(chapter => string.Equals(chapter.Id, progress.SelectedChapterId, StringComparison.Ordinal))
            ? progress.SelectedChapterId
            : chapters[0].Id;
        var selectedChapter = chapters.First(chapter => string.Equals(chapter.Id, selectedChapterId, StringComparison.Ordinal));
        var selectedSiteId = ResolveSelectedSiteId(selectedChapter, progress.SelectedSiteId);

        var clearedSiteIds = progress.ClearedSiteIds
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Distinct(StringComparer.Ordinal)
            .ToList();
        var clearedChapterIds = progress.ClearedChapterIds
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Distinct(StringComparer.Ordinal)
            .ToList();

        var storyCleared = progress.StoryCleared || chapters.All(chapter =>
            chapter.SiteIds.Count > 0 &&
            chapter.SiteIds.All(siteId => clearedSiteIds.Contains(siteId)));
        var endlessUnlocked = progress.EndlessUnlocked || storyCleared;

        return new CampaignProgressState(
            selectedChapterId,
            selectedSiteId,
            clearedChapterIds,
            clearedSiteIds,
            storyCleared,
            endlessUnlocked);
    }

    public IReadOnlyList<SiteTrackNodeState> BuildSiteTrack(string chapterId, string siteId)
    {
        if (!HasAuthoredCatalog
            || !_content.ExpeditionSites!.TryGetValue(siteId, out var site)
            || !_content.CampaignChapters!.ContainsKey(chapterId))
        {
            return Array.Empty<SiteTrackNodeState>();
        }

        var encounterIds = site.EncounterIds
            .Where(id => !string.IsNullOrWhiteSpace(id) && _content.Encounters!.ContainsKey(id))
            .Take(BattleNodesPerSite)
            .ToList();
        if (encounterIds.Count == 0)
        {
            return Array.Empty<SiteTrackNodeState>();
        }

        var nodes = new List<SiteTrackNodeState>(BattleNodesPerSite + 1);
        for (var index = 0; index < encounterIds.Count; index++)
        {
            var encounter = _content.Encounters![encounterIds[index]];
            nodes.Add(new SiteTrackNodeState(
                index,
                encounter.Id,
                encounter.RewardSourceId,
                true,
                false));
        }

        nodes.Add(new SiteTrackNodeState(
            encounterIds.Count,
            $"{site.Id}:extract",
            site.ExtractRewardSourceId,
            false,
            false));

        return nodes;
    }

    public BattleContextState BuildBattleContext(ActiveRunState run, string chapterId, string siteId, int nodeIndex)
    {
        if (!HasAuthoredCatalog
            || !_content.ExpeditionSites!.TryGetValue(siteId, out var site))
        {
            return BuildDebugSmokeContext(run, nodeIndex);
        }

        var encounterIds = site.EncounterIds
            .Where(id => !string.IsNullOrWhiteSpace(id) && _content.Encounters!.ContainsKey(id))
            .ToList();
        var clampedIndex = Math.Max(0, Math.Min(nodeIndex, Math.Max(0, encounterIds.Count - 1)));
        var encounterId = encounterIds[clampedIndex];
        var encounter = _content.Encounters![encounterId];
        var contextHash = ComputeContextHash(run.RunId, chapterId, siteId, clampedIndex, encounterId, encounter.RewardSourceId);

        return new BattleContextState(
            chapterId,
            siteId,
            clampedIndex,
            encounterId,
            ComputeSeed(contextHash),
            contextHash,
            encounter.RewardSourceId,
            Math.Max(1, encounter.ThreatSkulls),
            encounter.Kind == EncounterKindValue.Boss,
            encounter.FactionId,
            encounter.BossOverlayId);
    }

    public BattleContextState BuildDebugSmokeContext(ActiveRunState run, int nodeIndex)
    {
        var contextHash = ComputeContextHash(run.RunId, "debug", "quick_smoke", nodeIndex, "debug_smoke_observer", "reward_source_debug_smoke");
        return new BattleContextState(
            "debug",
            "quick_smoke",
            Math.Max(0, nodeIndex),
            "debug_smoke_observer",
            ComputeSeed(contextHash),
            contextHash,
            "reward_source_debug_smoke",
            1,
            false,
            "debug",
            string.Empty);
    }

    public bool TryResolveEncounter(BattleContextState context, out ResolvedEncounterContext resolved, out string error)
    {
        resolved = null!;
        error = string.Empty;

        if (!HasAuthoredCatalog)
        {
            error = "Authored encounter catalog is unavailable.";
            return false;
        }

        if (!_content.Encounters!.TryGetValue(context.EncounterId, out var encounter))
        {
            error = $"Encounter '{context.EncounterId}' not found.";
            return false;
        }

        if (!_content.EnemySquads!.TryGetValue(encounter.EnemySquadTemplateId, out var squad))
        {
            error = $"Enemy squad '{encounter.EnemySquadTemplateId}' not found.";
            return false;
        }

        var enemyParticipants = squad.Members
            .Select(member => new BattleParticipantSpec(
                string.IsNullOrWhiteSpace(member.Id) ? $"{encounter.Id}:{member.ArchetypeId}:{member.Anchor}" : member.Id,
                member.Name,
                member.ArchetypeId,
                member.Anchor,
                member.PositiveTraitId,
                member.NegativeTraitId,
                Array.Empty<BattleEquippedItemSpec>(),
                Array.Empty<string>(),
                squad.EnemyPosture,
                ResolveEnemyRoleTag(member),
                "opening:authored"))
            .ToList();

        var buildResult = BattleSetupBuilder.Build(Array.Empty<BattleParticipantSpec>(), new BattleEncounterPlan(enemyParticipants, squad.EnemyPosture), _content);
        if (!buildResult.IsSuccess)
        {
            error = buildResult.Error ?? $"Failed to resolve encounter '{context.EncounterId}'.";
            return false;
        }

        var enemies = buildResult.Enemies.ToList();
            if (encounter.Kind == EncounterKindValue.Boss
            && !string.IsNullOrWhiteSpace(encounter.BossOverlayId)
            && _content.BossOverlays is { } overlays
            && overlays.TryGetValue(encounter.BossOverlayId, out var overlay))
        {
            enemies = ApplyBossOverlay(enemies, squad, overlay);
        }

        resolved = new ResolvedEncounterContext(context, squad.EnemyPosture, enemies);
        return true;
    }

    public void ApplyBattleBootstrap(BattleState state, ResolvedEncounterContext context)
    {
        if (!context.Context.IsBoss
            || string.IsNullOrWhiteSpace(context.Context.BossOverlayId)
            || _content.BossOverlays is not { } overlays
            || !overlays.TryGetValue(context.Context.BossOverlayId, out var overlay))
        {
            return;
        }

        var captainId = context.Enemies
            .FirstOrDefault(enemy => enemy.CompileTags?.Contains("boss_captain") == true)?.Id
            ?? context.Enemies.FirstOrDefault()?.Id;
        if (string.IsNullOrWhiteSpace(captainId))
        {
            return;
        }

        var captain = state.Enemies.FirstOrDefault(unit => string.Equals(unit.Definition.Id, captainId, StringComparison.Ordinal));
        if (captain == null)
        {
            return;
        }

        foreach (var status in overlay.AppliedStatuses ?? Array.Empty<StatusApplicationSpec>())
        {
            captain.ApplyStatus(status);
        }
    }

    private string ResolveSelectedSiteId(CampaignChapterTemplate chapter, string requestedSiteId)
    {
        if (!string.IsNullOrWhiteSpace(requestedSiteId)
            && chapter.SiteIds.Contains(requestedSiteId))
        {
            return requestedSiteId;
        }

        var authoredSite = chapter.SiteIds
            .Where(id => _content.ExpeditionSites!.ContainsKey(id))
            .Select(id => _content.ExpeditionSites![id])
            .OrderBy(site => site.SiteOrder)
            .ThenBy(site => site.Id, StringComparer.Ordinal)
            .FirstOrDefault();
        return authoredSite?.Id ?? chapter.SiteIds.FirstOrDefault() ?? string.Empty;
    }

    private static string ResolveEnemyRoleTag(EnemySquadMemberTemplate member)
    {
        return member.Role switch
        {
            EnemySquadMemberRoleValue.Captain => "boss_captain",
            EnemySquadMemberRoleValue.Escort => "escort",
            _ => "enemy_unit",
        };
    }

    private static List<BattleUnitLoadout> ApplyBossOverlay(
        IReadOnlyList<BattleUnitLoadout> enemies,
        EnemySquadTemplate squad,
        BossOverlayTemplate overlay)
    {
        var captainId = squad.Members
            .FirstOrDefault(member => member.Role == EnemySquadMemberRoleValue.Captain)?.Id
            ?? squad.Members.FirstOrDefault()?.Id;
        if (string.IsNullOrWhiteSpace(captainId))
        {
            return enemies.ToList();
        }

        var results = new List<BattleUnitLoadout>(enemies.Count);
        foreach (var enemy in enemies)
        {
            if (!string.Equals(enemy.Id, captainId, StringComparison.Ordinal))
            {
                results.Add(enemy);
                continue;
            }

            var tags = new HashSet<string>(enemy.CompileTags ?? Array.Empty<string>(), StringComparer.Ordinal)
            {
                "boss",
                "boss_captain",
                $"boss_overlay:{overlay.Id}",
            };

            if (!string.IsNullOrWhiteSpace(overlay.SignatureAuraTag))
            {
                tags.Add(overlay.SignatureAuraTag);
            }

            if (!string.IsNullOrWhiteSpace(overlay.SignatureUtilityTag))
            {
                tags.Add(overlay.SignatureUtilityTag);
            }

            tags.Add($"boss_phase:{overlay.PhaseTrigger}");
            var ruleTags = new[]
                {
                    overlay.SignatureAuraTag,
                    overlay.SignatureUtilityTag,
                    $"boss_phase:{overlay.PhaseTrigger}",
                }
                .Where(tag => !string.IsNullOrWhiteSpace(tag))
                .Select(tag => new RuleModifier(RuleModifierKind.BehaviorTag, tag!))
                .ToList();
            var rulePackages = (enemy.RulePackages ?? Array.Empty<CombatRuleModifierPackage>()).ToList();
            if (ruleTags.Count > 0)
            {
                rulePackages.Add(new CombatRuleModifierPackage($"boss_overlay:{overlay.Id}", ModifierSource.Other, ruleTags));
            }

            results.Add(enemy with
            {
                CompileTags = tags.OrderBy(tag => tag, StringComparer.Ordinal).ToList(),
                RulePackages = rulePackages,
            });
        }

        return results;
    }

    private static string ComputeContextHash(
        string runId,
        string chapterId,
        string siteId,
        int nodeIndex,
        string encounterId,
        string rewardSourceId)
    {
        using var sha = SHA256.Create();
        var input = $"{runId}|{chapterId}|{siteId}|{nodeIndex}|{encounterId}|{rewardSourceId}";
        var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(input));
        var builder = new StringBuilder(bytes.Length * 2);
        foreach (var value in bytes)
        {
            builder.Append(value.ToString("x2"));
        }

        return builder.ToString();
    }

    private static int ComputeSeed(string contextHash)
    {
        var builder = new StringBuilder(8);
        foreach (var ch in contextHash.Take(8))
        {
            builder.Append(ch);
        }

        return Convert.ToInt32(builder.ToString(), 16) & int.MaxValue;
    }
}
