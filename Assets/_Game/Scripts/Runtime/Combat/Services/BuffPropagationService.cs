using System;
using System.Collections.Generic;
using System.Linq;
using SM.Combat.Model;

namespace SM.Combat.Services;

public static class BuffPropagationService
{
    public static BuffPropagationResult Resolve(
        EffectPositionSnapshot snapshot,
        BuffPropagationRequest request,
        IReadOnlyList<BuffPropagationResult>? sameCategoryCandidates = null)
    {
        var lingerByUnit = (request.LingerEntries ?? Array.Empty<AuraLingerEntry>())
            .GroupBy(entry => entry.UnitId, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.Max(entry => entry.EligibleUntilSeconds), StringComparer.Ordinal);
        var eligible = new List<BuffPropagationTarget>();
        var missed = 0;

        foreach (var unit in snapshot.Units.Where(unit => unit.Side == request.SourceSide).OrderBy(unit => unit.UnitId, StringComparer.Ordinal))
        {
            var distance = unit.Position.DistanceTo(request.Center);
            var inRadius = distance <= Math.Max(0f, request.Radius);
            var lingering = !inRadius
                            && lingerByUnit.TryGetValue(unit.UnitId, out var until)
                            && snapshot.TimeSeconds <= until
                            && distance <= request.Radius + Math.Max(0f, request.AuraMembershipLingerSeconds);
            if (inRadius || lingering)
            {
                eligible.Add(new BuffPropagationTarget(unit.UnitId, distance, lingering));
            }
            else
            {
                missed++;
            }
        }

        eligible = eligible
            .OrderBy(target => target.Distance)
            .ThenBy(target => target.UnitId, StringComparer.Ordinal)
            .ToList();

        var effectiveCount = Math.Min(eligible.Count, 4);
        var clusterLink = Math.Max(0, effectiveCount - 1);
        var link = ResolveTypeLink(request.Kind);
        var cap = ResolveTypeCap(request.Kind);
        var bonus = Math.Min(cap, link * clusterLink);
        var value = Math.Max(0f, request.BaseValue) * (1f + bonus);
        var result = new BuffPropagationResult(
            request.SourceId,
            request.Category,
            request.Kind,
            request.BaseValue,
            value,
            bonus,
            effectiveCount,
            0,
            missed,
            eligible);

        if (sameCategoryCandidates == null || sameCategoryCandidates.Count == 0)
        {
            return result;
        }

        var candidates = sameCategoryCandidates
            .Where(candidate => string.Equals(candidate.Category, request.Category, StringComparison.Ordinal))
            .Concat(new[] { result })
            .OrderByDescending(candidate => candidate.EffectiveValue)
            .ThenBy(candidate => candidate.WinningSourceId, StringComparer.Ordinal)
            .ToList();
        var winner = candidates[0];
        var second = candidates.Count > 1 ? candidates[1] : null;
        var cappedValue = second == null
            ? winner.EffectiveValue
            : winner.EffectiveValue + (second.EffectiveValue * 0.5f);
        return winner with
        {
            EffectiveValue = cappedValue,
            OvercapEvents = Math.Max(0, candidates.Count - 2),
        };
    }

    public static int ResolveCleanseTargetLimit(int effectiveCount)
    {
        return Math.Min(4, 2 + (int)MathF.Floor(Math.Max(0, effectiveCount - 1) * 0.5f));
    }

    private static float ResolveTypeLink(BuffPropagationKind kind)
    {
        return kind switch
        {
            BuffPropagationKind.BurstHeal => 0f,
            BuffPropagationKind.HealOverTime => 0.025f,
            BuffPropagationKind.ShieldBarrier => 0.035f,
            BuffPropagationKind.AttackBuff => 0.030f,
            BuffPropagationKind.CritBuff => 0.020f,
            BuffPropagationKind.DamageReductionAura => 0.025f,
            BuffPropagationKind.Cleanse => 0f,
            _ => 0f,
        };
    }

    private static float ResolveTypeCap(BuffPropagationKind kind)
    {
        return kind switch
        {
            BuffPropagationKind.BurstHeal => 0f,
            BuffPropagationKind.HealOverTime => 0.08f,
            BuffPropagationKind.ShieldBarrier => 0.12f,
            BuffPropagationKind.AttackBuff => 0.10f,
            BuffPropagationKind.CritBuff => 0.08f,
            BuffPropagationKind.DamageReductionAura => 0.10f,
            BuffPropagationKind.Cleanse => 0f,
            _ => 0f,
        };
    }
}
