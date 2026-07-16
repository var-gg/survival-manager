using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace SM.HeadlessMetrics;

/// <summary>wallclock 없이 campaign/site/decision 순서만 보존하는 결정적 관측 시점.</summary>
public sealed record PlayerVisibleTimelinePoint(
    int CampaignIndex,
    int SiteIndex,
    int DecisionIndex) : IComparable<PlayerVisibleTimelinePoint>
{
    public int CompareTo(PlayerVisibleTimelinePoint? other)
    {
        if (other == null)
        {
            return 1;
        }

        var campaign = CampaignIndex.CompareTo(other.CampaignIndex);
        if (campaign != 0)
        {
            return campaign;
        }

        var site = SiteIndex.CompareTo(other.SiteIndex);
        return site != 0 ? site : DecisionIndex.CompareTo(other.DecisionIndex);
    }
}

/// <summary>fact가 대응하는 player-visible 화면 의미의 안정 id.</summary>
public static class PlayerVisibleUiSource
{
    public const string RunSeedDisplay = "run_seed_display";
    public const string CampaignMap = "campaign_map";
    public const string SquadBuilderFormation = "squad_builder_formation";
    public const string TownRoster = "town_roster";
    public const string RosterSheetSkill = "roster_sheet_skill";
    public const string RosterSheetItem = "roster_sheet_item";
    public const string RosterSheetPassive = "roster_sheet_passive";
    public const string TownHudWallet = "town_hud_wallet";
    public const string RunAugmentPanel = "run_augment_panel";
    public const string SquadBuilderSynergy = "squad_builder_synergy";
    public const string CompendiumSynergy = "compendium_synergy";
    public const string EncounterPreview = "encounter_preview";
    public const string RewardCard = "reward_card";

    public static IReadOnlyList<string> All { get; } = new[]
    {
        RunSeedDisplay,
        CampaignMap,
        SquadBuilderFormation,
        TownRoster,
        RosterSheetSkill,
        RosterSheetItem,
        RosterSheetPassive,
        TownHudWallet,
        RunAugmentPanel,
        SquadBuilderSynergy,
        CompendiumSynergy,
        EncounterPreview,
        RewardCard,
    };
}

/// <summary>한 시점에 플레이어가 읽을 수 있었던 구조화 사실 한 건.</summary>
public sealed record PlayerVisibleFactRecord
{
    public const string CurrentSchemaVersion = "player-visible-fact-v1";

    public string SchemaVersion { get; init; } = CurrentSchemaVersion;
    public string EntryKind { get; init; } = "fact";
    public string RunId { get; init; } = string.Empty;
    public string CampaignId { get; init; } = string.Empty;
    public string FactId { get; init; } = string.Empty;
    public PlayerVisibleTimelinePoint ObservedAt { get; init; } = new(0, 0, 0);
    public string UiSource { get; init; } = string.Empty;
    public string Subject { get; init; } = string.Empty;
    public string Verb { get; init; } = string.Empty;
    public string Target { get; init; } = string.Empty;
    public string Condition { get; init; } = string.Empty;
    public string StackOrThreshold { get; init; } = string.Empty;
    public string AcquisitionHint { get; init; } = string.Empty;
    public string SourceText { get; init; } = string.Empty;
    public string ContentHash { get; init; } = string.Empty;

    public static PlayerVisibleFactRecord Create(
        string runId,
        string campaignId,
        PlayerVisibleTimelinePoint observedAt,
        string uiSource,
        string subject,
        string verb,
        string target,
        string condition,
        string stackOrThreshold,
        string acquisitionHint,
        string sourceText)
    {
        var contentHash = PlayerVisibleLedgerHash.Compute(
            uiSource,
            subject,
            verb,
            target,
            condition,
            stackOrThreshold,
            acquisitionHint,
            sourceText);
        return new PlayerVisibleFactRecord
        {
            RunId = runId ?? string.Empty,
            CampaignId = campaignId ?? string.Empty,
            FactId = $"fact-{contentHash}",
            ObservedAt = observedAt ?? throw new ArgumentNullException(nameof(observedAt)),
            UiSource = uiSource ?? string.Empty,
            Subject = subject ?? string.Empty,
            Verb = verb ?? string.Empty,
            Target = target ?? string.Empty,
            Condition = condition ?? string.Empty,
            StackOrThreshold = stackOrThreshold ?? string.Empty,
            AcquisitionHint = acquisitionHint ?? string.Empty,
            SourceText = sourceText ?? string.Empty,
            ContentHash = contentHash,
        };
    }
}

/// <summary>정책 decision sidecar가 가리키는 fact 링크.</summary>
public sealed record PlayerVisibleEvidenceRef(string FactId);

/// <summary>정책 action과 그 action 이전 fact 링크를 결합한 결정 provenance 행.</summary>
public sealed record PlayerVisibleDecisionRecord
{
    public const string CurrentSchemaVersion = "player-visible-decision-v1";

    public string SchemaVersion { get; init; } = CurrentSchemaVersion;
    public string EntryKind { get; init; } = "decision";
    public string RunId { get; init; } = string.Empty;
    public string CampaignId { get; init; } = string.Empty;
    public string DecisionId { get; init; } = string.Empty;
    public PlayerVisibleTimelinePoint DecidedAt { get; init; } = new(0, 0, 0);
    public string PolicyId { get; init; } = string.Empty;
    public string DecisionKind { get; init; } = string.Empty;
    public string Action { get; init; } = string.Empty;
    public string Rationale { get; init; } = string.Empty;
    public double EstimatedValue { get; init; }
    public string Confidence { get; init; } = "certain";
    public IReadOnlyList<PlayerVisibleEvidenceRef> EvidenceRefs { get; init; } = Array.Empty<PlayerVisibleEvidenceRef>();

    public static PlayerVisibleDecisionRecord Create(
        string runId,
        string campaignId,
        PlayerVisibleTimelinePoint decidedAt,
        string policyId,
        string decisionKind,
        string action,
        string rationale,
        double estimatedValue,
        IEnumerable<string> evidenceFactIds)
    {
        if (decidedAt == null)
        {
            throw new ArgumentNullException(nameof(decidedAt));
        }

        var normalizedEvidence = (evidenceFactIds ?? Array.Empty<string>())
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        var decisionHash = PlayerVisibleLedgerHash.Compute(
            runId,
            campaignId,
            decidedAt.CampaignIndex.ToString(CultureInfo.InvariantCulture),
            decidedAt.SiteIndex.ToString(CultureInfo.InvariantCulture),
            decidedAt.DecisionIndex.ToString(CultureInfo.InvariantCulture),
            policyId,
            decisionKind,
            action,
            rationale,
            estimatedValue.ToString("R", CultureInfo.InvariantCulture),
            string.Join("|", normalizedEvidence));
        return new PlayerVisibleDecisionRecord
        {
            RunId = runId ?? string.Empty,
            CampaignId = campaignId ?? string.Empty,
            DecisionId = $"decision-{decisionHash}",
            DecidedAt = decidedAt,
            PolicyId = policyId ?? string.Empty,
            DecisionKind = decisionKind ?? string.Empty,
            Action = action ?? string.Empty,
            Rationale = rationale ?? string.Empty,
            EstimatedValue = estimatedValue,
            EvidenceRefs = normalizedEvidence.Select(value => new PlayerVisibleEvidenceRef(value)).ToArray(),
        };
    }
}

internal static class PlayerVisibleLedgerHash
{
    public static string Compute(params string[] parts)
    {
        var canonical = new StringBuilder("PlayerVisibleFactLedgerV1|");
        foreach (var part in parts)
        {
            var value = part ?? string.Empty;
            canonical.Append(Encoding.UTF8.GetByteCount(value).ToString(CultureInfo.InvariantCulture))
                .Append(':')
                .Append(value)
                .Append('|');
        }

        using var sha256 = SHA256.Create();
        var bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(canonical.ToString()));
        var result = new StringBuilder(bytes.Length * 2);
        foreach (var value in bytes)
        {
            result.Append(value.ToString("x2", CultureInfo.InvariantCulture));
        }

        return result.ToString();
    }
}
