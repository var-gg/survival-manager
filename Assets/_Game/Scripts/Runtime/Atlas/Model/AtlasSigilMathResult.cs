using System.Collections.Generic;

namespace SM.Atlas.Model;

public sealed record AtlasSigilMathCell(
    AtlasHexCoordinate Hex,
    string NodeId,
    int CellIndex,
    double Falloff,
    AtlasCellRole Role,
    double CellImportance,
    double CellImportanceMass,
    bool HasEffect,
    double MassContribution,
    double LocalContribution);

public sealed record AtlasSigilMathEvaluation(
    string SigilId,
    string AnchorId,
    string ProfileId,
    AtlasModifierCategory Category,
    double TierBudget,
    double InfluenceMassRaw,
    double InfluenceMass,
    double NormalizedMass,
    double MassPotencyMultiplier,
    bool NormalizedMassClamped,
    double EffectivePotency,
    IReadOnlyList<AtlasSigilMathCell> Cells);

public sealed record AtlasNodeSigilMathEffect(
    string NodeId,
    AtlasModifierCategory Category,
    double RawPercent,
    double Percent,
    int SourceCount,
    bool SameCategoryDiminished,
    bool HardCapped,
    bool RiskBackedCapped);

public sealed record AtlasSigilMathResolution(
    IReadOnlyList<AtlasSigilMathEvaluation> Evaluations,
    IReadOnlyList<AtlasNodeSigilMathEffect> NodeEffects);

public sealed record AtlasProfileSimulationMetric(
    string ProfileId,
    int SampleCount,
    double ClampHitRate,
    double AverageNormalizedMass,
    double AverageEffectivePotency,
    double AverageUsefulCoverage,
    double AverageDeadCoverage);

public sealed record AtlasSigilMathSimulationReport(
    IReadOnlyList<AtlasProfileSimulationMetric> ProfileMetrics,
    IReadOnlyList<AtlasNodeSigilMathEffect> NodeEffects);
