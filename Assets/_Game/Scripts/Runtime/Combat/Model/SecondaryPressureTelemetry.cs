using System.Collections.Generic;
using System.Linq;

namespace SM.Combat.Model;

public sealed record SecondaryPressureRecipientTelemetry(
    string TargetId,
    long RawAllocated,
    long DamageAfterMitigationRaw);

public sealed record SecondaryPressureActionTelemetry(
    int StepIndex,
    string ActorId,
    string PrimaryTargetId,
    DamageType DamageType,
    long NormalizedDamageBudgetRaw,
    long PrimaryRawBudgetRaw,
    long PrimaryDamageAfterMitigationRaw,
    IReadOnlyList<SecondaryPressureRecipientTelemetry> Recipients);

/// <summary>
/// Heat secondary-pressure measurement lane. It observes authoritative fixed-point budgets only and is
/// intentionally absent from BattleResult/state hashes; gameplay never reads it.
/// </summary>
public sealed class SecondaryPressureTelemetryAccumulator
{
    private readonly List<SecondaryPressureActionTelemetry> _actions = new();

    public IReadOnlyList<SecondaryPressureActionTelemetry> Actions => _actions;

    public void Record(SecondaryPressureActionTelemetry action)
    {
        if (action != null)
        {
            _actions.Add(action);
        }
    }

    public SecondaryPressureTelemetrySnapshot BuildSnapshot()
        => new(_actions.ToArray());
}

public sealed record SecondaryPressureTelemetrySnapshot(
    IReadOnlyList<SecondaryPressureActionTelemetry> Actions)
{
    public long NormalizedDamageBudgetRaw => Actions.Sum(value => value.NormalizedDamageBudgetRaw);
    public long PrimaryRawBudgetRaw => Actions.Sum(value => value.PrimaryRawBudgetRaw);
    public long PrimaryDamageAfterMitigationRaw => Actions.Sum(value => value.PrimaryDamageAfterMitigationRaw);
    public long SecondaryRawAllocated => Actions.Sum(value => value.Recipients.Sum(recipient => recipient.RawAllocated));
    public long SecondaryDamageAfterMitigationRaw => Actions.Sum(
        value => value.Recipients.Sum(recipient => recipient.DamageAfterMitigationRaw));
}
