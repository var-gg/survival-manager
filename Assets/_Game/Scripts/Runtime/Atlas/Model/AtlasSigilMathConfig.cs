using System;
using System.Collections.Generic;

namespace SM.Atlas.Model;

public enum AtlasTraversalMode
{
    StoryFirstClear = 0,
    StoryRevisit = 1,
    EndlessRegion = 2,
}

public enum AtlasCellRole
{
    StageCandidateNow = 0,
    StageCandidateFuture1 = 1,
    StageCandidateFuture2Plus = 2,
    CombatStandard = 3,
    Clue = 4,
    Elite = 5,
    Boss = 6,
    BossAdjacent = 7,
    Extract = 8,
    ExtractAdjacent = 9,
    DetourOptional = 10,
    DetourHighValue = 11,
    Entry = 12,
    Visited = 13,
    DeadOrIrrelevant = 14,
    LockedButReachableLater = 15,
    OffBoard = 16,
}

public sealed record AtlasSigilMathProfile(
    string ProfileId,
    AtlasModifierCategory Category,
    IReadOnlyList<double> Falloff,
    double MassFloor,
    double ProfileScalar,
    double FrictionBonus);

public sealed record AtlasSigilCategoryMathParameters(
    double MassExponent,
    double MinMassMultiplier,
    double MaxMassMultiplier,
    double ContributionToPercentScale,
    IReadOnlyList<double> StackMultipliers);

public sealed record AtlasSigilMathConfig(
    double StoryCellMassFloor,
    double EndlessCellMassFloor,
    double VisitedMassFloor,
    double EdgeVoidMass,
    double StoryModeMassRef,
    double EndlessModeMassRef,
    double MinNormalizedMass,
    double MaxNormalizedMass,
    double SafeRewardCapPercent,
    double RiskCoupling,
    IReadOnlyDictionary<AtlasCellRole, double> CellImportance,
    IReadOnlyDictionary<string, AtlasSigilMathProfile> Profiles,
    IReadOnlyDictionary<AtlasModifierCategory, AtlasSigilCategoryMathParameters> CategoryParameters)
{
    public static AtlasSigilMathConfig CreateDefault()
    {
        return new AtlasSigilMathConfig(
            StoryCellMassFloor: 0.40,
            EndlessCellMassFloor: 0.45,
            VisitedMassFloor: 0.25,
            EdgeVoidMass: 0.60,
            StoryModeMassRef: 2.10,
            EndlessModeMassRef: 2.35,
            MinNormalizedMass: 0.65,
            MaxNormalizedMass: 1.85,
            SafeRewardCapPercent: 15.0,
            RiskCoupling: 0.35,
            CellImportance: new Dictionary<AtlasCellRole, double>
            {
                [AtlasCellRole.StageCandidateNow] = 1.00,
                [AtlasCellRole.StageCandidateFuture1] = 0.72,
                [AtlasCellRole.StageCandidateFuture2Plus] = 0.48,
                [AtlasCellRole.CombatStandard] = 0.75,
                [AtlasCellRole.Clue] = 0.85,
                [AtlasCellRole.Elite] = 0.90,
                [AtlasCellRole.Boss] = 1.15,
                [AtlasCellRole.BossAdjacent] = 0.85,
                [AtlasCellRole.Extract] = 0.65,
                [AtlasCellRole.ExtractAdjacent] = 0.55,
                [AtlasCellRole.DetourOptional] = 0.42,
                [AtlasCellRole.DetourHighValue] = 0.60,
                [AtlasCellRole.Entry] = 0.20,
                [AtlasCellRole.Visited] = 0.10,
                [AtlasCellRole.DeadOrIrrelevant] = 0.05,
                [AtlasCellRole.LockedButReachableLater] = 0.30,
                [AtlasCellRole.OffBoard] = 0.00,
            },
            Profiles: new Dictionary<string, AtlasSigilMathProfile>(StringComparer.Ordinal)
            {
                ["RewardBias.Cluster.Dense"] = new AtlasSigilMathProfile(
                    "RewardBias.Cluster.Dense",
                    AtlasModifierCategory.RewardBias,
                    new[] { 1.00, 0.80, 0.65 },
                    MassFloor: 1.60,
                    ProfileScalar: 1.05,
                    FrictionBonus: 1.08),
                ["RewardBias.Cluster.Wide"] = new AtlasSigilMathProfile(
                    "RewardBias.Cluster.Wide",
                    AtlasModifierCategory.RewardBias,
                    new[] { 1.00, 0.70, 0.70, 0.50, 0.45, 0.35 },
                    MassFloor: 2.00,
                    ProfileScalar: 0.86,
                    FrictionBonus: 1.00),
                ["ThreatPressure.Lane.Hard"] = new AtlasSigilMathProfile(
                    "ThreatPressure.Lane.Hard",
                    AtlasModifierCategory.ThreatPressure,
                    new[] { 1.00, 0.85, 0.55 },
                    MassFloor: 1.60,
                    ProfileScalar: 1.10,
                    FrictionBonus: 1.12),
                ["ThreatPressure.Lane.Long"] = new AtlasSigilMathProfile(
                    "ThreatPressure.Lane.Long",
                    AtlasModifierCategory.ThreatPressure,
                    new[] { 1.00, 0.75, 0.60, 0.45, 0.30 },
                    MassFloor: 1.85,
                    ProfileScalar: 0.90,
                    FrictionBonus: 1.02),
                ["AffinityBoost.ScoutArc.Deep"] = new AtlasSigilMathProfile(
                    "AffinityBoost.ScoutArc.Deep",
                    AtlasModifierCategory.AffinityBoost,
                    new[] { 1.00, 0.85, 0.60, 0.40 },
                    MassFloor: 1.60,
                    ProfileScalar: 0.95,
                    FrictionBonus: 1.03),
                ["AffinityBoost.ScoutArc.Wide"] = new AtlasSigilMathProfile(
                    "AffinityBoost.ScoutArc.Wide",
                    AtlasModifierCategory.AffinityBoost,
                    new[] { 1.00, 0.65, 0.65, 0.45, 0.45 },
                    MassFloor: 1.80,
                    ProfileScalar: 0.88,
                    FrictionBonus: 1.00),
            },
            CategoryParameters: new Dictionary<AtlasModifierCategory, AtlasSigilCategoryMathParameters>
            {
                [AtlasModifierCategory.RewardBias] = new AtlasSigilCategoryMathParameters(
                    MassExponent: 0.85,
                    MinMassMultiplier: 0.60,
                    MaxMassMultiplier: 1.35,
                    ContributionToPercentScale: 0.30,
                    StackMultipliers: new[] { 1.00, 0.45, 0.15 }),
                [AtlasModifierCategory.ThreatPressure] = new AtlasSigilCategoryMathParameters(
                    MassExponent: 0.85,
                    MinMassMultiplier: 0.60,
                    MaxMassMultiplier: 1.35,
                    ContributionToPercentScale: 0.25,
                    StackMultipliers: new[] { 1.00, 0.45, 0.15 }),
                [AtlasModifierCategory.AffinityBoost] = new AtlasSigilCategoryMathParameters(
                    MassExponent: 0.65,
                    MinMassMultiplier: 0.70,
                    MaxMassMultiplier: 1.25,
                    ContributionToPercentScale: 0.25,
                    StackMultipliers: new[] { 1.00, 0.35, 0.10 }),
            });
    }
}
