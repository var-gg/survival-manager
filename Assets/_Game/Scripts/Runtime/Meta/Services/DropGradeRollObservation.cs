using System.Collections.Generic;
using SM.Core.Content;

namespace SM.Meta.Services;

internal readonly record struct DropGradeRollObservation(
    ItemRarityTierValue Grade,
    bool JackpotComponentSelected,
    double BaseJackpotWeight,
    double EffectiveJackpotWeight,
    IReadOnlyList<double> GradeProbabilities,
    double RandomRoll,
    bool UsedFallback);
