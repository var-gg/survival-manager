using System;
using System.Collections.Generic;
using SM.HeadlessMetrics;

namespace SM.Editor.Validation;

public sealed class H100PreviewPolicyAcceptanceReport
{
    public string SchemaVersion { get; set; } = "h100-preview-policy-acceptance-v1";
    public string RunId { get; set; } = string.Empty;
    public string PolicyId { get; set; } = string.Empty;
    public bool GoldenNeutral { get; set; } = true;
    public string Status { get; set; } = "fail";
    public string FullResetDefinition { get; set; } =
        "full_reset iff every hero deployed before the decision is absent from the selected deployment; replacement_count is the number of those removed heroes";
    public SunkenSummary Sunken { get; set; } = new();
    public IReadOnlyList<HeldOutSummary> HeldOut { get; set; } = Array.Empty<HeldOutSummary>();
    public EvidenceSummary Evidence { get; set; } = new();
    public ResetSummary Reset { get; set; } = new();
    public int TechnicalFailureCount { get; set; }
    public IReadOnlyList<CheckResult> Checks { get; set; } = Array.Empty<CheckResult>();
    public IReadOnlyList<PairedCase> PairedCases { get; set; } = Array.Empty<PairedCase>();
    public H100Bt1GateReport.GateResult Bt8Partial { get; set; }

    public sealed class SunkenSummary
    {
        public int SampleCount { get; set; }
        public int CandidateCount { get; set; }
        public double SameStateOracleWinRate { get; set; }
        public double ChosenWinRate { get; set; }
        public double SelectionRegret { get; set; }
        public int WinningBuildCount { get; set; }
        public int WinningPlacementCount { get; set; }
    }

    public sealed class HeldOutSummary
    {
        public string SiteId { get; set; } = string.Empty;
        public int SampleCount { get; set; }
        public double BaselineCompletionRate { get; set; }
        public double PreviewCompletionRate { get; set; }
        public double Degradation { get; set; }
    }

    public sealed class EvidenceSummary
    {
        public int CounterAdaptDecisionCount { get; set; }
        public int SupportedCounterDecisionCount { get; set; }
        public int UnsupportedCounterDecisionCount { get; set; }
    }

    public sealed class ResetSummary
    {
        public int IdentityPreservingOpportunityCount { get; set; }
        public int FullResetCount { get; set; }
        public double UnnecessaryFullResetRate { get; set; }
    }

    public sealed class CheckResult
    {
        public string CheckId { get; set; } = string.Empty;
        public string Operator { get; set; } = string.Empty;
        public double Expected { get; set; }
        public double Actual { get; set; }
        public bool Pass { get; set; }
    }

    public sealed class PairedCase
    {
        public string SampleId { get; set; } = string.Empty;
        public string SiteId { get; set; } = string.Empty;
        public string BaselinePolicyId { get; set; } = string.Empty;
        public bool BaselineCompleted { get; set; }
        public bool PreviewCompleted { get; set; }
        public string BaselineBuildId { get; set; } = string.Empty;
        public string PreviewBuildId { get; set; } = string.Empty;
        public string BaselinePlacementId { get; set; } = string.Empty;
        public string PreviewPlacementId { get; set; } = string.Empty;
        public IReadOnlyList<string> SelectedHeroIds { get; set; } = Array.Empty<string>();
        public string FormationRule { get; set; } = string.Empty;
        public double BaselineBattleWinRate { get; set; }
        public double PreviewBattleWinRate { get; set; }
        public int ReplacementCount { get; set; }
        public int PreviousDeploymentCount { get; set; }
        public bool FullReset { get; set; }
        public bool IdentityPreservingCandidateAvailable { get; set; }
        public string DecisionReason { get; set; } = string.Empty;
        public IReadOnlyList<string> ThreatTags { get; set; } = Array.Empty<string>();
        public int CounterConnectionCount { get; set; }
        public bool CounterEvidenceSupported { get; set; }
        public string BaselineFailureCode { get; set; } = string.Empty;
        public string PreviewFailureCode { get; set; } = string.Empty;
    }
}
