using System.Collections.Generic;

namespace SM.HeadlessMetrics;

/// <summary>Pro Stage 5 Q1 판정표를 적용한 sunken_bastion 방향 판별 보고서.</summary>
public sealed class SunkenSolvabilityReport
{
    public string SchemaVersion { get; set; } = "sunken-solvability-report-v1";
    public string RunId { get; set; } = string.Empty;
    public string TargetSiteId { get; set; } = string.Empty;
    public bool GoldenNeutral { get; set; } = true;
    public int SnapshotCount { get; set; }
    public int SameStateCandidateCount { get; set; }
    public int LookbackCandidateCount { get; set; }
    public int FailedCandidateCount { get; set; }
    public double SameStateOracleWinRate { get; set; }
    public double ChosenWinRate { get; set; }
    public double SelectionRegret { get; set; }
    public double AvailabilityGap { get; set; }
    public double OneSiteLookbackOracle { get; set; }
    public string BestCounterFamily { get; set; } = string.Empty;
    public int WinningBuildCount { get; set; }
    public int WinningPlacementCount { get; set; }
    public double PuzzleLockStateRate { get; set; }
    public bool PuzzleLockSignal { get; set; }
    public string DecisionCell { get; set; } = string.Empty;
    public string DecisionLabelKo { get; set; } = string.Empty;
    public string DecisionRationale { get; set; } = string.Empty;
    public string SearchMode { get; set; } = string.Empty;
    public IReadOnlyList<CounterFamilySummary> CounterFamilies { get; set; } = new List<CounterFamilySummary>();

    public sealed class CounterFamilySummary
    {
        public string CounterFamilyId { get; set; } = string.Empty;
        public int SampleCount { get; set; }
        public int CandidateCount { get; set; }
        public double OracleWinRate { get; set; }
        public double MeanBattleWinRate { get; set; }
        public double MeanFinalTeamHpFraction { get; set; }
    }
}
