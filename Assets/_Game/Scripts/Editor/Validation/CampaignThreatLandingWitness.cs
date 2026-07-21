using System;
using System.Collections.Generic;
using System.Linq;
using SM.Combat.Model;
using SM.Core.Contracts;

namespace SM.Editor.Validation;

internal sealed class CampaignThreatLandingBattleObserver
{
    private readonly BattleState _state;
    private readonly HashSet<string> _allyBacklineIds;
    private readonly HashSet<string> _enemyFrontlineBodyIds;
    private readonly HashSet<string> _enemyCoreIds;
    private readonly Dictionary<string, HashSet<string>> _areaSkillIdsByActor;
    private readonly double _enemyCoreMaxHealth;

    private int? _firstThreatLandingTick;
    private double? _firstThreatLandingSeconds;
    private string _firstThreatKind = string.Empty;
    private int? _captainDeathTick;
    private int? _firstFrontlineBodyDeathTick;
    private double? _firstFrontlineBodyTtkSeconds;
    private double _preThreatPlayerDamage;
    private double _preThreatFrontlineDamage;
    private double _preThreatCoreDamage;
    private double? _coreEhpFractionAtFirstThreat;

    internal CampaignThreatLandingBattleObserver(BattleState state)
    {
        _state = state ?? throw new ArgumentNullException(nameof(state));
        _allyBacklineIds = state.Allies
            .Where(unit => unit.Anchor.IsBackRow() || unit.Behavior.FormationLine == FormationLine.Backline)
            .Select(unit => unit.Id.Value)
            .ToHashSet(StringComparer.Ordinal);
        _enemyCoreIds = state.Enemies
            .Where(unit => string.Equals(unit.Definition.RoleTag, "boss_captain", StringComparison.Ordinal))
            .Select(unit => unit.Id.Value)
            .ToHashSet(StringComparer.Ordinal);
        if (_enemyCoreIds.Count == 0)
        {
            throw new InvalidOperationException("Threat-landing witness requires an enemy boss_captain role.");
        }

        _enemyFrontlineBodyIds = state.Enemies
            .Where(unit => !_enemyCoreIds.Contains(unit.Id.Value) && IsFrontlineBody(unit))
            .Select(unit => unit.Id.Value)
            .ToHashSet(StringComparer.Ordinal);
        _enemyCoreMaxHealth = state.Enemies
            .Where(unit => _enemyCoreIds.Contains(unit.Id.Value))
            .Sum(unit => (double)unit.MaxHealth);
        _areaSkillIdsByActor = state.AllUnits.ToDictionary(
            unit => unit.Id.Value,
            unit => EnumerateSkills(unit)
                .Where(skill => skill.AreaEffectFamily != BattleAreaEffectFamily.SingleTarget)
                .Select(skill => skill.Id)
                .ToHashSet(StringComparer.Ordinal),
            StringComparer.Ordinal);
    }

    internal void ObserveStep(BattleSimulationStep step)
    {
        var threatKind = !_firstThreatLandingTick.HasValue
            ? DetectThreatKind(step)
            : string.Empty;
        if (!_firstThreatLandingTick.HasValue && string.IsNullOrEmpty(threatKind))
        {
            AccumulatePreThreatPlayerDamage(step);
        }
        else if (!_firstThreatLandingTick.HasValue)
        {
            _firstThreatLandingTick = step.StepIndex;
            _firstThreatLandingSeconds = step.TimeSeconds;
            _firstThreatKind = threatKind;
            _coreEhpFractionAtFirstThreat = ResolveCoreEhpFraction(step);
        }

        foreach (var battleEvent in step.Events.Where(value => value.EventKind == BattleEventKind.Kill))
        {
            var victimId = battleEvent.KillPayload?.ActualVictim.Value ?? battleEvent.TargetId?.Value;
            if (string.IsNullOrWhiteSpace(victimId))
            {
                continue;
            }

            if (!_captainDeathTick.HasValue && _enemyCoreIds.Contains(victimId))
            {
                _captainDeathTick = step.StepIndex;
            }

            if (!_firstFrontlineBodyDeathTick.HasValue && _enemyFrontlineBodyIds.Contains(victimId))
            {
                var killerId = battleEvent.KillPayload?.ActualKiller.Value ?? battleEvent.ActorId.Value;
                if (ResolveSide(killerId) == TeamSide.Ally)
                {
                    _firstFrontlineBodyDeathTick = step.StepIndex;
                    _firstFrontlineBodyTtkSeconds = step.TimeSeconds;
                }
            }
        }
    }

    internal CampaignThreatLandingObservation BuildObservation()
    {
        if (!_captainDeathTick.HasValue
            && _enemyCoreIds.All(id => _state.FindUnitById(id) is { IsAlive: false }))
        {
            _captainDeathTick = _state.StepIndex;
        }

        var preThreatFrontlineDamageFraction = _preThreatPlayerDamage <= 0d
            ? (double?)null
            : _preThreatFrontlineDamage / _preThreatPlayerDamage;
        var preThreatCoreDamageFraction = _preThreatPlayerDamage <= 0d
            ? (double?)null
            : _preThreatCoreDamage / _preThreatPlayerDamage;
        var frontlineTtkMinusThreatSeconds = _firstFrontlineBodyTtkSeconds.HasValue
                                             && _firstThreatLandingSeconds.HasValue
            ? _firstFrontlineBodyTtkSeconds.Value - _firstThreatLandingSeconds.Value
            : (double?)null;
        var threatLandedBeforeCaptainDeath = _firstThreatLandingTick.HasValue
                                             && (!_captainDeathTick.HasValue
                                                 || _firstThreatLandingTick.Value < _captainDeathTick.Value);

        return new CampaignThreatLandingObservation(
            _state.Seed,
            _firstThreatLandingTick,
            _firstThreatLandingSeconds,
            _firstThreatKind,
            threatLandedBeforeCaptainDeath,
            _captainDeathTick,
            _firstFrontlineBodyDeathTick,
            _firstFrontlineBodyTtkSeconds,
            _preThreatPlayerDamage,
            _preThreatFrontlineDamage,
            _preThreatCoreDamage,
            preThreatFrontlineDamageFraction,
            preThreatCoreDamageFraction,
            _coreEhpFractionAtFirstThreat,
            frontlineTtkMinusThreatSeconds);
    }

    private string DetectThreatKind(BattleSimulationStep step)
    {
        var diveLanded = false;
        var areaLanded = false;
        var units = step.Units.ToDictionary(unit => unit.Id, StringComparer.Ordinal);
        foreach (var intent in step.CombatEventIntents ?? Array.Empty<BattleCombatEventIntent>())
        {
            if (intent.Status != CombatEventIntentStatus.Contacted
                || ResolveSide(intent.ActorId.Value) != TeamSide.Enemy)
            {
                continue;
            }

            var contacts = intent.Contacts ?? Array.Empty<BattleContactIntent>();
            if (!contacts.Any(contact =>
                    contact.TargetId is { } targetId
                    && _allyBacklineIds.Contains(targetId.Value)
                    && !contact.IsHeal
                    && contact.Value > 0f))
            {
                continue;
            }

            diveLanded |= units.TryGetValue(intent.ActorId.Value, out var actor)
                          && actor.PositioningIntent == PositioningIntentKind.BacklineDive;
            areaLanded |= IsAreaContact(intent, contacts);
        }

        if (diveLanded)
        {
            return areaLanded ? "dive+aoe" : "dive";
        }

        return areaLanded ? "aoe" : string.Empty;
    }

    private bool IsAreaContact(BattleCombatEventIntent intent, IReadOnlyList<BattleContactIntent> contacts)
    {
        if (!string.IsNullOrWhiteSpace(intent.SkillId)
            && _areaSkillIdsByActor.TryGetValue(intent.ActorId.Value, out var skillIds)
            && skillIds.Contains(intent.SkillId))
        {
            return true;
        }

        if (intent.DeliveryKind is SkillDelivery.Nova or SkillDelivery.Aura or SkillDelivery.Zone)
        {
            return true;
        }

        return intent.Kind == CombatEventKind.Skill
               && contacts.Count(contact => !contact.IsHeal && contact.Value > 0f) > 1;
    }

    private void AccumulatePreThreatPlayerDamage(BattleSimulationStep step)
    {
        foreach (var battleEvent in step.Events)
        {
            if (battleEvent.EventKind != BattleEventKind.Action
                || battleEvent.Value <= 0f
                || battleEvent.TargetId is not { } targetId
                || ResolveSide(battleEvent.ActorId.Value) != TeamSide.Ally
                || ResolveSide(targetId.Value) != TeamSide.Enemy)
            {
                continue;
            }

            _preThreatPlayerDamage += battleEvent.Value;
            if (_enemyFrontlineBodyIds.Contains(targetId.Value))
            {
                _preThreatFrontlineDamage += battleEvent.Value;
            }
            else if (_enemyCoreIds.Contains(targetId.Value))
            {
                _preThreatCoreDamage += battleEvent.Value;
            }
        }
    }

    private double? ResolveCoreEhpFraction(BattleSimulationStep step)
    {
        if (_enemyCoreMaxHealth <= 0d)
        {
            return null;
        }

        return step.Units
                   .Where(unit => _enemyCoreIds.Contains(unit.Id))
                   .Sum(unit => Math.Max(0d, unit.CurrentHealth) + Math.Max(0d, unit.Barrier))
               / _enemyCoreMaxHealth;
    }

    private TeamSide? ResolveSide(string unitId) => _state.FindUnitById(unitId)?.Side;

    private static bool IsFrontlineBody(UnitSnapshot unit)
    {
        return unit.Anchor.IsFrontRow()
               || unit.Behavior.FormationLine == FormationLine.Frontline
               || ContainsToken(unit.Definition.RoleTag, "anchor")
               || ContainsToken(unit.Definition.RoleTag, "screen")
               || (unit.Definition.CompileTags ?? Array.Empty<string>()).Any(tag =>
                   ContainsToken(tag, "anchor") || ContainsToken(tag, "screen"));
    }

    private static bool ContainsToken(string value, string token)
    {
        return !string.IsNullOrWhiteSpace(value)
               && value.IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static IEnumerable<BattleSkillSpec> EnumerateSkills(UnitSnapshot unit)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var skill in unit.Definition.Skills ?? Array.Empty<BattleSkillSpec>())
        {
            if (skill != null && seen.Add(skill.Id))
            {
                yield return skill;
            }
        }

        foreach (var skill in new[]
                 {
                     unit.Definition.EffectiveSignatureActive,
                     unit.Definition.EffectiveFlexActive,
                 })
        {
            if (skill != null && seen.Add(skill.Id))
            {
                yield return skill;
            }
        }
    }
}

internal sealed class CampaignThreatLandingSweepAccumulator
{
    internal const string RangedReferenceSquadId = "ranged";

    private readonly List<CampaignThreatLandingBattleSample> _samples = new();

    internal void Record(
        CampaignBalanceArmSpec arm,
        CampaignBalanceGridCell cell,
        CampaignNodeIdentity node,
        CampaignThreatLandingObservation observation)
    {
        _samples.Add(new CampaignThreatLandingBattleSample(
            node.ChapterId,
            node.ChapterOrder,
            node.SiteId,
            node.SiteOrder,
            node.NodeId,
            node.EncounterId,
            arm.ArmId,
            cell.Squad.SquadId,
            cell.CellId,
            observation.BattleSeed,
            observation.FirstThreatLandingTick,
            observation.FirstThreatLandingSeconds,
            observation.FirstThreatKind,
            observation.ThreatLandedBeforeCaptainDeath,
            observation.CaptainDeathTick,
            observation.FirstFrontlineBodyDeathTick,
            observation.FirstFrontlineBodyTtkSeconds,
            observation.PreThreatPlayerDamage,
            observation.PreThreatFrontlineDamage,
            observation.PreThreatCoreDamage,
            observation.PreThreatFrontlineDamageFraction,
            observation.PreThreatCoreDamageFraction,
            observation.CoreEhpFractionAtFirstThreat,
            observation.FrontlineTtkMinusThreatSeconds));
    }

    internal CampaignThreatLandingWitnessReport BuildReport()
    {
        var samples = _samples
            .OrderBy(sample => sample.ChapterOrder)
            .ThenBy(sample => sample.SiteOrder)
            .ThenBy(sample => sample.EncounterId, StringComparer.Ordinal)
            .ThenBy(sample => sample.ArmId, StringComparer.Ordinal)
            .ThenBy(sample => sample.CellId, StringComparer.Ordinal)
            .ToArray();
        var ranged3RByBossAndArm = samples
            .Where(sample => string.Equals(sample.ReferenceSquadId, RangedReferenceSquadId, StringComparison.Ordinal))
            .GroupBy(sample => (sample.EncounterId, sample.ArmId))
            .OrderBy(group => group.Min(sample => sample.ChapterOrder))
            .ThenBy(group => group.Min(sample => sample.SiteOrder))
            .ThenBy(group => group.Key.EncounterId, StringComparer.Ordinal)
            .ThenBy(group => group.Key.ArmId, StringComparer.Ordinal)
            .Select(group => BuildAggregate(group.Key.EncounterId, group.Key.ArmId, group.ToArray()))
            .ToArray();
        return new CampaignThreatLandingWitnessReport
        {
            RangedReferenceSquadId = RangedReferenceSquadId,
            BossBattleSamples = samples,
            Ranged3RByBossAndArm = ranged3RByBossAndArm,
        };
    }

    private static CampaignThreatLandingAggregate BuildAggregate(
        string encounterId,
        string armId,
        IReadOnlyList<CampaignThreatLandingBattleSample> samples)
    {
        var sampleCount = samples.Count;
        var threatLandingCount = samples.Count(sample => sample.FirstThreatLandingTick.HasValue);
        var threatBeforeCaptainDeathCount = samples.Count(sample => sample.ThreatLandedBeforeCaptainDeath);
        var coreEhpAtOrAboveSixtyPercentCount = samples.Count(sample =>
            sample.CoreEhpFractionAtFirstThreat is >= 0.60d);
        return new CampaignThreatLandingAggregate(
            encounterId,
            armId,
            RangedReferenceSquadId,
            sampleCount,
            threatLandingCount,
            Rate(threatLandingCount, sampleCount),
            threatBeforeCaptainDeathCount,
            Rate(threatBeforeCaptainDeathCount, sampleCount),
            Quantile(samples.Select(sample => sample.FirstThreatLandingSeconds), 0.50d),
            Quantile(samples.Select(sample => sample.FirstThreatLandingSeconds), 0.90d),
            Quantile(samples.Select(sample => sample.FirstFrontlineBodyTtkSeconds), 0.50d),
            Quantile(samples.Select(sample => sample.FirstFrontlineBodyTtkSeconds), 0.90d),
            Quantile(samples.Select(sample => sample.PreThreatFrontlineDamageFraction), 0.50d),
            Quantile(samples.Select(sample => sample.PreThreatFrontlineDamageFraction), 0.10d),
            Quantile(samples.Select(sample => sample.PreThreatCoreDamageFraction), 0.50d),
            Quantile(samples.Select(sample => sample.CoreEhpFractionAtFirstThreat), 0.50d),
            coreEhpAtOrAboveSixtyPercentCount,
            Rate(coreEhpAtOrAboveSixtyPercentCount, sampleCount),
            Quantile(samples.Select(sample => sample.FrontlineTtkMinusThreatSeconds), 0.50d),
            Quantile(samples.Select(sample => sample.FrontlineTtkMinusThreatSeconds), 0.90d));
    }

    private static double Rate(int numerator, int denominator) => denominator > 0
        ? numerator / (double)denominator
        : 0d;

    private static double? Quantile(IEnumerable<double?> source, double quantile)
    {
        var values = source
            .Where(value => value.HasValue)
            .Select(value => value!.Value)
            .OrderBy(value => value)
            .ToArray();
        if (values.Length == 0)
        {
            return null;
        }

        var position = (values.Length - 1) * quantile;
        var lower = (int)Math.Floor(position);
        var upper = (int)Math.Ceiling(position);
        if (lower == upper)
        {
            return values[lower];
        }

        var weight = position - lower;
        return values[lower] + ((values[upper] - values[lower]) * weight);
    }
}

internal sealed record CampaignThreatLandingObservation(
    int BattleSeed,
    int? FirstThreatLandingTick,
    double? FirstThreatLandingSeconds,
    string FirstThreatKind,
    bool ThreatLandedBeforeCaptainDeath,
    int? CaptainDeathTick,
    int? FirstFrontlineBodyDeathTick,
    double? FirstFrontlineBodyTtkSeconds,
    double PreThreatPlayerDamage,
    double PreThreatFrontlineDamage,
    double PreThreatCoreDamage,
    double? PreThreatFrontlineDamageFraction,
    double? PreThreatCoreDamageFraction,
    double? CoreEhpFractionAtFirstThreat,
    double? FrontlineTtkMinusThreatSeconds);

public sealed record CampaignThreatLandingWitnessReport
{
    public const string CurrentSchemaVersion = "campaign-threat-landing-witness-v1";

    public string SchemaVersion { get; init; } = CurrentSchemaVersion;
    public string RangedReferenceSquadId { get; init; } = CampaignThreatLandingSweepAccumulator.RangedReferenceSquadId;
    public string ThreatDefinition { get; init; }
        = "enemy backline-dive contact or authored enemy area-skill contact on an allied back-row unit";
    public string CoreEhpFractionDefinition { get; init; }
        = "(captain current health + barrier) / captain max health";
    public IReadOnlyList<CampaignThreatLandingBattleSample> BossBattleSamples { get; init; }
        = Array.Empty<CampaignThreatLandingBattleSample>();
    public IReadOnlyList<CampaignThreatLandingAggregate> Ranged3RByBossAndArm { get; init; }
        = Array.Empty<CampaignThreatLandingAggregate>();
}

public sealed record CampaignThreatLandingBattleSample(
    string ChapterId,
    int ChapterOrder,
    string SiteId,
    int SiteOrder,
    string NodeId,
    string EncounterId,
    string ArmId,
    string ReferenceSquadId,
    string CellId,
    int BattleSeed,
    int? FirstThreatLandingTick,
    double? FirstThreatLandingSeconds,
    string FirstThreatKind,
    bool ThreatLandedBeforeCaptainDeath,
    int? CaptainDeathTick,
    int? FirstFrontlineBodyDeathTick,
    double? FirstFrontlineBodyTtkSeconds,
    double PreThreatPlayerDamage,
    double PreThreatFrontlineDamage,
    double PreThreatCoreDamage,
    double? PreThreatFrontlineDamageFraction,
    double? PreThreatCoreDamageFraction,
    double? CoreEhpFractionAtFirstThreat,
    double? FrontlineTtkMinusThreatSeconds);

public sealed record CampaignThreatLandingAggregate(
    string EncounterId,
    string ArmId,
    string ReferenceSquadId,
    int SampleCount,
    int ThreatLandingCount,
    double ThreatLandingRate,
    int ThreatBeforeCaptainDeathCount,
    double ThreatBeforeCaptainDeathRate,
    double? FirstThreatLandingMedianSeconds,
    double? FirstThreatLandingP90Seconds,
    double? FirstFrontlineBodyTtkMedianSeconds,
    double? FirstFrontlineBodyTtkP90Seconds,
    double? PreThreatFrontlineDamageFractionMedian,
    double? PreThreatFrontlineDamageFractionP10,
    double? PreThreatCoreDamageFractionMedian,
    double? CoreEhpFractionAtFirstThreatMedian,
    int CoreEhpAtOrAboveSixtyPercentCount,
    double CoreEhpAtOrAboveSixtyPercentRate,
    double? FrontlineTtkMinusThreatMedianSeconds,
    double? FrontlineTtkMinusThreatP90Seconds);
