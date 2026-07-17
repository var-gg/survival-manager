using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using SM.HeadlessMetrics;
using SM.HeadlessPolicies;

namespace SM.Editor.Validation;

/// <summary>policy observation을 UI 의미별 fact와 policy용 fact-id index로 결정적으로 투영한다.</summary>
internal static class H100PlayerVisibleFactProjector
{
    public static HeadlessPolicyObservation AttachEvidenceIndex(HeadlessPolicyObservation observation)
        => Project(string.Empty, string.Empty, new PlayerVisibleTimelinePoint(0, 0, 0), observation).Observation;

    public static H100PlayerVisibleFactProjection Project(
        string runId,
        string campaignId,
        PlayerVisibleTimelinePoint observedAt,
        HeadlessPolicyObservation observation)
    {
        if (observation == null)
        {
            throw new ArgumentNullException(nameof(observation));
        }

        var drafts = BuildDrafts(observation);
        var projected = drafts.Select(draft => new
        {
            Draft = draft,
            Fact = PlayerVisibleFactRecord.Create(
                runId,
                campaignId,
                observedAt,
                draft.UiSource,
                draft.Subject,
                draft.Verb,
                draft.Target,
                draft.Condition,
                draft.StackOrThreshold,
                draft.AcquisitionHint,
                draft.SourceText),
        }).ToArray();
        var index = new SortedDictionary<string, string>(StringComparer.Ordinal);
        foreach (var value in projected.Where(value => !string.IsNullOrWhiteSpace(value.Draft.EvidenceKey)))
        {
            if (index.TryGetValue(value.Draft.EvidenceKey, out var existing)
                && !string.Equals(existing, value.Fact.FactId, StringComparison.Ordinal))
            {
                throw new InvalidOperationException($"Evidence signal key collision: {value.Draft.EvidenceKey}");
            }

            index[value.Draft.EvidenceKey] = value.Fact.FactId;
        }

        var facts = projected.Select(value => value.Fact)
            .GroupBy(value => value.FactId, StringComparer.Ordinal)
            .Select(group => group.First())
            .OrderBy(value => value.FactId, StringComparer.Ordinal)
            .ToArray();
        return new H100PlayerVisibleFactProjection(observation.WithEvidenceFactIds(index), facts);
    }

    private static IReadOnlyList<FactDraft> BuildDrafts(HeadlessPolicyObservation observation)
    {
        var drafts = new List<FactDraft>();
        Add(
            drafts,
            HeadlessPolicyEvidence.DecisionSeedSignal,
            PlayerVisibleUiSource.RunSeedDisplay,
            "current_decision",
            "uses_seed",
            observation.DecisionSeed.ToString(CultureInfo.InvariantCulture),
            "seed is fixed before policy execution",
            string.Empty,
            "run context",
            $"decision seed {observation.DecisionSeed.ToString(CultureInfo.InvariantCulture)}");
        Add(
            drafts,
            HeadlessPolicyEvidence.CampaignContextSignal,
            PlayerVisibleUiSource.CampaignMap,
            "current_campaign_location",
            "shows",
            $"chapter={observation.ChapterId};site={observation.SiteId}",
            "currently selected site",
            string.Empty,
            "campaign map",
            $"{observation.ChapterId} / {observation.SiteId}");

        var anchors = string.Join("|", observation.Anchors.Select(value => value.ToString()));
        Add(
            drafts,
            HeadlessPolicyEvidence.DeploymentSurfaceSignal,
            PlayerVisibleUiSource.SquadBuilderFormation,
            "deployment",
            "offers_anchors",
            anchors,
            "legal placement surface",
            $"capacity={observation.DeployCapacity.ToString(CultureInfo.InvariantCulture)}",
            "squad builder",
            $"capacity {observation.DeployCapacity.ToString(CultureInfo.InvariantCulture)}; anchors {anchors}");

        var rosterSummary = string.Join("|", observation.Roster.Select(H100PlayerVisibleMechanicsFactFormatter.Hero));
        Add(
            drafts,
            HeadlessPolicyEvidence.RosterSurfaceSignal,
            PlayerVisibleUiSource.TownRoster,
            "expedition_roster",
            "shows_ordered_heroes",
            rosterSummary,
            "current expedition squad",
            $"count={observation.Roster.Count.ToString(CultureInfo.InvariantCulture)}",
            "town roster",
            rosterSummary);
        foreach (var hero in observation.Roster)
        {
            AddHeroFacts(drafts, hero);
        }

        Add(
            drafts,
            "wallet.current",
            PlayerVisibleUiSource.TownHudWallet,
            "wallet",
            "holds",
            $"gold={observation.Wallet.Gold.ToString(CultureInfo.InvariantCulture)};echo={observation.Wallet.Echo.ToString(CultureInfo.InvariantCulture)}",
            "current profile currencies",
            string.Empty,
            "town HUD",
            $"gold {observation.Wallet.Gold.ToString(CultureInfo.InvariantCulture)}, echo {observation.Wallet.Echo.ToString(CultureInfo.InvariantCulture)}");
        foreach (var augment in observation.TemporaryAugments)
        {
            var mechanics = H100PlayerVisibleMechanicsFactFormatter.Augment(augment);
            Add(
                drafts,
                $"augment.{augment.AugmentId}",
                PlayerVisibleUiSource.RunAugmentPanel,
                augment.AugmentId,
                "provides_mechanics",
                mechanics,
                "owned for current expedition",
                $"tier={augment.Tier.ToString(CultureInfo.InvariantCulture)}",
                "temporary augment",
                mechanics);
        }

        foreach (var synergy in observation.SynergyCounts)
        {
            Add(
                drafts,
                $"synergy.count.{synergy.CountedTagId}",
                PlayerVisibleUiSource.SquadBuilderSynergy,
                synergy.CountedTagId,
                "has_current_count",
                synergy.CurrentCount.ToString(CultureInfo.InvariantCulture),
                "currently deployed squad",
                string.Empty,
                "squad builder synergy panel",
                $"{synergy.CountedTagId} count {synergy.CurrentCount.ToString(CultureInfo.InvariantCulture)}");
        }

        foreach (var synergy in observation.SynergyCatalog)
        {
            var mechanics = H100PlayerVisibleMechanicsFactFormatter.Synergy(synergy);
            Add(
                drafts,
                $"synergy.catalog.{synergy.SynergyId}",
                PlayerVisibleUiSource.CompendiumSynergy,
                synergy.SynergyId,
                "describes_thresholds",
                mechanics,
                $"counts {synergy.CountedTagId}",
                string.Join("|", synergy.Tiers.Select(value => value.Threshold.ToString(CultureInfo.InvariantCulture))),
                "compendium",
                mechanics);
        }

        var enemyPreview = H100PlayerVisibleMechanicsFactFormatter.EnemyPreview(observation.EnemyPreview);
        Add(
            drafts,
            HeadlessPolicyEvidence.EnemyPreviewSignal,
            PlayerVisibleUiSource.EncounterPreview,
            "current_enemy_preview",
            observation.EnemyPreview.IsAvailable ? "shows" : "is_unavailable",
            enemyPreview,
            "current selected battle node only",
            observation.EnemyPreview.IsAvailable
                ? $"threat_skulls={observation.EnemyPreview.ThreatSkulls.ToString(CultureInfo.InvariantCulture)}"
                : string.Empty,
            "encounter preview",
            enemyPreview);
        for (var index = 0; index < observation.EnemyPreview.Units.Count; index++)
        {
            var unit = observation.EnemyPreview.Units[index];
            var mechanics = H100PlayerVisibleMechanicsFactFormatter.EnemyUnit(unit);
            Add(
                drafts,
                HeadlessPolicyEvidence.EnemyUnitSignal(index),
                PlayerVisibleUiSource.EncounterPreview,
                unit.ArchetypeId,
                "shows_role_and_anchor",
                mechanics,
                $"encounter={observation.EnemyPreview.EncounterId}",
                string.Empty,
                "encounter preview",
                mechanics);
        }

        var rewardSurface = string.Join("|", observation.RewardOptions
            .OrderBy(value => value.Index)
            .Select(value => $"{value.Index.ToString(CultureInfo.InvariantCulture)}:{value.Kind}:{value.PayloadId}:{value.GoldAmount.ToString(CultureInfo.InvariantCulture)}:{value.EchoAmount.ToString(CultureInfo.InvariantCulture)}:{value.PermanentSlotAmount.ToString(CultureInfo.InvariantCulture)}"));
        Add(
            drafts,
            HeadlessPolicyEvidence.RewardSurfaceSignal,
            PlayerVisibleUiSource.RewardCard,
            "current_reward_offer",
            "offers_options",
            rewardSurface,
            "currently presented reward cards",
            $"count={observation.RewardOptions.Count.ToString(CultureInfo.InvariantCulture)}",
            "reward settlement",
            rewardSurface);
        foreach (var option in observation.RewardOptions.OrderBy(value => value.Index))
        {
            var mechanics = H100PlayerVisibleMechanicsFactFormatter.Reward(option);
            Add(
                drafts,
                $"reward.option.{option.Index.ToString(CultureInfo.InvariantCulture)}",
                PlayerVisibleUiSource.RewardCard,
                $"reward_option_{option.Index.ToString(CultureInfo.InvariantCulture)}",
                "provides",
                mechanics,
                "currently selectable",
                string.Empty,
                "reward card",
                mechanics);
        }

        return drafts;
    }

    private static void AddHeroFacts(ICollection<FactDraft> drafts, HeadlessHeroObservation hero)
    {
        var summary = H100PlayerVisibleMechanicsFactFormatter.Hero(hero);
        Add(
            drafts,
            HeadlessPolicyEvidence.HeroSignal(hero.HeroId),
            PlayerVisibleUiSource.TownRoster,
            hero.HeroId,
            "shows_roster_state",
            summary,
            "current expedition squad",
            $"hp={hero.CurrentHp.ToString(CultureInfo.InvariantCulture)}/{hero.MaxHp.ToString(CultureInfo.InvariantCulture)}",
            "town roster",
            summary);
        foreach (var skill in hero.SkillCards)
        {
            var mechanics = H100PlayerVisibleMechanicsFactFormatter.Skill(skill);
            Add(
                drafts,
                HeadlessPolicyEvidence.HeroSkillSignal(hero.HeroId, skill.SkillId),
                PlayerVisibleUiSource.RosterSheetSkill,
                skill.SkillId,
                "describes_mechanics",
                mechanics,
                $"hero={hero.HeroId};slot={skill.SlotKind}",
                $"mana={skill.ManaCost.ToString("R", CultureInfo.InvariantCulture)};cooldown={skill.CooldownSeconds.ToString("R", CultureInfo.InvariantCulture)}",
                "roster skill card",
                mechanics);
        }

        AddFlexSkillFact(drafts, hero, "active", hero.FlexActiveSkillId);
        AddFlexSkillFact(drafts, hero, "passive", hero.FlexPassiveSkillId);
        foreach (var item in hero.EquippedItems)
        {
            var mechanics = H100PlayerVisibleMechanicsFactFormatter.Item(item);
            Add(
                drafts,
                $"hero.{hero.HeroId}.item.{item.ItemInstanceId}.{item.ItemId}",
                PlayerVisibleUiSource.RosterSheetItem,
                string.IsNullOrWhiteSpace(item.ItemInstanceId) ? item.ItemId : item.ItemInstanceId,
                "provides_mechanics",
                mechanics,
                $"equipped_by={hero.HeroId}",
                string.Empty,
                "equipped item",
                mechanics);
        }

        foreach (var passiveNodeId in hero.SelectedPassiveNodeIds.OrderBy(value => value, StringComparer.Ordinal))
        {
            Add(
                drafts,
                $"hero.{hero.HeroId}.passive.{passiveNodeId}",
                PlayerVisibleUiSource.RosterSheetPassive,
                hero.HeroId,
                "selected_passive_node",
                passiveNodeId,
                "current loadout",
                string.Empty,
                "roster passive sheet",
                passiveNodeId);
        }
    }

    private static void AddFlexSkillFact(
        ICollection<FactDraft> drafts,
        HeadlessHeroObservation hero,
        string flexKind,
        string skillId)
    {
        if (string.IsNullOrWhiteSpace(skillId))
        {
            return;
        }

        Add(
            drafts,
            $"hero.{hero.HeroId}.flex.{flexKind}",
            PlayerVisibleUiSource.RosterSheetSkill,
            hero.HeroId,
            $"selects_flex_{flexKind}",
            skillId,
            "current loadout",
            string.Empty,
            "roster skill sheet",
            skillId);
    }

    private static void Add(
        ICollection<FactDraft> drafts,
        string evidenceKey,
        string uiSource,
        string subject,
        string verb,
        string target,
        string condition,
        string stackOrThreshold,
        string acquisitionHint,
        string sourceText)
        => drafts.Add(new FactDraft(
            evidenceKey,
            uiSource,
            subject,
            verb,
            target,
            condition,
            stackOrThreshold,
            acquisitionHint,
            sourceText));

    private sealed record FactDraft(
        string EvidenceKey,
        string UiSource,
        string Subject,
        string Verb,
        string Target,
        string Condition,
        string StackOrThreshold,
        string AcquisitionHint,
        string SourceText);
}

internal sealed record H100PlayerVisibleFactProjection(
    HeadlessPolicyObservation Observation,
    IReadOnlyList<PlayerVisibleFactRecord> Facts);
